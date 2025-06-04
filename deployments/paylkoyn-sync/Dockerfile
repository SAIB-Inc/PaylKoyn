# Use the official Microsoft .NET runtime as a parent image
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app

# Use SDK image to build the application
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /app
COPY . .


WORKDIR /app/src/PaylKoyn.Sync
RUN dotnet restore
RUN dotnet build -c Release -o /app/build

# Publish the application
FROM build AS publish
RUN dotnet publish -c Release -o /app/publish

# Final stage/image
FROM base AS final
WORKDIR /app

# Install socat and netcat for TCP to Unix socket bridge
USER root
RUN apt-get update && apt-get install -y socat netcat-openbsd && rm -rf /var/lib/apt/lists/*

# Copy published application
COPY --from=publish /app/publish .


# Create wrapper script for cardano-node connection bridge
RUN echo '#!/bin/bash\n\
\n\
echo "=== PAYLKOYN.SYNC STARTING ==="\n\
\n\
# Create local Unix socket bridge to cardano-node TCP service\n\
echo "Setting up cardano-node connection bridge..."\n\
mkdir -p /tmp\n\
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
        socat UNIX-LISTEN:/tmp/preview-node.socket,fork,reuseaddr TCP:cardano-node:3333\n\
        echo "$(date): Socket bridge died, restarting..."\n\
        sleep 5\n\
    done\n\
} &\n\
\n\
# Wait for socket bridge to be ready\n\
echo "Waiting for local socket bridge..."\n\
sleep 5\n\
\n\
echo "Starting PaylKoyn.Sync..."\n\
export ASPNETCORE_ENVIRONMENT=Railway\n\
exec dotnet PaylKoyn.Sync.dll "$@"\n\
' > /entrypoint.sh

RUN chmod +x /entrypoint.sh

USER app
ENTRYPOINT ["/entrypoint.sh"]