coordinator:
	rm -rf file_storage/coordinator && \
	docker-compose up -d && \
	export CONFIG_PATH=config/coordinator/dev.yml && \
	go run cmd/coordinator/main.go

worker-1:
	rm -rf file_storage/worker-1 && \
	export CONFIG_PATH=config/worker-1/dev.yml && \
	go run cmd/worker/main.go

worker-2:
	rm -rf file_storage/worker-2 && \
	export CONFIG_PATH=config/worker-2/dev.yml && \
	go run cmd/worker/main.go