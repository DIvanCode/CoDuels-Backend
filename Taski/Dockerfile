FROM golang:1.24.0

ENV GOCACHE=/go-cache
ENV GOMODCACHE=/gomod-cache

WORKDIR /app

COPY go.mod go.sum ./
COPY ./cmd ./cmd
COPY ./internal ./internal
COPY ./file_storage ./file_storage

RUN --mount=type=cache,target=/gomod-cache --mount=type=cache,target=/go-cache \
  mkdir -p /app/bin && \
  go build -o /app/bin/taski /app/cmd/...

EXPOSE 5252

CMD ["/app/bin/taski"]
