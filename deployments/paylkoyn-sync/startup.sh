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

# Check if this is first run (no blockchain data exists)
if [ ! -d "/data/db" ] || [ -z "$(ls -A /data/db 2>/dev/null)" ]; then
    echo "First run detected - no existing blockchain data found"
    echo "This may take 20-30 minutes for Mithril snapshot download and restoration..."
    MAX_SOCKET_WAIT=3600  # 60 minutes timeout for first run
else
    echo "Existing blockchain data found - normal startup"
    MAX_SOCKET_WAIT=300  # 5 minutes timeout for restart
fi

# Wait for socket creation with smart timeout
echo "Waiting for cardano-node socket at ${CARDANO_SOCKET_PATH:-/ipc/node.socket}..."
TOTAL_WAIT=0
SOCKET_TIMEOUT_STARTED=false
SOCKET_TIMEOUT_WAIT=0
SOCKET_TIMEOUT_MAX=600  # 10 minutes after chunk validation completes

while [ ! -S "${CARDANO_SOCKET_PATH:-/ipc/node.socket}" ]; do
    # Check if cardano-node is still running
    if ! kill -0 $CARDANO_PID 2>/dev/null; then
        handle_error "cardano-node process died before creating socket"
    fi
    
    TOTAL_WAIT=$((TOTAL_WAIT + 1))
    
    # Check if chunk validation is complete (100%)
    if [ "$SOCKET_TIMEOUT_STARTED" = false ]; then
        # Try to capture cardano-node output - check multiple possible sources
        NODE_OUTPUT=""
        if command -v journalctl >/dev/null 2>&1; then
            NODE_OUTPUT=$(journalctl -u cardano-node --since "10 seconds ago" 2>/dev/null || true)
        fi
        if [ -z "$NODE_OUTPUT" ] && command -v docker >/dev/null 2>&1; then
            NODE_OUTPUT=$(docker logs cardano-node --tail 20 2>/dev/null || true)
        fi
        if [ -z "$NODE_OUTPUT" ]; then
            NODE_OUTPUT=$(tail -n 20 /proc/$CARDANO_PID/fd/1 2>/dev/null || true)
        fi
        
        # Check for chunk validation progress
        if echo "$NODE_OUTPUT" | grep -q "Validating chunk\|Validated chunk"; then
            # Extract progress percentage if available
            PROGRESS=$(echo "$NODE_OUTPUT" | grep -oE "Progress: [0-9]+\.[0-9]+%" | tail -1 | grep -oE "[0-9]+\.[0-9]+" || true)
            if [ ! -z "$PROGRESS" ]; then
                echo "Chunk validation progress: ${PROGRESS}%"
                # Check if validation is complete
                if (( $(echo "$PROGRESS >= 99.9" | bc -l 2>/dev/null || echo "0") )); then
                    echo "Chunk validation complete! Starting socket timeout..."
                    SOCKET_TIMEOUT_STARTED=true
                fi
            fi
        elif echo "$NODE_OUTPUT" | grep -q "Chain extended, new tip"; then
            # If we see chain tips being processed, validation is likely complete
            echo "Chain sync detected - starting socket timeout..."
            SOCKET_TIMEOUT_STARTED=true
        fi
    else
        # Socket timeout has started, count down
        SOCKET_TIMEOUT_WAIT=$((SOCKET_TIMEOUT_WAIT + 1))
        if [ $SOCKET_TIMEOUT_WAIT -ge $SOCKET_TIMEOUT_MAX ]; then
            handle_error "Timeout waiting for cardano-node socket after chunk validation completed (${SOCKET_TIMEOUT_MAX} seconds)"
        fi
    fi
    
    # Status updates
    if [ $((TOTAL_WAIT % 30)) -eq 0 ] && [ $TOTAL_WAIT -gt 0 ]; then
        MINUTES=$((TOTAL_WAIT / 60))
        SECONDS=$((TOTAL_WAIT % 60))
        echo "Still waiting for cardano-node... (${MINUTES}m ${SECONDS}s elapsed)"
        if [ "$SOCKET_TIMEOUT_STARTED" = false ]; then
            echo "  Note: Waiting for chunk validation to complete (no timeout until 100%)"
        else
            REMAINING=$((SOCKET_TIMEOUT_MAX - SOCKET_TIMEOUT_WAIT))
            echo "  Note: Chunk validation complete, waiting for socket (timeout in ${REMAINING}s)"
        fi
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