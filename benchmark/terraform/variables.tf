# Common Variables
variable "resource_group_name" {
  description = "Name of the resource group"
  type        = string
}

variable "location" {
  description = "Azure region for resources"
  type        = string
  default     = "westeurope"
}

variable "environment_name" {
  description = "Environment name (e.g., test, dev, prod)"
  type        = string
  default     = "test"
}

variable "application_name" {
  description = "Application name"
  type        = string
  default     = "drifttest"
}

variable "tags" {
  description = "Common tags for all resources"
  type        = map(string)
  default = {
    Environment  = "test"
    Application  = "drifttest"
    ResourceType = "Infrastructure"
    IaC          = "Terraform"
  }
}

# Deployment flags
variable "deploy_vnet" {
  description = "Deploy Virtual Network"
  type        = bool
  default     = true
}

variable "deploy_nsg" {
  description = "Deploy Network Security Group"
  type        = bool
  default     = true
}

variable "deploy_storage" {
  description = "Deploy Storage Account"
  type        = bool
  default     = true
}

variable "deploy_app_service_plan" {
  description = "Deploy App Service Plan"
  type        = bool
  default     = true
}

variable "deploy_log_analytics" {
  description = "Deploy Log Analytics Workspace"
  type        = bool
  default     = true
}

variable "deploy_key_vault" {
  description = "Deploy Key Vault"
  type        = bool
  default     = false
}

variable "deploy_service_bus" {
  description = "Deploy Service Bus"
  type        = bool
  default     = false
}

# Storage Account Configuration
variable "storage_config" {
  description = "Storage account configuration"
  type = object({
    name_prefix               = string
    sku_name                  = string
    kind                      = string
    access_tier               = string
    allow_blob_public_access  = bool
    allow_shared_key_access   = bool
    minimum_tls_version       = string
    https_traffic_only        = bool
    network_acls_default_action = string
    is_hns_enabled            = bool
    large_file_shares_state   = string
  })
  default = {
    name_prefix               = "drifttestsa"
    sku_name                  = "Standard_LRS"
    kind                      = "StorageV2"
    access_tier               = "Hot"
    allow_blob_public_access  = false
    allow_shared_key_access   = true
    minimum_tls_version       = "TLS1_2"
    https_traffic_only        = true
    network_acls_default_action = "Allow"
    is_hns_enabled            = false
    large_file_shares_state   = "Disabled"
  }
}

# Virtual Network Configuration
variable "vnet_config" {
  description = "Virtual network configuration"
  type = object({
    name                    = string
    address_space           = list(string)
    enable_ddos_protection  = bool
    subnets = list(object({
      name                                    = string
      address_prefix                          = string
      private_endpoint_network_policies       = string
      private_link_service_network_policies   = string
    }))
  })
  default = {
    name                   = "drifttest-vnet"
    address_space          = ["10.0.0.0/16"]
    enable_ddos_protection = false
    subnets = [
      {
        name                                    = "drifttest-subnet"
        address_prefix                          = "10.0.0.0/24"
        private_endpoint_network_policies       = "Disabled"
        private_link_service_network_policies   = "Enabled"
      },
      {
        name                                    = "drifttest-private-subnet"
        address_prefix                          = "10.0.1.0/24"
        private_endpoint_network_policies       = "Disabled"
        private_link_service_network_policies   = "Enabled"
      },
      {
        name                                    = "drifttest-private-subnet-2"
        address_prefix                          = "10.0.2.0/24"
        private_endpoint_network_policies       = "Disabled"
        private_link_service_network_policies   = "Enabled"
      }
    ]
  }
}

# Network Security Group Configuration
variable "nsg_config" {
  description = "Network Security Group configuration"
  type = object({
    name = string
    security_rules = list(object({
      name                       = string
      priority                   = number
      access                     = string
      direction                  = string
      protocol                   = string
      source_port_range          = string
      destination_port_range     = string
      source_address_prefix      = string
      destination_address_prefix = string
    }))
  })
  default = {
    name = "drifttest-nsg"
    security_rules = [
      {
        name                       = "AllowHTTP"
        priority                   = 100
        access                     = "Allow"
        direction                  = "Inbound"
        protocol                   = "Tcp"
        source_port_range          = "*"
        destination_port_range     = "80"
        source_address_prefix      = "*"
        destination_address_prefix = "*"
      },
      {
        name                       = "AllowHTTPS"
        priority                   = 110
        access                     = "Allow"
        direction                  = "Inbound"
        protocol                   = "Tcp"
        source_port_range          = "*"
        destination_port_range     = "443"
        source_address_prefix      = "*"
        destination_address_prefix = "*"
      },
      {
        name                       = "DenyAllInbound"
        priority                   = 1000
        access                     = "Deny"
        direction                  = "Inbound"
        protocol                   = "*"
        source_port_range          = "*"
        destination_port_range     = "*"
        source_address_prefix      = "*"
        destination_address_prefix = "*"
      }
    ]
  }
}

# App Service Plan Configuration
variable "app_service_plan_config" {
  description = "App Service Plan configuration"
  type = object({
    name           = string
    sku_name       = string
    os_type        = string
    zone_redundant = bool
  })
  default = {
    name           = "drifttest-asp"
    sku_name       = "F1"
    os_type        = "Windows"
    zone_redundant = false
  }
}

# Log Analytics Workspace Configuration
variable "log_analytics_config" {
  description = "Log Analytics Workspace configuration"
  type = object({
    name_prefix       = string
    sku               = string
    retention_in_days = number
  })
  default = {
    name_prefix       = "drifttest-law"
    sku               = "PerGB2018"
    retention_in_days = 30
  }
}

# Key Vault Configuration
variable "key_vault_config" {
  description = "Key Vault configuration"
  type = object({
    name_prefix                  = string
    sku_name                     = string
    enabled_for_deployment       = bool
    enabled_for_template_deployment = bool
    enabled_for_disk_encryption  = bool
    enable_rbac_authorization    = bool
    public_network_access_enabled = bool
  })
  default = {
    name_prefix                  = "drifttest-kv"
    sku_name                     = "standard"
    enabled_for_deployment       = false
    enabled_for_template_deployment = false
    enabled_for_disk_encryption  = false
    enable_rbac_authorization    = true
    public_network_access_enabled = true
  }
}

# Service Bus Configuration
variable "service_bus_config" {
  description = "Service Bus configuration"
  type = object({
    name                  = string
    sku                   = string
    local_auth_enabled    = bool
    public_network_access = bool
    minimum_tls_version   = string
    queues = list(object({
      name                                 = string
      max_delivery_count                   = number
      lock_duration                        = string
      requires_duplicate_detection         = bool
      requires_session                     = bool
      dead_lettering_on_message_expiration = bool
    }))
  })
  default = {
    name                  = "drifttest-servicebus"
    sku                   = "Basic"
    local_auth_enabled    = true
    public_network_access = true
    minimum_tls_version   = "1.2"
    queues = [
      {
        name                                 = "orders"
        max_delivery_count                   = 10
        lock_duration                        = "PT5M"
        requires_duplicate_detection         = false
        requires_session                     = false
        dead_lettering_on_message_expiration = false
      },
      {
        name                                 = "deadletter"
        max_delivery_count                   = 1
        lock_duration                        = "PT1M"
        requires_duplicate_detection         = false
        requires_session                     = false
        dead_lettering_on_message_expiration = false
      },
      {
        name                                 = "notifications"
        max_delivery_count                   = 5
        lock_duration                        = "PT2M"
        requires_duplicate_detection         = false
        requires_session                     = false
        dead_lettering_on_message_expiration = false
      }
    ]
  }
}
