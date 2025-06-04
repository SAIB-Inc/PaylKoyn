# PaylKoyn.Node Docker image for Railway deployment
FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS build

# Set working directory
WORKDIR /src

# Copy project files
COPY src/PaylKoyn.Node/PaylKoyn.Node.csproj src/PaylKoyn.Node/
COPY src/PaylKoyn.Data/PaylKoyn.Data.csproj src/PaylKoyn.Data/

# Restore dependencies
RUN dotnet restore src/PaylKoyn.Node/PaylKoyn.Node.csproj

# Copy source code
COPY src/ src/

# Build and publish
RUN dotnet publish src/PaylKoyn.Node/PaylKoyn.Node.csproj -c Release -o /app/publish

# Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:9.0

# Set working directory
WORKDIR /app

# Install sudo for data directory permissions
USER root
RUN apt-get update && apt-get install -y sudo && rm -rf /var/lib/apt/lists/*
RUN echo 'app ALL=(ALL) NOPASSWD:ALL' >> /etc/sudoers

# Copy published application
COPY --from=build /app/publish .

# Create simple entrypoint for data permissions
RUN echo '#!/bin/bash\n\
echo "=== PAYLKOYN.NODE STARTING ==="\n\
sudo chown -R app:app /data 2>/dev/null || true\n\
echo "Starting PaylKoyn.Node..."\n\
exec dotnet PaylKoyn.Node.dll "$@"\n\
' > /entrypoint.sh

RUN chmod +x /entrypoint.sh

# Expose port
EXPOSE 8080

# Set environment variables
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8080

USER app
ENTRYPOINT ["/entrypoint.sh"]