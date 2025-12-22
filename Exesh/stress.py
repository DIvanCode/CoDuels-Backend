import os
import subprocess
import psycopg2
import time
import requests
import random


class Coordinator:
    base_config = """
env: docker
http_server:
  addr: 0.0.0.0:{PORT}
  metrics_addr: 0.0.0.0:{METRICS_PORT}
storage:
  connection_string: host=exesh_postgres port=5432 database=exesh user=coordinator password=secret
  init_timeout: 60s
filestorage:
  root_dir: file_storage/cluster-{CLUSTER}/coordinator
  trasher:
    workers: 1
    collector_iterations_delay: 60
    worker_iterations_delay: 5
input_provider:
  filestorage_bucket_ttl: 15m
  artifact_ttl: 5m
job_factory:
  output:
    compiled_cpp: bin
    run_output: output
    check_verdict: verdict
  filestorage_endpoint: http://coordinator-{CLUSTER}:{PORT}
execution_scheduler:
  executions_interval: 1s
  max_concurrency: 1
  execution_retry_after: 1500s
worker_pool:
  worker_die_after: 10s
artifact_registry:
  artifact_ttl: 3m
sender:
  brokers:
    - kafka:9092
  topic: exesh.step-updates
"""

    base_docker_compose = """
    coordinator-{CLUSTER}:
        build:
            context: ../../
            dockerfile: Dockerfile
        command:
            - /app/bin/coordinator
        container_name: coordinator-{CLUSTER}
        ports:
            - "{PORT}:{PORT}"
        networks:
            - coduels
        environment:
            CONFIG_PATH: /{CLUSTER_PATH}/coordinator.yml
        depends_on:
            postgres:
                condition: service_healthy
        volumes:
            - ../../{CLUSTER_PATH}:/{CLUSTER_PATH}:ro
"""

    def __init__(self, cluster, port, metrics_port):
        self.cluster = cluster
        self.port = port
        self.metrics_port = metrics_port

    def generate_config(self):
        config = self.base_config[:]
        config = config.replace("{CLUSTER}", str(self.cluster))
        config = config.replace("{PORT}", str(self.port))
        config = config.replace("{METRICS_PORT}", str(self.metrics_port))
        return config

    def generate_docker_compose(self, cluster_path):
        docker_compose = self.base_docker_compose[:]
        docker_compose = docker_compose.replace("{CLUSTER}", str(self.cluster))
        docker_compose = docker_compose.replace("{CLUSTER_PATH}", cluster_path)
        docker_compose = docker_compose.replace("{PORT}", str(self.port))
        docker_compose = docker_compose.replace("{METRICS_PORT}", str(self.metrics_port))
        return docker_compose


class Worker:
    base_config = """
env: docker
http_server:
  addr: 0.0.0.0:{PORT}
  metrics_addr: 0.0.0.0:{METRICS_PORT}
filestorage:
  root_dir: file_storage/cluster-{CLUSTER}/worker-{WORKER}
  trasher:
    workers: 1
    collector_iterations_delay: 60
    worker_iterations_delay: 5
input_provider:
  filestorage_bucket_ttl: 15m
  artifact_ttl: 5m
output_provider:
  artifact_ttl: 5m
worker:
  id: http://worker-{CLUSTER}-{WORKER}:{PORT}
  free_slots: 4
  coordinator_endpoint: http://coordinator-{CLUSTER}:{COORDINATOR_PORT}
  heartbeat_delay: 2s
  worker_delay: 1s
"""

    base_docker_compose = """
    worker-{CLUSTER}-{WORKER}:
        build:
            context: ../../
            dockerfile: Dockerfile
        command:
            - /app/bin/worker
        container_name: worker-{CLUSTER}-{WORKER}
        networks:
            - coduels
        environment:
            CONFIG_PATH: /{CLUSTER_PATH}/worker-{WORKER}.yml
        volumes:
            - /var/run/docker.sock:/var/run/docker.sock
            - ../../{CLUSTER_PATH}:/{CLUSTER_PATH}:ro
"""

    def __init__(self, cluster, worker, port, metrics_port, coordinator_port):
        self.cluster = cluster
        self.worker = worker
        self.port = port
        self.metrics_port = metrics_port
        self.coordinator_port = coordinator_port

    def generate_config(self):
        config = self.base_config[:]
        config = config.replace("{CLUSTER}", str(self.cluster))
        config = config.replace("{WORKER}", str(self.worker))
        config = config.replace("{PORT}", str(self.port))
        config = config.replace("{METRICS_PORT}", str(self.metrics_port))
        config = config.replace("{COORDINATOR_PORT}", str(self.coordinator_port))
        return config

    def generate_docker_compose(self, cluster_path):
        docker_compose = self.base_docker_compose[:]
        docker_compose = docker_compose.replace("{CLUSTER}", str(self.cluster))
        docker_compose = docker_compose.replace("{CLUSTER_PATH}", cluster_path)
        docker_compose = docker_compose.replace("{WORKER}", str(self.worker))
        docker_compose = docker_compose.replace("{PORT}", str(self.port))
        docker_compose = docker_compose.replace("{METRICS_PORT}", str(self.metrics_port))
        return docker_compose


class PortManager:
    def __init__(self, begin_port):
        self.curr_port = begin_port
    
    def get_port(self):
        port = self.curr_port
        self.curr_port += 1
        return port


class Cluster:
    def __init__(self, port_manager, config_path, cluster_id, n_workers):
        self.port_manager = port_manager
        self.cluster_id = cluster_id
        self.cluster_path = config_path + "/cluster-" + str(self.cluster_id)
        self.n_workers = n_workers
    
    
    def create_configs(self):
        os.mkdir(self.cluster_path)

        self.coordinator = Coordinator(self.cluster_id, self.port_manager.get_port(), self.port_manager.get_port())

        f = open(self.cluster_path + "/coordinator.yml", "w")
        f.write(self.coordinator.generate_config())
        f.close()

        self.workers = []
        for worker_id in range(self.n_workers):
            worker = Worker(self.cluster_id, worker_id, self.port_manager.get_port(), self.port_manager.get_port(), self.coordinator.port)

            f = open(self.cluster_path + "/worker-" + str(worker_id) + ".yml", "w")
            f.write(worker.generate_config())
            f.close()

            self.workers.append(worker)

    def generate_docker_compose(self):
        docker_compose = self.coordinator.generate_docker_compose(self.cluster_path)
        for worker in self.workers:
            docker_compose += worker.generate_docker_compose(self.cluster_path)
        return docker_compose


class Exesh:
    infrastructure_docker_compose = """
networks:
    coduels:
        name: coduels
        driver: bridge

volumes:
    postgres:

services:
    zookeeper:
        image: confluentinc/cp-zookeeper:7.8.3
        container_name: zookeeper
        networks:
            - coduels
        ports:
            - "2181:2181"
        environment:
            ZOOKEEPER_CLIENT_PORT: 2181

    kafka:
        image: confluentinc/cp-kafka:7.8.3
        container_name: kafka
        networks:
            - coduels
        ports:
            - "9092:9092"
            - "29092:29092"
        environment:
            KAFKA_BROKER_ID: 1
            KAFKA_ZOOKEEPER_CONNECT: zookeeper:2181
            KAFKA_LISTENER_SECURITY_PROTOCOL_MAP: PLAINTEXT:PLAINTEXT,PLAINTEXT_HOST:PLAINTEXT
            KAFKA_LISTENERS: PLAINTEXT://0.0.0.0:9092,PLAINTEXT_HOST://0.0.0.0:29092
            KAFKA_ADVERTISED_LISTENERS: PLAINTEXT://kafka:9092,PLAINTEXT_HOST://localhost:29092
            KAFKA_INTER_BROKER_LISTENER_NAME: PLAINTEXT
            KAFKA_OFFSETS_TOPIC_REPLICATION_FACTOR: 1
            KAFKA_CREATE_TOPICS: "exesh.step-updates:1:1"
        depends_on:
            - zookeeper

    kafka-init-topics:
        image: confluentinc/cp-kafka:7.8.3
        container_name: kafka_init_topics
        networks:
            - coduels
        command: >
            bash -c "
            sleep 10 &&
            kafka-topics --create --topic exesh.step-updates --bootstrap-server kafka:9092 --partitions 1 --replication-factor 1
            "
        depends_on:
            - kafka

    postgres:
        image: postgres:16.11
        container_name: exesh_postgres
        networks:
            - coduels
        ports:
            - "5432:5432"
        environment:
            POSTGRES_USER: coordinator
            POSTGRES_PASSWORD: secret
            POSTGRES_DB: exesh
        healthcheck:
            test: ["CMD-SHELL", "pg_isready -U coordinator"]
            interval: 1s
            timeout: 0s
            retries: 30
        volumes:
            - postgres:/var/lib/postgresql/data
"""

    def __init__(self, n_clusters, n_workers, config_path):
        self.n_clusters = n_clusters
        self.n_workers = n_workers
        self.config_path = config_path
    
    def configure(self):
        docker_compose = self.infrastructure_docker_compose
        os.mkdir(self.config_path)

        port_manager = PortManager(5556)

        self.clusters = []
        for cluster_id in range(self.n_clusters):
            cluster = Cluster(port_manager, self.config_path, cluster_id, self.n_workers)
            cluster.create_configs()

            docker_compose += cluster.generate_docker_compose()

            self.clusters.append(cluster)
        
        f = open(self.config_path + "/docker-compose.yml", "w")
        f.write(docker_compose)
        f.close()
    
    def run(self):
        pwd = os.curdir
        os.chdir(self.config_path)
        try:
            subprocess.run(["docker-compose", "up", "-d", "--build"], check=True)
        except subprocess.CalledProcessError as e:
            print(f"An error occurred: {e}")
        os.chdir(pwd)


class Execution:
    def __init__(self, id, steps, status, created_at, scheduled_at, finished_at):
        self.id = id
        self.steps = steps
        self.status = status
        self.created_at = created_at
        self.scheduled_at = scheduled_at
        self.finished_at = finished_at


class Database:
    def __init__(self):
        pass
    
    def connect(self):
        conn = None
        try:
            conn = psycopg2.connect(
                dbname="exesh",
                user="coordinator",
                password="secret",
                host="0.0.0.0",
                port="5432"
            )
            return conn

        except psycopg2.Error as e:
            print(f"Error connecting to PostgreSQL: {e}")
            return None


    def get_executions(self, ids):
        executions = []

        conn = self.connect()
        if conn:
            cursor = conn.cursor()
            try:
                cursor.execute("SELECT id, steps, status, created_at, scheduled_at, finished_at FROM Executions")

                for row in cursor.fetchall():
                    id, steps, status, created_at, scheduled_at, finished_at = row
                    if id in ids:
                        executions.append(Execution(id, steps, status, created_at, scheduled_at, finished_at))

            except psycopg2.Error as e:
                print(f"Error executing query: {e}")
                return[]

            finally:
                if cursor:
                    cursor.close()
                if conn:
                    conn.close()
        
        return executions


class Client:
    def __init__(self, ports):
        self.ports = ports

    def compile_checker(self):
        return {
            "name": "compile checker",
            "type": "compile_cpp",
            "code": {
                "type": "inline",
                "content": """
    #include<bits/stdc++.h>

    using namespace std;

    int main(int argv, char **argc) {
        if (argv != 3) {
            return -1;
        }

        string correct_output_file = argc[1];
        string suspect_output_file = argc[2];

        ifstream correct(correct_output_file);
        ifstream suspect(suspect_output_file);

        int correct_output;
        correct >> correct_output;
        int suspect_output;
        suspect >> suspect_output;

        if (correct_output == suspect_output) {
            cout << "OK";
        } else {
            cout << "WA";
        }

        return 0;
    }
    """
            }
        }

    def run_py(self, test):
        return {
            "name": "run suspect on test " + str(test),
            "type": "run_py",
            "code": {
                "type": "inline",
                "content": "print(sum(map(int, input().split())))"
            },
            "run_input": {
                "type": "inline",
                "content": "1 2\n"
            },
            "time_limit": 2000,
            "memory_limit": 256,
            "show_output": False
        }

    def check(self, test):
        return {
            "name": "check on test " + str(test),
            "type": "check_cpp",
            "compiled_checker": {
                "type": "other_step",
                "step_name": "compile checker"
            },
            "correct_output": {
                "type": "inline",
                "content": "3\n"
            },
            "suspect_output": {
                "type": "other_step",
                "step_name": "run suspect on test " + str(test)
            }
        }

    def send_execution(self, port):
        steps = [self.compile_checker()]
        for test in range(1, 31):
            steps.append(self.run_py(test))
        for test in range(1, 31):
            steps.append(self.check(test))
        response = requests.post("http://0.0.0.0:" + str(port) + "/execute", json={"steps": steps})
        return response.json()["execution_id"]

    def send_executions(self, total_seconds, n_executions):
        ids = []
        interval = total_seconds / n_executions
        for i in range(n_executions):
            ids.append(self.send_execution(random.choice(self.ports)))
            time.sleep(interval / 60)
        return ids


class Stress:
    def __init__(self, system, n_executions, total_seconds):
        self.system = system
        self.n_executions = n_executions
        self.total_seconds = total_seconds

    def work(self):
        ports = [cluster.coordinator.port for cluster in self.system.clusters]
        client = Client(ports)

        ids = client.send_executions(self.total_seconds, self.n_executions)

        database = Database()
        while True:
            executions = database.get_executions(ids)
            finished = 0
            for e in executions:
                if e.status == "finished":
                    finished += 1
            print(finished, " / ", self.n_executions, sep="")
            if finished == self.n_executions:
                break
            time.sleep(2)
        
        executions.sort(key=lambda e: e.created_at)

        durations = []
        for e in executions:
            durations.append((e.finished_at - e.created_at).total_seconds())
        return durations


system = Exesh(n_clusters=1, n_workers=20, config_path="config/test")
system.configure()
system.run()

print("system setup success")

time.sleep(5)

stress = Stress(system=system, n_executions=10, total_seconds=10)
d = stress.work()

print(*d)
