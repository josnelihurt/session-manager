# Build stage - Native AOT for Alpine (musl)
FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build

# Install Native AOT prerequisites (clang and zlib for Alpine)
RUN apk add --no-cache clang zlib-dev

WORKDIR /src

# Copy csproj and restore dependencies
COPY src/SessionManager.Api/SessionManager.Api.csproj SessionManager.Api/
RUN dotnet restore "SessionManager.Api/SessionManager.Api.csproj"

# Copy the rest of the application
COPY src/SessionManager.Api/ SessionManager.Api/

# Build and publish with Native AOT for Alpine (linux-musl-x64)
WORKDIR /src/SessionManager.Api
RUN dotnet publish "SessionManager.Api.csproj" \
    -c Release \
    -r linux-musl-x64 \
    -p:PublishAot=true \
    -o /app/publish

# Runtime stage - pure Alpine (no .NET runtime needed)
FROM alpine:latest AS runtime

# Install runtime dependencies (including ICU for Native AOT globalization)
RUN apk --no-cache add ca-certificates libgcc icu-libs

WORKDIR /app
EXPOSE 8080

# Copy native binary
COPY --from=build /app/publish/SessionManager.Api .

ENTRYPOINT ["./SessionManager.Api"]
