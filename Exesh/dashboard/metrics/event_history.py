from time import perf_counter

from django.db import connection
from django.utils import timezone

from .models import ExeshExecutionEvent, ExeshJobEvent, ExeshWorkerEvent


def event_dashboard_history(start, end):
    started = perf_counter()
    since, until = dashboard_window(start, end)
    execution_buckets = execution_bucket_rows(since, until)
    execution_pick_buckets = execution_pick_bucket_rows(since, until)
    worker_buckets = worker_bucket_rows(since, until)
    job_events = job_event_rows(since, until)
    latest_worker_rows = latest_worker_events()
    counts = event_counts(since, until)
    return {
        "execution": execution_buckets,
        "executions": executions_series(execution_pick_buckets),
        "workers": worker_series(worker_buckets),
        "jobs": job_rectangles(job_events),
        "latest_workers": latest_workers(latest_worker_rows),
        "latest_worker_pool": latest_worker_pool(latest_worker_rows),
        "meta": {
            "execution_events": counts["execution_events"],
            "worker_events": counts["worker_events"],
            "job_events": len(job_events),
            "execution_points": len(execution_buckets),
            "execution_pick_points": len(execution_pick_buckets),
            "worker_points": len(worker_buckets),
            "elapsed_ms": round((perf_counter() - started) * 1000, 2),
        },
    }


def dashboard_window(start, end):
    since = start
    until = end
    if since > until:
        since, until = until, since
    return since, until


def execution_bucket_rows(since, until):
    return query_rows(
        """
        SELECT
            EXTRACT(EPOCH FROM date_trunc('second', happened_at))::bigint AS timestamp,
            COUNT(*) FILTER (WHERE event_type = 'started')::bigint AS started_rate,
            COUNT(*) FILTER (WHERE event_type = 'finished')::bigint AS finished_rate,
            COUNT(*) FILTER (WHERE event_type = 'picked_candidate')::bigint AS scheduler_pick_rate,
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
        GROUP BY date_trunc('second', happened_at)
        ORDER BY timestamp
        """,
        [since, until],
    )


def execution_pick_bucket_rows(since, until):
    return query_rows(
        """
        SELECT
            execution_id,
            EXTRACT(EPOCH FROM date_trunc('second', happened_at))::bigint AS timestamp,
            AVG(priority)::float AS priority,
            AVG(progress_ratio)::float AS progress_ratio
        FROM exesh_execution_events
        WHERE happened_at >= %s AND happened_at <= %s AND event_type = 'picked_candidate'
        GROUP BY execution_id, date_trunc('second', happened_at)
        ORDER BY execution_id, timestamp
        """,
        [since, until],
    )


def worker_bucket_rows(since, until):
    return query_rows(
        """
        SELECT
            worker_id,
            EXTRACT(EPOCH FROM date_trunc('second', happened_at))::bigint AS timestamp,
            AVG(100.0 * running_jobs / GREATEST(total_slots, 1))::float AS slot_utilization_percent,
            AVG(100.0 * used_memory_mb / GREATEST(total_memory_mb, 1))::float AS memory_utilization_percent,
            AVG(free_slots)::float AS free_slots,
            AVG(available_memory_mb)::float AS available_memory_mb,
            AVG(running_jobs)::float AS running_jobs,
            AVG(used_memory_mb)::float AS used_memory_mb
        FROM exesh_worker_events
        WHERE happened_at >= %s AND happened_at <= %s
        GROUP BY worker_id, date_trunc('second', happened_at)
        ORDER BY worker_id, timestamp
        """,
        [since, until],
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
        record["points"].append(
            {
                "timestamp": row["timestamp"],
                "priority": row["priority"],
                "progress_ratio": row["progress_ratio"],
            }
        )
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


def latest_worker_events():
    return list(
        ExeshWorkerEvent.objects.order_by("worker_id", "-happened_at")
        .distinct("worker_id")
        .values(
            "happened_at",
            "event_type",
            "worker_id",
            "total_slots",
            "total_memory_mb",
            "free_slots",
            "available_memory_mb",
            "running_jobs",
            "used_memory_mb",
        )
    )


def event_counts(since, until):
    return {
        "execution_events": ExeshExecutionEvent.objects.filter(happened_at__gte=since, happened_at__lte=until).count(),
        "worker_events": ExeshWorkerEvent.objects.filter(happened_at__gte=since, happened_at__lte=until).count(),
    }


def query_rows(sql, params):
    with connection.cursor() as cursor:
        cursor.execute(sql, params)
        columns = [column[0] for column in cursor.description]
        return [dict(zip(columns, row)) for row in cursor.fetchall()]
