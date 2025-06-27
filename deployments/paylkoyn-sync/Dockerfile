# Build stage for PaylKoyn.Sync
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy entire solution
COPY . .

# Build and publish PaylKoyn.Sync
WORKDIR /src/src/PaylKoyn.Sync
RUN dotnet restore
RUN dotnet publish -c Release -o /app/publish

# Runtime stage based on Blink Labs cardano-node
FROM ghcr.io/blinklabs-io/cardano-node:10.4.1-3

# Install .NET 9.0 runtime dependencies
USER root
RUN apt-get update && \
    apt-get install -y --no-install-recommends \
        libicu72 \
        libssl3 \
        ca-certificates \
        curl && \
    rm -rf /var/lib/apt/lists/*

# Install .NET 9.0 runtime
RUN curl -sSL https://dot.net/v1/dotnet-install.sh | bash /dev/stdin \
    --channel 9.0 \
    --runtime aspnetcore \
    --install-dir /usr/share/dotnet \
    --no-path && \
    ln -s /usr/share/dotnet/dotnet /usr/local/bin/

# Copy PaylKoyn.Sync from build stage
COPY --from=build /app/publish /app/paylkoyn-sync

# Copy startup script
COPY deployments/paylkoyn-sync/startup.sh /app/startup.sh

# Create necessary directories
RUN mkdir -p /data /ipc && \
    chmod +x /app/startup.sh

# Environment variables for cardano-node
ENV NETWORK=preview
ENV RESTORE_SNAPSHOT=true
ENV CARDANO_SOCKET_PATH=/ipc/node.socket

# Use our unified startup script
ENTRYPOINT ["/app/startup.sh"]