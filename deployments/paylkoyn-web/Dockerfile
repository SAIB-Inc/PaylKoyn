# PaylKoyn.Web Docker image for Railway deployment
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build

# Install Bun for frontend build
RUN curl -fsSL https://bun.sh/install | bash
ENV PATH="/root/.bun/bin:$PATH"

# Set working directory
WORKDIR /src

# Copy project files for restore
COPY src/PaylKoyn.Web/PaylKoyn.Web.csproj src/PaylKoyn.Web/
COPY src/PaylKoyn.Data/PaylKoyn.Data.csproj src/PaylKoyn.Data/

# Restore .NET dependencies
RUN dotnet restore src/PaylKoyn.Web/PaylKoyn.Web.csproj

# Copy source code
COPY src/ src/

# Build frontend assets
WORKDIR /src/src/PaylKoyn.Web/wwwroot
RUN bun install
RUN bun run build

# Build and publish .NET application
WORKDIR /src
RUN dotnet publish src/PaylKoyn.Web/PaylKoyn.Web.csproj -c Release -o /app/publish

# Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:9.0

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
ENTRYPOINT ["dotnet", "PaylKoyn.Web.dll"]