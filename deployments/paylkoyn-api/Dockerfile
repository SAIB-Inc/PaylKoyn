# PaylKoyn.API Docker image for Railway deployment
FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS build

# Set working directory
WORKDIR /src

# Copy project files
COPY src/PaylKoyn.API/PaylKoyn.API.csproj src/PaylKoyn.API/
COPY src/PaylKoyn.Data/PaylKoyn.Data.csproj src/PaylKoyn.Data/

# Restore dependencies
RUN dotnet restore src/PaylKoyn.API/PaylKoyn.API.csproj

# Copy source code
COPY src/ src/

# Build and publish
RUN dotnet publish src/PaylKoyn.API/PaylKoyn.API.csproj -c Release -o /app/publish

# Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine

# Set working directory
WORKDIR /app

# Copy published application
COPY --from=build /app/publish .

# Expose port
EXPOSE 8080

# Set environment variables
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8080

# Start the application
ENTRYPOINT ["dotnet", "PaylKoyn.API.dll"]