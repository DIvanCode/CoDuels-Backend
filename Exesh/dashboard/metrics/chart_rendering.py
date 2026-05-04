from datetime import datetime
from html import escape
from time import perf_counter

from .event_history import event_dashboard_history


COLORS = {
    "started": "#58b9d7",
    "finished": "#46c278",
    "picks": "#e0b64b",
    "p50": "#7297ff",
    "p95": "#e56767",
}

PALETTE = ["#58b9d7", "#46c278", "#e0b64b", "#e56767", "#7297ff", "#ba7cff"]


def rendered_dashboard(start, end):
    started = perf_counter()
    history = event_dashboard_history(start=start, end=end)
    execution = history["execution"]
    latest_point = execution[-1] if execution else {}
    domain = chart_domain(history)
    rendered = {
        "status": status_html(history, latest_point, domain),
        "kpis": kpis(history, latest_point),
        "charts": {
            "throughput": line_chart(
                [
                    series_from_values("started/s", COLORS["started"], execution, "started_rate"),
                    series_from_values("finished/s", COLORS["finished"], execution, "finished_rate"),
                    series_from_values("pick/s", COLORS["picks"], execution, "scheduler_pick_rate"),
                ],
                domain=domain,
            ),
            "fairness": line_chart(
                [
                    series_from_values("priority p50", COLORS["p50"], execution, "priority_p50"),
                    series_from_values("priority p95", COLORS["p95"], execution, "priority_p95"),
                    series_from_values("progress pick p10", COLORS["picks"], execution, "progress_pick_p10"),
                    series_from_values("progress pick p90", COLORS["started"], execution, "progress_pick_p90"),
                ],
                domain=domain,
            ),
            "execution_priority": line_chart(execution_series(history["executions"], "priority"), domain=domain),
            "execution_progress": line_chart(execution_series(history["executions"], "progress_ratio"), forced_max=1, domain=domain),
            "worker_slots": line_chart(worker_series(history["workers"], "slot_utilization_percent"), domain=domain),
            "worker_memory": line_chart(worker_series(history["workers"], "memory_utilization_percent"), domain=domain),
            "rectangles": rectangles_svg(history["jobs"], domain=domain),
        },
        "tables": {
            "worker_pool": worker_pool_table(history["latest_worker_pool"]),
            "workers": workers_table(history["latest_workers"]),
        },
        "meta": {
            **history["meta"],
            "render_elapsed_ms": round((perf_counter() - started) * 1000, 2),
        },
    }
    return rendered


def chart_domain(history):
    meta = history["meta"]
    return {
        "start": meta["window_start"],
        "end": meta["window_end"],
        "timezone_offset_seconds": meta["timezone_offset_seconds"],
    }


def status_html(history, latest_point, domain):
    meta = history["meta"]
    raw_events = meta["execution_events"] + meta["worker_events"] + meta["job_events"]
    chart_points = meta["execution_points"] + meta["execution_pick_points"] + meta["worker_points"] + meta["job_events"]
    timestamp = latest_point.get("timestamp")
    text = "no events" if not timestamp else format_time(timestamp, domain)
    return (
        f'<span class="ok">{len(history["execution"])} execution points</span>'
        f" | {chart_points} rendered points | {raw_events} raw events | {fmt(meta['elapsed_ms'])} ms db | {text}"
    )


def kpis(history, point):
    workers = history["latest_worker_pool"].get("registered_workers") or len(history["latest_workers"])
    return {
        "startedRate": fmt(point.get("started_rate", 0)),
        "finishedRate": fmt(point.get("finished_rate", 0)),
        "pickRate": fmt(point.get("scheduler_pick_rate", 0)),
        "durationP95": f"{fmt(point.get('duration_p95', 0))}s",
        "priorityP95": fmt(point.get("priority_p95", 0)),
        "workerCount": fmt(workers),
    }


def series_from_values(name, color, rows, field):
    return {
        "name": name,
        "color": color,
        "points": [{"timestamp": row["timestamp"], "value": row.get(field, 0) or 0} for row in rows],
    }


def execution_series(executions, field):
    result = []
    for index, execution in enumerate(executions):
        points = execution.get("points") or []
        if not points:
            continue
        result.append(
            {
                "name": short_execution(execution["execution_id"]),
                "color": PALETTE[index % len(PALETTE)],
                "points": [{"timestamp": point["timestamp"], "value": point.get(field, 0) or 0} for point in points],
            }
        )
    return result


def worker_series(workers, field):
    result = []
    for index, worker_id in enumerate(sorted(workers)):
        result.append(
            {
                "name": short_worker(worker_id),
                "color": PALETTE[index % len(PALETTE)],
                "points": [
                    {"timestamp": point["timestamp"], "value": point.get(field, 0) or 0}
                    for point in workers[worker_id]
                ],
            }
        )
    return result


def line_chart(series, forced_max=None, domain=None):
    width = 900
    height = 260
    pad = {"left": 46, "right": 16, "top": 14, "bottom": 58}
    values = [point["value"] for line in series for point in line["points"] if is_number(point["value"])]
    max_value = forced_max if forced_max is not None else max([1, *values]) * 1.12
    min_ts = domain["start"] if domain else 0
    max_ts = domain["end"] if domain else 1
    plot_width = width - pad["left"] - pad["right"]
    plot_height = height - pad["top"] - pad["bottom"]

    def x(timestamp):
        return pad["left"] + plot_width * (timestamp - min_ts) / max(0.001, max_ts - min_ts)

    def y(value):
        return height - pad["bottom"] - plot_height * value / max(0.001, max_value)

    parts = [
        f'<svg viewBox="0 0 {width} {height}" role="img">',
        "<style>text{font:12px system-ui;fill:#9299a8}.grid{stroke:#333846}.legend{fill:#edf0f5}</style>",
    ]
    for i in range(5):
        gy = pad["top"] + plot_height * i / 4
        label = fmt(max_value - max_value * i / 4)
        parts.append(f'<line class="grid" x1="{pad["left"]}" x2="{width - pad["right"]}" y1="{gy}" y2="{gy}"/>')
        parts.append(f'<text x="6" y="{gy + 4}">{escape(label)}</text>')

    for tick in time_ticks(min_ts, max_ts, max_labels=max(2, plot_width // 140)):
        tx = x(tick)
        parts.append(f'<line class="grid" x1="{tx:.2f}" x2="{tx:.2f}" y1="{pad["top"]}" y2="{height - pad["bottom"]}"/>')
        parts.append(f'<text text-anchor="middle" x="{tx:.2f}" y="{height - 34}">{escape(format_time(tick, domain))}</text>')

    for line in series:
        points = line["points"]
        if not points:
            continue
        path = []
        for index, point in enumerate(points):
            command = "M" if index == 0 else "L"
            path.append(f"{command}{x(point['timestamp']):.2f},{y(point['value']):.2f}")
        parts.append(
            f'<path d="{" ".join(path)}" fill="none" stroke="{escape(line["color"])}" stroke-width="2"/>'
        )

    legend_x = pad["left"]
    legend_y = height - 12
    for line in series[:24]:
        name = escape(line["name"])
        parts.append(f'<rect x="{legend_x}" y="{legend_y - 8}" width="10" height="10" fill="{escape(line["color"])}"/>')
        parts.append(f'<text class="legend" x="{legend_x + 14}" y="{legend_y + 1}">{name}</text>')
        legend_x += min(160, 34 + len(line["name"]) * 7)
    if len(series) > 24:
        parts.append(f'<text x="{legend_x}" y="{legend_y + 1}">+{len(series) - 24}</text>')

    parts.append("</svg>")
    return "".join(parts)


def rectangles_svg(jobs, domain):
    if not jobs:
        return '<div style="padding:16px;color:#9299a8">No retained job rectangles yet.</div>'
    min_start = domain["start"]
    max_finish = domain["end"]
    max_memory = max([1, *[job.get("memory_end_mb") or job.get("memory_mb") or 0 for job in jobs]])
    workers = sorted(set([job.get("worker_id") or "unknown" for job in jobs]))
    width = max(960, int((max_finish - min_start) * 120 + 220))
    lane_height = 250
    height = len(workers) * lane_height + 42
    plot_left = 150
    plot_top = 28
    plot_width = width - plot_left - 24

    def x(timestamp):
        return plot_left + ((timestamp - min_start) / max(0.001, max_finish - min_start)) * plot_width

    def y(worker_index, memory):
        return plot_top + worker_index * lane_height + (1 - memory / max_memory) * (lane_height - 46)

    parts = [
        f'<svg width="{width}" height="{height}" viewBox="0 0 {width} {height}" role="img">',
        "<style>text{font:12px system-ui;fill:#9299a8}.axis{stroke:#333846}.rect{stroke:#101217;stroke-width:1}.label{fill:#edf0f5}</style>",
    ]
    for index, worker in enumerate(workers):
        y0 = plot_top + index * lane_height
        parts.append(f'<text class="label" x="12" y="{y0 + 18}">{escape(short_worker(worker))}</text>')
        parts.append(f'<line class="axis" x1="{plot_left}" x2="{width - 20}" y1="{y0}" y2="{y0}"/>')
        parts.append(f'<text x="96" y="{y(index, max_memory) + 4}">{fmt(max_memory)} MB</text>')
        parts.append(f'<text x="110" y="{y(index, 0) + 4}">0 MB</text>')

    axis_y = height - 16
    for tick in time_ticks(min_start, max_finish, max_labels=max(2, plot_width // 140)):
        tx = x(tick)
        parts.append(f'<line class="axis" x1="{tx:.2f}" x2="{tx:.2f}" y1="{plot_top}" y2="{height - 36}"/>')
        parts.append(f'<text text-anchor="middle" x="{tx:.2f}" y="{axis_y}">{escape(format_time(tick, domain))}</text>')

    for index, job in enumerate(jobs):
        worker_index = max(0, workers.index(job.get("worker_id") or "unknown"))
        start = job.get("start_timestamp_seconds") or min_start
        finish = positive_time(job.get("finish_timestamp_seconds")) or max_finish
        memory_start = job.get("memory_start_mb") or 0
        memory_end = job.get("memory_end_mb") or job.get("memory_mb") or 0
        rect_x = x(start)
        rect_y = y(worker_index, memory_end)
        rect_w = max(2, x(finish) - rect_x)
        rect_h = max(2, y(worker_index, memory_start) - rect_y)
        title = (
            f"{job.get('status') or 'running'} {job.get('job_type') or ''}\n"
            f"job={job.get('job_id')}\nexecution={job.get('execution_id')}\n"
            f"{fmt(finish - start)}s, {fmt(memory_end - memory_start)} MB"
        )
        parts.append(
            f'<rect class="rect" x="{rect_x:.2f}" y="{rect_y:.2f}" width="{rect_w:.2f}" height="{rect_h:.2f}" '
            f'rx="2" fill="{status_color(job.get("status"), index)}" opacity="0.82"><title>{escape(title)}</title></rect>'
        )
    parts.append("</svg>")
    return "".join(parts)


def worker_pool_table(worker_pool):
    rows = []
    for worker in worker_pool.get("workers", []):
        rows.append(
            [
                short_worker(worker["worker_id"]),
                fmt(worker.get("total_slots", 0)),
                fmt(worker.get("running_jobs", 0)),
                f"{fmt(worker.get('total_memory_mb', 0))} MB",
                f"{fmt(worker.get('used_expected_memory_mb', 0))} MB",
                f"{fmt(worker.get('free_expected_memory_mb', 0))} MB",
                f"{fmt(worker.get('last_heartbeat_age_seconds', 0))}s",
            ]
        )
    return table(["worker", "slots", "running", "memory", "used", "free", "heartbeat age"], rows)


def workers_table(workers):
    rows = []
    for worker in workers:
        rows.append(
            [
                short_worker(worker["worker_id"]),
                fmt(worker.get("total_slots", 0)),
                fmt(worker.get("free_slots", 0)),
                f"{fmt(worker.get('total_memory_mb', 0))} MB",
                f"{fmt(worker.get('available_memory_mb', 0))} MB",
                fmt(worker.get("running_jobs", 0)),
                f"{fmt(worker.get('used_memory_mb', 0))} MB",
            ]
        )
    return table(["worker", "slots", "free", "memory", "available", "running", "used"], rows)


def table(headers, rows):
    if not rows:
        return '<div style="padding:12px;color:#9299a8">No data.</div>'
    head = "".join(f"<th>{escape(header)}</th>" for header in headers)
    body = "".join("<tr>" + "".join(f"<td>{escape(str(cell))}</td>" for cell in row) + "</tr>" for row in rows)
    return f"<table><thead><tr>{head}</tr></thead><tbody>{body}</tbody></table>"


def positive_time(value):
    return value if value and value > 0 else None


def status_color(status, index):
    if status == "OK":
        return "#46c278"
    if status in ("TL", "ML"):
        return "#e0b64b"
    if status:
        return "#e56767"
    return PALETTE[index % len(PALETTE)]


def short_worker(worker_id):
    return str(worker_id or "").replace("http://", "")


def short_execution(execution_id):
    value = str(execution_id or "")
    return value[:8] if len(value) > 10 else value


def format_time(timestamp, domain):
    return datetime.utcfromtimestamp(timestamp + domain["timezone_offset_seconds"]).strftime("%H:%M:%S")


def time_ticks(start, end, max_labels):
    if end <= start:
        return [start]
    max_labels = max(2, int(max_labels))
    step = nice_time_step((end - start) / (max_labels - 1))
    first = (int(start) // step) * step
    if first < start:
        first += step
    ticks = []
    current = first
    while current <= end and len(ticks) < max_labels:
        ticks.append(current)
        current += step
    if not ticks:
        return [start, end]
    return ticks


def nice_time_step(raw_step):
    for step in [1, 2, 5, 10, 15, 30, 60, 120, 300, 600, 900, 1800, 3600, 7200, 14400]:
        if raw_step <= step:
            return step
    return 28800


def fmt(value):
    value = float(value or 0)
    if abs(value) >= 1000:
        return f"{value:.0f}"
    if abs(value) >= 10:
        return f"{value:.1f}"
    return f"{value:.2f}"


def is_number(value):
    return isinstance(value, (int, float))
