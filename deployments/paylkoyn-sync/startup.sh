#!/bin/bash
set -euo pipefail

echo "=== STARTING PAYLKOYN-SYNC CONTAINER ==="

# Simple error handler
die() {
    echo "ERROR: $1" >&2
    exit 1
}

# Set permissions if needed
[ -d "/data" ] && chown -R $(id -u):$(id -g) /data 2>/dev/null || true

# Start cardano-node (in foreground to see output)
echo "Starting cardano-node..."
/usr/local/bin/entrypoint "$@" &
CARDANO_PID=$!

# Wait for socket file to exist
SOCKET_PATH="${CARDANO_SOCKET_PATH:-/ipc/node.socket}"
echo "Waiting for cardano-node socket at $SOCKET_PATH..."
WAIT_COUNT=0
while [ ! -S "$SOCKET_PATH" ]; do
    # Check if cardano-node is still running
    kill -0 $CARDANO_PID 2>/dev/null || die "cardano-node died"
    
    WAIT_COUNT=$((WAIT_COUNT + 1))
    if [ $((WAIT_COUNT % 30)) -eq 0 ]; then
        echo "Still waiting for socket... (${WAIT_COUNT}s elapsed)"
    fi
    
    sleep 1
done

echo "Socket file exists, giving cardano-node a moment to be ready..."
sleep 5

# Start PaylKoyn.Sync
echo "Starting PaylKoyn.Sync..."
cd /app/paylkoyn-sync || die "PaylKoyn.Sync directory not found"

export CardanoNodeConnection__UnixSocket__Path=${CARDANO_SOCKET_PATH:-/ipc/node.socket}

dotnet PaylKoyn.Sync.dll &
SYNC_PID=$!

# Simple cleanup on exit
cleanup() {
    echo "Shutting down..."
    [ -n "${SYNC_PID:-}" ] && kill $SYNC_PID 2>/dev/null || true
    [ -n "${CARDANO_PID:-}" ] && kill $CARDANO_PID 2>/dev/null || true
    wait
}
trap cleanup EXIT

# Monitor both processes
while true; do
    kill -0 $CARDANO_PID 2>/dev/null || die "cardano-node died"
    kill -0 $SYNC_PID 2>/dev/null || die "PaylKoyn.Sync died"
    sleep 5
done