#!/bin/bash
# =============================================================================
# IaC Benchmark Runner - Multiple Iterations with Full Metrics
# =============================================================================
set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
RESULTS_DIR="$SCRIPT_DIR/results"
TIMESTAMP=$(date +%Y%m%d_%H%M%S)

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
CYAN='\033[0;36m'
NC='\033[0m'

# Configuration
ITERATIONS=${1:-3}
BICEP_RG="driftguard-benchmark-bicep-rg"
TERRAFORM_RG="driftguard-benchmark-terraform-rg"
PULUMI_RG="driftguard-benchmark-pulumi-rg"

# Results arrays
declare -a BICEP_DEPLOY_TIMES
declare -a BICEP_DESTROY_TIMES
declare -a TERRAFORM_DEPLOY_TIMES
declare -a TERRAFORM_DESTROY_TIMES
declare -a PULUMI_DEPLOY_TIMES
declare -a PULUMI_DESTROY_TIMES

mkdir -p "$RESULTS_DIR"

echo -e "${CYAN}╔═══════════════════════════════════════════════════════════════╗${NC}"
echo -e "${CYAN}║       IaC Benchmark - $ITERATIONS Iterations per Tool              ║${NC}"
echo -e "${CYAN}╚═══════════════════════════════════════════════════════════════╝${NC}"

# Helper: Clean resource group
clean_rg() {
    local rg=$1
    az resource list --resource-group "$rg" --query "[].id" -o tsv 2>/dev/null | xargs -r -I {} az resource delete --ids {} 2>/dev/null || true
    sleep 5
}

# Benchmark Bicep
benchmark_bicep() {
    local iteration=$1
    echo -e "\n${BLUE}[Bicep] Iteration $iteration - Deploying...${NC}"
    
    local start=$(date +%s.%N)
    az deployment group create \
        --resource-group "$BICEP_RG" \
        --template-file "$SCRIPT_DIR/../samples/main-template.bicep" \
        --parameters "$SCRIPT_DIR/../samples/main-template.bicepparam" \
        --output none 2>&1
    local end=$(date +%s.%N)
    local deploy_time=$(echo "$end - $start" | bc)
    BICEP_DEPLOY_TIMES+=("$deploy_time")
    echo -e "${GREEN}  ✓ Deploy: ${deploy_time}s${NC}"
    
    echo -e "${BLUE}[Bicep] Iteration $iteration - Destroying...${NC}"
    start=$(date +%s.%N)
    clean_rg "$BICEP_RG"
    end=$(date +%s.%N)
    local destroy_time=$(echo "$end - $start" | bc)
    BICEP_DESTROY_TIMES+=("$destroy_time")
    echo -e "${GREEN}  ✓ Destroy: ${destroy_time}s${NC}"
}

# Benchmark Terraform
benchmark_terraform() {
    local iteration=$1
    echo -e "\n${BLUE}[Terraform] Iteration $iteration - Deploying...${NC}"
    
    cd "$SCRIPT_DIR/terraform"
    
    local start=$(date +%s.%N)
    terraform apply -auto-approve -var="resource_group_name=$TERRAFORM_RG" > /dev/null 2>&1
    local end=$(date +%s.%N)
    local deploy_time=$(echo "$end - $start" | bc)
    TERRAFORM_DEPLOY_TIMES+=("$deploy_time")
    echo -e "${GREEN}  ✓ Deploy: ${deploy_time}s${NC}"
    
    echo -e "${BLUE}[Terraform] Iteration $iteration - Destroying...${NC}"
    start=$(date +%s.%N)
    terraform destroy -auto-approve -var="resource_group_name=$TERRAFORM_RG" > /dev/null 2>&1
    end=$(date +%s.%N)
    local destroy_time=$(echo "$end - $start" | bc)
    TERRAFORM_DESTROY_TIMES+=("$destroy_time")
    echo -e "${GREEN}  ✓ Destroy: ${destroy_time}s${NC}"
    
    cd "$SCRIPT_DIR"
}

# Benchmark Pulumi
benchmark_pulumi() {
    local iteration=$1
    echo -e "\n${BLUE}[Pulumi] Iteration $iteration - Deploying...${NC}"
    
    cd "$SCRIPT_DIR/pulumi"
    source venv/bin/activate
    export PATH="$PATH:$HOME/.pulumi/bin"
    export PULUMI_CONFIG_PASSPHRASE=""
    
    local start=$(date +%s.%N)
    pulumi up --yes --skip-preview > /dev/null 2>&1
    local end=$(date +%s.%N)
    local deploy_time=$(echo "$end - $start" | bc)
    PULUMI_DEPLOY_TIMES+=("$deploy_time")
    echo -e "${GREEN}  ✓ Deploy: ${deploy_time}s${NC}"
    
    echo -e "${BLUE}[Pulumi] Iteration $iteration - Destroying...${NC}"
    start=$(date +%s.%N)
    pulumi destroy --yes --skip-preview > /dev/null 2>&1
    end=$(date +%s.%N)
    local destroy_time=$(echo "$end - $start" | bc)
    PULUMI_DESTROY_TIMES+=("$destroy_time")
    echo -e "${GREEN}  ✓ Destroy: ${destroy_time}s${NC}"
    
    deactivate
    cd "$SCRIPT_DIR"
}

# Calculate stats
calc_stats() {
    local -n arr=$1
    local sum=0
    local min=999999
    local max=0
    
    for val in "${arr[@]}"; do
        sum=$(echo "$sum + $val" | bc)
        if (( $(echo "$val < $min" | bc -l) )); then min=$val; fi
        if (( $(echo "$val > $max" | bc -l) )); then max=$val; fi
    done
    
    local avg=$(echo "scale=2; $sum / ${#arr[@]}" | bc)
    echo "$min $max $avg"
}

# Run benchmarks
for ((i=1; i<=ITERATIONS; i++)); do
    echo -e "\n${YELLOW}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
    echo -e "${YELLOW}                    ITERATION $i of $ITERATIONS                     ${NC}"
    echo -e "${YELLOW}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
    
    benchmark_bicep "$i"
    benchmark_terraform "$i"
    benchmark_pulumi "$i"
done

# Calculate statistics
read bicep_deploy_min bicep_deploy_max bicep_deploy_avg <<< $(calc_stats BICEP_DEPLOY_TIMES)
read bicep_destroy_min bicep_destroy_max bicep_destroy_avg <<< $(calc_stats BICEP_DESTROY_TIMES)
read tf_deploy_min tf_deploy_max tf_deploy_avg <<< $(calc_stats TERRAFORM_DEPLOY_TIMES)
read tf_destroy_min tf_destroy_max tf_destroy_avg <<< $(calc_stats TERRAFORM_DESTROY_TIMES)
read pulumi_deploy_min pulumi_deploy_max pulumi_deploy_avg <<< $(calc_stats PULUMI_DEPLOY_TIMES)
read pulumi_destroy_min pulumi_destroy_max pulumi_destroy_avg <<< $(calc_stats PULUMI_DESTROY_TIMES)

# Print summary
echo -e "\n${CYAN}═══════════════════════════════════════════════════════════════${NC}"
echo -e "${CYAN}                    BENCHMARK RESULTS                           ${NC}"
echo -e "${CYAN}═══════════════════════════════════════════════════════════════${NC}"

echo -e "\n${GREEN}DEPLOYMENT TIMES (seconds):${NC}"
printf "%-12s %10s %10s %10s\n" "Tool" "Min" "Max" "Avg"
printf "%-12s %10s %10s %10s\n" "--------" "------" "------" "------"
printf "%-12s %10.2f %10.2f %10.2f\n" "Bicep" "$bicep_deploy_min" "$bicep_deploy_max" "$bicep_deploy_avg"
printf "%-12s %10.2f %10.2f %10.2f\n" "Terraform" "$tf_deploy_min" "$tf_deploy_max" "$tf_deploy_avg"
printf "%-12s %10.2f %10.2f %10.2f\n" "Pulumi" "$pulumi_deploy_min" "$pulumi_deploy_max" "$pulumi_deploy_avg"

echo -e "\n${GREEN}DESTROY TIMES (seconds):${NC}"
printf "%-12s %10s %10s %10s\n" "Tool" "Min" "Max" "Avg"
printf "%-12s %10s %10s %10s\n" "--------" "------" "------" "------"
printf "%-12s %10.2f %10.2f %10.2f\n" "Bicep" "$bicep_destroy_min" "$bicep_destroy_max" "$bicep_destroy_avg"
printf "%-12s %10.2f %10.2f %10.2f\n" "Terraform" "$tf_destroy_min" "$tf_destroy_max" "$tf_destroy_avg"
printf "%-12s %10.2f %10.2f %10.2f\n" "Pulumi" "$pulumi_destroy_min" "$pulumi_destroy_max" "$pulumi_destroy_avg"

# Determine winners
deploy_winner="Bicep"
deploy_best=$bicep_deploy_avg
if (( $(echo "$tf_deploy_avg < $deploy_best" | bc -l) )); then deploy_winner="Terraform"; deploy_best=$tf_deploy_avg; fi
if (( $(echo "$pulumi_deploy_avg < $deploy_best" | bc -l) )); then deploy_winner="Pulumi"; deploy_best=$pulumi_deploy_avg; fi

destroy_winner="Bicep"
destroy_best=$bicep_destroy_avg
if (( $(echo "$tf_destroy_avg < $destroy_best" | bc -l) )); then destroy_winner="Terraform"; destroy_best=$tf_destroy_avg; fi
if (( $(echo "$pulumi_destroy_avg < $destroy_best" | bc -l) )); then destroy_winner="Pulumi"; destroy_best=$pulumi_destroy_avg; fi

echo -e "\n${YELLOW}🏆 WINNERS:${NC}"
echo -e "  Fastest Deploy:  ${GREEN}$deploy_winner${NC} (${deploy_best}s avg)"
echo -e "  Fastest Destroy: ${GREEN}$destroy_winner${NC} (${destroy_best}s avg)"

# Save JSON results
cat > "$RESULTS_DIR/benchmark_${TIMESTAMP}.json" << EOF
{
  "timestamp": "$TIMESTAMP",
  "iterations": $ITERATIONS,
  "results": {
    "bicep": {
      "deploy": { "times": [$(IFS=,; echo "${BICEP_DEPLOY_TIMES[*]}")], "min": $bicep_deploy_min, "max": $bicep_deploy_max, "avg": $bicep_deploy_avg },
      "destroy": { "times": [$(IFS=,; echo "${BICEP_DESTROY_TIMES[*]}")], "min": $bicep_destroy_min, "max": $bicep_destroy_max, "avg": $bicep_destroy_avg }
    },
    "terraform": {
      "deploy": { "times": [$(IFS=,; echo "${TERRAFORM_DEPLOY_TIMES[*]}")], "min": $tf_deploy_min, "max": $tf_deploy_max, "avg": $tf_deploy_avg },
      "destroy": { "times": [$(IFS=,; echo "${TERRAFORM_DESTROY_TIMES[*]}")], "min": $tf_destroy_min, "max": $tf_destroy_max, "avg": $tf_destroy_avg }
    },
    "pulumi": {
      "deploy": { "times": [$(IFS=,; echo "${PULUMI_DEPLOY_TIMES[*]}")], "min": $pulumi_deploy_min, "max": $pulumi_deploy_max, "avg": $pulumi_deploy_avg },
      "destroy": { "times": [$(IFS=,; echo "${PULUMI_DESTROY_TIMES[*]}")], "min": $pulumi_destroy_min, "max": $pulumi_destroy_max, "avg": $pulumi_destroy_avg }
    }
  },
  "winners": {
    "deploy": "$deploy_winner",
    "destroy": "$destroy_winner"
  }
}
EOF

echo -e "\n${GREEN}Results saved to: $RESULTS_DIR/benchmark_${TIMESTAMP}.json${NC}"
