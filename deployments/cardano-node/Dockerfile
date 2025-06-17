FROM ghcr.io/blinklabs-io/cardano-node:10.4.1-3

# Install HAProxy for stable socket forwarding
USER root
RUN apt-get update && \
    apt-get install -y --no-install-recommends haproxy && \
    rm -rf /var/lib/apt/lists/*

# Set environment variables for preview testnet
ENV NETWORK=preview
ENV RESTORE_SNAPSHOT=true

# Copy configuration files
COPY deployments/cardano-node/haproxy.cfg /etc/haproxy/haproxy.cfg
COPY deployments/cardano-node/startup.sh /wrapper-entrypoint.sh

# Set execute permissions
RUN chmod +x /wrapper-entrypoint.sh

# Expose both cardano-node port and HAProxy TCP port
EXPOSE 3001 3333

# Use wrapper as entrypoint
ENTRYPOINT ["/wrapper-entrypoint.sh"]
CMD []