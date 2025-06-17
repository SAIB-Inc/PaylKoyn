#!/bin/bash
set -e

echo "=== CARDANO NODE WRAPPER STARTING ==="

# Function to monitor and start HAProxy
start_haproxy() {
    local socket_path="/ipc/node.socket"
    
    echo "Waiting for cardano-node socket at: $socket_path"
    
    # Wait for socket to exist (up to 3 minutes)
    local attempts=0
    while [ $attempts -lt 90 ]; do
        if [ -S "$socket_path" ]; then
            echo "Socket found! Starting HAProxy on port 3333..."
            
            # Run HAProxy with automatic restart
            while true; do
                echo "$(date): Starting HAProxy bridge..."
                haproxy -f /etc/haproxy/haproxy.cfg
                echo "$(date): HAProxy exited with code $?, restarting in 5s..."
                sleep 5
            done
            return 0
        fi
        
        attempts=$((attempts + 1))
        echo "Attempt $attempts/90: Socket not found, waiting..."
        sleep 2
    done
    
    echo "ERROR: Socket not found after 3 minutes!"
    return 1
}

# Start HAProxy in background
start_haproxy &

# Start the original cardano-node
echo "Starting cardano-node..."
exec /usr/local/bin/entrypoint "$@"