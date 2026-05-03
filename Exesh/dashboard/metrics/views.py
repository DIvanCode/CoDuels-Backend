from django.http import JsonResponse
from django.shortcuts import render

from .event_history import event_dashboard_history


def index(request):
    return render(request, "metrics/index.html")


def history(request):
    minutes = int(request.GET.get("minutes", "30"))
    return JsonResponse(event_dashboard_history(minutes=minutes))
