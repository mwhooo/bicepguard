# Testing DriftGuard Docker Container with Samples

This guide shows how to test the DriftGuard container with the sample templates.

## ✅ Authentication Works!

The container successfully mounts your Azure CLI credentials. Here's proof from our tests:

### Authentication Methods

#### 1. **Local Development (Recommended)**
Mount your Azure CLI config directory:

```bash
docker run --rm \
  -v ~/.azure:/root/.azure \
  -v $(pwd)/samples:/workspace \
  driftguard:test \
  --bicep-file main-template.bicepparam \
  --resource-group your-resource-group
```

**How it works:**
- `-v ~/.azure:/root/.azure` mounts your Azure credentials (read-write needed for CLI logging)
- The container uses your existing `az login` session
- No additional authentication required!

#### 2. **CI/CD with Service Principal**
Set environment variables instead of mounting credentials:

```bash
docker run --rm \
  -e AZURE_CLIENT_ID=$AZURE_CLIENT_ID \
  -e AZURE_CLIENT_SECRET=$AZURE_CLIENT_SECRET \
  -e AZURE_TENANT_ID=$AZURE_TENANT_ID \
  -v $(pwd)/samples:/workspace \
  driftguard:test \
  --bicep-file main-template.bicepparam \
  --resource-group your-resource-group
```

**How it works:**
- Environment variables configure Azure CLI automatically
- No credential files needed
- Perfect for pipelines

#### 3. **Managed Identity (Azure Compute)**
When running on Azure VMs or Container Instances with managed identity:

```bash
docker run --rm \
  -v $(pwd)/samples:/workspace \
  driftguard:test \
  --bicep-file main-template.bicepparam \
  --resource-group your-resource-group
```

**How it works:**
- Azure automatically provides credentials
- No configuration needed
- Most secure option for Azure-hosted workloads

## Quick Test

### 1. Verify Authentication

```bash
# Test if Azure CLI works in the container
docker run --rm --entrypoint sh \
  -v ~/.azure:/root/.azure \
  driftguard:test \
  -c "az account show --query '{name:name, id:id}' -o table"
```

Expected output:
```
Name
--------------------------
ATOS BNN PLATFORM MGMT
```

### 2. Run DriftGuard Help

```bash
docker run --rm driftguard:test --help
```

### 3. Test with Sample Template

First, create a test resource group (if it doesn't exist):

```bash
az group create --name driftguard-test --location westeurope
```

Then run DriftGuard:

```bash
docker run --rm \
  -v ~/.azure:/root/.azure \
  -v $(pwd)/samples:/workspace \
  driftguard:test \
  --bicep-file main-template.bicepparam \
  --resource-group driftguard-test
```

### 4. With Custom Ignore Configuration

```bash
docker run --rm \
  -v ~/.azure:/root/.azure \
  -v $(pwd)/samples:/workspace \
  -v $(pwd)/drift-ignore.json:/workspace/drift-ignore.json:ro \
  driftguard:test \
  --bicep-file main-template.bicepparam \
  --resource-group driftguard-test \
  --ignore-config drift-ignore.json
```

### 5. Generate HTML Report

```bash
docker run --rm \
  -v ~/.azure:/root/.azure \
  -v $(pwd)/samples:/workspace \
  driftguard:test \
  --bicep-file main-template.bicepparam \
  --resource-group driftguard-test \
  --output Html
```

The HTML report will be created in the mounted `/workspace` directory!

## Directory Structure

When using the container, the file structure looks like:

```
Container:
  /opt/driftguard/        ← DriftGuard binary
  /workspace/             ← Your mounted files (templates, configs, output)
    ├── main-template.bicep
    ├── main-template.bicepparam
    ├── drift-ignore.json
    └── bicep-modules/
```

**Key Points:**
- Application is in `/opt/driftguard`
- Working directory is `/workspace`
- Mount your templates to `/workspace`
- All file paths are relative to `/workspace`

## Common Issues & Solutions

### ❌ "Resource group not found"
**Solution:** Create the resource group first:
```bash
az group create --name your-rg --location westeurope
```

### ❌ "No ignore configuration found"
**Expected!** This is just a warning. The tool works without it.

To use custom ignore config:
```bash
-v $(pwd)/drift-ignore.json:/workspace/drift-ignore.json:ro
```

### ❌ "ERROR: Please run 'az login'"
**Solution:** Your Azure credentials aren't mounted or expired.

Check locally first:
```bash
az account show
```

If expired, re-login:
```bash
az login
```

Then mount credentials:
```bash
-v ~/.azure:/root/.azure
```

### ❌ "Read-only file system" error for .azure
**Solution:** Mount read-write (Azure CLI writes logs):
```bash
-v ~/.azure:/root/.azure  # Not :ro
```

### ❌ "Security violation: Referenced file outside allowed directory"
**Solution:** This shouldn't happen with the updated Dockerfile, but if it does:
- Ensure files are mounted to `/workspace`
- Use relative paths in your .bicepparam files
- Check that `using` statements point to files in `/workspace`

## Real-World Examples

### Azure DevOps Pipeline

```yaml
- task: Docker@2
  displayName: 'Run DriftGuard'
  inputs:
    command: 'run'
    arguments: >
      --rm
      -v $(Build.SourcesDirectory)/bicep:/workspace
      -e AZURE_CLIENT_ID=$(AZURE_CLIENT_ID)
      -e AZURE_CLIENT_SECRET=$(AZURE_CLIENT_SECRET)
      -e AZURE_TENANT_ID=$(AZURE_TENANT_ID)
      mwhooo/driftguard:latest
      --bicep-file main-template.bicepparam
      --resource-group $(ResourceGroup)
      --output Json > drift-report.json
```

### GitHub Actions

```yaml
- name: Run DriftGuard
  run: |
    docker run --rm \
      -v ${{ github.workspace }}/templates:/workspace \
      -e AZURE_CLIENT_ID=${{ secrets.AZURE_CLIENT_ID }} \
      -e AZURE_CLIENT_SECRET=${{ secrets.AZURE_CLIENT_SECRET }} \
      -e AZURE_TENANT_ID=${{ secrets.AZURE_TENANT_ID }} \
      mwhooo/driftguard:latest \
      --bicep-file main.bicepparam \
      --resource-group ${{ env.RESOURCE_GROUP }} \
      --output Markdown
```

### Bash Script for Regular Checks

```bash
#!/bin/bash
set -e

RESOURCE_GROUP="your-rg"
TEMPLATE_DIR="./bicep"

echo "Running DriftGuard..."
docker run --rm \
  -v ~/.azure:/root/.azure \
  -v "$TEMPLATE_DIR:/workspace" \
  mwhooo/driftguard:latest \
  --bicep-file main.bicepparam \
  --resource-group "$RESOURCE_GROUP" \
  --output Html

echo "Drift report generated: drift-report.html"
```

## Success Indicators

You'll know it's working when you see:

```
🔍 Azure DriftGuard v5.0.0
📄 Bicep Template: main-template.bicepparam
🎯 Deployment Scope: ResourceGroup
🏗️  Resource Group: driftguard-test
📊 Output Format: Console
```

Authentication is working if:
- ✅ No "Please run 'az login'" errors
- ✅ Resource group validation succeeds
- ✅ What-if analysis runs

## Performance Notes

- **First Run:** Slower (pulling base images, resolving Bicep modules)
- **Subsequent Runs:** Faster (cached layers and modules)
- **Container Startup:** ~2-3 seconds
- **Drift Detection:** Same as native (depends on resource count)

## Next Steps

1. **Test locally** with these examples
2. **Update registry** in workflow: [.github/workflows/docker-publish.yml](../.github/workflows/docker-publish.yml)
3. **Push to ACR** and share via scope maps/tokens
4. **Integrate** into your CI/CD pipelines

## Questions or Issues?

Authentication in containers CAN work! The key is:
- ✅ Mount credentials correctly
- ✅ Or use environment variables
- ✅ Or leverage managed identity

You've got this! 🚀
