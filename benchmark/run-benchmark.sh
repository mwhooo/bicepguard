#!/bin/bash
# =============================================================================
# IaC Benchmark Script - Compare Bicep, Terraform, and Pulumi deployment speeds
# =============================================================================
set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
CYAN='\033[0;36m'
NC='\033[0m' # No Color

# Configuration
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BICEP_DIR="$SCRIPT_DIR/../samples"
TERRAFORM_DIR="$SCRIPT_DIR/terraform"
PULUMI_DIR="$SCRIPT_DIR/pulumi"
RESULTS_DIR="$SCRIPT_DIR/results"
TIMESTAMP=$(date +%Y%m%d_%H%M%S)
RESULTS_FILE="$RESULTS_DIR/benchmark_$TIMESTAMP.json"
REPORT_FILE="$RESULTS_DIR/benchmark_report_$TIMESTAMP.html"

# Default values
RESOURCE_GROUP=""
LOCATION="westeurope"
ITERATIONS=3
SKIP_DESTROY=false
TOOLS_TO_RUN="all"

# Print usage
usage() {
    echo -e "${CYAN}IaC Benchmark Script${NC}"
    echo ""
    echo "Usage: $0 [OPTIONS]"
    echo ""
    echo "Options:"
    echo "  -g, --resource-group NAME    Resource group name (required)"
    echo "  -l, --location LOCATION      Azure location (default: westeurope)"
    echo "  -i, --iterations NUM         Number of iterations per tool (default: 3)"
    echo "  -t, --tools TOOLS            Comma-separated tools to run: bicep,terraform,pulumi,all (default: all)"
    echo "  --skip-destroy               Skip resource destruction after deployment"
    echo "  -h, --help                   Show this help message"
    echo ""
    echo "Example:"
    echo "  $0 -g my-benchmark-rg -i 5 -t bicep,terraform"
}

# Parse arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        -g|--resource-group)
            RESOURCE_GROUP="$2"
            shift 2
            ;;
        -l|--location)
            LOCATION="$2"
            shift 2
            ;;
        -i|--iterations)
            ITERATIONS="$2"
            shift 2
            ;;
        -t|--tools)
            TOOLS_TO_RUN="$2"
            shift 2
            ;;
        --skip-destroy)
            SKIP_DESTROY=true
            shift
            ;;
        -h|--help)
            usage
            exit 0
            ;;
        *)
            echo -e "${RED}Unknown option: $1${NC}"
            usage
            exit 1
            ;;
    esac
done

# Validate required parameters
if [ -z "$RESOURCE_GROUP" ]; then
    echo -e "${RED}Error: Resource group name is required${NC}"
    usage
    exit 1
fi

# Create results directory
mkdir -p "$RESULTS_DIR"

# Initialize results JSON
echo '{
  "benchmark_info": {
    "timestamp": "'$TIMESTAMP'",
    "resource_group": "'$RESOURCE_GROUP'",
    "location": "'$LOCATION'",
    "iterations": '$ITERATIONS'
  },
  "results": {
    "bicep": [],
    "terraform": [],
    "pulumi": []
  },
  "summary": {}
}' > "$RESULTS_FILE"

# Helper function to update JSON results
update_results() {
    local tool=$1
    local operation=$2
    local duration=$3
    local iteration=$4
    local status=$5
    
    # Use jq to update the results file
    local tmp_file=$(mktemp)
    jq ".results.$tool += [{\"iteration\": $iteration, \"operation\": \"$operation\", \"duration_seconds\": $duration, \"status\": \"$status\"}]" "$RESULTS_FILE" > "$tmp_file"
    mv "$tmp_file" "$RESULTS_FILE"
}

# Helper function to calculate statistics
calculate_stats() {
    local tool=$1
    local operation=$2
    
    # Extract durations for successful operations
    local durations=$(jq -r ".results.$tool[] | select(.operation == \"$operation\" and .status == \"success\") | .duration_seconds" "$RESULTS_FILE")
    
    if [ -z "$durations" ]; then
        echo "0 0 0 0"
        return
    fi
    
    # Calculate stats using awk
    echo "$durations" | awk '
    BEGIN { min = 999999; max = 0; sum = 0; count = 0 }
    {
        if ($1 < min) min = $1
        if ($1 > max) max = $1
        sum += $1
        count++
    }
    END {
        if (count > 0) {
            avg = sum / count
            printf "%.2f %.2f %.2f %d", min, max, avg, count
        } else {
            print "0 0 0 0"
        }
    }'
}

# Check prerequisites
check_prerequisites() {
    echo -e "${BLUE}Checking prerequisites...${NC}"
    
    local missing=()
    
    # Check Azure CLI
    if ! command -v az &> /dev/null; then
        missing+=("Azure CLI (az)")
    else
        echo -e "  ${GREEN}✓${NC} Azure CLI found"
    fi
    
    # Check Bicep
    if ! command -v az bicep &> /dev/null; then
        missing+=("Bicep (az bicep)")
    else
        echo -e "  ${GREEN}✓${NC} Bicep CLI found"
    fi
    
    # Check Terraform
    if ! command -v terraform &> /dev/null; then
        missing+=("Terraform")
    else
        echo -e "  ${GREEN}✓${NC} Terraform found: $(terraform version -json | jq -r '.terraform_version')"
    fi
    
    # Check Pulumi
    if ! command -v pulumi &> /dev/null; then
        missing+=("Pulumi")
    else
        echo -e "  ${GREEN}✓${NC} Pulumi found: $(pulumi version)"
    fi
    
    # Check jq
    if ! command -v jq &> /dev/null; then
        missing+=("jq")
    else
        echo -e "  ${GREEN}✓${NC} jq found"
    fi
    
    if [ ${#missing[@]} -gt 0 ]; then
        echo -e "${RED}Missing prerequisites:${NC}"
        for prereq in "${missing[@]}"; do
            echo -e "  ${RED}✗${NC} $prereq"
        done
        exit 1
    fi
    
    # Check Azure login
    if ! az account show &> /dev/null; then
        echo -e "${RED}Error: Not logged in to Azure. Run 'az login' first.${NC}"
        exit 1
    fi
    echo -e "  ${GREEN}✓${NC} Azure CLI logged in"
}

# Create resource group if it doesn't exist
setup_resource_group() {
    echo -e "\n${BLUE}Setting up resource group...${NC}"
    
    if az group show --name "$RESOURCE_GROUP" &> /dev/null; then
        echo -e "  ${YELLOW}Resource group '$RESOURCE_GROUP' already exists${NC}"
    else
        echo -e "  Creating resource group '$RESOURCE_GROUP' in '$LOCATION'..."
        az group create --name "$RESOURCE_GROUP" --location "$LOCATION" --output none
        echo -e "  ${GREEN}✓${NC} Resource group created"
    fi
}

# Clear all resources from resource group
clear_resources() {
    echo -e "${YELLOW}Clearing resources from resource group...${NC}"
    
    # Get all resource IDs
    local resources=$(az resource list --resource-group "$RESOURCE_GROUP" --query "[].id" -o tsv 2>/dev/null)
    
    if [ -n "$resources" ]; then
        echo "$resources" | while read -r resource_id; do
            if [ -n "$resource_id" ]; then
                echo -e "  Deleting: $resource_id"
                az resource delete --ids "$resource_id" --output none 2>/dev/null || true
            fi
        done
        
        # Wait for deletions to complete
        sleep 30
    fi
    
    echo -e "  ${GREEN}✓${NC} Resources cleared"
}

# Benchmark Bicep deployment
benchmark_bicep() {
    local iteration=$1
    echo -e "\n${CYAN}[Bicep] Iteration $iteration${NC}"
    
    cd "$BICEP_DIR"
    
    # Deploy
    echo -e "  ${BLUE}Deploying...${NC}"
    local start_time=$(date +%s.%N)
    
    if az deployment group create \
        --resource-group "$RESOURCE_GROUP" \
        --template-file main-template.bicep \
        --parameters main-template.bicepparam \
        --output none 2>&1; then
        
        local end_time=$(date +%s.%N)
        local duration=$(echo "$end_time - $start_time" | bc)
        echo -e "  ${GREEN}✓${NC} Deployment completed in ${duration}s"
        update_results "bicep" "deploy" "$duration" "$iteration" "success"
    else
        local end_time=$(date +%s.%N)
        local duration=$(echo "$end_time - $start_time" | bc)
        echo -e "  ${RED}✗${NC} Deployment failed after ${duration}s"
        update_results "bicep" "deploy" "$duration" "$iteration" "failed"
    fi
    
    # Destroy
    if [ "$SKIP_DESTROY" = false ]; then
        echo -e "  ${BLUE}Destroying resources...${NC}"
        start_time=$(date +%s.%N)
        clear_resources
        end_time=$(date +%s.%N)
        duration=$(echo "$end_time - $start_time" | bc)
        echo -e "  ${GREEN}✓${NC} Destruction completed in ${duration}s"
        update_results "bicep" "destroy" "$duration" "$iteration" "success"
    fi
}

# Benchmark Terraform deployment
benchmark_terraform() {
    local iteration=$1
    echo -e "\n${CYAN}[Terraform] Iteration $iteration${NC}"
    
    cd "$TERRAFORM_DIR"
    
    # Initialize if needed
    if [ ! -d ".terraform" ]; then
        echo -e "  ${BLUE}Initializing Terraform...${NC}"
        terraform init -input=false > /dev/null
    fi
    
    # Create tfvars
    cat > benchmark.tfvars << EOF
resource_group_name = "$RESOURCE_GROUP"
location            = "$LOCATION"
tags = {
  Environment  = "benchmark"
  Application  = "drifttest"
  ResourceType = "Infrastructure"
  IaC          = "Terraform"
  Iteration    = "$iteration"
}
EOF
    
    # Deploy
    echo -e "  ${BLUE}Deploying...${NC}"
    local start_time=$(date +%s.%N)
    
    if terraform apply -var-file=benchmark.tfvars -auto-approve -input=false > /dev/null 2>&1; then
        local end_time=$(date +%s.%N)
        local duration=$(echo "$end_time - $start_time" | bc)
        echo -e "  ${GREEN}✓${NC} Deployment completed in ${duration}s"
        update_results "terraform" "deploy" "$duration" "$iteration" "success"
    else
        local end_time=$(date +%s.%N)
        local duration=$(echo "$end_time - $start_time" | bc)
        echo -e "  ${RED}✗${NC} Deployment failed after ${duration}s"
        update_results "terraform" "deploy" "$duration" "$iteration" "failed"
    fi
    
    # Destroy
    if [ "$SKIP_DESTROY" = false ]; then
        echo -e "  ${BLUE}Destroying resources...${NC}"
        start_time=$(date +%s.%N)
        
        if terraform destroy -var-file=benchmark.tfvars -auto-approve -input=false > /dev/null 2>&1; then
            end_time=$(date +%s.%N)
            duration=$(echo "$end_time - $start_time" | bc)
            echo -e "  ${GREEN}✓${NC} Destruction completed in ${duration}s"
            update_results "terraform" "destroy" "$duration" "$iteration" "success"
        else
            end_time=$(date +%s.%N)
            duration=$(echo "$end_time - $start_time" | bc)
            echo -e "  ${RED}✗${NC} Destruction failed after ${duration}s"
            update_results "terraform" "destroy" "$duration" "$iteration" "failed"
        fi
    fi
    
    rm -f benchmark.tfvars
}

# Benchmark Pulumi deployment
benchmark_pulumi() {
    local iteration=$1
    echo -e "\n${CYAN}[Pulumi] Iteration $iteration${NC}"
    
    cd "$PULUMI_DIR"
    
    # Setup virtual environment if needed
    if [ ! -d "venv" ]; then
        echo -e "  ${BLUE}Setting up Python virtual environment...${NC}"
        python3 -m venv venv
        source venv/bin/activate
        pip install -r requirements.txt -q
    else
        source venv/bin/activate
    fi
    
    # Select stack
    pulumi stack select benchmark 2>/dev/null || pulumi stack init benchmark
    
    # Set config
    pulumi config set resourceGroupName "$RESOURCE_GROUP"
    pulumi config set location "$LOCATION"
    pulumi config set azure-native:location "$LOCATION"
    
    # Deploy
    echo -e "  ${BLUE}Deploying...${NC}"
    local start_time=$(date +%s.%N)
    
    if pulumi up --yes --skip-preview > /dev/null 2>&1; then
        local end_time=$(date +%s.%N)
        local duration=$(echo "$end_time - $start_time" | bc)
        echo -e "  ${GREEN}✓${NC} Deployment completed in ${duration}s"
        update_results "pulumi" "deploy" "$duration" "$iteration" "success"
    else
        local end_time=$(date +%s.%N)
        local duration=$(echo "$end_time - $start_time" | bc)
        echo -e "  ${RED}✗${NC} Deployment failed after ${duration}s"
        update_results "pulumi" "deploy" "$duration" "$iteration" "failed"
    fi
    
    # Destroy
    if [ "$SKIP_DESTROY" = false ]; then
        echo -e "  ${BLUE}Destroying resources...${NC}"
        start_time=$(date +%s.%N)
        
        if pulumi destroy --yes --skip-preview > /dev/null 2>&1; then
            end_time=$(date +%s.%N)
            duration=$(echo "$end_time - $start_time" | bc)
            echo -e "  ${GREEN}✓${NC} Destruction completed in ${duration}s"
            update_results "pulumi" "destroy" "$duration" "$iteration" "success"
        else
            end_time=$(date +%s.%N)
            duration=$(echo "$end_time - $start_time" | bc)
            echo -e "  ${RED}✗${NC} Destruction failed after ${duration}s"
            update_results "pulumi" "destroy" "$duration" "$iteration" "failed"
        fi
    fi
    
    deactivate
}

# Generate summary statistics
generate_summary() {
    echo -e "\n${BLUE}Generating summary statistics...${NC}"
    
    local tmp_file=$(mktemp)
    
    # Calculate stats for each tool and operation
    for tool in bicep terraform pulumi; do
        for op in deploy destroy; do
            read -r min max avg count <<< $(calculate_stats "$tool" "$op")
            jq ".summary.${tool}_${op} = {\"min\": $min, \"max\": $max, \"avg\": $avg, \"successful_runs\": $count}" "$RESULTS_FILE" > "$tmp_file"
            mv "$tmp_file" "$RESULTS_FILE"
        done
    done
    
    echo -e "  ${GREEN}✓${NC} Summary statistics generated"
}

# Generate HTML report
generate_html_report() {
    echo -e "\n${BLUE}Generating HTML report...${NC}"
    
    # Read summary data
    local bicep_deploy_avg=$(jq -r '.summary.bicep_deploy.avg // 0' "$RESULTS_FILE")
    local terraform_deploy_avg=$(jq -r '.summary.terraform_deploy.avg // 0' "$RESULTS_FILE")
    local pulumi_deploy_avg=$(jq -r '.summary.pulumi_deploy.avg // 0' "$RESULTS_FILE")
    
    local bicep_destroy_avg=$(jq -r '.summary.bicep_destroy.avg // 0' "$RESULTS_FILE")
    local terraform_destroy_avg=$(jq -r '.summary.terraform_destroy.avg // 0' "$RESULTS_FILE")
    local pulumi_destroy_avg=$(jq -r '.summary.pulumi_destroy.avg // 0' "$RESULTS_FILE")
    
    cat > "$REPORT_FILE" << 'HTMLHEADER'
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>IaC Benchmark Report - DriftGuard</title>
    <script src="https://cdn.jsdelivr.net/npm/chart.js"></script>
    <style>
        :root {
            --bicep-color: #0078d4;
            --terraform-color: #7b42bc;
            --pulumi-color: #f7bf2a;
            --bg-color: #f5f5f5;
            --card-bg: #ffffff;
            --text-color: #333333;
            --border-color: #e0e0e0;
        }
        
        * {
            margin: 0;
            padding: 0;
            box-sizing: border-box;
        }
        
        body {
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
            background-color: var(--bg-color);
            color: var(--text-color);
            line-height: 1.6;
            padding: 2rem;
        }
        
        .container {
            max-width: 1200px;
            margin: 0 auto;
        }
        
        header {
            text-align: center;
            margin-bottom: 2rem;
            padding: 2rem;
            background: linear-gradient(135deg, var(--bicep-color), var(--terraform-color), var(--pulumi-color));
            border-radius: 12px;
            color: white;
        }
        
        header h1 {
            font-size: 2.5rem;
            margin-bottom: 0.5rem;
        }
        
        header p {
            font-size: 1.1rem;
            opacity: 0.9;
        }
        
        .grid {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(300px, 1fr));
            gap: 1.5rem;
            margin-bottom: 2rem;
        }
        
        .card {
            background: var(--card-bg);
            border-radius: 12px;
            padding: 1.5rem;
            box-shadow: 0 4px 6px rgba(0, 0, 0, 0.1);
        }
        
        .card h2 {
            font-size: 1.2rem;
            margin-bottom: 1rem;
            padding-bottom: 0.5rem;
            border-bottom: 2px solid var(--border-color);
        }
        
        .card.bicep h2 { border-color: var(--bicep-color); }
        .card.terraform h2 { border-color: var(--terraform-color); }
        .card.pulumi h2 { border-color: var(--pulumi-color); }
        
        .metric {
            display: flex;
            justify-content: space-between;
            padding: 0.5rem 0;
            border-bottom: 1px solid var(--border-color);
        }
        
        .metric:last-child {
            border-bottom: none;
        }
        
        .metric-label {
            color: #666;
        }
        
        .metric-value {
            font-weight: 600;
        }
        
        .chart-container {
            background: var(--card-bg);
            border-radius: 12px;
            padding: 1.5rem;
            box-shadow: 0 4px 6px rgba(0, 0, 0, 0.1);
            margin-bottom: 2rem;
        }
        
        .chart-container h2 {
            margin-bottom: 1rem;
        }
        
        .chart-wrapper {
            position: relative;
            height: 400px;
        }
        
        table {
            width: 100%;
            border-collapse: collapse;
            margin-top: 1rem;
        }
        
        th, td {
            padding: 0.75rem;
            text-align: left;
            border-bottom: 1px solid var(--border-color);
        }
        
        th {
            background-color: var(--bg-color);
            font-weight: 600;
        }
        
        tr:hover {
            background-color: var(--bg-color);
        }
        
        .badge {
            display: inline-block;
            padding: 0.25rem 0.75rem;
            border-radius: 20px;
            font-size: 0.85rem;
            font-weight: 500;
        }
        
        .badge.success { background: #d4edda; color: #155724; }
        .badge.failed { background: #f8d7da; color: #721c24; }
        
        .winner {
            display: inline-block;
            background: linear-gradient(135deg, #ffd700, #ffed4a);
            color: #333;
            padding: 0.25rem 0.75rem;
            border-radius: 20px;
            font-weight: 600;
            margin-left: 0.5rem;
        }
        
        footer {
            text-align: center;
            margin-top: 2rem;
            padding-top: 1rem;
            border-top: 1px solid var(--border-color);
            color: #666;
        }
    </style>
</head>
<body>
    <div class="container">
        <header>
            <h1>🏎️ IaC Benchmark Report</h1>
            <p>Comparing Bicep, Terraform, and Pulumi deployment speeds</p>
        </header>
        
        <div class="card" style="margin-bottom: 2rem;">
            <h2>📊 Benchmark Configuration</h2>
            <div class="metric">
                <span class="metric-label">Timestamp</span>
HTMLHEADER

    echo "                <span class=\"metric-value\">$(date -d @$(echo $TIMESTAMP | sed 's/_/ /g' | awk '{print mktime($1" "$2" 00 00 00")}') '+%Y-%m-%d %H:%M:%S' 2>/dev/null || echo $TIMESTAMP)</span>" >> "$REPORT_FILE"
    
    cat >> "$REPORT_FILE" << 'HTMLMID1'
            </div>
            <div class="metric">
                <span class="metric-label">Resource Group</span>
HTMLMID1

    echo "                <span class=\"metric-value\">$RESOURCE_GROUP</span>" >> "$REPORT_FILE"
    
    cat >> "$REPORT_FILE" << 'HTMLMID2'
            </div>
            <div class="metric">
                <span class="metric-label">Location</span>
HTMLMID2

    echo "                <span class=\"metric-value\">$LOCATION</span>" >> "$REPORT_FILE"
    
    cat >> "$REPORT_FILE" << 'HTMLMID3'
            </div>
            <div class="metric">
                <span class="metric-label">Iterations per Tool</span>
HTMLMID3

    echo "                <span class=\"metric-value\">$ITERATIONS</span>" >> "$REPORT_FILE"
    
    cat >> "$REPORT_FILE" << 'HTMLMID4'
            </div>
        </div>
        
        <div class="grid">
            <div class="card bicep">
                <h2>🔷 Bicep</h2>
                <div class="metric">
                    <span class="metric-label">Avg Deploy Time</span>
HTMLMID4

    # Determine winner
    local deploy_winner="none"
    local min_deploy=$(echo "$bicep_deploy_avg $terraform_deploy_avg $pulumi_deploy_avg" | tr ' ' '\n' | grep -v '^0$' | sort -n | head -1)
    if [ "$bicep_deploy_avg" = "$min_deploy" ] && [ "$bicep_deploy_avg" != "0" ]; then
        deploy_winner="bicep"
    elif [ "$terraform_deploy_avg" = "$min_deploy" ] && [ "$terraform_deploy_avg" != "0" ]; then
        deploy_winner="terraform"
    elif [ "$pulumi_deploy_avg" = "$min_deploy" ] && [ "$pulumi_deploy_avg" != "0" ]; then
        deploy_winner="pulumi"
    fi

    # Bicep metrics
    echo "                    <span class=\"metric-value\">${bicep_deploy_avg}s" >> "$REPORT_FILE"
    [ "$deploy_winner" = "bicep" ] && echo "<span class=\"winner\">🏆 Fastest</span>" >> "$REPORT_FILE"
    echo "</span>" >> "$REPORT_FILE"
    
    cat >> "$REPORT_FILE" << HTMLBICEP
                </div>
                <div class="metric">
                    <span class="metric-label">Avg Destroy Time</span>
                    <span class="metric-value">${bicep_destroy_avg}s</span>
                </div>
                <div class="metric">
                    <span class="metric-label">Min Deploy Time</span>
                    <span class="metric-value">$(jq -r '.summary.bicep_deploy.min // "N/A"' "$RESULTS_FILE")s</span>
                </div>
                <div class="metric">
                    <span class="metric-label">Max Deploy Time</span>
                    <span class="metric-value">$(jq -r '.summary.bicep_deploy.max // "N/A"' "$RESULTS_FILE")s</span>
                </div>
            </div>
            
            <div class="card terraform">
                <h2>🟣 Terraform</h2>
                <div class="metric">
                    <span class="metric-label">Avg Deploy Time</span>
                    <span class="metric-value">${terraform_deploy_avg}s
HTMLBICEP

    [ "$deploy_winner" = "terraform" ] && echo "<span class=\"winner\">🏆 Fastest</span>" >> "$REPORT_FILE"
    
    cat >> "$REPORT_FILE" << HTMLTERRAFORM
</span>
                </div>
                <div class="metric">
                    <span class="metric-label">Avg Destroy Time</span>
                    <span class="metric-value">${terraform_destroy_avg}s</span>
                </div>
                <div class="metric">
                    <span class="metric-label">Min Deploy Time</span>
                    <span class="metric-value">$(jq -r '.summary.terraform_deploy.min // "N/A"' "$RESULTS_FILE")s</span>
                </div>
                <div class="metric">
                    <span class="metric-label">Max Deploy Time</span>
                    <span class="metric-value">$(jq -r '.summary.terraform_deploy.max // "N/A"' "$RESULTS_FILE")s</span>
                </div>
            </div>
            
            <div class="card pulumi">
                <h2>🟡 Pulumi</h2>
                <div class="metric">
                    <span class="metric-label">Avg Deploy Time</span>
                    <span class="metric-value">${pulumi_deploy_avg}s
HTMLTERRAFORM

    [ "$deploy_winner" = "pulumi" ] && echo "<span class=\"winner\">🏆 Fastest</span>" >> "$REPORT_FILE"
    
    cat >> "$REPORT_FILE" << HTMLPULUMI
</span>
                </div>
                <div class="metric">
                    <span class="metric-label">Avg Destroy Time</span>
                    <span class="metric-value">${pulumi_destroy_avg}s</span>
                </div>
                <div class="metric">
                    <span class="metric-label">Min Deploy Time</span>
                    <span class="metric-value">$(jq -r '.summary.pulumi_deploy.min // "N/A"' "$RESULTS_FILE")s</span>
                </div>
                <div class="metric">
                    <span class="metric-label">Max Deploy Time</span>
                    <span class="metric-value">$(jq -r '.summary.pulumi_deploy.max // "N/A"' "$RESULTS_FILE")s</span>
                </div>
            </div>
        </div>
        
        <div class="chart-container">
            <h2>📈 Deployment Time Comparison</h2>
            <div class="chart-wrapper">
                <canvas id="deployChart"></canvas>
            </div>
        </div>
        
        <div class="card">
            <h2>📋 Detailed Results</h2>
            <table>
                <thead>
                    <tr>
                        <th>Tool</th>
                        <th>Iteration</th>
                        <th>Operation</th>
                        <th>Duration (s)</th>
                        <th>Status</th>
                    </tr>
                </thead>
                <tbody>
HTMLPULUMI

    # Add detailed results to table
    jq -r '.results | to_entries[] | .key as $tool | .value[] | "<tr><td>\($tool | ascii_upcase)</td><td>\(.iteration)</td><td>\(.operation)</td><td>\(.duration_seconds)</td><td><span class=\"badge \(.status)\">\(.status)</span></td></tr>"' "$RESULTS_FILE" >> "$REPORT_FILE"
    
    cat >> "$REPORT_FILE" << HTMLFOOTER
                </tbody>
            </table>
        </div>
        
        <footer>
            <p>Generated by DriftGuard IaC Benchmark Tool</p>
            <p>Results file: $RESULTS_FILE</p>
        </footer>
    </div>
    
    <script>
        const ctx = document.getElementById('deployChart').getContext('2d');
        
        // Extract deploy times from results
        const bicepDeploys = $(jq '[.results.bicep[] | select(.operation == "deploy") | .duration_seconds]' "$RESULTS_FILE");
        const terraformDeploys = $(jq '[.results.terraform[] | select(.operation == "deploy") | .duration_seconds]' "$RESULTS_FILE");
        const pulumiDeploys = $(jq '[.results.pulumi[] | select(.operation == "deploy") | .duration_seconds]' "$RESULTS_FILE");
        
        const maxLen = Math.max(bicepDeploys.length, terraformDeploys.length, pulumiDeploys.length);
        const labels = Array.from({length: maxLen}, (_, i) => 'Iteration ' + (i + 1));
        
        new Chart(ctx, {
            type: 'bar',
            data: {
                labels: labels,
                datasets: [
                    {
                        label: 'Bicep',
                        data: bicepDeploys,
                        backgroundColor: 'rgba(0, 120, 212, 0.7)',
                        borderColor: 'rgba(0, 120, 212, 1)',
                        borderWidth: 1
                    },
                    {
                        label: 'Terraform',
                        data: terraformDeploys,
                        backgroundColor: 'rgba(123, 66, 188, 0.7)',
                        borderColor: 'rgba(123, 66, 188, 1)',
                        borderWidth: 1
                    },
                    {
                        label: 'Pulumi',
                        data: pulumiDeploys,
                        backgroundColor: 'rgba(247, 191, 42, 0.7)',
                        borderColor: 'rgba(247, 191, 42, 1)',
                        borderWidth: 1
                    }
                ]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    title: {
                        display: true,
                        text: 'Deployment Time per Iteration (seconds)'
                    },
                    legend: {
                        position: 'top'
                    }
                },
                scales: {
                    y: {
                        beginAtZero: true,
                        title: {
                            display: true,
                            text: 'Time (seconds)'
                        }
                    }
                }
            }
        });
    </script>
</body>
</html>
HTMLFOOTER

    echo -e "  ${GREEN}✓${NC} HTML report generated: $REPORT_FILE"
}

# Print summary to console
print_summary() {
    echo -e "\n${CYAN}═══════════════════════════════════════════════════════════════${NC}"
    echo -e "${CYAN}                    BENCHMARK SUMMARY                            ${NC}"
    echo -e "${CYAN}═══════════════════════════════════════════════════════════════${NC}"
    
    echo -e "\n${BLUE}Deployment Times (average):${NC}"
    printf "  %-15s %10s %10s %10s\n" "Tool" "Min" "Max" "Avg"
    printf "  %-15s %10s %10s %10s\n" "----" "---" "---" "---"
    
    for tool in bicep terraform pulumi; do
        local min=$(jq -r ".summary.${tool}_deploy.min // \"N/A\"" "$RESULTS_FILE")
        local max=$(jq -r ".summary.${tool}_deploy.max // \"N/A\"" "$RESULTS_FILE")
        local avg=$(jq -r ".summary.${tool}_deploy.avg // \"N/A\"" "$RESULTS_FILE")
        printf "  %-15s %10ss %10ss %10ss\n" "$tool" "$min" "$max" "$avg"
    done
    
    echo -e "\n${BLUE}Destruction Times (average):${NC}"
    printf "  %-15s %10s %10s %10s\n" "Tool" "Min" "Max" "Avg"
    printf "  %-15s %10s %10s %10s\n" "----" "---" "---" "---"
    
    for tool in bicep terraform pulumi; do
        local min=$(jq -r ".summary.${tool}_destroy.min // \"N/A\"" "$RESULTS_FILE")
        local max=$(jq -r ".summary.${tool}_destroy.max // \"N/A\"" "$RESULTS_FILE")
        local avg=$(jq -r ".summary.${tool}_destroy.avg // \"N/A\"" "$RESULTS_FILE")
        printf "  %-15s %10ss %10ss %10ss\n" "$tool" "$min" "$max" "$avg"
    done
    
    echo -e "\n${GREEN}Results saved to:${NC}"
    echo -e "  JSON: $RESULTS_FILE"
    echo -e "  HTML: $REPORT_FILE"
    echo ""
}

# Main execution
main() {
    echo -e "${CYAN}╔═══════════════════════════════════════════════════════════════╗${NC}"
    echo -e "${CYAN}║           IaC Benchmark - Bicep vs Terraform vs Pulumi        ║${NC}"
    echo -e "${CYAN}╚═══════════════════════════════════════════════════════════════╝${NC}"
    
    check_prerequisites
    setup_resource_group
    
    # Determine which tools to run
    local run_bicep=false
    local run_terraform=false
    local run_pulumi=false
    
    if [ "$TOOLS_TO_RUN" = "all" ]; then
        run_bicep=true
        run_terraform=true
        run_pulumi=true
    else
        IFS=',' read -ra TOOLS <<< "$TOOLS_TO_RUN"
        for tool in "${TOOLS[@]}"; do
            case $tool in
                bicep) run_bicep=true ;;
                terraform) run_terraform=true ;;
                pulumi) run_pulumi=true ;;
            esac
        done
    fi
    
    # Run benchmarks
    for ((i=1; i<=ITERATIONS; i++)); do
        echo -e "\n${YELLOW}═══════════════════════════════════════════════════════════════${NC}"
        echo -e "${YELLOW}                    ITERATION $i of $ITERATIONS                     ${NC}"
        echo -e "${YELLOW}═══════════════════════════════════════════════════════════════${NC}"
        
        [ "$run_bicep" = true ] && benchmark_bicep "$i"
        [ "$run_terraform" = true ] && benchmark_terraform "$i"
        [ "$run_pulumi" = true ] && benchmark_pulumi "$i"
    done
    
    generate_summary
    generate_html_report
    print_summary
}

# Run main
main
