#!/bin/bash
set -euo pipefail

echo "=== CARDANO NODE WRAPPER STARTING ==="

# Function to handle errors
handle_error() {
    echo "ERROR: $1" >&2
    exit 1
}

# Function to monitor and start HAProxy
start_haproxy() {
    local socket_path="/ipc/node.socket"
    
    echo "Waiting for cardano-node socket at: $socket_path"
    
    # Wait for socket to exist (up to 3 minutes)
    local attempts=0
    while [ $attempts -lt 90 ]; do
        if [ -S "$socket_path" ]; then
            echo "Socket found! Starting HAProxy on port 3333..."
            
            # Start HAProxy in daemon mode with PID file
            haproxy -f /etc/haproxy/haproxy.cfg -D -p /var/run/haproxy.pid || {
                handle_error "Failed to start HAProxy"
            }
            
            # Verify HAProxy started
            sleep 2
            if ! kill -0 $(cat /var/run/haproxy.pid 2>/dev/null) 2>/dev/null; then
                handle_error "HAProxy failed to start or died immediately"
            fi
            
            echo "HAProxy started successfully"
            
            # Monitor HAProxy health
            while true; do
                sleep 10
                if ! kill -0 $(cat /var/run/haproxy.pid 2>/dev/null) 2>/dev/null; then
                    echo "ERROR: HAProxy died unexpectedly!" >&2
                    exit 1
                fi
            done
        fi
        
        attempts=$((attempts + 1))
        echo "Attempt $attempts/90: Socket not found, waiting..."
        sleep 2
    done
    
    handle_error "Cardano node socket not found after 3 minutes!"
}

# Start HAProxy monitor in background
start_haproxy &
HAPROXY_PID=$!

# Monitor both cardano-node and HAProxy
{
    sleep 30  # Give cardano-node time to start
    while true; do
        # Check if HAProxy monitor is still running
        if ! kill -0 $HAPROXY_PID 2>/dev/null; then
            handle_error "HAProxy monitor process died"
        fi
        
        # Check if cardano-node process exists
        if ! pgrep -f "cardano-node" > /dev/null; then
            handle_error "Cardano node process died"
        fi
        
        sleep 10
    done
} &

# Start the original cardano-node
echo "Starting cardano-node..."
exec /usr/local/bin/entrypoint "$@" || handle_error "Failed to start cardano-node"