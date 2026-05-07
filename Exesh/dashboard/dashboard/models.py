from django.db import models


class ExeshExecutionEvent(models.Model):
    happened_at = models.DateTimeField()
    event_type = models.TextField()
    execution_id = models.CharField(max_length=36)
    priority = models.FloatField()
    progress_ratio = models.FloatField()
    duration_seconds = models.FloatField()
    status = models.TextField()

    class Meta:
        managed = False
        db_table = "exesh_execution_events"


class ExeshJobEvent(models.Model):
    happened_at = models.DateTimeField()
    event_type = models.TextField()
    job_id = models.CharField(max_length=40)
    execution_id = models.CharField(max_length=36)
    worker_id = models.TextField()
    job_type = models.TextField()
    status = models.TextField()
    expected_memory_mb = models.IntegerField()
    expected_duration_ms = models.IntegerField()
    memory_start_mb = models.IntegerField()
    memory_end_mb = models.IntegerField()
    promised_start_at = models.DateTimeField(null=True)
    started_at = models.DateTimeField(null=True)
    finished_at = models.DateTimeField(null=True)
    expected_finished_at = models.DateTimeField(null=True)
    actual_duration_seconds = models.FloatField()
    scheduler_latency_seconds = models.FloatField()

    class Meta:
        managed = False
        db_table = "exesh_job_events"


class ExeshWorkerEvent(models.Model):
    happened_at = models.DateTimeField()
    event_type = models.TextField()
    worker_id = models.TextField()
    total_slots = models.IntegerField()
    total_memory_mb = models.IntegerField()
    free_slots = models.IntegerField()
    available_memory_mb = models.IntegerField()
    running_jobs = models.IntegerField()
    used_memory_mb = models.IntegerField()

    class Meta:
        managed = False
        db_table = "exesh_worker_events"
