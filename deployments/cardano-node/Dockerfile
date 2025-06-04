FROM ghcr.io/blinklabs-io/cardano-node:10.4.1-3

# Install socat for socket forwarding
USER root
RUN apt-get update && apt-get install -y socat && rm -rf /var/lib/apt/lists/*

# Set environment variables for preview testnet (using blinklabs network mode)
ENV NETWORK=preview
ENV RESTORE_SNAPSHOT=true

# Create wrapper script that preserves blinklabs functionality
RUN echo '#!/bin/bash\n\
\n\
# Start socat forwarder in background\n\
{\n\
    echo "=== PAYL KOYN WRAPPER SCRIPT STARTING ==="\n\
    echo "Starting socat bridge setup..."\n\
    SOCKET_PATH="/ipc/node.socket"\n\
    \n\
    echo "Waiting for socket at: $SOCKET_PATH"\n\
    # Wait up to 3 minutes for socket (90 attempts × 2s)\n\
    for i in $(seq 1 90); do\n\
        echo "Attempt $i/90: Checking for socket..." >&2\n\
        if [ -S "$SOCKET_PATH" ]; then\n\
            echo "Socket found! Starting socat TCP forwarder on port 3333..." >&2\n\
            # Start socat monitoring in background\n\
            {\n\
                while true; do\n\
                    echo "$(date): SOCAT STATUS CHECK - port 3333 listener active" >&2\n\
                    sleep 30\n\
                done\n\
            } &\n\
            # Run socat with restart capability\n\
            while true; do\n\
                echo "$(date): Starting socat bridge..." >&2\n\
                socat TCP6-LISTEN:3333,fork,reuseaddr,bind=[::] UNIX-CONNECT:$SOCKET_PATH\n\
                echo "$(date): Socat bridge died, restarting in 5 seconds..." >&2\n\
                sleep 5\n\
            done\n\
        else\n\
            echo "Socket not found, waiting..." >&2\n\
        fi\n\
        sleep 2\n\
    done\n\
    echo "ERROR: Socket not found at $SOCKET_PATH after 3 minutes"\n\
    exit 1\n\
} &\n\
\n\
# Call original blinklabs entrypoint with all arguments\n\
exec /usr/local/bin/entrypoint "$@"\n\
' > /wrapper-entrypoint.sh

RUN chmod +x /wrapper-entrypoint.sh

# Expose node port and socat TCP port
EXPOSE 3001 3333

# Keep running as root like the original blinklabs image

# Use our wrapper as entrypoint, preserving blinklabs functionality
# No arguments needed for network mode - blinklabs will auto-detect based on NETWORK env var
ENTRYPOINT ["/wrapper-entrypoint.sh"]
CMD []