# 🐳 Docker Usage Guide

DriftGuard is available as a public Docker container on Docker Hub, making it easy to run without installing dependencies.

## Prerequisites

- Docker installed
- Azure CLI authentication (via `az login` on your host machine)

## Quick Start

### Pull the Image

```bash
# Pull latest version (no authentication needed - it's public!)
docker pull mwhooo/driftguard:latest

# Pull specific version
docker pull mwhooo/driftguard:5.0.0
```

**Image Location:** https://hub.docker.com/r/mwhooo/driftguard

### Basic Usage

```bash
docker run --rm \
  -v ~/.azure:/root/.azure \
  -v $(pwd):/workspace \
  mwhooo/driftguard:latest \
  --bicep-file template.bicep \
  --resource-group myResourceGroup
```

## Usage Patterns

### 1. Mount Current Directory

Run DriftGuard on a template in your current directory:

```bash
docker run --rm \
  -v ~/.azure:/root/.azure \
  -v $(pwd):/workspace \
  mwhooo/driftguard:latest \
  --bicep-file main.bicep \
  --resource-group myRG
```

### 2. With Auto-Fix

Enable automatic drift remediation:

```bash
docker run --rm \
  -v ~/.azure:/root/.azure \
  -v $(pwd):/workspace \
  mwhooo/driftguard:latest \
  --bicep-file template.bicep \
  --resource-group myRG \
  --autofix
```

### 3. Generate HTML Report

Output HTML report to local directory:

```bash
docker run --rm \
  -v ~/.azure:/root/.azure \
  -v $(pwd):/workspace \
  mwhooo/driftguard:latest \
  --bicep-file template.bicep \
  --resource-group myRG \
  --output Html
```

### 4. With Custom Ignore Configuration

Use a custom drift-ignore.json file:

```bash
docker run --rm \
  -v ~/.azure:/root/.azure \
  -v $(pwd):/workspace \
  mwhooo/driftguard:latest \
  --bicep-file template.bicep \
  --resource-group myRG \
  --ignore-config custom-ignore.json
```

### 5. Azure DevOps Pipeline

```yaml
- task: AzureCLI@2
  displayName: 'Detect Drift'
  inputs:
    azureSubscription: 'MyServiceConnection'
    scriptType: 'bash'
    scriptLocation: 'inlineScript'
    inlineScript: |
      docker run --rm \
        -v $(Build.SourcesDirectory):/workspace \
        -e AZURE_CONFIG_DIR=/root/.azure \
        mwhooo/driftguard:latest \
        --bicep-file /workspace/template.bicep \
        --resource-group $(resourceGroup) \
        --output Json > drift-report.json
```

### 6. GitHub Actions

```yaml
- name: Run DriftGuard
  run: |
    docker run --rm \
      -v ${{ github.workspace }}:/workspace \
      mwhooo/driftguard:latest \
      --bicep-file /workspace/main.bicep \
      --resource-group ${{ env.RESOURCE_GROUP }}
```

## Authentication Options

### Option 1: Azure CLI Credentials (Recommended for Local)

Mount your Azure CLI config directory (read-write, as Azure CLI writes logs):

```bash
-v ~/.azure:/root/.azure
```

**Note:** The mount must be read-write (not `:ro`) because Azure CLI writes log files.

### Option 2: Service Principal (Recommended for CI/CD)

Set environment variables:

```bash
docker run --rm \
  -e AZURE_CLIENT_ID=$AZURE_CLIENT_ID \
  -e AZURE_CLIENT_SECRET=$AZURE_CLIENT_SECRET \
  -e AZURE_TENANT_ID=$AZURE_TENANT_ID \
  -v $(pwd):/workspace \
  mwhooo/driftguard:latest \
  --bicep-file /workspace/template.bicep \
  --resource-group myRG
```

### Option 3: Managed Identity (Azure VM/Container Instances)

No credentials needed when running on Azure compute with managed identity enabled:

```bash
docker run --rm \
  -v $(pwd):/workspace \
  mwhooo/driftguard:latest \
  --bicep-file /workspace/template.bicep \
  --resource-group myRG
```

## Volume Mounts

| Mount | Purpose | Example |
|-------|---------|---------|
| `/workspace` | Template files and output | `-v $(pwd):/workspace` |
| `/root/.azure` | Azure CLI credentials | `-v ~/.azure:/root/.azure:ro` |
| `/app/drift-ignore.json` | Custom ignore config | `-v ./ignore.json:/app/drift-ignore.json:ro` |

## Environment Variables

| Variable | Description | Example |
|----------|-------------|---------|
| `AZURE_SUBSCRIPTION_ID` | Target subscription | `abc-123-def` |
| `AZURE_CLIENT_ID` | Service principal ID | `xyz-789` |
| `AZURE_CLIENT_SECRET` | Service principal secret | `secret123` |
| `AZURE_TENANT_ID` | Azure tenant ID | `tenant-id` |

## Public Availability

**This image is publicly available on Docker Hub!**

- **Repository:** https://hub.docker.com/r/mwhooo/driftguard
- **Pull Command:** `docker pull mwhooo/driftguard`
- **No Authentication Required:** Anyone can pull and use the image
- **Free:** Docker Hub public images are free with unlimited pulls

## Troubleshooting

### Permission Denied

Ensure volume mounts have read permissions:

```bash
-v $(pwd):/workspace:ro  # Read-only for templates
```

### Azure Authentication Fails

Test authentication first:

```bash
docker run --rm \
  -v ~/.azure:/root/.azure:ro \
  mwhooo/driftguard:latest \
  bash -c "az account show"
```

### Bicep Version Issues

The container includes the latest Bicep CLI. To verify:

```bash
docker run --rm \
  mwhooo/driftguard:latest \
  bash -c "az bicep version"
```

## Building Locally

To build the image locally:

```bash
# Build
docker build -t driftguard:local .

# Run
docker run --rm \
  -v $(pwd):/workspace \
  -v ~/.azure:/root/.azure:ro \
  driftguard:local \
  --bicep-file /workspace/template.bicep \
  --resource-group myRG
```

## Image Details

- **Base Image**: `mcr.microsoft.com/azure-cli:latest`
- **Runtime**: .NET 8.0
- **Included Tools**: Azure CLI, Bicep CLI
- **Size**: ~300MB compressed
- **Architecture**: linux/amd64

## Best Practices

1. **Use specific version tags** in production pipelines instead of `latest`
2. **Mount Azure credentials read-only** to prevent accidental modifications
3. **Use ACR tokens** for sharing instead of admin credentials
4. **Run as non-root** in production (add USER directive in custom Dockerfile)
5. **Scan images regularly** for vulnerabilities using ACR security scanning

## Support

For issues or questions:
- GitHub Issues: https://github.com/NL-AMS-PLATFORM-ENGINEER/pe-az-driftguard/issues
- Internal Documentation: See `docs/` directory
