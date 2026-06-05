# Multi-stage .NET 8 image. Build context = repo root. Targets net8.0 (built with the 8.0 SDK).
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY src/ ./src/
RUN dotnet restore src/Arabica.Api/Arabica.Api.csproj
RUN dotnet publish src/Arabica.Api/Arabica.Api.csproj -c Release -o /app --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
COPY --from=build /app ./
EXPOSE 8080
ENTRYPOINT ["dotnet", "Arabica.Api.dll"]
