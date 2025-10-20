cd Duely && docker-compose down --volumes && cd .. && \
cd Taski && docker-compose down --volumes && cd .. && \
cd Exesh && docker-compose down --volumes && cd .. && \
docker-compose down --volumes && \
docker ps
