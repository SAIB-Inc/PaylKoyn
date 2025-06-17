#!/bin/bash
set -euo pipefail

echo "=== PAYLKOYN.SYNC STARTING ==="

# Function to handle errors
handle_error() {
    echo "ERROR: $1" >&2
    exit 1
}

# Ensure proper permissions
echo "Setting up permissions..."
sudo chown -R app:app /data /ipc 2>/dev/null || true
sudo rm -f /ipc/node.socket 2>/dev/null || true

# Wait for cardano-node with timeout
echo "Waiting for cardano-node:3333..."
RETRY_COUNT=0
MAX_RETRIES=30
while ! nc -w 1 cardano-node 3333 < /dev/null 2>/dev/null; do
    RETRY_COUNT=$((RETRY_COUNT + 1))
    if [ $RETRY_COUNT -ge $MAX_RETRIES ]; then
        handle_error "cardano-node:3333 not available after $MAX_RETRIES attempts"
    fi
    echo "cardano-node not ready, attempt $RETRY_COUNT/$MAX_RETRIES..."
    sleep 2
done

echo "cardano-node:3333 is available!"

# Start HAProxy with failure detection
echo "Starting HAProxy..."
haproxy -f /etc/haproxy/haproxy.cfg -D -p /var/run/haproxy.pid || handle_error "Failed to start HAProxy"

# Verify HAProxy started successfully
sleep 2
if ! kill -0 $(cat /var/run/haproxy.pid 2>/dev/null) 2>/dev/null; then
    handle_error "HAProxy failed to start or died immediately"
fi

# Monitor HAProxy in background
{
    while true; do
        sleep 10
        if ! kill -0 $(cat /var/run/haproxy.pid 2>/dev/null) 2>/dev/null; then
            echo "ERROR: HAProxy died unexpectedly!" >&2
            exit 1
        fi
    done
} &

# Verify socket is created
SOCKET_RETRY=0
while [ ! -S /ipc/node.socket ]; do
    SOCKET_RETRY=$((SOCKET_RETRY + 1))
    if [ $SOCKET_RETRY -ge 10 ]; then
        handle_error "HAProxy failed to create Unix socket after 10 attempts"
    fi
    echo "Waiting for HAProxy to create socket, attempt $SOCKET_RETRY/10..."
    sleep 1
done

echo "HAProxy socket ready at /ipc/node.socket"

# Start the application
echo "Starting PaylKoyn.Sync..."
export ASPNETCORE_ENVIRONMENT=Railway
exec dotnet PaylKoyn.Sync.dll "$@"