FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/backend/CrownRankApp.API/CrownRankApp.API.csproj \
    -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --

**Choose Dockerfile—the first screenshot.** I checked your repository: the API targets **.NET 10**, but there isn’t a Dockerfile yet.

First, add a file named **`Dockerfile`** at the repository root, alongside `README.md`, containing:

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/backend/CrownRankApp.API/CrownRankApp.API.csproj \
    -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://0.0.0.0:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "CrownRankApp.API.dll"]