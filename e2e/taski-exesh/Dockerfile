FROM golang:1.24-alpine

WORKDIR /test

COPY go.mod ./
COPY taski_exesh_test.go ./

CMD ["go", "test", "-v", "-count=1", "-timeout=10m", "./..."]
