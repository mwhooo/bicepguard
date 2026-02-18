# 🔗 BicepGuard Integration Guide

This guide shows how to integrate BicepGuard into your existing Azure infrastructure repositories to enable automated drift detection.

## 📋 Prerequisites

Before integrating BicepGuard, ensure you have:

- [ ] Azure subscription with resources to monitor
- [ ] Bicep templates defining your infrastructure
- [ ] GitHub repository for your infrastructure code
- [ ] Azure OIDC authentication configured (see [OIDC Setup](OIDC-SETUP.md))

## 🚀 Quick Integration (5 minutes)

### Option 1: Reusable Workflow (Recommended)

The fastest way to integrate BicepGuard is using our reusable workflow. Add this file to your repository:

**`.github/workflows/drift-monitoring.yml`**

```yaml
name: Azure Drift Monitoring

on:
  schedule:
    - cron: '0 */6 * * *'  # Every 6 hours
  workflow_dispatch:        # Manual trigger

permissions:
  contents: read
  issues: write
  id-token: write

jobs:
  detect-drift:
    uses: mwhooo/bicepguard/.github/workflows/drift-detection-reusable.yml@v4.0.0
    with:
      bicep-file: 'infrastructure/main.bicepparam'
      resource-group: 'my-resource-group'
      environment-name: 'production'
    secrets:
      AZURE_CLIENT_ID: ${{ secrets.AZURE_CLIENT_ID }}
      AZURE_TENANT_ID: ${{ secrets.AZURE_TENANT_ID }}
      AZURE_SUBSCRIPTION_ID: ${{ secrets.AZURE_SUBSCRIPTION_ID }}
```

### Option 2: Multi-Environment Monitoring

For monitoring multiple environments:

```yaml
name: Azure Drift Monitoring

on:
  schedule:
    - cron: '0 */6 * * *'
  workflow_dispatch:

permissions:
  contents: read
  issues: write
  id-token: write

jobs:
  dev:
    uses: mwhooo/bicepguard/.github/workflows/drift-detection-reusable.yml@v4.0.0
    with:
      bicep-file: 'infrastructure/main.bicepparam'
      resource-group: 'myapp-dev-rg'
      environment-name: 'dev'
    secrets:
      AZURE_CLIENT_ID: ${{ secrets.AZURE_CLIENT_ID }}
      AZURE_TENANT_ID: ${{ secrets.AZURE_TENANT_ID }}
      AZURE_SUBSCRIPTION_ID: ${{ secrets.AZURE_SUBSCRIPTION_ID }}

  staging:
    uses: mwhooo/bicepguard/.github/workflows/drift-detection-reusable.yml@v4.0.0
    with:
      bicep-file: 'infrastructure/main.bicepparam'
      resource-group: 'myapp-staging-rg'
      environment-name: 'staging'
    secrets:
      AZURE_CLIENT_ID: ${{ secrets.AZURE_CLIENT_ID }}
      AZURE_TENANT_ID: ${{ secrets.AZURE_TENANT_ID }}
      AZURE_SUBSCRIPTION_ID: ${{ secrets.AZURE_SUBSCRIPTION_ID }}

  production:
    uses: mwhooo/bicepguard/.github/workflows/drift-detection-reusable.yml@v4.0.0
    with:
      bicep-file: 'infrastructure/main.bicepparam'
      resource-group: 'myapp-prod-rg'
      environment-name: 'production'
    secrets:
      AZURE_CLIENT_ID: ${{ secrets.AZURE_CLIENT_ID }}
      AZURE_TENANT_ID: ${{ secrets.AZURE_TENANT_ID }}
      AZURE_SUBSCRIPTION_ID: ${{ secrets.AZURE_SUBSCRIPTION_ID }}
```

## ⚙️ Configuration Options

### Reusable Workflow Inputs

| Input | Required | Default | Description |
|-------|----------|---------|-------------|
| `bicep-file` | ✅ | - | Path to Bicep template or .bicepparam file |
| `resource-group` | ✅ | - | Azure resource group to monitor |
| `environment-name` | ❌ | `production` | Environment name for reporting |
| `bicepguard-version` | ❌ | `v4.0.0` | BicepGuard version to use |
| `autofix` | ❌ | `false` | Auto-deploy template to fix drift |
| `create-issue` | ❌ | `true` | Create GitHub issue on drift |
| `ignore-config` | ❌ | - | Path to custom drift-ignore.json |

### Required Secrets

| Secret | Description |
|--------|-------------|
| `AZURE_CLIENT_ID` | Azure Service Principal Application ID |
| `AZURE_TENANT_ID` | Azure Active Directory Tenant ID |
| `AZURE_SUBSCRIPTION_ID` | Azure Subscription ID |

## 🔐 Azure Authentication Setup

### 1. Create Service Principal

```bash
# Create service principal with Reader role
az ad sp create-for-rbac --name "BicepGuard-YourApp" \
  --role "Reader" \
  --scopes "/subscriptions/YOUR_SUBSCRIPTION_ID/resourceGroups/YOUR_RG"
```

### 2. Configure OIDC Federation

```bash
# Get the Application ID
APP_ID=$(az ad app list --display-name "BicepGuard-YourApp" --query "[0].appId" -o tsv)

# Create federated credential for main branch
az ad app federated-credential create --id $APP_ID --parameters '{
  "name": "GitHub-Actions-Main",
  "issuer": "https://token.actions.githubusercontent.com",
  "subject": "repo:YOUR_ORG/YOUR_REPO:ref:refs/heads/main",
  "audiences": ["api://AzureADTokenExchange"]
}'
```

### 3. Add GitHub Secrets

Go to your repository **Settings → Secrets and variables → Actions** and add:

- `AZURE_CLIENT_ID`
- `AZURE_TENANT_ID`  
- `AZURE_SUBSCRIPTION_ID`

## 🔇 Custom Ignore Configuration

To suppress Azure platform noise, create a `drift-ignore.json` in your repository:

```json
{
  "version": "1.0",
  "globalIgnorePatterns": [
    "properties.provisioningState",
    "etag",
    "id",
    "type",
    "location"
  ],
  "resourceIgnoreRules": [
    {
      "resourceType": "Microsoft.Storage/storageAccounts",
      "ignorePatterns": [
        "properties.primaryEndpoints",
        "properties.creationTime"
      ]
    }
  ]
}
```

Then reference it in your workflow:

```yaml
jobs:
  detect-drift:
    uses: mwhooo/bicepguard/.github/workflows/drift-detection-reusable.yml@v4.0.0
    with:
      bicep-file: 'infrastructure/main.bicepparam'
      resource-group: 'my-resource-group'
      ignore-config: 'drift-ignore.json'  # 👈 Add this
    secrets:
      # ...
```

## 🔧 Advanced: Auto-Remediation

Enable automatic drift remediation (use with caution in production):

```yaml
jobs:
  detect-and-fix:
    uses: mwhooo/bicepguard/.github/workflows/drift-detection-reusable.yml@v4.0.0
    with:
      bicep-file: 'infrastructure/main.bicepparam'
      resource-group: 'my-dev-rg'
      environment-name: 'dev'
      autofix: true  # 👈 Auto-deploy on drift
    secrets:
      # ...
```

> ⚠️ **Warning**: Auto-remediation requires the service principal to have `Contributor` role instead of `Reader`.

## 📊 Understanding Drift Reports

When drift is detected, BicepGuard will:

1. **Create a GitHub Issue** with details about the drift
2. **Upload artifacts** with JSON reports
3. **Label the issue** with `drift-detection` and environment name

### Example Issue

```
🚨 Configuration Drift Detected - production

Environment: production
Resource Group: myapp-prod-rg
Detection Time: 2026-01-12T14:00:00Z

Drifted Resources:
- Microsoft.Storage/storageAccounts/mystorageaccount
  - properties.allowBlobPublicAccess: Expected false, Actual true
```

## 🔄 Migration from Standalone Monitoring

If you're currently running BicepGuard manually or with a custom workflow:

1. Remove your existing drift monitoring workflow
2. Add the reusable workflow as shown above
3. Ensure your Bicep files are in the paths specified
4. Verify OIDC authentication is configured

## 🆘 Troubleshooting

### Common Issues

| Issue | Solution |
|-------|----------|
| OIDC login fails | Verify federated credential subject matches your repo/branch |
| Bicep file not found | Check the path is relative to repository root |
| Permission denied | Ensure service principal has Reader role on resource group |
| False positives | Add ignore patterns to drift-ignore.json |

### Debug Mode

Run the workflow manually with debug logging:

```bash
gh workflow run drift-monitoring.yml
gh run watch
```

## 📚 Additional Resources

- [Drift Ignore Configuration](DRIFT-IGNORE.md) - Complete ignore pattern reference
- [OIDC Authentication Setup](OIDC-SETUP.md) - Detailed Azure OIDC guide
- [Monitoring Best Practices](MONITORING.md) - Tips for effective monitoring
- [Main Documentation](../README.md) - Full BicepGuard documentation
