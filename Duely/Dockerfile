FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /app

COPY . .

RUN dotnet restore Duely.sln
RUN dotnet build Duely.sln -c Release --no-restore
RUN dotnet publish src/Duely/Duely.csproj -c Release -o /out --no-build

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime

WORKDIR /app

COPY --from=build /out .

ENV ASPNETCORE_URLS=http://+:5001
EXPOSE 5001

CMD ["dotnet", "Duely.dll"]
