# PostgreSQL benchmark service for Railway performance testing
FROM postgres:16-alpine

# Install additional tools for benchmarking
RUN apk add --no-cache curl time

# Copy benchmark scripts
COPY benchmark.sh /benchmark.sh
RUN chmod +x /benchmark.sh

# Set default environment
ENV PGCONNECT_TIMEOUT=10
ENV PGCOMMAND_TIMEOUT=30

# Run benchmark
ENTRYPOINT ["/benchmark.sh"]