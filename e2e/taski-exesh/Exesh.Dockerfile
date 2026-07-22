FROM golang:1.24.0

ENV GOCACHE=/go-cache
ENV GOMODCACHE=/gomod-cache

WORKDIR /app
COPY Exesh/go.mod Exesh/go.sum ./
COPY filestorage /filestorage
RUN go mod edit -replace github.com/DIvanCode/filestorage=/filestorage
COPY Exesh/cmd ./cmd
COPY Exesh/internal ./internal
COPY Exesh/config ./config
COPY Exesh/config/isolate.conf /usr/local/etc/isolate
COPY Exesh/testlib/testlib.h /usr/local/include/testlib.h

RUN --mount=type=cache,target=/gomod-cache --mount=type=cache,target=/go-cache \
  mkdir -p /app/bin && \
  go build -o /app/bin /app/cmd/...

WORKDIR /
COPY Exesh/isolate ./isolate
RUN apt update && \
    apt install make libcap-dev libsystemd-dev libseccomp-dev -y && \
    cd isolate && \
    make isolate && \
    cp isolate /usr/local/bin/isolate

EXPOSE 5253
EXPOSE 5254
EXPOSE 5255
