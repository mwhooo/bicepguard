# Using DriftGuard with JSON Parameter Files

DriftGuard supports Azure ARM JSON parameter files in addition to `.bicepparam` files. This is useful for existing Landing Zone deployments and MLZ workflows.

## JSON Parameter File Format

Standard Azure ARM parameter file format:

```json
{
    "$schema": "https://schema.management.azure.com/schemas/2019-04-01/deploymentParameters.json#",
    "contentVersion": "1.0.0.0",
    "parameters": {
        "virtualNetworks": {
            "value": [...]
        }
    }
}
```

## Usage Examples

### Resource Group Scope

```bash
# Basic drift detection
dotnet run -- \
  --bicep-file ./samples/storage-account.bicep \
  --parameters-file ./samples/storage-params.json \
  --resource-group my-resource-group

# With autofix
dotnet run -- \
  --bicep-file ./samples/storage-account.bicep \
  --parameters-file ./samples/storage-params.json \
  --resource-group my-resource-group \
  --autofix
```

### Subscription Scope (MLZ/Landing Zones)

```bash
# Networking deployment example
dotnet run -- \
  --bicep-file /path/to/networking.bicep \
  --parameters-file /path/to/prod.lz01.networking.params.json \
  --scope Subscription \
  --subscription 12345678-1234-1234-1234-123456789abc \
  --location westeurope

# With custom ignore config
dotnet run -- \
  --bicep-file /path/to/networking.bicep \
  --parameters-file /path/to/prod.lz01.networking.params.json \
  --scope Subscription \
  --subscription 12345678-1234-1234-1234-123456789abc \
  --location westeurope \
  --ignore-config custom-ignore.json
```

### MLZ Hub & Spoke Example

For Microsoft Managed Landing Zones networking:

```bash
# Check drift for Hub networking
dotnet run -- \
  --bicep-file parentModules/networking/networking.bicep \
  --parameters-file input/plt/prod/parentModules/cnty/prod.cnty.networking.params.json \
  --scope Subscription \
  --subscription $HUB_SUBSCRIPTION_ID \
  --location westeurope \
  --ignore-config drift-ignore.json

# Check drift for Spoke (Landing Zone)
dotnet run -- \
  --bicep-file parentModules/networking/networking.bicep \
  --parameters-file input/plt/prod/parentModules/lz01/prod.lz01.networking.params.json \
  --scope Subscription \
  --subscription $LZ_SUBSCRIPTION_ID \
  --location westeurope \
  --ignore-config drift-ignore.json
```

## Integration with CI/CD

Example GitHub Actions step:

```yaml
- name: Check Drift
  run: |
    ./DriftGuard \
      --bicep-file ${{ env.networkingTemplateFile }} \
      --parameters-file ${{ env.networkingParametersFile }} \
      --scope Subscription \
      --subscription ${{ secrets.SUBSCRIPTION_ID }} \
      --location westeurope \
      --output Json
```

## Notes

- **Subscription scope** requires `--subscription` and `--location` parameters
- **Resource group scope** requires `--resource-group` parameter
- JSON parameter files must follow the Azure ARM parameter schema
- The `--parameters-file` option works alongside existing `.bicepparam` support
- Use `--ignore-config` to suppress Azure platform noise (highly recommended for production)
