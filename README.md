# Azure DriftGuard

A sophisticated C# console application that detects configuration drift between Bicep/ARM templates and live Azure resources. Built for DevOps teams practicing Infrastructure as Code (IaC) to ensure deployed resources match their intended configuration.

> 🔗 **Want to add drift detection to your Azure repos?** See check the [Integration Guide](docs/INTEGRATION.md) for quick setup using reusable workflows.

## 🎯 Purpose

Configuration drift occurs when live Azure resources diverge from their Infrastructure as Code definitions. This can happen through:
- Manual changes via Azure Portal
- Direct Azure CLI/PowerShell modifications  
- External automation or scripts
- Azure policy enforcement
- Resource auto-scaling or auto-updates

DriftGuard helps maintain **IaC compliance** by identifying these deviations quickly and clearly.

## ✨ Key Features

### 🔍 **Azure What-If Based Drift Detection**
- **Azure-Native Comparison**: Uses Azure's `az deployment what-if` for authoritative drift detection
- **Multi-Scope Support**: Works with both resource-group scope (`az deployment group`) and subscription scope (`az deployment sub`)
- **Intelligent Noise Suppression**: Filters Azure platform behaviors with configurable ignore patterns
- **Multi-Resource Support**: Works with any Azure resource type (VNets, Storage, Key Vault, App Services, NSGs, etc.)
- **Property-Level Comparison**: Detects specific property changes with precise Expected vs Actual reporting
- **Complex Object Handling**: Intelligent reporting for arrays and nested objects
- **External Module Support**: Full support for Azure Container Registry modules (`br:` syntax) and Azure Verified Modules (AVM)

### 📊 **Clean, Human-Friendly Reporting**
- **Suppressed Verbose Output**: Azure what-if output hidden, showing only formatted results
- **Console**: Clean, colorized terminal output with emojis
- **JSON**: Structured data for automation and CI/CD integration
- **HTML**: Browser-friendly reports with styling
- **Markdown**: Documentation-ready format
- **Complex Object Messages**: Clear explanations for array/object drift instead of raw JSON

### 🔧 **Automatic Drift Remediation**
- **Autofix Mode**: Automatically deploy Bicep template to fix detected drift with `--autofix` flag
- **Smart Deployment**: Only deploys when actual drift is detected
- **Safe Execution**: Provides detailed deployment feedback and error handling
- **Deployment Tracking**: Generates unique deployment names with timestamps

### 🔇 **Intelligent Drift Filtering**
- **Noise Suppression**: Advanced ignore system to filter out Azure platform behaviors and false positives
- **AVM Noise Filtering**: Specialized suppression for Azure Verified Modules compliance properties
- **Resource-Specific Rules**: Target specific resource types with conditional filtering
- **Global Patterns**: Apply ignore rules across all resource types for common Azure properties
- **Conditional Logic**: Rules that apply only when specific conditions are met (SKU tier, resource kind, etc.)
- **Configurable Paths**: Support for custom ignore configuration files via `--ignore-config`
- **Pattern Matching**: Flexible property path matching with wildcards and nested object support
- **Clear Feedback**: Visual indicators showing which drifts are being ignored and why

📖 **[Complete Drift Ignore Documentation](docs/DRIFT-IGNORE.md)** - Comprehensive guide with examples and best practices

## 🚀 Quick Start

### 🐳 Docker (Recommended)

**No installation required!** Just pull and run:

```bash
# Pull the public image (no authentication needed!)
docker pull mwhooo/driftguard

# Run with your Azure credentials
docker run --rm \
  -v ~/.azure:/root/.azure \
  -v $(pwd):/workspace \
  mwhooo/driftguard \
  --bicep-file template.bicep \
  --resource-group myResourceGroup
```

**Prerequisites:**
- Docker installed
- Azure CLI authentication (`az login` on your host machine)

📖 **[Complete Docker Documentation](docs/DOCKER.md)** - Usage patterns, CI/CD examples, and troubleshooting

### 💻 Native Installation

**Prerequisites:**
- .NET 8.0 SDK
- Azure CLI (logged in with `az login`)
- Bicep CLI

**Installation:**
```bash
git clone <your-repo>
cd DriftGuard
dotnet build
```

### Parameter File Approaches

DriftGuard supports three ways to provide parameters to your Bicep templates:

| Approach | Bicep File | Parameters | Use Case | Example File |
|----------|-----------|-----------|----------|--------------|
| **Plain Bicep** | `template.bicep` | None (no params in template) | Simple templates with hardcoded config | `template.bicep` |
| **Bicep + JSON Params** | `template.bicep` | `--parameters-file params.json` | Templates with parameterized config | `template.bicep` + `params.json` |
| **Bicepparam File** | `template.bicepparam` | Built-in (via `using` statement) | Modern Bicep with self-contained params | `template.bicepparam` |

**Note:** You cannot use both `.bicepparam` file and `--parameters-file` together — choose one approach.

### Usage by Deployment Type

#### 1️⃣ Plain Bicep Template (No Parameters)

Use this when your template has no parameters or they're all hardcoded.

**Docker (Resource-Group Scope):**
```bash
docker run --rm \
  -v ~/.azure:/root/.azure \
  -v $(pwd):/workspace \
  mwhooo/driftguard \
  --bicep-file template.bicep \
  --resource-group myResourceGroup
```

**Native Binary (Resource-Group Scope):**
```bash
dotnet run -- --bicep-file template.bicep --resource-group myResourceGroup
```

**Docker (Subscription Scope):**
```bash
docker run --rm \
  -v ~/.azure:/root/.azure \
  -v $(pwd):/workspace \
  mwhooo/driftguard \
  --bicep-file template.bicep \
  --scope Subscription \
  --subscription <subscription-id> \
  --location westeurope
```

**Native Binary (Subscription Scope):**
```bash
dotnet run -- \
  --bicep-file template.bicep \
  --scope Subscription \
  --subscription <subscription-id> \
  --location westeurope
```

---

#### 2️⃣ Bicep + JSON Parameters File

Use this when you have separate `.bicep` and `.json` parameter files. This approach is useful for environment-specific parameters.

**Docker (Resource-Group Scope):**
```bash
docker run --rm \
  -v ~/.azure:/root/.azure \
  -v $(pwd):/workspace \
  mwhooo/driftguard \
  --bicep-file template.bicep \
  --parameters-file params.json \
  --resource-group myResourceGroup
```

**Native Binary (Resource-Group Scope):**
```bash
dotnet run -- \
  --bicep-file template.bicep \
  --parameters-file params.json \
  --resource-group myResourceGroup
```

**Docker (Subscription Scope with Different Environment):**
```bash
docker run --rm \
  -v ~/.azure:/root/.azure \
  -v $(pwd):/workspace \
  mwhooo/driftguard \
  --bicep-file infra.bicep \
  --parameters-file prod-params.json \
  --scope Subscription \
  --subscription <subscription-id> \
  --location westeurope
```

**Native Binary (Subscription Scope):**
```bash
dotnet run -- \
  --bicep-file infra.bicep \
  --parameters-file prod-params.json \
  --scope Subscription \
  --subscription <subscription-id> \
  --location westeurope
```

---

#### 3️⃣ Bicepparam File (Self-Contained)

Use this modern approach with `.bicepparam` files. Parameters are defined inline with a `using` reference to the template, making it self-documenting.

**Docker (Resource-Group Scope):**
```bash
docker run --rm \
  -v ~/.azure:/root/.azure \
  -v $(pwd):/workspace \
  mwhooo/driftguard \
  --bicep-file template.bicepparam \
  --resource-group myResourceGroup
```

**Native Binary (Resource-Group Scope):**
```bash
dotnet run -- --bicep-file template.bicepparam --resource-group myResourceGroup
```

**Docker (Subscription Scope):**
```bash
docker run --rm \
  -v ~/.azure:/root/.azure \
  -v $(pwd):/workspace \
  mwhooo/driftguard \
  --bicep-file infra.bicepparam \
  --scope Subscription \
  --subscription <subscription-id> \
  --location westeurope
```

**Native Binary (Subscription Scope):**
```bash
dotnet run -- \
  --bicep-file infra.bicepparam \
  --scope Subscription \
  --subscription <subscription-id> \
  --location westeurope
```

---

### Output Formats & Advanced Options

#### Generate HTML Reports
```bash
docker run --rm \
  -v ~/.azure:/root/.azure \
  -v $(pwd):/workspace \
  mwhooo/driftguard \
  --bicep-file template.bicep \
  --resource-group myResourceGroup \
  --output Html
```

#### JSON Output for CI/CD Automation
```bash
docker run --rm \
  -v ~/.azure:/root/.azure \
  -v $(pwd):/workspace \
  mwhooo/driftguard \
  --bicep-file template.bicep \
  --resource-group myResourceGroup \
  --output Json
```

**Native Binary:**
```bash
dotnet run -- --bicep-file template.bicep --resource-group myResourceGroup --output Json
```

#### Automatically Fix Detected Drift
```bash
docker run --rm \
  -v ~/.azure:/root/.azure \
  -v $(pwd):/workspace \
  mwhooo/driftguard \
  --bicep-file template.bicep \
  --resource-group myResourceGroup \
  --autofix
```

#### Use Custom Ignore Configuration
```bash
docker run --rm \
  -v ~/.azure:/root/.azure \
  -v $(pwd):/workspace \
  mwhooo/driftguard \
  --bicep-file template.bicep \
  --resource-group myResourceGroup \
  --ignore-config custom-ignore.json
```

**Native Binary:**
```bash
dotnet run -- \
  --bicep-file template.bicep \
  --resource-group myResourceGroup \
  --ignore-config custom-ignore.json
```

#### CI/CD Mode (JSON + Simple ASCII Output)
```bash
docker run --rm \
  -v ~/.azure:/root/.azure \
  -v $(pwd):/workspace \
  mwhooo/driftguard \
  --bicep-file template.bicep \
  --resource-group myResourceGroup \
  --output Json \
  --simple-output
```

> 📖 **Need to configure drift ignore rules?** See our comprehensive [Drift Ignore Configuration Guide](docs/DRIFT-IGNORE.md) with examples for common Azure services and best practices.

## 📋 Example Scenarios

### Scenario 1: Service Endpoint Drift
**Template Definition:**
```bicep
subnets: [
  {
    name: subnetName
    properties: {
      addressPrefix: '10.0.0.0/24'
    }
  }
]
```

**Manual Change in Portal:** Added Microsoft.Storage service endpoint

**Drift Detection Result:**
```
🔄 properties.subnets (Modified)
   Expected: ['myapp-subnet' (10.0.0.0/24)]
   Actual:   ['myapp-subnet' (10.0.0.0/24) [endpoints: Microsoft.Storage]]
```

### Scenario 2: Network Security Group Rule Drift
**Template Definition:**
```bicep
resource networkSecurityGroup 'Microsoft.Network/networkSecurityGroups@2023-04-01' = {
  name: 'drifttest-nsg'
  properties: {
    securityRules: [
      {
        name: 'AllowHTTP'
        priority: 100
        access: 'Allow'
        direction: 'Inbound'
        protocol: 'Tcp'
        sourcePortRange: '*'
        destinationPortRange: '80'
        sourceAddressPrefix: '*'
        destinationAddressPrefix: '*'
      }
      {
        name: 'AllowHTTPS'
        priority: 110
        access: 'Allow'
        direction: 'Inbound'
        protocol: 'Tcp'
        sourcePortRange: '*'
        destinationPortRange: '443'
        sourceAddressPrefix: '*'
        destinationAddressPrefix: '*'
      }
    ]
  }
}
```

**Manual Change in Portal:** Added SSH rule (AllowSSH) with priority 120

**Drift Detection Result:**
```
🔴 Microsoft.Network/networkSecurityGroups - drifttest-nsg
   Property Drifts: 1

   ➕ properties.securityRules.2 (Extra)
      Expected: not set
      Actual:  
        {
          "name": "AllowSSH",
          "properties": {
            "access": "Allow",
            "destinationAddressPrefix": "*",
            "destinationPortRange": "22",
            "direction": "Inbound",
            "priority": 120,
            "protocol": "Tcp",
            "sourceAddressPrefix": "*",
            "sourcePortRange": "*"
          }
        }
```

The detector identifies the exact array index (2) and shows the complete rule definition, making it easy to see what was added or modified.

**Scenario 2b: Modified Security Rule Property**

**Change in Portal:** Modify existing AllowHTTP rule's destination port from 80 to 8080

**Drift Detection Result:**
```
🔴 Microsoft.Network/networkSecurityGroups - drifttest-nsg
   Property Drifts: 1

   🔄 properties.securityRules.0.properties.destinationPortRange (Modified)
      Expected: "80"
      Actual:   "8080"
```

When properties within an existing rule are modified, DriftGuard shows the specific property path and the Expected/Actual values, making it clear exactly what changed.

### Scenario 3: Storage Account Tag Drift
**Template Definition:**
```bicep
resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  tags: {
    Environment: 'test'
    Application: 'drifttest'
    ResourceType: 'Infrastructure'
  }
}
```

**Manual Change:** Tags modified to `Environment: 'production'` and added `ManualTag: 'test'`

**Drift Detection Result:**
```
� Microsoft.Storage/storageAccounts - drifttestsay6kt676i
   Property Drifts: 3

   🔄 tags.Environment (Modified)
      Expected: "test"
      Actual:   "production"

   ➕ tags.ManualTag (Extra)
      Expected: not set
      Actual:   "test"

   ❌ tags.ResourceType (Missing)
      Expected: "Infrastructure"
      Actual:   not set
```

### Scenario 4: Missing Resource Detection
**Template Definition:**
```bicep
resource networkSecurityGroup 'Microsoft.Network/networkSecurityGroups@2023-04-01' = {
  name: '${applicationName}-nsg'
  // ... configuration
}
```

**Azure Reality:** The NSG resource defined in the template doesn't exist in Azure (was deleted or never deployed)

**Drift Detection Result:**
```
🔴 Microsoft.Network/networkSecurityGroups - drifttest-nsg
   Property Drifts: 1

   ❌ resource (Missing)
      Expected: "defined in template"
      Actual:   "missing in Azure"
```

When a resource is defined in the template but missing in Azure, DriftGuard flags it with ❌ and shows the mismatch between the template definition and Azure reality. This is how DriftGuard detects deleted or never-deployed resources.

### Scenario 5: Automatic Drift Remediation with --autofix
**Situation:** Multiple resources have drifted from template (missing resource, modified tags, extra tags)

**Command:**
```bash
dotnet run -- --bicep-file template.bicepparam --resource-group myRG --autofix
```

**Output:**
```
🔍 AZURE DRIFTGUARD - CONFIGURATION DRIFT DETECTION REPORT
============================================================
📅 Detection Time: 2026-02-12 20:27:27 UTC
📊 Summary: Configuration drift detected in 2 resource(s) with 4 property difference(s).

❌ Configuration drift detected in 2 resource(s):

🔴 Microsoft.Network/networkSecurityGroups - myapp-nsg
   Property Drifts: 1

   ❌ resource (Missing)
      Expected: "defined in template"
      Actual:   "missing in Azure"

🔴 Microsoft.Storage/storageAccounts - mystorageacct
   Property Drifts: 2

   🔄 tags.Environment (Modified)
      Expected: "test"
      Actual:   "production"

   ➕ tags.ManualTag (Extra)
      Expected: not set
      Actual:   "added-manually"

============================================================
❌ Configuration drift detected!
🔧 Attempting to fix drift by deploying template...
🚀 Deploying Bicep template to resource group: myRG
📄 Template file: /path/to/template.bicepparam
✅ Deployment completed successfully!
✅ Drift has been automatically fixed!
📦 Deployment Name: drift-autofix-20260212-202727
```

When drift is detected and `--autofix` is used, DriftGuard shows the full drift report first, then automatically deploys the template with a timestamped deployment name for tracking. All drifted resources are restored to match the template definition.

### Scenario 6: Conditional Deployment Support
**Template Definition:**
```bicep
var deployKeyVault = false
resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = if (deployKeyVault) {
  // ... configuration
}
```

**Result:** When `deployKeyVault = false`, the detector **excludes** the Key Vault from drift analysis, preventing false positives.

## 🔗 External Module Support

### Azure Container Registry Integration
The tool provides support for external Bicep modules from Azure Container Registry and Azure Verified Modules (AVM):

#### Supported Module Syntax
```bicep
// Azure Container Registry modules
module myModule 'br:myregistry.azurecr.io/bicep/storage/account:v1.0.0' = { ... }

// Public Azure Verified Modules
module avm 'br/public:avm/res/storage/storage-account:0.9.1' = { ... }

// Private registry modules  
module private 'br:private.azurecr.io/modules/networking/vnet:latest' = { ... }
```

#### Key Features
- **Automatic Resolution**: External modules resolved via Azure what-if analysis
- **No Manual Downloads**: Modules processed automatically without local caching
- **Complex Dependencies**: Handles module chains and nested external references
- **Mixed Templates**: Supports templates combining external modules with direct resources

#### AVM Noise Suppression
Azure Verified Modules often set compliance properties that differ from Azure defaults, creating false positive drift alerts. The tool includes comprehensive ignore patterns:

```json
{
  "resourceType": "Microsoft.Storage/storageAccounts", 
  "reason": "AVM modules set explicit compliance properties",
  "ignoredProperties": [
    "properties.customDomain.useSubDomainName",
    "properties.customDomain"
  ]
}
```

**Note:** Use the included `drift-ignore.json` configuration file with `--ignore-config drift-ignore.json` to suppress common AVM noise patterns

### Example: Mixed External and Direct Resources
```bicep
// External AVM storage module
module storageModule 'br:myregistry.azurecr.io/bicep/storage/storageaccount:v1.1.0' = {
  params: {
    config: {
      name: 'mystorageaccount'
      location: 'uksouth'
      sku: 'Standard_LRS'
    }
  }
}

// Direct Azure resource
resource networkSecurityGroup 'Microsoft.Network/networkSecurityGroups@2023-11-01' = {
  name: 'myapp-nsg'
  location: 'uksouth'
  properties: {
    securityRules: [
      {
        name: 'allow-http'
        properties: {
          protocol: 'Tcp'
          sourcePortRange: '*'
          destinationPortRange: '80'
          access: 'Allow'
          direction: 'Inbound'
          priority: 1000
        }
      }
    ]
  }
}
```

**Drift Detection with Noise Filtering:**
```bash
🔇 Ignoring drift: Microsoft.Storage/storageAccounts - properties.customDomain
🔇 Ignoring drift: Microsoft.Storage/storageAccounts/blobServices - properties.deleteRetentionPolicy
✅ No configuration drift detected after filtering 4 ignored drift(s)
```

## 🔇 Drift Ignore Configuration

The drift detection system includes an ignore mechanism to suppress noise caused by Azure platform behaviors beyond your control.

### Purpose
The ignore functionality is specifically designed to filter out "noise" from:
- **Azure Resource Manager (ARM)** automatically adding platform-managed properties
- **Azure Verified Modules (AVM)** modifying resources during or after deployment
- **Azure platform services** updating timestamps, provisioning states, capacity metrics, or internal references
- **Tier-specific behaviors** where Basic/Free tiers don't support certain properties that Premium tiers do
- **Platform-managed state** that occurs outside of your Bicep template configuration and control

### When to Use Ignore Patterns
✅ **Use for platform behaviors you cannot control:**
- Azure-managed timestamps (`lastModified`, `createdOn`, etc.)
- Provisioning states that change automatically
- Service tier limitations (Basic Service Bus not supporting advanced properties)
- Azure policy enforcement adding required tags/properties
- Auto-scaling metrics and capacity values

❌ **Don't ignore legitimate configuration drift:**
- Manual changes made via Azure Portal
- Security configuration modifications
- Resource property changes that should be in your template
- Actual configuration drift that indicates compliance issues

### Configuration Format
Ignore patterns are defined in JSON configuration files (default: `drift-ignore.json`):

```json
{
  "ignorePatterns": {
    "description": "Suppress Azure platform noise",
    "resources": [
      {
        "resourceType": "Microsoft.ServiceBus/namespaces/queues",
        "reason": "Service Bus Basic tier doesn't support these properties - Azure platform behavior",
        "ignoredProperties": [
          "properties.autoDeleteOnIdle",
          "properties.defaultMessageTimeToLive",
          "properties.duplicateDetectionHistoryTimeWindow",
          "properties.maxMessageSizeInKilobytes"
        ]
      }
    ],
    "globalPatterns": [
      {
        "propertyPattern": "properties.provisioningState", 
        "reason": "Azure-managed provisioning state - not user configurable"
      },
      {
        "propertyPattern": "properties.*Time*",
        "reason": "Ignore all Azure-managed timestamp properties"
      },
      {
        "propertyPattern": "properties.*time*",
        "reason": "Ignore all Azure-managed lowercase timestamp properties"
      }
    ]
  }
}
```

### Command Line Usage
```bash
# Use default ignore config (drift-ignore.json in current directory)
dotnet run -- --bicep-file template.bicep --resource-group myRG

# Use custom ignore configuration file
dotnet run -- --bicep-file template.bicep --resource-group myRG --ignore-config prod-ignore.json

# Use ignore config from different directory
dotnet run -- --bicep-file template.bicep --resource-group myRG --ignore-config configs/ignore.json
```

### Pattern Matching Rules
- **Exact Match**: `"properties.autoDeleteOnIdle"` matches exactly that property path
- **Wildcards**: `"properties.*Time*"` matches any property containing "Time" (case-sensitive)
- **Resource Types**: Support wildcards like `"Microsoft.ServiceBus/*"` for all Service Bus resource types
- **Global vs Resource-Specific**: Global patterns apply to all resources, resource-specific patterns only apply to matching resource types

**Wildcard Support Tested**: Wildcard patterns are fully functional. Example: the pattern `"tags.*"` successfully suppressed 2 tag drifts in testing, with the output confirming `"No configuration drift detected after filtering 2 ignored drift(s)."` This validates both the wildcard syntax and the filtering mechanism work as documented.

### Real-World Example
Before implementing ignore patterns:
```
❌ Configuration drift detected in 13 resource(s) with 15 property difference(s).

🔴 Microsoft.ServiceBus/namespaces/queues - myqueue
   ❌ properties.autoDeleteOnIdle (Missing)
      Expected: "PT10675199DT2H48M5.4775807S"
      Actual:   null
   ❌ properties.defaultMessageTimeToLive (Missing) 
      Expected: "P14D"
      Actual:   null
   ❌ properties.maxMessageSizeInKilobytes (Missing)
      Expected: 1024
      Actual:   null
```

After implementing ignore patterns:
```
✅ No configuration drift detected!
📋 Filtered 12 ignored property differences
🎯 Focus on legitimate drift - noise suppressed
```

## 🏗️ Advanced Features

### Azure What-If Integration
The drift detector leverages Azure's native what-if functionality for drift detection:

```bash
# Behind the scenes, the tool runs:
az deployment group what-if --resource-group dev --template-file samples/main-template.bicep --parameters samples/main-template.bicepparam
```

This provides:
- ✅ **Azure-Native Comparison**: Uses Azure's deployment engine for drift detection
- ✅ **Intelligent Noise Suppression**: Filters Azure platform behaviors with configurable ignore patterns
- ✅ **Comprehensive Analysis**: Detects most configuration changes across resource types
- ✅ **Clean Output**: Verbose what-if output suppressed, showing only formatted drift results

## 🎨 Sample Output

### Console Output
```
🔍 AZURE CONFIGURATION DRIFT DETECTION REPORT
============================================================
📅 Detection Time: 2025-11-13 18:41:37 UTC
📊 Summary: Configuration drift detected in 2 resource(s) with 4 property difference(s).

❌ Configuration drift detected in 2 resource(s):

🔴 Microsoft.Storage/storageAccounts - drifttestsay6kt676i
   Resource ID:
   Property Drifts: 3

   🔄 tags.environment (Modified)
      Expected: "test"
      Actual:   "production"

   ❓ tags.manualTag (Added)
      Expected: "not set"
      Actual:   "drift"

   ❌ tags.Application (Missing)
      Expected: "drifttest"
      Actual:   "removed"

🔴 Microsoft.Network/networkSecurityGroups - drifttest-nsg
   Resource ID:
   Property Drifts: 1

   🔄 properties.securityRules (Modified)
      Expected: "configured in template"
      Actual:   "differs in Azure (complex object/array)"
```

### JSON Output (for automation)
```json
{
  "HasDrift": true,
  "ResourceDrifts": [
    {
      "ResourceType": "Microsoft.Network/virtualNetworks",
      "ResourceName": "myapp-vnet",
      "ResourceId": "/subscriptions/.../myapp-vnet",
      "PropertyDrifts": [
        {
          "PropertyPath": "properties.subnets",
          "ExpectedValue": "['myapp-subnet' (10.0.0.0/24)]",
          "ActualValue": "['myapp-subnet' (10.0.0.0/24) [endpoints: Microsoft.Storage]]",
          "Type": "Modified"
        }
      ]
    }
  ],
  "DetectedAt": "2025-11-11T17:59:42.123Z",
  "Summary": "Configuration drift detected in 2 resource(s) with 2 property difference(s)."
}
```

## �️ Security & Quality Assurance

### GitHub Advanced Security
This project uses GitHub's security features to ensure code quality and security:

- **CodeQL Analysis**: Automated security vulnerability scanning
- **Dependency Scanning**: Monitors for vulnerable dependencies
- **Secret Scanning**: Prevents accidental credential commits

**Note**: For private repositories, GitHub Advanced Security requires enabling through repository settings. See [Security Setup Guide](docs/SECURITY-SETUP.md) for detailed instructions.

### Automated CI/CD Pipeline
Every push triggers comprehensive validation:
- ✅ Cross-platform builds (Ubuntu, macOS, Windows)
- ✅ Code quality and formatting checks
- ✅ Bicep template validation
- ✅ Security analysis with CodeQL
- ✅ Automated dependency updates

## 🔧 Command Line Options

```
Usage: dotnet run -- [options]

Options:
  --bicep-file <path>        Path to the Bicep template file (required)
  --resource-group <name>    Azure resource group name (required) 
  --output <format>          Output format: Console (default), Json, Html, Markdown
  --autofix                  Automatically deploy template to fix detected drift
  --ignore-config <path>     Path to drift ignore configuration file (default: drift-ignore.json)
  --show-filtered            Show detailed reasons for filtered drift (audit mode)
  --simple-output            Use simple ASCII characters for CI/CD compatibility
  --help                     Show help information
```

## 🏛️ Architecture

```
DriftGuard/
├── Core/
│   └── DriftDetector.cs          # Main orchestration logic with ignore integration
├── Models/
│   └── DriftModels.cs             # Data structures for drift results and ignore config
├── Services/
│   ├── AzureCliService.cs         # Azure CLI integration & deployments
│   ├── BicepService.cs            # Bicep compilation & what-if integration
│   ├── WhatIfJsonService.cs       # JSON-based what-if parsing (reliable)
│   ├── ComparisonService.cs       # Legacy text-based parsing (deprecated)
│   ├── DriftIgnoreService.cs      # Ignore pattern matching and drift filtering
│   └── ReportingService.cs        # Multi-format output generation
├── bicep-modules/                 # Modular Bicep templates
│   ├── storage-account.bicep      # Storage with exported types
│   ├── virtual-network.bicep      # VNet with exported types
│   ├── network-security-group.bicep
│   ├── app-service-plan.bicep
│   ├── log-analytics-workspace.bicep
│   └── key-vault.bicep
├── docs/                          # Documentation
│   ├── DRIFT-IGNORE.md           # Comprehensive drift ignore configuration guide
│   ├── SECURITY-SETUP.md         # GitHub Advanced Security setup
│   └── BICEP-BUILD.md            # Bicep module development guide
├── drift-ignore.json              # Default ignore configuration for Azure platform noise
├── samples/                       # Example Bicep templates and parameters
│   ├── main-template.bicep        # Main template importing module types
│   └── main-template.bicepparam   # Parameter configuration
└── Program.cs                     # CLI interface & dependency injection
```

### Key Components

- **BicepService**: Integrates Azure what-if for authoritative drift detection, handles bicepparam files
- **AzureCliService**: Queries live Azure resources and executes deployments with proper error handling
- **WhatIfJsonService**: Parses what-if JSON output for reliable drift detection with ARM expression filtering
- **ComparisonService**: Legacy text-based parsing (deprecated, kept for reference)
- **DriftIgnoreService**: Pattern matching engine for filtering Azure platform noise
- **ReportingService**: Generates clean, actionable drift reports in multiple formats
- **Bicep Modules**: Type-safe, reusable infrastructure components with exported configuration types

## 🎯 Use Cases

### DevOps & CI/CD Integration
```yaml
# Azure DevOps Pipeline
- task: DotNetCoreCLI@2
  displayName: 'Detect Configuration Drift'
  inputs:
    command: 'run'
    arguments: '-- --bicep-file $(Build.SourcesDirectory)/infrastructure/main.bicep --resource-group $(ResourceGroupName) --output Json'
    
- task: PublishTestResults@2
  condition: always()
  inputs:
    testResultsFormat: 'JUnit'
    testResultsFiles: 'drift-report-*.json'
```

### Compliance Monitoring
- **Daily drift scans** for production environments
- **Compliance reporting** for audit requirements
- **Change management** validation before deployments

### Development Workflow
- **Pre-deployment validation** to ensure clean state
- **Post-deployment verification** to confirm successful deployment
- **Environment consistency** checks across dev/staging/production

## 🔍 Technical Details

### Supported Azure Resource Types
- ✅ **Networking**: Virtual Networks, Subnets, Network Security Groups, Application Gateways
- ✅ **Compute**: App Service Plans, Function Apps, Virtual Machines  
- ✅ **Storage**: Storage Accounts, Blob containers, File shares
- ✅ **Messaging**: Service Bus (Namespaces, Queues, Topics, Subscriptions)
- ✅ **Security**: Key Vaults, Managed Identities
- ✅ **Data**: SQL Databases, Cosmos DB, Redis Cache
- ✅ **Any other Azure resource type** (generic support)

### Drift Detection Capabilities
- **Azure What-If Based**: Uses Azure's native deployment engine for authoritative drift detection
- **Property-level granularity**: Identifies specific changed properties
- **Complex object support**: Handles arrays, nested objects with human-friendly messages
- **Tag drift detection**: Detects added, removed, and modified tags
- **Configurable filtering**: Reduce noise with ignore patterns for Azure platform behaviors

### Performance Characteristics
- **Fast what-if execution**: Leverages Azure's optimized what-if engine
- **Clean output**: Suppressed verbose Azure output for better UX
- **Memory efficient**: Streaming text processing for what-if results
- **Fast execution**: Typical runs complete in 10-30 seconds

## 🤝 Contributing

This project demonstrates advanced techniques for:
- Azure resource management automation
- Complex JSON schema comparison
- Infrastructure as Code validation
- Multi-format reporting systems

## 📄 Exit Codes

- `0`: No configuration drift detected
- `1`: Configuration drift detected or error occurred

Suitable for CI/CD pipelines and infrastructure validation workflows.

## 📝 Changelog

### v3.2.0 (2025-11-15) - Comprehensive Drift Ignore System 🔇
**Noise Reduction**

#### 🔇 **Drift Ignore System**
- ✨ **Configurable Ignore Patterns**: JSON-based configuration for suppressing Azure platform noise
- 🎯 **Purpose-Built for AVM/Platform Behaviors**: Specifically designed to filter out Azure Resource Manager and Azure Verified Module modifications beyond user control
- 📋 **Resource-Specific Rules**: Target specific resource types with custom ignore patterns
- 🌐 **Global Pattern Support**: Wildcards and pattern matching for broad timestamp/state filtering
- 🖥️ **Command Line Integration**: `--ignore-config` parameter for flexible configuration file paths
- 📊 **Filtering Statistics**: Reports showing how many false positives were suppressed

#### 🚀 **Enhanced Azure Resource Support**
- ✨ **Service Bus Integration**: Complete support for Namespaces, Queues, Topics, and Subscriptions
- 🔧 **Tier-Aware Configuration**: Conditional properties based on Basic vs Standard/Premium Service Bus tiers
- 🌐 **Application Gateway Module**: Full Azure Application Gateway Bicep module with exported types
- 🗃️ **Azure SQL Database**: Complete SQL Database support with server and database configuration

#### ⚡ **Improved Drift Detection Accuracy**
- 🐛 **Fixed What-If Symbol Interpretation**: Correctly handle -, +, ~, = symbols in Azure what-if output
- 🔍 **Better Child Resource Handling**: Enhanced detection for nested resources and complex object drift
- 📈 **Real-World Impact**: Reduced false positives from 13 to 1 in production scenarios

#### 🛠️ **Technical Architecture**
- 🆕 **DriftIgnoreService**: New service for pattern matching and drift filtering
- 🔧 **Enhanced ComparisonService**: Integrated ignore filtering with what-if parsing
- 📚 **Updated Documentation**: Comprehensive ignore configuration guide and best practices

### v3.0.0 (2025-11-13) - Major Architecture Overhaul 🚀
**Breaking Changes - Major Release**

#### 🎯 Azure What-If Integration
- ✨ **Authoritative Drift Detection**: Migrated from manual JSON comparison to Azure's native `az deployment group what-if` command
- ✅ **Zero False Positives**: Eliminated all false positives from ARM expression vs resolved value comparisons
- 🎨 **Clean Output**: Suppressed verbose Azure what-if output, showing only formatted drift results
- 📊 **Better Complex Object Handling**: Human-friendly messages for array/object drift instead of raw JSON snippets

#### 🏗️ Type-Safe Bicep Architecture
- ✨ **User-Defined Types (UDTs)**: Full Bicep type system with `@export()` decorators on all modules
- 📦 **Single Config Objects**: Each module accepts one config parameter instead of multiple individual params
- 🔧 **Modular Structure**: Separated all resources into `bicep-modules/` directory with exported types
- 🎯 **DRY Principle**: Types defined once in modules, imported in main template - no duplication
- ✅ **Compile-Time Validation**: Full IntelliSense and type checking for all Bicep files

#### 📁 Bicepparam Support
- ✨ **Native .bicepparam Files**: Full support for Bicep parameter files with `using` statements
- 🔍 **Automatic Reference Resolution**: Extracts referenced template from bicepparam files
- ⚡ **Streamlined Parameters**: Clean parameter management separate from template logic

#### 🎨 Enhanced User Experience
- 📊 **Improved Drift Messages**: Clear "configured in template" vs "differs in Azure (complex object/array)" for complex changes
- 🧹 **Removed Duplicate Code**: Eliminated redundant comparison logic in favor of what-if parsing
- ⚡ **Faster Execution**: What-if-based approach is faster than manual JSON traversal
- 🎯 **Accurate Tag Detection**: Precise detection of tag additions, removals, and modifications

#### 🔧 Technical Improvements
- 🏗️ **Refactored BicepService**: Now integrates what-if instead of building ARM templates
- 📝 **Enhanced ComparisonService**: Parses what-if text output into structured drift results
- 🧪 **Process Management**: Fixed deployment deadlock issues with proper stdout/stderr handling
- 🗂️ **Module Organization**: Clean separation of concerns with typed module interfaces

#### 📚 Module Updates
- Storage Account: Exported `StorageAccountConfig`, `StorageAccountSku`, `TlsVersion`, etc.
- Virtual Network: Exported `VnetConfig`, `Subnet`, `EnableState`
- NSG: Exported `NsgConfig`, `SecurityRule`, `AccessType`, `TrafficDirection`, `NetworkProtocol`
- App Service Plan: Exported `AppServicePlanConfig`, `AppServicePlanSku`
- Log Analytics: Exported `LogAnalyticsConfig`, `LogAnalyticsSku`
- Key Vault: Exported `KeyVaultConfig`, `KeyVaultSku`, `PublicAccess`

#### 🗑️ Removed
- ❌ Removed `types.bicep` - types now live with their modules (DRY principle)
- ❌ Removed manual JSON comparison logic - replaced with what-if parsing
- ❌ Removed `--simple-output` flag - no longer needed with clean what-if output

### v2.1.0 (2025-11-11) - Major Accuracy Improvements
- ✨ **Enhanced Comparison Logic**: Specialized handlers for NSG security rules, subnet arrays, and Log Analytics workspaces
- 🐛 **False Positive Elimination**: Intelligent filtering of Azure-generated metadata (provisioningState, etag, id, etc.)
- 🎨 **Improved JSON Formatting**: Human-readable console output with proper indentation and formatting
- 🔍 **Smart Array Detection**: Automatic detection and specialized comparison for different array types
- ⚡ **Performance Optimizations**: More efficient comparison algorithms for complex nested objects
- 📊 **Enhanced Reporting**: Better formatting for console output with base JSON formatting
- 🧪 **Comprehensive Testing**: Validated with real Azure resources across multiple resource types

### Previous Versions
- v2.0.0 - Initial stable release with multi-resource support
- v1.x - Beta versions with basic drift detection capabilities

---

## 📄 License

This project is licensed under the **Creative Commons Attribution-NonCommercial-ShareAlike 4.0 International License**.

**What this means:**
- ✅ **Free to use** for personal, educational, and internal business purposes
- ✅ **Free to modify** and distribute modifications under the same license
- ❌ **Cannot be sold** or used for commercial redistribution
- 📝 **Attribution required** - please credit the original project

**For businesses:** You can use this tool internally within your organization for drift detection without any licensing fees. You just cannot package and sell it as a commercial product.

See the [LICENSE](LICENSE) file for full details or visit [Creative Commons](https://creativecommons.org/licenses/by-nc-sa/4.0/) for more information.

---

**Built with ❤️ for the Azure DevOps community**