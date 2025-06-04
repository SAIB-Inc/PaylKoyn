#!/bin/bash

echo "Testing cardano-cli via socat..."

# Debug: Check DNS resolution and basic connectivity
echo "=== DEBUGGING CONNECTIVITY ==="
echo "Resolving cardano-node hostname..."
nslookup cardano-node || echo "DNS resolution failed"

echo "Checking if cardano-node host is reachable..."
ping -c 3 cardano-node || echo "Ping failed"

echo "Getting cardano-node IP address..."
CARDANO_IP=$(nslookup cardano-node | grep "Address:" | tail -1 | cut -d' ' -f2)
echo "Cardano-node IP: $CARDANO_IP"

echo "Testing direct IP connection..."
ping -c 1 $CARDANO_IP && echo "Direct IP ping successful" || echo "Direct IP ping failed"

echo "Checking ports with netcat..."
echo "Testing port 3001 (cardano node)..."
nc -w 2 cardano-node 3001 < /dev/null && echo "Port 3001 is open" || echo "Port 3001 is closed/filtered"

echo "Testing port 3333 (socat bridge)..."
nc -w 2 cardano-node 3333 < /dev/null && echo "Port 3333 is open" || echo "Port 3333 is closed/filtered"

echo "Testing direct IP ports..."
nc -w 2 $CARDANO_IP 3001 < /dev/null && echo "IP port 3001 is open" || echo "IP port 3001 is closed/filtered"
nc -w 2 $CARDANO_IP 3333 < /dev/null && echo "IP port 3333 is open" || echo "IP port 3333 is closed/filtered"

echo "=== END DEBUG ==="

# Wait for cardano-node service to be available
echo "Now waiting for cardano-node:3333 to become available..."
ATTEMPTS=0
while ! nc -w 1 cardano-node 3333 < /dev/null; do
    ATTEMPTS=$((ATTEMPTS + 1))
    echo "Attempt $ATTEMPTS: Waiting for cardano-node:3333..."
    if [ $ATTEMPTS -eq 30 ]; then
        echo "Giving up after 30 attempts (60 seconds)"
        exit 1
    fi
    sleep 2
done

echo "cardano-node:3333 is available!"

# Start socat bridge with monitoring
echo "Starting socat bridge..."
socat UNIX-LISTEN:/tmp/node.socket,fork,reuseaddr TCP:cardano-node:3333 &
SOCAT_PID=$!

# Wait for socket to be created with better checking
echo "Waiting for Unix socket to be created..."
for i in $(seq 1 30); do
    if [ -S /tmp/node.socket ]; then
        echo "Unix socket created successfully!"
        break
    fi
    if [ $i -eq 30 ]; then
        echo "ERROR: Unix socket not created after 60 seconds"
        exit 1
    fi
    sleep 2
done

# Function to restart socat if it dies
restart_socat() {
    echo "$(date): Socat bridge died, restarting..."
    socat UNIX-LISTEN:/tmp/node.socket,fork,reuseaddr TCP:cardano-node:3333 &
    SOCAT_PID=$!
}

# Test cardano-cli in a loop with better error handling
echo "Testing cardano-cli query tip..."
while true; do
    # Check if socat is still running
    if ! kill -0 $SOCAT_PID 2>/dev/null; then
        restart_socat
        sleep 3
    fi
    
    echo "$(date): Querying blockchain tip..."
    if cardano-cli query tip --socket-path /tmp/node.socket --testnet-magic 2; then
        echo "Query successful!"
    else
        echo "Query failed - checking connection..."
        if ! nc -w 1 cardano-node 3333 < /dev/null; then
            echo "Lost connection to cardano-node:3333"
        fi
    fi
    echo "---"
    sleep 30
done
