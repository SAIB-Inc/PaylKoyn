# PaylKoyn.ImageGen Docker image for Railway deployment
FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS build

# Set working directory
WORKDIR /src

# Copy project files
COPY src/PaylKoyn.ImageGen/PaylKoyn.ImageGen.csproj src/PaylKoyn.ImageGen/
COPY src/PaylKoyn.Data/PaylKoyn.Data.csproj src/PaylKoyn.Data/

# Restore dependencies
RUN dotnet restore src/PaylKoyn.ImageGen/PaylKoyn.ImageGen.csproj

# Copy source code
COPY src/ src/

# Build and publish
RUN dotnet publish src/PaylKoyn.ImageGen/PaylKoyn.ImageGen.csproj -c Release -o /app/publish

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

# Copy Assets folder for NFT image generation
COPY src/PaylKoyn.ImageGen/Assets ./Assets

# Create simple entrypoint for data permissions
RUN echo '#!/bin/bash\n\
echo "=== PAYLKOYN.IMAGEGEN STARTING ==="\n\
sudo chown -R app:app /data 2>/dev/null || true\n\
echo "Starting PaylKoyn.ImageGen..."\n\
exec dotnet PaylKoyn.ImageGen.dll "$@"\n\
' > /entrypoint.sh

RUN chmod +x /entrypoint.sh

# Expose port
EXPOSE 8080

# Set environment variables
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8080

USER app
ENTRYPOINT ["/entrypoint.sh"]