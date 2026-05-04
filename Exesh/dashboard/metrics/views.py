from django.http import JsonResponse
from django.shortcuts import render
from django.utils import timezone
from django.utils.dateparse import parse_datetime

from .chart_rendering import rendered_dashboard


def index(request):
    return render(request, "metrics/index.html")


def history(request):
    start = parse_dashboard_datetime(request.GET.get("start"))
    end = parse_dashboard_datetime(request.GET.get("end"))
    if start is None or end is None:
        return JsonResponse({"error": "start and end with timezone are required"}, status=400)
    return JsonResponse(rendered_dashboard(start=start, end=end))


def parse_dashboard_datetime(value):
    if not value:
        return None
    parsed = parse_datetime(value)
    if parsed is None:
        return None
    if timezone.is_naive(parsed):
        return None
    return parsed
