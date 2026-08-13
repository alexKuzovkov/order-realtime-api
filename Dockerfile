FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY OrderRealtime.sln ./
COPY src/OrderRealtime.Api/OrderRealtime.Api.csproj src/OrderRealtime.Api/
RUN dotnet restore src/OrderRealtime.Api/OrderRealtime.Api.csproj

COPY src/OrderRealtime.Api/ src/OrderRealtime.Api/
RUN dotnet publish src/OrderRealtime.Api/OrderRealtime.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
COPY --from=build /app/publish ./

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

USER $APP_UID
ENTRYPOINT ["dotnet", "OrderRealtime.Api.dll"]
