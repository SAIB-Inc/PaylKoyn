#!/bin/bash
set -euo pipefail

echo "=== UNIFIED PAYLKOYN-SYNC CONTAINER STARTING ==="

# Function to handle errors
handle_error() {
    echo "ERROR: $1" >&2
    exit 1
}

# Ensure data directory has correct permissions
if [ -d "/data" ]; then
    echo "Setting permissions on /data directory..."
    chown -R $(id -u):$(id -g) /data 2>/dev/null || true
fi

# Start cardano-node using Blink Labs entrypoint in background
echo "Starting cardano-node with Blink Labs entrypoint..."
echo "Network: ${NETWORK:-preview}"
echo "Restore snapshot: ${RESTORE_SNAPSHOT:-true}"
echo "Socket path: ${CARDANO_SOCKET_PATH:-/ipc/node.socket}"

# Run the Blink Labs entrypoint in background
/usr/local/bin/entrypoint "$@" &
CARDANO_PID=$!

echo "Cardano-node started with PID: $CARDANO_PID"

# Wait for socket creation with timeout
echo "Waiting for cardano-node socket at ${CARDANO_SOCKET_PATH:-/ipc/node.socket}..."
SOCKET_WAIT=0
MAX_SOCKET_WAIT=180  # 3 minutes timeout

while [ ! -S "${CARDANO_SOCKET_PATH:-/ipc/node.socket}" ]; do
    # Check if cardano-node is still running
    if ! kill -0 $CARDANO_PID 2>/dev/null; then
        handle_error "cardano-node process died before creating socket"
    fi
    
    SOCKET_WAIT=$((SOCKET_WAIT + 1))
    if [ $SOCKET_WAIT -ge $MAX_SOCKET_WAIT ]; then
        handle_error "Timeout waiting for cardano-node socket after ${MAX_SOCKET_WAIT} seconds"
    fi
    
    if [ $((SOCKET_WAIT % 10)) -eq 0 ]; then
        echo "Still waiting for socket... (${SOCKET_WAIT}s elapsed)"
    fi
    
    sleep 1
done

echo "Socket ready at ${CARDANO_SOCKET_PATH:-/ipc/node.socket}!"

# Give cardano-node a moment to fully initialize
sleep 5

# Start PaylKoyn.Sync
echo "Starting PaylKoyn.Sync..."
cd /app/paylkoyn-sync
export ASPNETCORE_ENVIRONMENT=Railway
export CardanoNodeConnection__UnixSocket__Path=${CARDANO_SOCKET_PATH:-/ipc/node.socket}

# Start PaylKoyn.Sync in background
dotnet PaylKoyn.Sync.dll &
SYNC_PID=$!

echo "PaylKoyn.Sync started with PID: $SYNC_PID"

# Function to cleanup on exit
cleanup() {
    echo "Shutting down..."
    if [ ! -z "${SYNC_PID:-}" ]; then
        echo "Stopping PaylKoyn.Sync (PID: $SYNC_PID)..."
        kill $SYNC_PID 2>/dev/null || true
    fi
    if [ ! -z "${CARDANO_PID:-}" ]; then
        echo "Stopping cardano-node (PID: $CARDANO_PID)..."
        kill $CARDANO_PID 2>/dev/null || true
    fi
    # Wait for processes to terminate
    wait
    echo "Shutdown complete"
}

# Set up signal handlers
trap cleanup EXIT SIGTERM SIGINT

# Monitor both processes
echo "Monitoring both processes..."
while true; do
    # Check cardano-node
    if ! kill -0 $CARDANO_PID 2>/dev/null; then
        echo "ERROR: cardano-node process died unexpectedly"
        kill $SYNC_PID 2>/dev/null || true
        exit 1
    fi
    
    # Check PaylKoyn.Sync
    if ! kill -0 $SYNC_PID 2>/dev/null; then
        echo "ERROR: PaylKoyn.Sync process died unexpectedly"
        kill $CARDANO_PID 2>/dev/null || true
        exit 1
    fi
    
    # Brief status every 30 seconds
    if [ $((SECONDS % 30)) -eq 0 ]; then
        echo "Status: Both processes running (cardano-node: $CARDANO_PID, sync: $SYNC_PID)"
    fi
    
    sleep 5
done