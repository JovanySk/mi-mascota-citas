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

# Writable folder for the SQLite file (Database__Provider=Sqlite), the private pet photos and
# the data-protection keys that sign the login cookie. Mount /app/data as a volume to keep them.
RUN mkdir -p /app/data/fotos /app/data/keys && chown -R $APP_UID /app/data

ENV ASPNETCORE_URLS=http://+:8080 \
    TZ=America/Mexico_City \
    ConnectionStrings__Sqlite="Data Source=/app/data/mimascota.db" \
    PetPhotos__Folder=/app/data/fotos \
    DataProtection__KeysPath=/app/data/keys
EXPOSE 8080

# Defaults to SQL Server (appsettings.json). Pass the real connection string at run time:
#   -e ConnectionStrings__SqlServer="Server=...;Password=...;TrustServerCertificate=True"
# or run without a database server:
#   -e Database__Provider=Sqlite
# The first administrator account is created from:
#   -e Admin__Email=tu@correo.mx -e Admin__Password="..."
# The login cookie is marked Secure outside Development: serve the app over HTTPS
# (behind a reverse proxy) or open it as http://localhost, which browsers treat as secure.
USER $APP_UID
ENTRYPOINT ["dotnet", "MiMascota.Web.dll"]
