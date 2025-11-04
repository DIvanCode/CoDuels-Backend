import requests
import json
import time
import threading
import os
from datetime import datetime

class DuelClient:
    def __init__(self, base_url):
        self.base_url = base_url.rstrip('/')
        self.access_token = None
        self.refresh_token = None
        self.current_duel_id = None
        self.is_duel_active = False
        self.should_stop_polling = False
        self.submissions_status = {}
        self.sse_thread = None
        self.polling_threads = []
    
    def print_message(self, message):
        """Корректный вывод сообщений когда отображается >"""
        print(f"\n\n{message}\n")
        print("> ", end='', flush=True)
        
    def login(self):
        """Аутентификация пользователя"""
        nickname = input("Логин: ").strip()
        password = input("Пароль: ").strip()
        
        response = requests.post(
            f"{self.base_url}/users/login",
            json={"nickname": nickname, "password": password}
        )
        
        if response.status_code == 200:
            tokens = response.json()
            self.access_token = tokens["access_token"]
            self.refresh_token = tokens["refresh_token"]
            return True
        else:
            print(f"Ошибка аутентификации: {response.status_code} - {response.text}")
            return False
    
    def refresh_tokens(self):
        """Обновление токенов"""
        if not self.refresh_token:
            return False
            
        response = requests.post(
            f"{self.base_url}/users/refresh",
            json={"refresh_token": self.refresh_token}
        )
        
        if response.status_code == 200:
            tokens = response.json()
            self.access_token = tokens["access_token"]
            self.refresh_token = tokens["refresh_token"]
            return True
        else:
            print("Не удалось обновить токены")
            return False
    
    def make_authenticated_request(self, method, url, **kwargs):
        """Выполнение запроса с автоматическим обновлением токенов при необходимости"""
        headers = kwargs.get('headers', {})
        headers['Authorization'] = f'Bearer {self.access_token}'
        kwargs['headers'] = headers
        
        response = requests.request(method, url, **kwargs)
        
        # Если токен истек, пытаемся обновить и повторить запрос
        if response.status_code == 401:
            if self.refresh_tokens():
                headers['Authorization'] = f'Bearer {self.access_token}'
                response = requests.request(method, url, **kwargs)
        
        return response
    
    def connect_to_duel(self):
        """Подключение к очереди дуэлей"""
        if self.is_duel_active:
            print("Вы уже участвуете в дуэли!")

        self.sse_thread = threading.Thread(target=self._sse_listener)
        self.sse_thread.daemon = True
        self.sse_thread.start()

        print("\nВы вошли в очередь ожидания дуэли!")
        
    def _sse_listener(self):
        """Прослушивание SSE событий"""
        try:
            response = self.make_authenticated_request(
                'GET',
                f"{self.base_url}/duels/connect",
                headers={
                    'Accept': 'text/event-stream'
                },
                stream=True
            )
            
            if response.status_code != 200:
                self.print_message(f"Ошибка подключения: {response.status_code}")
                return
            
            # Читаем напрямую из сырого потока
            while not self.should_stop_polling:
                line = response.raw.readline()
                if not line:
                    break
                    
                line = line.decode('utf-8').strip()
                
                if line == ':':
                    continue
                    
                if line.startswith('event:'):
                    event_type = line.split(':', 1)[1].strip()
                elif line.startswith('data:'):
                    data = json.loads(line.split(':', 1)[1].strip())
                    
                    if event_type == "DuelStarted":
                        self.handle_duel_started(data)
                    elif event_type == "DuelFinished":
                        self.handle_duel_finished(data)
                        
        except Exception as e:
            self.print_message(f"Ошибка: {e}")
    
    def handle_duel_started(self, data):
        """Обработка начала дуэли"""
        self.current_duel_id = data["duel_id"]
        self.is_duel_active = True
        self.should_stop_polling = False
        
        print(f"\n\n=== ДУЭЛЬ НАЧАЛАСЬ! ID: {self.current_duel_id} ===")
        
        # Получаем информацию о дуэли
        duel_info = self.get_duel_info(self.current_duel_id)
        if duel_info:
            # Получаем информацию о противнике
            opponent_info = self.get_user_info(duel_info["opponent_id"])
            opponent_name = opponent_info["nickname"] if opponent_info else "Неизвестный"
            print(f"Противник: {opponent_name}")
            
            # Получаем и отображаем задачу
            self.display_task(duel_info["task_id"])
        
        print("\nДля отправки решения используйте команду: submit <путь_к_файлу>\n")
        print("> ", end='', flush=True)
    
    def handle_duel_finished(self, data):
        if self.current_duel_id != data["duel_id"]:
            return
        """Обработка завершения дуэли"""
        time.sleep(2)  # Ждем 2 секунды
        
        # Получаем финальную информацию о дуэли
        duel_info = self.get_duel_info(self.current_duel_id)
        if duel_info:
            result = duel_info.get("result", "Неизвестно")
            if result == "Win":
                result = "ПОБЕДА!!!"
            elif result == "Lose":
                result = "ПОРАЖЕНИЕ :("
            else:
                result = "НИЧЬЯ."
            print(f"\n\n=== РЕЗУЛЬТАТ ДУЭЛИ: {result} ===")
        
        self.is_duel_active = False
        self.should_stop_polling = True
        self.current_duel_id = None
        
        # Ждем завершения всех потоков опроса
        for thread in self.polling_threads:
            thread.join(timeout=1)
        self.polling_threads.clear()
        
        exit(0)
    
    def get_duel_info(self, duel_id):
        """Получение информации о дуэли"""
        response = self.make_authenticated_request(
            'GET', f"{self.base_url}/duels/{duel_id}"
        )
        
        if response.status_code == 200:
            return response.json()
        else:
            print(f"Ошибка получения информации о дуэли: {response.status_code}")
            return None
    
    def get_user_info(self, user_id):
        """Получение информации о пользователе"""
        response = self.make_authenticated_request(
            'GET', f"{self.base_url}/users/{user_id}"
        )
        
        if response.status_code == 200:
            return response.json()
        else:
            print(f"Ошибка получения информации о пользователе: {response.status_code}")
            return None
    
    def display_task(self, task_id):
        """Отображение условия задачи и тестов"""
        # Получаем информацию о задаче
        task_response = self.make_authenticated_request(
            'GET', f"{self.base_url}/task/{task_id}"
        )
        
        if task_response.status_code != 200:
            print(f"Ошибка получения задачи: {task_response.status_code}")
            return
        
        task_data = task_response.json()
        task = task_data["task"]
        
        print(f"\n=== ЗАДАЧА: {task['title']} ===")
        print(f"Ограничения: TL={task['tl']}ms, ML={task['ml']}MB")
        
        # Получаем и отображаем условие
        statement_content = self.get_task_file(task_id, task["statement"])
        if statement_content:
            print(f"\n--- УСЛОВИЕ ---\n{statement_content}")
        
        # Получаем и отображаем тесты
        print("\n--- ТЕСТЫ ---")
        for test in task["tests"]:
            input_content = self.get_task_file(task_id, test["input"])
            output_content = self.get_task_file(task_id, test["output"])
            
            if input_content and output_content:
                print(f"\nТест {test['order']}:")
                print(f"Входные данные:\n{input_content}")
                print(f"Ожидаемый вывод:\n{output_content}")
    
    def get_task_file(self, task_id, file_path):
        """Получение файла задачи"""
        response = self.make_authenticated_request(
            'GET', f"{self.base_url}/task/{task_id}/{file_path}"
        )
        
        if response.status_code == 200:
            return response.text
        else:
            print(f"Ошибка получения файла {file_path}: {response.status_code}")
            return None
    
    def submit_solution(self, file_path):
        """Отправка решения на проверку"""
        if not self.is_duel_active or not self.current_duel_id:
            print("Вы не участвуете в активной дуэли!")
            return
        
        if not os.path.exists(file_path):
            print(f"Файл {file_path} не найден!")
            return
        
        try:
            with open(file_path, 'r', encoding='utf-8') as f:
                solution_code = f.read()
        except Exception as e:
            print(f"Ошибка чтения файла: {e}")
            return
        
        # Определяем язык программирования по расширению файла
        language = self.detect_language(file_path)
        if not language:
            print("Не удалось определить язык программирования. Укажите расширение файла (.py, .cpp, .java)")
            return
        
        submission_data = {
            "solution": solution_code,
            "language": language
        }
        
        response = self.make_authenticated_request(
            'POST', 
            f"{self.base_url}/duels/{self.current_duel_id}/submissions",
            json=submission_data
        )
        
        if response.status_code == 200:
            submission_id = response.json()["submission_id"]
            print(f"Решение отправлено! ID посылки: {submission_id}")
            
            # Запускаем опрос статуса посылки
            thread = threading.Thread(
                target=self.poll_submission_status,
                args=(submission_id,),
                daemon=True
            )
            self.polling_threads.append(thread)
            thread.start()
        else:
            print(f"Ошибка отправки решения: {response.status_code} - {response.text}")
    
    def detect_language(self, file_path):
        """Определение языка программирования по расширению файла"""
        ext = os.path.splitext(file_path)[1].lower()
        language_map = {
            '.py': 'Python',
            '.cpp': 'C++',
            '.go': 'Golang'
        }
        return language_map.get(ext)
    
    def poll_submission_status(self, submission_id):
        """Опрос статуса посылки"""
        last_message = ""
        
        while not self.should_stop_polling:
            response = self.make_authenticated_request(
                'GET',
                f"{self.base_url}/duels/{self.current_duel_id}/submissions/{submission_id}"
            )
            
            if response.status_code == 200:
                submission_data = response.json()
                status = submission_data["status"]
                message = submission_data.get("message", "")
                
                # Выводим сообщение только если оно изменилось
                if message and message != last_message:
                    self.print_message(f"Посылка {submission_id}: {message}")
                    last_message = message
                
                # Выводим вердикт при завершении
                if status == "Done":
                    verdict = submission_data.get("verdict", "Unknown")
                    message = submission_data.get("message", "")
                    self.print_message(f"=== РЕЗУЛЬТАТ ПОСЫЛКИ {submission_id} ===\nВердикт: {verdict}")
                    if message:
                        print(f"Сообщение: {message}")
                    break
            
            time.sleep(1)  # Опрос каждую секунду
    
    def run(self):
        """Основной цикл программы"""
        if not self.login():
            return
        
        print("\nДобро пожаловать!")

        self.connect_to_duel()
        
        while True:
            try:
                print()  # Пустая строка перед prompt
                command = input("> ").strip()
                
                if command.startswith("submit "):
                    if len(command.split()) >= 2:
                        file_path = command.split(" ", 1)[1]
                        self.submit_solution(file_path)
                    else:
                        print("Использование: submit <путь_к_файлу>")
                elif command:
                    print("Неизвестная команда. Доступные команды: submit")
                    
            except KeyboardInterrupt:
                print("\n\nВыход из программы...")
                self.should_stop_polling = True
                break
            except Exception as e:
                print(f"Ошибка: {e}")

def main():
    # Укажите базовый URL вашего бэкенда
    BASE_URL = "http://85.192.32.240/api"
    
    client = DuelClient(BASE_URL)
    client.run()

if __name__ == "__main__":
    main()