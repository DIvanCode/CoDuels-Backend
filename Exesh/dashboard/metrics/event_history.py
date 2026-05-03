from datetime import timedelta

from django.utils import timezone

from .models import ExeshExecutionEvent, ExeshJobEvent, ExeshWorkerEvent


def event_dashboard_history(minutes=30):
    since = timezone.now() - timedelta(minutes=minutes)
    return {
        "execution": execution_series(since),
        "executions": executions_series(since),
        "workers": worker_series(since),
        "jobs": job_rectangles(since),
        "latest_workers": latest_workers(),
        "latest_worker_pool": latest_worker_pool(),
    }


def execution_series(since):
    events = list(ExeshExecutionEvent.objects.filter(happened_at__gte=since).order_by("happened_at"))
    buckets = {}
    priorities = {}
    durations = {}
    progresses = {}
    active = set()
    for event in events:
        ts = bucket_ts(event.happened_at)
        item = buckets.setdefault(ts, {"timestamp": ts, "started_rate": 0, "finished_rate": 0, "scheduler_pick_rate": 0})
        if event.event_type == "started":
            item["started_rate"] += 1
            active.add(event.execution_id)
        elif event.event_type == "finished":
            item["finished_rate"] += 1
            durations.setdefault(ts, []).append(event.duration_seconds)
            active.discard(event.execution_id)
        elif event.event_type == "picked_candidate":
            item["scheduler_pick_rate"] += 1
            priorities.setdefault(ts, []).append(event.priority)
            progresses.setdefault(ts, []).append(event.progress_ratio)
        item["active_executions"] = len(active)

    result = []
    previous_active = 0
    for ts in sorted(buckets):
        item = buckets[ts]
        item["active_executions"] = item.get("active_executions", previous_active)
        previous_active = item["active_executions"]
        item["duration_p50"] = percentile(durations.get(ts, []), 0.5)
        item["duration_p95"] = percentile(durations.get(ts, []), 0.95)
        item["priority_p50"] = percentile(priorities.get(ts, []), 0.5)
        item["priority_p95"] = percentile(priorities.get(ts, []), 0.95)
        item["progress_pick_p10"] = percentile(progresses.get(ts, []), 0.1)
        item["progress_pick_p50"] = percentile(progresses.get(ts, []), 0.5)
        item["progress_pick_p90"] = percentile(progresses.get(ts, []), 0.9)
        result.append(item)
    return result


def executions_series(since):
    result = {}
    for event in ExeshExecutionEvent.objects.filter(happened_at__gte=since).order_by("happened_at"):
        record = result.setdefault(
            event.execution_id,
            {
                "execution_id": event.execution_id,
                "status": "",
                "started_at": 0,
                "finished_at": 0,
                "duration_seconds": 0,
                "points": [],
            },
        )
        if event.event_type == "started":
            record["started_at"] = event.happened_at.timestamp()
        elif event.event_type == "finished":
            record["finished_at"] = event.happened_at.timestamp()
            record["duration_seconds"] = event.duration_seconds
            record["status"] = event.status
        elif event.event_type == "picked_candidate":
            record["points"].append(
                {
                    "timestamp": event.happened_at.timestamp(),
                    "priority": event.priority,
                    "progress_ratio": event.progress_ratio,
                }
            )
    return sorted(result.values(), key=lambda row: row.get("started_at") or row["points"][0]["timestamp"] if row["points"] else 0)


def worker_series(since):
    result = {}
    for event in ExeshWorkerEvent.objects.filter(happened_at__gte=since).order_by("happened_at"):
        total_slots = max(1, event.total_slots)
        total_memory = max(1, event.total_memory_mb)
        result.setdefault(event.worker_id, []).append(
            {
                "timestamp": event.happened_at.timestamp(),
                "slot_utilization_percent": 100 * event.running_jobs / total_slots,
                "memory_utilization_percent": 100 * event.used_memory_mb / total_memory,
                "free_slots": event.free_slots,
                "available_memory_mb": event.available_memory_mb,
                "running_jobs": event.running_jobs,
                "used_memory_mb": event.used_memory_mb,
            }
        )
    return result


def job_rectangles(since):
    latest_by_job = {}
    for event in ExeshJobEvent.objects.filter(happened_at__gte=since).order_by("happened_at"):
        record = latest_by_job.setdefault(
            event.job_id,
            {
                "job_id": event.job_id,
                "execution_id": event.execution_id,
                "worker_id": event.worker_id,
                "job_type": event.job_type,
                "status": "",
                "memory_start_mb": event.memory_start_mb,
                "memory_end_mb": event.memory_end_mb,
                "memory_mb": event.expected_memory_mb,
                "start_timestamp_seconds": 0,
                "finish_timestamp_seconds": 0,
                "expected_finish_timestamp_seconds": 0,
                "expected_duration_seconds": event.expected_duration_ms / 1000,
                "promised_start_timestamp_seconds": 0,
            },
        )
        record["worker_id"] = event.worker_id or record["worker_id"]
        record["status"] = event.status or record["status"]
        record["memory_start_mb"] = event.memory_start_mb
        record["memory_end_mb"] = event.memory_end_mb
        if event.promised_start_at:
            record["promised_start_timestamp_seconds"] = event.promised_start_at.timestamp()
        if event.started_at:
            record["start_timestamp_seconds"] = event.started_at.timestamp()
        if event.finished_at:
            record["finish_timestamp_seconds"] = event.finished_at.timestamp()
        if event.expected_finished_at:
            record["expected_finish_timestamp_seconds"] = event.expected_finished_at.timestamp()
    return [record for record in latest_by_job.values() if record["start_timestamp_seconds"]]


def latest_workers():
    return [
        {
            "worker_id": event.worker_id,
            "total_slots": event.total_slots,
            "free_slots": event.free_slots,
            "total_memory_mb": event.total_memory_mb,
            "available_memory_mb": event.available_memory_mb,
            "running_jobs": event.running_jobs,
            "used_memory_mb": event.used_memory_mb,
        }
        for event in latest_worker_events()
        if event.event_type != "removed"
    ]


def latest_worker_pool():
    workers = [
        {
            "worker_id": event.worker_id,
            "registered": 1,
            "total_slots": event.total_slots,
            "total_memory_mb": event.total_memory_mb,
            "running_jobs": event.running_jobs,
            "used_expected_memory_mb": event.used_memory_mb,
            "free_expected_slots": event.free_slots,
            "free_expected_memory_mb": event.available_memory_mb,
            "last_heartbeat_age_seconds": (timezone.now() - event.happened_at).total_seconds(),
        }
        for event in latest_worker_events()
        if event.event_type != "removed"
    ]
    return {"registered_workers": len(workers), "workers": sorted(workers, key=lambda row: row["worker_id"])}


def latest_worker_events():
    return ExeshWorkerEvent.objects.order_by("worker_id", "-happened_at").distinct("worker_id")


def bucket_ts(dt):
    return int(dt.timestamp())


def percentile(values, q):
    if not values:
        return 0
    values = sorted(values)
    index = min(len(values) - 1, max(0, round((len(values) - 1) * q)))
    return values[index]
