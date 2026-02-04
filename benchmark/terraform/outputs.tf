# Outputs
output "resource_group_name" {
  description = "The name of the resource group"
  value       = data.azurerm_resource_group.main.name
}

output "unique_suffix" {
  description = "The unique suffix used for resource names"
  value       = local.unique_suffix
}

# Virtual Network
output "vnet_id" {
  description = "The ID of the Virtual Network"
  value       = var.deploy_vnet ? azurerm_virtual_network.main[0].id : null
}

output "vnet_name" {
  description = "The name of the Virtual Network"
  value       = var.deploy_vnet ? azurerm_virtual_network.main[0].name : null
}

output "subnet_ids" {
  description = "Map of subnet names to IDs"
  value       = var.deploy_vnet ? { for k, v in azurerm_subnet.main : k => v.id } : {}
}

# Network Security Group
output "nsg_id" {
  description = "The ID of the Network Security Group"
  value       = var.deploy_nsg ? azurerm_network_security_group.main[0].id : null
}

output "nsg_name" {
  description = "The name of the Network Security Group"
  value       = var.deploy_nsg ? azurerm_network_security_group.main[0].name : null
}

# Storage Account
output "storage_account_id" {
  description = "The ID of the Storage Account"
  value       = var.deploy_storage ? azurerm_storage_account.main[0].id : null
}

output "storage_account_name" {
  description = "The name of the Storage Account"
  value       = var.deploy_storage ? azurerm_storage_account.main[0].name : null
}

# App Service Plan
output "app_service_plan_id" {
  description = "The ID of the App Service Plan"
  value       = var.deploy_app_service_plan ? azurerm_service_plan.main[0].id : null
}

output "app_service_plan_name" {
  description = "The name of the App Service Plan"
  value       = var.deploy_app_service_plan ? azurerm_service_plan.main[0].name : null
}

# Log Analytics Workspace
output "log_analytics_workspace_id" {
  description = "The ID of the Log Analytics Workspace"
  value       = var.deploy_log_analytics ? azurerm_log_analytics_workspace.main[0].id : null
}

output "log_analytics_workspace_name" {
  description = "The name of the Log Analytics Workspace"
  value       = var.deploy_log_analytics ? azurerm_log_analytics_workspace.main[0].name : null
}

# Key Vault
output "key_vault_id" {
  description = "The ID of the Key Vault"
  value       = var.deploy_key_vault ? azurerm_key_vault.main[0].id : null
}

output "key_vault_name" {
  description = "The name of the Key Vault"
  value       = var.deploy_key_vault ? azurerm_key_vault.main[0].name : null
}

# Service Bus
output "service_bus_namespace_id" {
  description = "The ID of the Service Bus Namespace"
  value       = var.deploy_service_bus ? azurerm_servicebus_namespace.main[0].id : null
}

output "service_bus_namespace_name" {
  description = "The name of the Service Bus Namespace"
  value       = var.deploy_service_bus ? azurerm_servicebus_namespace.main[0].name : null
}
