import os
import shlex
from pathlib import Path


BASE_DIR = Path(__file__).resolve().parent.parent

SECRET_KEY = "exesh-dashboard-local-dev"
DEBUG = True
ALLOWED_HOSTS = ["*"]

INSTALLED_APPS = [
    "django.contrib.staticfiles",
    "metrics",
]

MIDDLEWARE = [
    "django.middleware.security.SecurityMiddleware",
    "django.middleware.common.CommonMiddleware",
]

ROOT_URLCONF = "exesh_dashboard.urls"

TEMPLATES = [
    {
        "BACKEND": "django.template.backends.django.DjangoTemplates",
        "DIRS": [BASE_DIR / "templates"],
        "APP_DIRS": True,
        "OPTIONS": {"context_processors": []},
    }
]

WSGI_APPLICATION = "exesh_dashboard.wsgi.application"

def postgres_database_config():
    connection_string = os.getenv("EXESH_DASHBOARD_DB_CONNECTION_STRING", "")
    values = {}
    if connection_string:
        for item in shlex.split(connection_string):
            if "=" in item:
                key, value = item.split("=", 1)
                values[key] = value
    return {
        "ENGINE": "django.db.backends.postgresql",
        "HOST": os.getenv("EXESH_DASHBOARD_DB_HOST", values.get("host", "localhost")),
        "PORT": os.getenv("EXESH_DASHBOARD_DB_PORT", values.get("port", "5433")),
        "NAME": os.getenv("EXESH_DASHBOARD_DB_NAME", values.get("database", values.get("dbname", "exesh"))),
        "USER": os.getenv("EXESH_DASHBOARD_DB_USER", values.get("user", "coordinator")),
        "PASSWORD": os.getenv("EXESH_DASHBOARD_DB_PASSWORD", values.get("password", "secret")),
    }


DATABASES = {"default": postgres_database_config()}

STATIC_URL = "static/"
STATICFILES_DIRS = [BASE_DIR / "static"]

DEFAULT_AUTO_FIELD = "django.db.models.BigAutoField"
