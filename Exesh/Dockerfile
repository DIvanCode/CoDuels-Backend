FROM golang:1.24.0

ENV GOCACHE=/go-cache
ENV GOMODCACHE=/gomod-cache

WORKDIR /app
COPY go.mod go.sum ./
COPY ./cmd ./cmd
COPY ./internal ./internal
COPY ./config/isolate.conf /usr/local/etc/isolate

RUN --mount=type=cache,target=/gomod-cache --mount=type=cache,target=/go-cache \
  mkdir -p /app/bin && \
  go build -o /app/bin /app/cmd/...

WORKDIR /
COPY ./isolate ./isolate
RUN apt update && \
    apt install make libcap-dev libsystemd-dev -y && \
    cd isolate && \
    make isolate && \
    cp isolate /usr/local/bin/isolate

EXPOSE 5253
EXPOSE 5254
EXPOSE 5255
