# IaC Benchmark - Bicep vs Terraform vs Pulumi

This directory contains Infrastructure as Code configurations in three different tools (Bicep, Terraform, and Pulumi) for benchmarking deployment speeds.

## Overview

The benchmark deploys the same Azure resources using each IaC tool and measures:
- **Deployment Time**: Time to create all resources
- **Destruction Time**: Time to remove all resources
- **Consistency**: Variation across multiple iterations

### Resources Deployed

| Resource | Description |
|----------|-------------|
| Virtual Network | VNet with 3 subnets |
| Network Security Group | NSG with HTTP, HTTPS, and deny rules |
| Storage Account | StorageV2 with standard settings |
| App Service Plan | Free tier Windows plan |
| Log Analytics Workspace | PerGB2018 SKU with 30-day retention |
| Key Vault (optional) | Standard SKU with RBAC |
| Service Bus (optional) | Basic tier with queues |

## Prerequisites

### Required Tools

```bash
# Azure CLI
curl -sL https://aka.ms/InstallAzureCLIDeb | sudo bash
az login

# Terraform
wget -O- https://apt.releases.hashicorp.com/gpg | gpg --dearmor | sudo tee /usr/share/keyrings/hashicorp-archive-keyring.gpg
echo "deb [signed-by=/usr/share/keyrings/hashicorp-archive-keyring.gpg] https://apt.releases.hashicorp.com $(lsb_release -cs) main" | sudo tee /etc/apt/sources.list.d/hashicorp.list
sudo apt update && sudo apt install terraform

# Pulumi
curl -fsSL https://get.pulumi.com | sh
# Add to PATH: export PATH=$PATH:$HOME/.pulumi/bin

# jq (for JSON processing)
sudo apt install jq

# bc (for calculations)
sudo apt install bc
```

### Verify Installation

```bash
az --version
terraform version
pulumi version
jq --version
```

## Directory Structure

```
benchmark/
├── README.md                     # This file
├── run-benchmark.sh              # Main benchmark script
├── results/                      # Benchmark results (auto-created)
│   ├── benchmark_YYYYMMDD_HHMMSS.json
│   └── benchmark_report_YYYYMMDD_HHMMSS.html
├── terraform/
│   ├── providers.tf              # Provider configuration
│   ├── variables.tf              # Variable definitions
│   ├── main.tf                   # Resource definitions
│   ├── outputs.tf                # Output definitions
│   └── terraform.tfvars.example  # Example variables file
└── pulumi/
    ├── Pulumi.yaml               # Project configuration
    ├── Pulumi.benchmark.yaml     # Stack configuration
    ├── __main__.py               # Python infrastructure code
    └── requirements.txt          # Python dependencies
```

## Quick Start

### 1. Create a Resource Group

```bash
az group create --name driftguard-benchmark-rg --location westeurope
```

### 2. Run the Benchmark

```bash
cd benchmark
chmod +x run-benchmark.sh

# Run all tools with 3 iterations (default)
./run-benchmark.sh -g driftguard-benchmark-rg

# Run specific tools
./run-benchmark.sh -g driftguard-benchmark-rg -t bicep,terraform

# Run with more iterations
./run-benchmark.sh -g driftguard-benchmark-rg -i 5

# Skip resource destruction (for debugging)
./run-benchmark.sh -g driftguard-benchmark-rg --skip-destroy
```

### 3. View Results

After the benchmark completes, open the HTML report:

```bash
# Linux
xdg-open results/benchmark_report_*.html

# macOS
open results/benchmark_report_*.html
```

## Command Line Options

| Option | Description | Default |
|--------|-------------|---------|
| `-g, --resource-group` | Azure resource group name | Required |
| `-l, --location` | Azure region | westeurope |
| `-i, --iterations` | Number of test iterations | 3 |
| `-t, --tools` | Tools to benchmark (bicep,terraform,pulumi,all) | all |
| `--skip-destroy` | Don't destroy resources after deployment | false |
| `-h, --help` | Show help message | - |

## Manual Deployment

### Bicep

```bash
cd ../samples

# Deploy
az deployment group create \
  --resource-group driftguard-benchmark-rg \
  --template-file main-template.bicep \
  --parameters main-template.bicepparam

# Destroy
az group delete --name driftguard-benchmark-rg --yes
```

### Terraform

```bash
cd terraform

# Initialize
terraform init

# Create tfvars
cp terraform.tfvars.example terraform.tfvars
# Edit terraform.tfvars with your values

# Deploy
terraform apply -var-file=terraform.tfvars

# Destroy
terraform destroy -var-file=terraform.tfvars
```

### Pulumi

```bash
cd pulumi

# Setup Python environment
python3 -m venv venv
source venv/bin/activate
pip install -r requirements.txt

# Login (use local state for testing)
pulumi login --local

# Create/select stack
pulumi stack init benchmark
# or
pulumi stack select benchmark

# Configure
pulumi config set resourceGroupName driftguard-benchmark-rg
pulumi config set azure-native:location westeurope

# Deploy
pulumi up

# Destroy
pulumi destroy

# Cleanup
deactivate
```

## Understanding the Results

### JSON Output

```json
{
  "benchmark_info": {
    "timestamp": "20260122_143022",
    "resource_group": "driftguard-benchmark-rg",
    "location": "westeurope",
    "iterations": 3
  },
  "results": {
    "bicep": [
      {"iteration": 1, "operation": "deploy", "duration_seconds": 45.23, "status": "success"},
      {"iteration": 1, "operation": "destroy", "duration_seconds": 32.15, "status": "success"}
    ],
    "terraform": [...],
    "pulumi": [...]
  },
  "summary": {
    "bicep_deploy": {"min": 44.12, "max": 47.33, "avg": 45.56, "successful_runs": 3},
    "terraform_deploy": {"min": 52.45, "max": 58.21, "avg": 55.12, "successful_runs": 3},
    "pulumi_deploy": {"min": 48.33, "max": 51.22, "avg": 49.85, "successful_runs": 3}
  }
}
```

### HTML Report

The HTML report includes:
- 📊 Visual comparison charts
- 🏆 Winner highlighting
- 📋 Detailed per-iteration results
- 📈 Min/Max/Average statistics

## Factors Affecting Results

### Why Times May Vary

1. **Azure API Latency**: Network conditions to Azure region
2. **Resource Dependencies**: Some resources depend on others
3. **State Management**: Terraform and Pulumi maintain state
4. **Parallelization**: Each tool handles parallel resource creation differently
5. **Provider Versions**: Different SDK versions may have performance differences

### Tips for Accurate Benchmarks

1. Run at least 5 iterations for statistical significance
2. Run during off-peak hours for consistent API latency
3. Use the same Azure region for all tests
4. Ensure no other deployments to the same subscription
5. Run multiple times on different days to account for Azure variability

## Comparison Matrix

| Feature | Bicep | Terraform | Pulumi |
|---------|-------|-----------|--------|
| Language | DSL | HCL | Python/TS/Go/C# |
| State Management | Azure (ARM) | Local/Remote | Pulumi Cloud/Local |
| Azure-Native | ✅ Yes | Provider-based | Provider-based |
| Learning Curve | Low | Medium | Low (if you know the language) |
| Multi-Cloud | ❌ No | ✅ Yes | ✅ Yes |
| Preview/What-If | ✅ Yes | ✅ Yes | ✅ Yes |
| Cost | Free | Free (OSS) | Free (OSS) |

## Troubleshooting

### Common Issues

**Terraform: "Provider not found"**
```bash
cd terraform
terraform init
```

**Pulumi: "No stack selected"**
```bash
pulumi stack init benchmark
# or
pulumi stack select benchmark
```

**Azure: "AuthorizationFailed"**
```bash
az login
az account set --subscription <subscription-id>
```

**Pulumi: "Missing configuration"**
```bash
pulumi config set resourceGroupName <your-rg>
pulumi config set azure-native:location westeurope
```

## Contributing

To add new resources to the benchmark:

1. Add to Bicep module in `../samples/bicep-modules/`
2. Add equivalent Terraform resource in `terraform/main.tf`
3. Add equivalent Pulumi resource in `pulumi/__main__.py`
4. Update deployment flags in all configurations
