FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY PlateformeFormation.Api.csproj .
RUN dotnet restore

COPY . .
RUN dotnet publish -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# PdfSharpCore (PDF export) needs real font files to resolve typefaces — the base image ships none,
# which crashes FormationExportService's constructor (and therefore any request touching the
# formation controller, since it's built eagerly by DI) with "No Fonts installed on this device!".
RUN apt-get update && apt-get install -y --no-install-recommends fontconfig fonts-dejavu-core \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app .

# .dockerignore excludes local test uploads, but ASP.NET Core needs wwwroot to exist to serve
# static files at all — the app creates the documents/avatars/exports subfolders itself at runtime.
RUN mkdir -p wwwroot/uploads

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "PlateformeFormation.Api.dll"]
