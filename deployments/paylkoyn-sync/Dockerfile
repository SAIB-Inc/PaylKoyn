# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy entire solution
COPY . .

# Build and publish
WORKDIR /src/src/PaylKoyn.Sync
RUN dotnet restore
RUN dotnet publish -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Install HAProxy for stable TCP-Unix socket bridging
USER root
RUN apt-get update && \
    apt-get install -y --no-install-recommends \
        haproxy \
        netcat-openbsd \
        sudo && \
    rm -rf /var/lib/apt/lists/* && \
    echo 'app ALL=(ALL) NOPASSWD:ALL' >> /etc/sudoers

# Copy published application
COPY --from=build /app/publish .

# Copy configuration files
COPY deployments/paylkoyn-sync/haproxy.cfg /etc/haproxy/haproxy.cfg
COPY deployments/paylkoyn-sync/startup.sh /app/startup.sh

# Create required directories and set permissions
RUN mkdir -p /ipc /data && \
    chown -R app:app /ipc /data /etc/haproxy && \
    chmod +x /app/startup.sh

# Switch to app user
USER app

ENTRYPOINT ["/app/startup.sh"]