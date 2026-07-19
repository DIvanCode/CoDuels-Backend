from django.urls import path

from dashboard import views


urlpatterns = [
    path("health", views.health, name="health"),
    path("", views.index, name="index"),
]
