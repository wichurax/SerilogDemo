# Multi-stage Dockerfile for SerilogDemo API
# Optimized for multi-instance deployment behind load balancer

#------------------------------------------------------------------------------
# Stage 1: Build
#------------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj and restore dependencies (cached layer)
COPY ["SerilogDemo.csproj", "."]
RUN dotnet restore "SerilogDemo.csproj"

# Copy source and build
COPY . .
RUN dotnet build "SerilogDemo.csproj" -c Release -o /app/build

#------------------------------------------------------------------------------
# Stage 2: Publish
#------------------------------------------------------------------------------
FROM build AS publish
RUN dotnet publish "SerilogDemo.csproj" -c Release -o /app/publish /p:UseAppHost=false

#------------------------------------------------------------------------------
# Stage 3: Runtime
#------------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final

# Install curl for health checks
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

# Create non-root user for security
RUN groupadd -r appgroup && useradd -r -g appgroup appuser

WORKDIR /app

# Copy published app
COPY --from=publish /app/publish .

# Change ownership to non-root user
RUN chown -R appuser:appgroup /app

# Switch to non-root user
USER appuser

# Expose port (Kestrel default)
EXPOSE 8080

# Environment variables for containerized deployment
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENV Logging__Console__FormatterName=Json

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "SerilogDemo.dll"]