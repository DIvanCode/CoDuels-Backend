FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /app
COPY . .

RUN dotnet restore src/Duely/Duely.csproj
RUN dotnet build src/Duely/Duely.csproj -c Release --no-restore
RUN dotnet publish src/Duely/Duely.csproj -c Release -o /out --no-build

FROM build AS migration
RUN dotnet tool install --global dotnet-ef --version 9.0.10

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime

WORKDIR /app
COPY --from=build /out .

ENV ASPNETCORE_URLS=http://+:5001
EXPOSE 5001

CMD ["dotnet", "Duely.dll"]
