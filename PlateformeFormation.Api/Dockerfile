FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY PlateformeFormation.Api.csproj .
RUN dotnet restore

COPY . .
RUN dotnet publish -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app .

# .dockerignore excludes local test uploads, but ASP.NET Core needs wwwroot to exist to serve
# static files at all — the app creates the documents/avatars/exports subfolders itself at runtime.
RUN mkdir -p wwwroot/uploads

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "PlateformeFormation.Api.dll"]
