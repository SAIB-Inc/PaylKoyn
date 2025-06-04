FROM alpine:latest

# Install dependencies
RUN apk add --no-cache \
    bash \
    curl \
    socat \
    netcat-openbsd \
    bind-tools \
    iputils \
    nmap

# Download and install cardano-cli 10.4.1 (fixed extraction)
# Extract to temp directory to avoid overwriting /bin/
RUN mkdir -p /tmp/cardano \
    && cd /tmp/cardano \
    && curl -L -o cardano-node.tar.gz \
    "https://github.com/IntersectMBO/cardano-node/releases/download/10.4.1/cardano-node-10.4.1-linux.tar.gz" \
    && tar -xzf cardano-node.tar.gz \
    && ls -la \
    && mv bin/cardano-cli /usr/local/bin/ \
    && chmod +x /usr/local/bin/cardano-cli \
    && cd / \
    && rm -rf /tmp/cardano

# Copy and run the clean test script
COPY --chmod=755 test-cli.sh /test-cli.sh
CMD ["/bin/bash", "/test-cli.sh"]