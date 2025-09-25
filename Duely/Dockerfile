FROM mcr.microsoft.com/dotnet/sdk:8.0

EXPOSE 5001

WORKDIR /Source

RUN dotnet tool install --global dotnet-ef

COPY . .

RUN dotnet publish Duely.sln -p:PublishDir=/Build


WORKDIR /App

ENV ASPNETCORE_URLS=http://0.0.0.0:5001

CMD ["/Build/Duely"]