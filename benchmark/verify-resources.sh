#!/bin/bash
# =============================================================================
# Resource Verification Script - Compare resources across IaC deployments
# =============================================================================

BLUE='\033[0;34m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

echo -e "${BLUE}═══════════════════════════════════════════════════════════════${NC}"
echo -e "${BLUE}       IaC Benchmark - Resource Verification Report            ${NC}"
echo -e "${BLUE}═══════════════════════════════════════════════════════════════${NC}"
echo ""

RGS=("driftguard-benchmark-bicep-rg" "driftguard-benchmark-terraform-rg" "driftguard-benchmark-pulumi-rg")
TOOLS=("Bicep" "Terraform" "Pulumi")

for i in "${!RGS[@]}"; do
    RG="${RGS[$i]}"
    TOOL="${TOOLS[$i]}"
    
    echo -e "${YELLOW}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
    echo -e "${GREEN}${TOOL}${NC} - Resource Group: ${RG}"
    echo -e "${YELLOW}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
    
    echo -e "\n${BLUE}Resources:${NC}"
    az resource list --resource-group "$RG" --query "[].{Name:name, Type:type, Location:location}" -o table
    
    echo -e "\n${BLUE}Virtual Network Details:${NC}"
    az network vnet list --resource-group "$RG" --query "[].{Name:name, AddressSpace:addressSpace.addressPrefixes[0], Subnets:subnets[].name}" -o table 2>/dev/null || echo "  No VNet found"
    
    echo -e "\n${BLUE}NSG Rules:${NC}"
    az network nsg list --resource-group "$RG" --query "[].{Name:name, Rules:securityRules[].{Name:name, Priority:priority, Access:access, Direction:direction}}" -o json 2>/dev/null | jq -r '.[] | "  NSG: \(.Name)", (.Rules[] | "    - \(.Name) (Priority: \(.Priority), \(.Access), \(.Direction))")' 2>/dev/null || echo "  No NSG found"
    
    echo -e "\n${BLUE}Storage Account:${NC}"
    az storage account list --resource-group "$RG" --query "[].{Name:name, SKU:sku.name, Kind:kind, AccessTier:accessTier}" -o table 2>/dev/null || echo "  No Storage Account found"
    
    echo -e "\n${BLUE}App Service Plan:${NC}"
    az appservice plan list --resource-group "$RG" --query "[].{Name:name, SKU:sku.name, Tier:sku.tier}" -o table 2>/dev/null || echo "  No App Service Plan found"
    
    echo -e "\n${BLUE}Log Analytics Workspace:${NC}"
    az monitor log-analytics workspace list --resource-group "$RG" --query "[].{Name:name, SKU:sku.name, RetentionDays:retentionInDays}" -o table 2>/dev/null || echo "  No Log Analytics Workspace found"
    
    echo ""
done

echo -e "${BLUE}═══════════════════════════════════════════════════════════════${NC}"
echo -e "${GREEN}Verification complete!${NC}"
echo -e "${BLUE}═══════════════════════════════════════════════════════════════${NC}"
