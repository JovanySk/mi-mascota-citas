# ---- Build ----
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Restore first so this layer is cached while only the code changes.
COPY src/MiMascota.Web/MiMascota.Web.csproj src/MiMascota.Web/
RUN dotnet restore src/MiMascota.Web/MiMascota.Web.csproj

COPY src/ src/
RUN dotnet publish src/MiMascota.Web/MiMascota.Web.csproj -c Release -o /app/publish --no-restore

# ---- Runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# Writable folder for the SQLite file when running with Database__Provider=Sqlite.
RUN mkdir -p /app/data && chown $APP_UID /app/data

ENV ASPNETCORE_URLS=http://+:8080 \
    TZ=America/Mexico_City \
    ConnectionStrings__Sqlite="Data Source=/app/data/mimascota.db"
EXPOSE 8080

# Defaults to SQL Server (appsettings.json). Pass the real connection string at run time:
#   -e ConnectionStrings__SqlServer="Server=...;Password=...;TrustServerCertificate=True"
# or run without a database server:
#   -e Database__Provider=Sqlite
USER $APP_UID
ENTRYPOINT ["dotnet", "MiMascota.Web.dll"]
