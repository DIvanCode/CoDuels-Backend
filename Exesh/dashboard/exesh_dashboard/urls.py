from django.urls import path

from metrics import views


urlpatterns = [
    path("", views.index, name="index"),
    path("api/history/", views.history, name="history"),
]
