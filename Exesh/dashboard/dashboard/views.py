from datetime import timedelta

from django.shortcuts import render
from django.utils import timezone
from django.utils.dateparse import parse_datetime

from .chart_rendering import rendered_dashboard


def index(request):
    start, end = dashboard_window(request)
    payload = rendered_dashboard(start=start, end=end)
    return render(
        request,
        "dashboard/index.html",
        {
            **payload,
            "start_value": format_datetime_local(start),
            "end_value": format_datetime_local(end),
        },
    )


def parse_dashboard_datetime(value):
    if not value:
        return None
    parsed = parse_datetime(value)
    if parsed is None:
        return None
    if timezone.is_naive(parsed):
        return timezone.make_aware(parsed, timezone.get_current_timezone())
    return parsed


def dashboard_window(request):
    end = parse_dashboard_datetime(request.GET.get("end")) or timezone.localtime()
    start = parse_dashboard_datetime(request.GET.get("start")) or end - timedelta(minutes=30)
    if start > end:
        start, end = end, start
    return start, end


def format_datetime_local(value):
    return timezone.localtime(value).strftime("%Y-%m-%dT%H:%M:%S")
