# Multi-stage build for DriftGuard
# Stage 1: Build the application
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Accept version as build argument
ARG VERSION=0.0.0-dev

# Copy project files
COPY ["DriftGuard.csproj", "./"]
COPY ["Directory.Build.props", "./"]

# Restore dependencies
RUN dotnet restore "DriftGuard.csproj"

# Copy source code
COPY . .

# Build and publish with version
RUN dotnet publish "DriftGuard.csproj" -c Release -o /app/publish --no-restore -p:Version=${VERSION}

# Stage 2: Runtime image with Azure CLI and Bicep
FROM mcr.microsoft.com/azure-cli:latest

# Install Bicep CLI
RUN az bicep install

# Copy the published application from build stage to /opt/driftguard
WORKDIR /opt/driftguard
COPY --from=build /app/publish .

# Set working directory to /workspace for user files
WORKDIR /workspace

# Install .NET 8 runtime (required to run the app)
# Azure CLI image uses Azure Linux with tdnf package manager
RUN tdnf install -y dotnet-runtime-8.0 icu \
    && tdnf clean all

# Set environment variables
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false \
    LC_ALL=en_US.UTF-8 \
    LANG=en_US.UTF-8 \
    PATH="/opt/driftguard:${PATH}"

# Set the entrypoint to DriftGuard
ENTRYPOINT ["dotnet", "/opt/driftguard/DriftGuard.dll"]

# Default help command if no args provided
CMD ["--help"]

# Metadata
LABEL org.opencontainers.image.title="DriftGuard" \
      org.opencontainers.image.description="Azure infrastructure drift detection and remediation tool" \
      org.opencontainers.image.vendor="Mark Bakker" \
      org.opencontainers.image.source="https://github.com/mwhooo/driftguard"
