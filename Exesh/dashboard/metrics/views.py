from django.http import JsonResponse
from django.shortcuts import render

from .chart_rendering import rendered_dashboard


def index(request):
    return render(request, "metrics/index.html")


def history(request):
    minutes = int(request.GET.get("minutes", "30"))
    return JsonResponse(rendered_dashboard(minutes=minutes))
