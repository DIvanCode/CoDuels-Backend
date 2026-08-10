FROM golang:1.24.0 AS build

ENV GOCACHE=/go-cache
ENV GOMODCACHE=/gomod-cache

WORKDIR /app

COPY go.mod go.sum ./
COPY ./cmd ./cmd
COPY ./internal ./internal
COPY ./config ./config

RUN --mount=type=cache,target=/gomod-cache --mount=type=cache,target=/go-cache \
  mkdir -p /app/bin && \
  go build -o /app/bin/taski /app/cmd/taski/... && \
  go build -o /app/bin/uploader /app/cmd/uploader/...

FROM golang:1.24.0 AS uploader

RUN apt-get update && \
  apt-get install -y --no-install-recommends g++ python3 && \
  rm -rf /var/lib/apt/lists/*

ENV CONFIG_PATH=/app/config/uploader.yml
ENV FILESTORAGE_ROOT_DIR=/data/tasks
ENV HOME=/tmp
ENV GOCACHE=/tmp/go-cache

WORKDIR /app

COPY --from=build /app/bin/uploader /app/bin/uploader
COPY ./scripts/uploader_config.yml /app/config/uploader.yml

ENTRYPOINT ["/app/bin/uploader"]

FROM golang:1.24.0 AS taski

WORKDIR /app

COPY --from=build /app/bin/taski /app/bin/taski
COPY ./config ./config

EXPOSE 5252

CMD ["/app/bin/taski"]
