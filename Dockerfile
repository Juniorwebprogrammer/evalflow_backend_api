# Etapa 1: Base runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

# Etapa 2: Build + publish
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copiamos solo el csproj primero para cachear el restore
COPY ["evalflow_backend_api.csproj", "./"]
RUN dotnet restore "./evalflow_backend_api.csproj"

# Copiamos el resto del código y publicamos
COPY . .
RUN dotnet publish "evalflow_backend_api.csproj" -c $BUILD_CONFIGURATION -o /app/publish --no-restore /p:UseAppHost=false

# Etapa 3: Final (sin privilegios de root)
FROM base AS final
COPY --from=build /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "evalflow_backend_api.dll"]
