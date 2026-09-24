# Grafana: дашборды и алерты CoDuels

Этот документ — инструкция для ручного создания дашбордов. Источники данных: Grafana Cloud Prometheus и Loki. В запросах используется метка `env="prod"`, которую добавляет Alloy. Интервал сбора метрик приложений — 15 секунд, метрик хоста — 30 секунд. Для графиков ниже выберите шаг не меньше 15 секунд.

Панели с метриками HTTP используют один и тот же histogram `http_server_request_duration_seconds` у Duely, Taski, Exesh и Analyzer. Метки: `service`, `http_request_method`, `http_route`, `http_response_status_code`. Путь метрики у Analyzer — внутренний порт 8001; HTTP API остаётся на 8000. Счётчики являются частью histogram (`_count`, `_bucket`, `_sum`). Метрики Duely `queued_*`, `submissions_testing_failed_last_5m`, `outbox_dispatch` и `status_poller_last_success_timestamp_seconds` — gauges.

## 1. Состояние сервисов и HTTP

| Панель | PromQL / LogQL | Единица |
| --- | --- | --- |
| Доступность целей | `up{env="prod",service=~"duely|taski|exesh-coordinator|exesh-worker|analyzer|alloy"}` | 0/1 |
| Запросы по сервисам и статусам | `sum by (service, http_response_status_code) (rate(http_server_request_duration_seconds_count{env="prod"}[5m]))` | запросы/с |
| HTTP 5xx приложений | `sum by (service) (increase(http_server_request_duration_seconds_count{env="prod",http_response_status_code=~"5.."}[5m]))` | запросы за 5 мин |
| Доля HTTP 5xx | `sum by (service) (rate(http_server_request_duration_seconds_count{env="prod",http_response_status_code=~"5.."}[5m])) / sum by (service) (rate(http_server_request_duration_seconds_count{env="prod"}[5m]))` | доля, показать в % |
| p95 времени ответа | `histogram_quantile(0.95, sum by (service, le) (rate(http_server_request_duration_seconds_bucket{env="prod"}[5m])))` | секунды |
| 5xx на внешнем Nginx | `sum by (status) (count_over_time({env="prod",service="nginx"} | json | status=~"5.." [5m]))` | запросы за 5 мин |
| Загруженная модель Analyzer | `coduels_analyzer_model_loaded{env="prod",service="analyzer"}` | 0/1; `role=production` или `baseline` |

Панель Nginx дополняет метрики приложений: показывает ошибки прокси даже при недоступности upstream. URI содержится только в строке лога, а не в метке, чтобы не создавать метки с неограниченным числом значений. Для подробностей откройте строки `{env="prod",service="nginx"} | json | status=~"5.."`.

## 2. Очередь проверки и доставка статусов

| Панель | PromQL | Единица |
| --- | --- | --- |
| Submission по статусам | `submissions{env="prod",service="duely"}` | шт. |
| CodeRun по статусам | `runs{env="prod",service="duely"}` | шт. |
| Queued старше 10 минут | `queued_stale{env="prod",service="duely"}` | шт.; `kind=submission` или `code_run` |
| Возраст старейшего Queued | `queued_oldest_age_seconds{env="prod",service="duely"}` | секунды |
| Testing Failed за последние 5 минут | `submissions_testing_failed_last_5m{env="prod",service="duely"}` | шт. |
| Outbox по типу и статусу | `outbox_dispatch{env="prod",service="duely"}` | шт.; `type=TestSolution` или `RunUserCode` |
| Время с успешного опроса Duely | `time() - status_poller_last_success_timestamp_seconds{env="prod",service="duely"}` | секунды; `source=taski` или `exesh` |
| Незавершённые решения Taski | `coduels_taski_solutions_in_progress{env="prod",service="taski"}` | шт. |
| Решения Taski старше 10 минут | `coduels_taski_solutions_in_progress_older_than_10m{env="prod",service="taski"}` | шт. |
| Возраст старейшего решения Taski | `coduels_taski_solutions_oldest_in_progress_age_seconds{env="prod",service="taski"}` | секунды |
| Время с успешного опроса Exesh из Taski | `time() - coduels_taski_rest_poller_last_success_timestamp_seconds{env="prod",service="taski"}` | секунды |
| Неудачные циклы опроса Taski | `increase(coduels_taski_rest_poller_failures_total{env="prod",service="taski"}[5m])` | циклы за 5 мин |

`submissions_testing_failed_last_5m` считает завершённые Submission с точным вердиктом `Testing Failed` по времени завершения, поэтому одна ошибка остаётся видимой примерно пять минут. Для существовавших до миграции завершённых Submission время завершения отсутствует; исторические ошибки не попадают в это окно.

Если ожидающих работ нет, цикл опроса может быть успешным без обращения к соседнему сервису. Поэтому свежий timestamp опроса сам по себе не подтверждает доступность Taski или Exesh; смотрите `up`, HTTP и возраст очереди вместе.

## 3. Исполнение Exesh

Все запросы ниже используют `service="exesh-coordinator"`, поскольку состояние планировщика находится в координаторе.

| Панель | PromQL | Единица |
| --- | --- | --- |
| Активные исполнения | `coduels_exesh_coordinator_active_executions{env="prod",service="exesh-coordinator"}` | шт. |
| Готовые, обещанные и запущенные задания | `{__name__=~"coduels_exesh_coordinator_(ready|promised|started)_jobs",env="prod",service="exesh-coordinator"}` | шт. |
| Зарегистрированные воркеры | `coduels_exesh_coordinator_workers{env="prod",service="exesh-coordinator"}` | шт. |
| Занятые и доступные слоты | `{__name__=~"coduels_exesh_coordinator_worker_slots_(used|total)",env="prod",service="exesh-coordinator"}` | слоты |
| Загрузка лимита планировщика | `coduels_exesh_coordinator_now_weight{env="prod",service="exesh-coordinator"} / coduels_exesh_coordinator_capacity_weight{env="prod",service="exesh-coordinator"}` | доля, показать в % |

## 4. Логи

Используйте панель **Logs** с Loki. Каждая строка содержит `service`, `service_name` и `container`; метка `service=exesh` объединяет координатор и воркеры, а `service_name` позволяет разделить их.

| Панель | LogQL |
| --- | --- |
| Все логи | `{env="prod"}` |
| Ошибки Duely, Taski, Exesh | `{env="prod",service=~"duely|taski|exesh",level=~"(?i)(error|critical)"}` |
| Ошибки всех сервисов с распознанным уровнем | `{env="prod",level=~"(?i)(error|crit|critical|alert|emerg|fatal|panic)"}` |
| Поиск ошибок в остальных форматах логов | `{env="prod"} |~ "(?i)(error|critical|fatal|panic)"` |
| Число ошибок по сервисам | `sum by (service) (count_over_time({env="prod",service=~"duely|taski|exesh",level=~"(?i)(error|critical)"}[5m]))` |
| Ошибки Nginx | `{env="prod",service="nginx"} | json | status=~"5.."` |

Для отладки откройте логи конкретного контейнера через `{env="prod",container="worker-1"}`. У Nginx код ответа извлекается из JSON строки при запросе; это не метка потока. Alloy распознаёт уровни в логах Duely, Taski, Exesh, Analyzer, Nginx, Caddy и собственных логах. Поиск по тексту полезен для остальных форматов, но может совпасть с обычным сообщением, в котором упомянута ошибка.

## 5. Ресурсы хоста и наблюдаемость сбора

| Панель | PromQL | Единица |
| --- | --- | --- |
| Загрузка CPU хоста | `100 * (1 - avg(rate(node_cpu_seconds_total{env="prod",service="host",mode="idle"}[5m])))` | % |
| Использование памяти хоста | `100 * (1 - node_memory_MemAvailable_bytes{env="prod",service="host"} / node_memory_MemTotal_bytes{env="prod",service="host"})` | % |
| Использование корневой файловой системы | `100 * (1 - node_filesystem_avail_bytes{env="prod",service="host",mountpoint="/"} / node_filesystem_size_bytes{env="prod",service="host",mountpoint="/"})` | % |
| Сбор метрик Alloy | `up{env="prod",service="alloy"}` | 0/1 |

В этом разделе нет метрик PostgreSQL. Перед включением панели диска проверьте, что экспортёр на конкретном хосте действительно публикует `mountpoint="/"`.

## Алерты

Создайте правила с интервалом оценки 1 минута. Время `for` означает, что условие должно сохраняться указанное время. Для критических `up` и Alloy настройте **No data → Alerting**: если Alloy перестанет отправлять данные, выражение `up == 0` само не получит нового значения.

| Событие | Условие | `for` | Уровень |
| --- | --- | --- | --- |
| Testing Failed | `submissions_testing_failed_last_5m{env="prod",service="duely"} > 0` | 0 мин | critical |
| Submission или CodeRun застрял в Queued | `queued_stale{env="prod",service="duely"} > 0` | 2 мин | critical |
| HTTP 5xx приложения | `sum by (service) (increase(http_server_request_duration_seconds_count{env="prod",http_response_status_code=~"5.."}[5m])) > 0` | 0 мин | warning |
| HTTP 5xx на Nginx | `sum(count_over_time({env="prod",service="nginx"} | json | status=~"5.." [5m])) > 0` | 0 мин | warning |
| Лог уровня Error/Critical в Duely, Taski или Exesh | `sum by (service) (count_over_time({env="prod",service=~"duely|taski|exesh",level=~"(?i)(error|critical)"}[5m])) > 0` | 0 мин | warning |
| Недоступна цель Prometheus | `up{env="prod",service=~"duely|taski|exesh-coordinator|exesh-worker|analyzer|alloy"} == 0` | 2 мин | critical |
| Нет данных от Alloy | `up{env="prod",service="alloy"}` возвращает No data | 2 мин | critical |
| Слишком старый успешный опрос Duely | `time() - status_poller_last_success_timestamp_seconds{env="prod",service="duely"} > 120` | 2 мин | warning |
| Слишком старый успешный опрос Taski | `time() - coduels_taski_rest_poller_last_success_timestamp_seconds{env="prod",service="taski"} > 120` | 2 мин | warning |
| Очередь Taski старше 10 минут | `coduels_taski_solutions_in_progress_older_than_10m{env="prod",service="taski"} > 0` | 2 мин | critical |
| Нет зарегистрированных воркеров | `coduels_exesh_coordinator_workers{env="prod",service="exesh-coordinator"} == 0` | 2 мин | critical |
| Есть готовые задания при свободных слотах | `coduels_exesh_coordinator_ready_jobs{env="prod",service="exesh-coordinator"} > 0 and on (env, service) coduels_exesh_coordinator_worker_slots_used{env="prod",service="exesh-coordinator"} < coduels_exesh_coordinator_worker_slots_total{env="prod",service="exesh-coordinator"}` | 5 мин | warning |
| Повторные ошибки опроса Taski | `increase(coduels_taski_rest_poller_failures_total{env="prod",service="taski"}[5m]) > 0` | 0 мин | warning |
| Outbox повторяет отправку работы | `outbox_dispatch{env="prod",service="duely",type=~"TestSolution|RunUserCode",status="ToRetry"} > 0` | 5 мин | warning |
| Analyzer работает на резервной модели | `coduels_analyzer_model_loaded{env="prod",service="analyzer",role="baseline"} == 1` | 5 мин | warning |
| Высокая нагрузка CPU хоста | `100 * (1 - avg(rate(node_cpu_seconds_total{env="prod",service="host",mode="idle"}[5m]))) > 90` | 10 мин | warning |
| Мало свободной памяти хоста | `100 * (1 - node_memory_MemAvailable_bytes{env="prod",service="host"} / node_memory_MemTotal_bytes{env="prod",service="host"}) > 90` | 5 мин | warning |
| Заполнен диск хоста | `100 * (1 - node_filesystem_avail_bytes{env="prod",service="host",mountpoint="/"} / node_filesystem_size_bytes{env="prod",service="host",mountpoint="/"}) > 85` | 5 мин | warning |

Для событийных правил по 5xx и ошибкам логов установите **No data → Normal**: при отсутствии таких событий запрос может вернуть пустой вектор. При создании Loki-алерта в Grafana, если редактор не принимает LogQL как финальное условие, добавьте выражение **Reduce: last** и **Threshold: > 0** к запросу из таблицы. Для `Testing Failed` и 5xx используйте `for=0`, потому что данные уже агрегированы за пять минут и дополнительное ожидание может пропустить короткое событие. Настройте группировку уведомлений по `service` и `kind`, чтобы не получить много одинаковых сообщений.
