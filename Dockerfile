# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /src

# Copy csproj and restore dependencies
COPY src/SessionManager.Api/SessionManager.Api.csproj SessionManager.Api/
RUN dotnet restore "SessionManager.Api/SessionManager.Api.csproj"

# Copy the rest of the application
COPY src/SessionManager.Api/ SessionManager.Api/

# Build and publish
WORKDIR /src/SessionManager.Api
RUN dotnet publish "SessionManager.Api.csproj" -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS runtime
WORKDIR /app

EXPOSE 8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "SessionManager.Api.dll"]
