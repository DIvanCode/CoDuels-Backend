from math import ceil
from time import perf_counter

from django.db import connection
from django.utils import timezone

from .models import ExeshJobEvent


def dashboard_data(start, end):
    started = perf_counter()
    since, until = dashboard_window(start, end)
    timings = {}
    bucket_seconds = dashboard_bucket_seconds(since, until)
    execution_buckets = timed("execution", timings, execution_bucket_rows, since, until, bucket_seconds)
    execution_pick_buckets = timed("execution_picks", timings, execution_pick_bucket_rows, since, until, bucket_seconds)
    execution_progress_buckets = timed(
        "execution_progress",
        timings,
        execution_progress_bucket_rows,
        since,
        until,
        bucket_seconds,
    )
    worker_buckets = timed("workers", timings, worker_bucket_rows, since, until, bucket_seconds)
    job_events = timed("jobs", timings, job_event_rows, since, until)
    latest_worker_rows = timed("latest_workers", timings, latest_worker_events, since, until)
    execution_events = sum(row["raw_events"] for row in execution_buckets)
    worker_events = sum(row["raw_events"] for row in worker_buckets)
    return {
        "execution": execution_buckets,
        "execution_priorities": executions_series(execution_pick_buckets),
        "execution_progress": executions_series(execution_progress_buckets),
        "workers": worker_series(worker_buckets),
        "jobs": job_rectangles(job_events),
        "latest_workers": latest_workers(latest_worker_rows),
        "latest_worker_pool": latest_worker_pool(latest_worker_rows),
        "meta": {
            "bucket_seconds": bucket_seconds,
            "execution_events": execution_events,
            "worker_events": worker_events,
            "job_events": len(job_events),
            "execution_points": len(execution_buckets),
            "execution_pick_points": len(execution_pick_buckets),
            "execution_progress_points": len(execution_progress_buckets),
            "worker_points": len(worker_buckets),
            "window_start": since.timestamp(),
            "window_end": until.timestamp(),
            "timezone_offset_seconds": since.utcoffset().total_seconds() if since.utcoffset() else 0,
            "query_timings": timings,
            "elapsed_ms": round((perf_counter() - started) * 1000, 2),
        },
    }


def dashboard_window(start, end):
    since = start
    until = end
    if since > until:
        since, until = until, since
    return since, until


def timed(name, timings, func, *args):
    started = perf_counter()
    result = func(*args)
    timings[name] = round((perf_counter() - started) * 1000, 2)
    return result


def dashboard_bucket_seconds(since, until):
    window_seconds = max(1, int((until - since).total_seconds()))
    return max(1, int(ceil(window_seconds / 900)))


def bucket_timestamp_sql(column_name):
    return f"(floor(EXTRACT(EPOCH FROM {column_name}) / %s) * %s)::bigint"


def execution_bucket_rows(since, until, bucket_seconds):
    return query_rows(
        f"""
        SELECT
            {bucket_timestamp_sql("happened_at")} AS timestamp,
            COUNT(*)::bigint AS raw_events,
            (COUNT(*) FILTER (WHERE event_type = 'started')::float / %s)::float AS started_rate,
            (COUNT(*) FILTER (WHERE event_type = 'finished')::float / %s)::float AS finished_rate,
            (COUNT(*) FILTER (WHERE event_type = 'picked_candidate')::float / %s)::float AS scheduler_pick_rate,
            0::bigint AS active_executions,
            COALESCE(
                percentile_cont(0.5) WITHIN GROUP (ORDER BY duration_seconds)
                    FILTER (WHERE event_type = 'finished'),
                0
            )::float AS duration_p50,
            COALESCE(
                percentile_cont(0.95) WITHIN GROUP (ORDER BY duration_seconds)
                    FILTER (WHERE event_type = 'finished'),
                0
            )::float AS duration_p95,
            COALESCE(
                percentile_cont(0.5) WITHIN GROUP (ORDER BY priority)
                    FILTER (WHERE event_type = 'picked_candidate'),
                0
            )::float AS priority_p50,
            COALESCE(
                percentile_cont(0.95) WITHIN GROUP (ORDER BY priority)
                    FILTER (WHERE event_type = 'picked_candidate'),
                0
            )::float AS priority_p95,
            COALESCE(
                percentile_cont(0.1) WITHIN GROUP (ORDER BY progress_ratio)
                    FILTER (WHERE event_type = 'picked_candidate'),
                0
            )::float AS progress_pick_p10,
            COALESCE(
                percentile_cont(0.5) WITHIN GROUP (ORDER BY progress_ratio)
                    FILTER (WHERE event_type = 'picked_candidate'),
                0
            )::float AS progress_pick_p50,
            COALESCE(
                percentile_cont(0.9) WITHIN GROUP (ORDER BY progress_ratio)
                    FILTER (WHERE event_type = 'picked_candidate'),
                0
            )::float AS progress_pick_p90
        FROM exesh_execution_events
        WHERE happened_at >= %s AND happened_at <= %s
        GROUP BY timestamp
        ORDER BY timestamp
        """,
        [bucket_seconds, bucket_seconds, bucket_seconds, bucket_seconds, bucket_seconds, since, until],
    )


def execution_pick_bucket_rows(since, until, bucket_seconds):
    return query_rows(
        f"""
        SELECT
            execution_id,
            {bucket_timestamp_sql("happened_at")} AS timestamp,
            AVG(priority)::float AS priority,
            AVG(progress_ratio)::float AS progress_ratio
        FROM exesh_execution_events
        WHERE happened_at >= %s AND happened_at <= %s AND event_type = 'picked_candidate'
        GROUP BY execution_id, timestamp
        ORDER BY execution_id, timestamp
        """,
        [bucket_seconds, bucket_seconds, since, until],
    )


def execution_progress_bucket_rows(since, until, bucket_seconds):
    return query_rows(
        f"""
        SELECT
            execution_id,
            {bucket_timestamp_sql("happened_at")} AS timestamp,
            MAX(progress_ratio)::float AS progress_ratio
        FROM exesh_execution_events
        WHERE happened_at >= %s AND happened_at <= %s AND event_type IN ('picked_candidate', 'finished')
        GROUP BY execution_id, timestamp
        ORDER BY execution_id, timestamp
        """,
        [bucket_seconds, bucket_seconds, since, until],
    )


def worker_bucket_rows(since, until, bucket_seconds):
    return query_rows(
        f"""
        SELECT
            worker_id,
            {bucket_timestamp_sql("happened_at")} AS timestamp,
            COUNT(*)::bigint AS raw_events,
            AVG(100.0 * running_jobs / GREATEST(total_slots, 1))::float AS slot_utilization_percent,
            AVG(100.0 * used_memory_mb / GREATEST(total_memory_mb, 1))::float AS memory_utilization_percent,
            AVG(free_slots)::float AS free_slots,
            AVG(available_memory_mb)::float AS available_memory_mb,
            AVG(running_jobs)::float AS running_jobs,
            AVG(used_memory_mb)::float AS used_memory_mb
        FROM exesh_worker_events
        WHERE happened_at >= %s AND happened_at <= %s
        GROUP BY worker_id, timestamp
        ORDER BY worker_id, timestamp
        """,
        [bucket_seconds, bucket_seconds, since, until],
    )


def job_event_rows(since, until):
    return list(
        ExeshJobEvent.objects.filter(happened_at__gte=since, happened_at__lte=until)
        .order_by("happened_at")
        .values(
            "happened_at",
            "event_type",
            "job_id",
            "execution_id",
            "worker_id",
            "job_type",
            "status",
            "expected_memory_mb",
            "expected_duration_ms",
            "memory_start_mb",
            "memory_end_mb",
            "promised_start_at",
            "started_at",
            "finished_at",
            "expected_finished_at",
        )
    )


def executions_series(rows):
    result = {}
    for row in rows:
        record = result.setdefault(
            row["execution_id"],
            {
                "execution_id": row["execution_id"],
                "status": "",
                "started_at": 0,
                "finished_at": 0,
                "duration_seconds": 0,
                "points": [],
            },
        )
        point = {"timestamp": row["timestamp"]}
        if "priority" in row:
            point["priority"] = row["priority"]
        if "progress_ratio" in row:
            point["progress_ratio"] = row["progress_ratio"]
        record["points"].append(point)
    return sorted(result.values(), key=lambda row: row.get("started_at") or row["points"][0]["timestamp"] if row["points"] else 0)


def worker_series(rows):
    result = {}
    for row in rows:
        result.setdefault(row["worker_id"], []).append(
            {
                "timestamp": row["timestamp"],
                "slot_utilization_percent": row["slot_utilization_percent"],
                "memory_utilization_percent": row["memory_utilization_percent"],
                "free_slots": row["free_slots"],
                "available_memory_mb": row["available_memory_mb"],
                "running_jobs": row["running_jobs"],
                "used_memory_mb": row["used_memory_mb"],
            }
        )
    return result


def job_rectangles(events):
    latest_by_job = {}
    for event in events:
        record = latest_by_job.setdefault(
            event["job_id"],
            {
                "job_id": event["job_id"],
                "execution_id": event["execution_id"],
                "worker_id": event["worker_id"],
                "job_type": event["job_type"],
                "status": "",
                "memory_start_mb": event["memory_start_mb"],
                "memory_end_mb": event["memory_end_mb"],
                "memory_mb": event["expected_memory_mb"],
                "start_timestamp_seconds": 0,
                "finish_timestamp_seconds": 0,
                "expected_finish_timestamp_seconds": 0,
                "expected_duration_seconds": event["expected_duration_ms"] / 1000,
                "promised_start_timestamp_seconds": 0,
            },
        )
        record["worker_id"] = event["worker_id"] or record["worker_id"]
        record["status"] = event["status"] or record["status"]
        record["memory_start_mb"] = event["memory_start_mb"]
        record["memory_end_mb"] = event["memory_end_mb"]
        if event["promised_start_at"]:
            record["promised_start_timestamp_seconds"] = event["promised_start_at"].timestamp()
        if event["started_at"]:
            record["start_timestamp_seconds"] = event["started_at"].timestamp()
        if event["finished_at"]:
            record["finish_timestamp_seconds"] = event["finished_at"].timestamp()
        if event["expected_finished_at"]:
            record["expected_finish_timestamp_seconds"] = event["expected_finished_at"].timestamp()
    return [record for record in latest_by_job.values() if record["start_timestamp_seconds"]]


def latest_workers(events):
    return [
        {
            "worker_id": event["worker_id"],
            "total_slots": event["total_slots"],
            "free_slots": event["free_slots"],
            "total_memory_mb": event["total_memory_mb"],
            "available_memory_mb": event["available_memory_mb"],
            "running_jobs": event["running_jobs"],
            "used_memory_mb": event["used_memory_mb"],
        }
        for event in events
        if event["event_type"] != "removed"
    ]


def latest_worker_pool(events):
    workers = [
        {
            "worker_id": event["worker_id"],
            "registered": 1,
            "total_slots": event["total_slots"],
            "total_memory_mb": event["total_memory_mb"],
            "running_jobs": event["running_jobs"],
            "used_expected_memory_mb": event["used_memory_mb"],
            "free_expected_slots": event["free_slots"],
            "free_expected_memory_mb": event["available_memory_mb"],
            "last_heartbeat_age_seconds": (timezone.now() - event["happened_at"]).total_seconds(),
        }
        for event in events
        if event["event_type"] != "removed"
    ]
    return {"registered_workers": len(workers), "workers": sorted(workers, key=lambda row: row["worker_id"])}


def latest_worker_events(since, until):
    return query_rows(
        """
        WITH window_workers AS (
            SELECT DISTINCT worker_id
            FROM exesh_worker_events
            WHERE happened_at >= %s AND happened_at <= %s
        )
        SELECT
            latest.happened_at,
            latest.event_type,
            latest.worker_id,
            latest.total_slots,
            latest.total_memory_mb,
            latest.free_slots,
            latest.available_memory_mb,
            latest.running_jobs,
            latest.used_memory_mb
        FROM window_workers
        CROSS JOIN LATERAL (
            SELECT
                happened_at,
                event_type,
                worker_id,
                total_slots,
                total_memory_mb,
                free_slots,
                available_memory_mb,
                running_jobs,
                used_memory_mb
            FROM exesh_worker_events
            WHERE worker_id = window_workers.worker_id AND happened_at <= %s
            ORDER BY happened_at DESC
            LIMIT 1
        ) latest
        ORDER BY latest.worker_id
        """,
        [since, until, until],
    )

def query_rows(sql, params):
    with connection.cursor() as cursor:
        cursor.execute(sql, params)
        columns = [column[0] for column in cursor.description]
        return [dict(zip(columns, row)) for row in cursor.fetchall()]
