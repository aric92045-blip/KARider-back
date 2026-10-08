# syntax=docker/dockerfile:1
# Imagen para Azure Container Apps / App Service for Containers (alternativa al despliegue de código).

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY global.json Directory.Build.props KARider.sln ./
COPY src/KARider.Domain/KARider.Domain.csproj src/KARider.Domain/
COPY src/KARider.Application/KARider.Application.csproj src/KARider.Application/
COPY src/KARider.Infrastructure/KARider.Infrastructure.csproj src/KARider.Infrastructure/
COPY src/KARider.API/KARider.API.csproj src/KARider.API/
RUN dotnet restore src/KARider.API/KARider.API.csproj
COPY src/ src/
RUN dotnet publish src/KARider.API/KARider.API.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080 \
    ASPNETCORE_FORWARDEDHEADERS_ENABLED=true \
    DOTNET_gcServer=1
COPY --from=build /app/publish .
# Usuario sin privilegios incluido en las imágenes oficiales de .NET 8+
USER $APP_UID
EXPOSE 8080
# Salud: Azure consulta /health/live (proceso) y /health/ready (PostgreSQL)
ENTRYPOINT ["dotnet", "KARider.API.dll"]
