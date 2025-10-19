docker-compose up -d --build && \
cd Exesh && docker-compose up -d --build && cd .. && \
cd Taski && docker-compose up -d --build && cd .. && \
cd Duely && docker-compose up -d --build && cd .. && \
docker ps
