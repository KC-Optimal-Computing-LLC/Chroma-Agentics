FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY backend/ backend/
RUN dotnet restore backend/ChromaAgentics.Backend.sln

RUN dotnet publish backend/src/ChromaAgentics.Backend/ChromaAgentics.Backend.csproj \
    --configuration Release \
    --output /app/publish \
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ChromaAgentics.Backend.dll"]
