# Multi-stage Dockerfile for .NET 9+ applications
# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY . ./
RUN dotnet restore
RUN dotnet publish -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS production
WORKDIR /app

# Install curl for health checks
# Update package lists, install curl for HTTP requests/health checks,
# and remove the apt package lists cache to reduce the final image size.
# The 'rm -rf /var/lib/apt/lists/*' cleanup is a Docker best practice that
# removes cached package metadata which is no longer needed after installation,
# typically saving 20-40MB in the final image layer.
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

# Health check configuration
HEALTHCHECK --interval=10s --timeout=5s --start-period=5s --retries=5 \
  CMD curl -f http://localhost:5000/api/v1/health || exit 1

ENTRYPOINT ["dotnet", "CompanyInfo.Api.dll"]