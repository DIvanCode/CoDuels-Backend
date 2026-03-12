docker pull gcc:latest
docker pull python:3
docker pull golang:latest

docker compose up -d --build

cd Exesh && docker compose up -d --build && cd ..
cd Taski && docker compose up -d --build && cd ..
cd Duely && docker compose up -d --build && cd ..

docker ps