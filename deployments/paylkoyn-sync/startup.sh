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

# Start cardano-node
echo "Starting cardano-node..."
/usr/local/bin/entrypoint "$@" &
CARDANO_PID=$!

# Wait for socket to be ready
echo "Waiting for cardano-node socket..."
while true; do
    # Check if cardano-node is still running
    kill -0 $CARDANO_PID 2>/dev/null || die "cardano-node died"
    
    # Check if socket is ready by looking at recent output
    if tail -n 50 /proc/$CARDANO_PID/fd/1 2>/dev/null | grep -q "LocalSocketUp.*${CARDANO_SOCKET_PATH:-/ipc/node.socket}\|TrServerStarted.*LocalAddress.*${CARDANO_SOCKET_PATH:-/ipc/node.socket}"; then
        echo "Socket is ready!"
        break
    fi
    
    sleep 1
done

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