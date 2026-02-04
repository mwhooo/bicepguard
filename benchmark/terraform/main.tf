# Data sources
data "azurerm_resource_group" "main" {
  name = var.resource_group_name
}

data "azurerm_client_config" "current" {}

# Random suffix for unique names
resource "random_string" "unique_suffix" {
  length  = 8
  special = false
  upper   = false
}

locals {
  unique_suffix = random_string.unique_suffix.result
}

# Network Security Group
resource "azurerm_network_security_group" "main" {
  count               = var.deploy_nsg ? 1 : 0
  name                = var.nsg_config.name
  location            = var.location
  resource_group_name = data.azurerm_resource_group.main.name
  tags                = var.tags

  dynamic "security_rule" {
    for_each = var.nsg_config.security_rules
    content {
      name                       = security_rule.value.name
      priority                   = security_rule.value.priority
      access                     = security_rule.value.access
      direction                  = security_rule.value.direction
      protocol                   = security_rule.value.protocol
      source_port_range          = security_rule.value.source_port_range
      destination_port_range     = security_rule.value.destination_port_range
      source_address_prefix      = security_rule.value.source_address_prefix
      destination_address_prefix = security_rule.value.destination_address_prefix
    }
  }
}

# Virtual Network
resource "azurerm_virtual_network" "main" {
  count               = var.deploy_vnet ? 1 : 0
  name                = var.vnet_config.name
  location            = var.location
  resource_group_name = data.azurerm_resource_group.main.name
  address_space       = var.vnet_config.address_space
  tags                = var.tags
}

# Subnets
resource "azurerm_subnet" "main" {
  for_each = var.deploy_vnet ? { for idx, subnet in var.vnet_config.subnets : subnet.name => subnet } : {}

  name                                           = each.value.name
  resource_group_name                            = data.azurerm_resource_group.main.name
  virtual_network_name                           = azurerm_virtual_network.main[0].name
  address_prefixes                               = [each.value.address_prefix]
  private_endpoint_network_policies_enabled      = each.value.private_endpoint_network_policies == "Disabled" ? false : true
  private_link_service_network_policies_enabled  = each.value.private_link_service_network_policies == "Enabled"
}

# Storage Account
resource "azurerm_storage_account" "main" {
  count                           = var.deploy_storage ? 1 : 0
  name                            = "${var.storage_config.name_prefix}${local.unique_suffix}"
  resource_group_name             = data.azurerm_resource_group.main.name
  location                        = var.location
  account_tier                    = split("_", var.storage_config.sku_name)[0]
  account_replication_type        = split("_", var.storage_config.sku_name)[1]
  account_kind                    = var.storage_config.kind
  access_tier                     = var.storage_config.access_tier
  allow_nested_items_to_be_public = var.storage_config.allow_blob_public_access
  shared_access_key_enabled       = var.storage_config.allow_shared_key_access
  min_tls_version                 = var.storage_config.minimum_tls_version
  enable_https_traffic_only       = var.storage_config.https_traffic_only
  is_hns_enabled                  = var.storage_config.is_hns_enabled
  large_file_share_enabled        = var.storage_config.large_file_shares_state == "Enabled"
  tags                            = var.tags

  network_rules {
    default_action = var.storage_config.network_acls_default_action
  }
}

# App Service Plan
resource "azurerm_service_plan" "main" {
  count               = var.deploy_app_service_plan ? 1 : 0
  name                = var.app_service_plan_config.name
  resource_group_name = data.azurerm_resource_group.main.name
  location            = var.location
  os_type             = var.app_service_plan_config.os_type
  sku_name            = var.app_service_plan_config.sku_name
  zone_balancing_enabled = var.app_service_plan_config.zone_redundant
  tags                = var.tags
}

# Log Analytics Workspace
resource "azurerm_log_analytics_workspace" "main" {
  count               = var.deploy_log_analytics ? 1 : 0
  name                = "${var.log_analytics_config.name_prefix}-${local.unique_suffix}"
  resource_group_name = data.azurerm_resource_group.main.name
  location            = var.location
  sku                 = var.log_analytics_config.sku
  retention_in_days   = var.log_analytics_config.retention_in_days
  tags                = var.tags
}

# Key Vault
resource "azurerm_key_vault" "main" {
  count                           = var.deploy_key_vault ? 1 : 0
  name                            = "${var.key_vault_config.name_prefix}${local.unique_suffix}"
  resource_group_name             = data.azurerm_resource_group.main.name
  location                        = var.location
  tenant_id                       = data.azurerm_client_config.current.tenant_id
  sku_name                        = var.key_vault_config.sku_name
  enabled_for_deployment          = var.key_vault_config.enabled_for_deployment
  enabled_for_template_deployment = var.key_vault_config.enabled_for_template_deployment
  enabled_for_disk_encryption     = var.key_vault_config.enabled_for_disk_encryption
  enable_rbac_authorization       = var.key_vault_config.enable_rbac_authorization
  public_network_access_enabled   = var.key_vault_config.public_network_access_enabled
  tags                            = var.tags
}

# Service Bus Namespace
resource "azurerm_servicebus_namespace" "main" {
  count                         = var.deploy_service_bus ? 1 : 0
  name                          = var.service_bus_config.name
  resource_group_name           = data.azurerm_resource_group.main.name
  location                      = var.location
  sku                           = var.service_bus_config.sku
  local_auth_enabled            = var.service_bus_config.local_auth_enabled
  public_network_access_enabled = var.service_bus_config.public_network_access
  minimum_tls_version           = var.service_bus_config.minimum_tls_version
  tags                          = var.tags
}

# Service Bus Queues
resource "azurerm_servicebus_queue" "main" {
  for_each = var.deploy_service_bus ? { for queue in var.service_bus_config.queues : queue.name => queue } : {}

  name                                 = each.value.name
  namespace_id                         = azurerm_servicebus_namespace.main[0].id
  max_delivery_count                   = each.value.max_delivery_count
  lock_duration                        = each.value.lock_duration
  requires_duplicate_detection         = each.value.requires_duplicate_detection
  requires_session                     = each.value.requires_session
  dead_lettering_on_message_expiration = each.value.dead_lettering_on_message_expiration
}
