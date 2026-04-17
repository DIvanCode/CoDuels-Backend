# Analyzer

ML anti-cheat сервис с двумя моделями:

- `baseline`: Logistic Regression (`artifacts/baseline_logreg.pkl`)
- `production`: Random Forest (`artifacts/prod_random_forest.pkl`)

`production` используется в `/predict` по умолчанию. Если production-артефакт отсутствует, сервис автоматически использует baseline как fallback.

## CI/CD поток

1. При `push` в `master` запускается `.github/workflows/analyzer_push.yml`.
2. В `build` job:
   - запускается `python3 train.py --data-dir data/train`;
   - в логах job печатаются метрики для baseline и production;
   - в `artifacts/` сохраняются:
     - `baseline_logreg.pkl`
     - `baseline_logreg.metrics.json`
     - `prod_random_forest.pkl`
     - `prod_random_forest.metrics.json`
   - ansible build playbook собирает Docker image `divancode74/coduels-analyzer:${GITHUB_SHA}`.
3. В `deploy` job ansible перезапускает контейнер на сервере.
4. При старте контейнера модель загружается в память (из `artifacts/prod_random_forest.pkl`), после чего `/predict` готов к запросам.

## Формат данных

Тренировочный датасет хранится в репозитории:

- `data/train/normal/*.json`
- `data/train/cheater/*.json`

Формат файла:

```json
{
  "actions": [...],
  "user_rating": 1500
}
```

`actions` должны соответствовать схемам из `domain/user_actions.py`.

## Локальное обучение

Обучить обе модели (baseline + production):

```bash
python3 train.py --data-dir data/train
```

## Локальный Jupyter

Есть пример ноутбука: `notebooks/analyzer_training.ipynb`.

Он использует модули проекта (`ml.training`, `ml.trainers`) и строит график метрик через `matplotlib`.

Рекомендуемые шаги:

```bash
pip3 install -r requirements.txt
cd notebooks
jupyter notebook
```

## Environment переменные runtime

- `ANALYZER_MODEL_PATH` (legacy override, самый высокий приоритет)
- `ANALYZER_PROD_MODEL_PATH`
- `ANALYZER_BASELINE_MODEL_PATH`

## Secrets

- `DOCKER_PASSWORD`
- `SSH_PASSWORD`
