FROM golang:1.24.0

WORKDIR /app

COPY . .

RUN go mod download
RUN go build -o ./taski /app/cmd/main.go

EXPOSE 5252

CMD ["./taski"]