#!/bin/bash
set -e

echo "=== PAYLKOYN.SYNC STARTING ==="

# Ensure proper permissions
echo "Setting up permissions..."
sudo chown -R app:app /data /ipc 2>/dev/null || true
sudo rm -f /ipc/node.socket 2>/dev/null || true

# Wait for cardano-node
echo "Waiting for cardano-node:3333..."
while ! nc -w 1 cardano-node 3333 < /dev/null 2>/dev/null; do
    echo "cardano-node not ready, retrying..."
    sleep 2
done

echo "cardano-node:3333 is available!"

# Start HAProxy in background with monitoring
{
    while true; do
        echo "$(date): Starting HAProxy..."
        haproxy -f /etc/haproxy/haproxy.cfg
        echo "$(date): HAProxy exited with code $?, restarting in 5s..."
        sleep 5
    done
} &

# Give HAProxy time to create socket
sleep 3

# Start the application
echo "Starting PaylKoyn.Sync..."
export ASPNETCORE_ENVIRONMENT=Railway
exec dotnet PaylKoyn.Sync.dll "$@"