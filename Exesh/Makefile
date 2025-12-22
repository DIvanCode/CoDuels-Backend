stress:
	python3 stress.py

clean:
	rm -rf config/test && \
	docker rm -f $$(docker ps -aq) && \
	docker volume rm test_postgres
