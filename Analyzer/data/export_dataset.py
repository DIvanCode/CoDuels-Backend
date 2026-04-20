import json
import os
import urllib.error
import urllib.parse
import urllib.request


BASE_DIR_PATH = os.path.dirname(os.path.abspath(__file__))
INPUT_FILE_PATH = os.path.join(BASE_DIR_PATH, "action_keys.txt")
OUTPUT_BASE_DIR_PATH = os.path.join(BASE_DIR_PATH, "train")
NORMAL_OUTPUT_DIR_NAME = "normal"
CHEATER_OUTPUT_DIR_NAME = "cheater"
FIRST_CHEAT_DUEL_ID = 100
ACTIONS_ENDPOINT_URL = "http://72.56.8.150/api/actions"
BEARER_TOKEN = "PUT_YOUR_TOKEN_HERE"
USER_RATING = 1500
REQUEST_TIMEOUT_SECONDS = 30


def parse_line(line: str) -> tuple[int, int, str]:
    parts = [part.strip() for part in line.split(",")]
    if len(parts) != 3:
        raise ValueError("expected 3 comma-separated values: duel_id,user_id,task_key")

    duel_id = int(parts[0])
    user_id = int(parts[1])
    task_key = parts[2]
    if not task_key:
        raise ValueError("task_key is empty")

    return duel_id, user_id, task_key


def fetch_actions(duel_id: int, user_id: int, task_key: str) -> list:
    query = urllib.parse.urlencode(
        {
            "duelId": duel_id,
            "userId": user_id,
            "taskKey": task_key,
        }
    )
    url = f"{ACTIONS_ENDPOINT_URL}?{query}"

    request = urllib.request.Request(
        url=url,
        method="GET",
        headers={
            "Authorization": f"Bearer {BEARER_TOKEN}",
            "Accept": "application/json",
        },
    )

    with urllib.request.urlopen(request, timeout=REQUEST_TIMEOUT_SECONDS) as response:
        response_body = response.read().decode("utf-8")

    data = json.loads(response_body)
    if not isinstance(data, list):
        raise ValueError(f"expected JSON array from endpoint, got: {type(data).__name__}")

    return data


def write_export_file(duel_id: int, user_id: int, task_key: str, actions: list) -> None:
    output_dir_name = (
        NORMAL_OUTPUT_DIR_NAME
        if duel_id < FIRST_CHEAT_DUEL_ID
        else CHEATER_OUTPUT_DIR_NAME
    )
    output_dir_path = os.path.join(OUTPUT_BASE_DIR_PATH, output_dir_name)
    os.makedirs(output_dir_path, exist_ok=True)

    file_name = f"duel_{duel_id}_user_{user_id}_task_{task_key}.json"
    output_path = os.path.join(output_dir_path, file_name)

    payload = {
        "actions": actions,
        "user_rating": USER_RATING,
    }

    with open(output_path, "w", encoding="utf-8") as file:
        json.dump(payload, file, ensure_ascii=False)


def main() -> None:
    if not os.path.exists(INPUT_FILE_PATH):
        raise FileNotFoundError(f"input file not found: {INPUT_FILE_PATH}")

    with open(INPUT_FILE_PATH, "r", encoding="utf-8") as file:
        lines = file.readlines()

    for line_number, raw_line in enumerate(lines, start=1):
        line = raw_line.strip()
        if not line or line.startswith("#"):
            continue

        try:
            duel_id, user_id, task_key = parse_line(line)
            actions = fetch_actions(duel_id, user_id, task_key)
            write_export_file(duel_id, user_id, task_key, actions)
            print(f"[OK] line {line_number}: duel={duel_id}, user={user_id}, task={task_key}")
        except (ValueError, urllib.error.URLError, urllib.error.HTTPError, json.JSONDecodeError) as error:
            print(f"[ERROR] line {line_number}: {line} -> {error}")


if __name__ == "__main__":
    main()
