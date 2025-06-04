# PaylKoyn.ImageGen Docker image for Railway deployment
FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS build

# Set working directory
WORKDIR /src

# Copy project files
COPY src/PaylKoyn.ImageGen/PaylKoyn.ImageGen.csproj src/PaylKoyn.ImageGen/
COPY src/PaylKoyn.Data/PaylKoyn.Data.csproj src/PaylKoyn.Data/

# Restore dependencies
RUN dotnet restore src/PaylKoyn.ImageGen/PaylKoyn.ImageGen.csproj

# Copy source code
COPY src/ src/

# Build and publish
RUN dotnet publish src/PaylKoyn.ImageGen/PaylKoyn.ImageGen.csproj -c Release -o /app/publish

# Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:9.0

# Set working directory
WORKDIR /app

# Install socat, netcat, and sudo for TCP to Unix socket bridge
USER root
RUN apt-get update && apt-get install -y socat netcat-openbsd sudo && rm -rf /var/lib/apt/lists/*
RUN echo 'app ALL=(ALL) NOPASSWD:ALL' >> /etc/sudoers

# Copy published application
COPY --from=build /app/publish .

# Create /ipc directory with proper permissions
RUN mkdir -p /ipc && chown app:app /ipc

# Create wrapper script for cardano-node connection bridge
RUN echo '#!/bin/bash\n\
\n\
echo "=== PAYLKOYN.IMAGEGEN STARTING ==="\n\
\n\
# Ensure /data has proper permissions for mounted volume\n\
echo "Setting up data directory permissions..."\n\
sudo chown -R app:app /data 2>/dev/null || true\n\
\n\
# Create local Unix socket bridge to cardano-node TCP service\n\
echo "Setting up cardano-node connection bridge..."\n\
\n\
# Start socat bridge in background (TCP to Unix socket)\n\
{\n\
    echo "Waiting for cardano-node:3333 to be available..."\n\
    while ! nc -w 1 cardano-node 3333 < /dev/null 2>/dev/null; do\n\
        echo "Waiting for cardano-node:3333..."\n\
        sleep 2\n\
    done\n\
    \n\
    echo "cardano-node:3333 available! Starting socket bridge..."\n\
    while true; do\n\
        echo "$(date): Starting socat TCP->Unix bridge..."\n\
        socat UNIX-LISTEN:/ipc/node.socket,fork,reuseaddr TCP:cardano-node:3333\n\
        echo "$(date): Socket bridge died, restarting..."\n\
        sleep 5\n\
    done\n\
} &\n\
\n\
# Wait for socket bridge to be ready\n\
echo "Waiting for local socket bridge..."\n\
sleep 5\n\
\n\
echo "Starting PaylKoyn.ImageGen..."\n\
export ASPNETCORE_ENVIRONMENT=Railway\n\
exec dotnet PaylKoyn.ImageGen.dll "$@"\n\
' > /entrypoint.sh

RUN chmod +x /entrypoint.sh

# Expose port
EXPOSE 8080

# Set environment variables
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8080

USER app
ENTRYPOINT ["/entrypoint.sh"]