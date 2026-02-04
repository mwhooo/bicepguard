"""IaC Benchmark - Pulumi Azure Infrastructure"""

import pulumi
from pulumi_azure_native import resources, network, storage, web, operationalinsights, keyvault, servicebus
import pulumi_random as random

# Configuration
config = pulumi.Config()
azure_config = pulumi.Config("azure-native")

# Get configuration values
resource_group_name = config.require("resourceGroupName")
location = config.get("location") or "westeurope"
environment_name = config.get("environmentName") or "test"
application_name = config.get("applicationName") or "drifttest"

# Deployment flags
deploy_vnet = config.get_bool("deployVnet") or True
deploy_nsg = config.get_bool("deployNsg") or True
deploy_storage = config.get_bool("deployStorage") or True
deploy_app_service_plan = config.get_bool("deployAppServicePlan") or True
deploy_log_analytics = config.get_bool("deployLogAnalytics") or True
deploy_key_vault = config.get_bool("deployKeyVault") or False
deploy_service_bus = config.get_bool("deployServiceBus") or False

# Common tags
tags = {
    "Environment": environment_name,
    "Application": application_name,
    "ResourceType": "Infrastructure",
    "IaC": "Pulumi"
}

# Generate unique suffix
unique_suffix = random.RandomString("unique-suffix",
    length=8,
    special=False,
    upper=False
)

# Get existing resource group
resource_group = resources.get_resource_group(resource_group_name)

# Network Security Group
nsg = None
if deploy_nsg:
    nsg = network.NetworkSecurityGroup("drifttest-nsg",
        network_security_group_name="drifttest-nsg",
        resource_group_name=resource_group_name,
        location=location,
        tags=tags,
        security_rules=[
            network.SecurityRuleArgs(
                name="AllowHTTP",
                priority=100,
                access="Allow",
                direction="Inbound",
                protocol="Tcp",
                source_port_range="*",
                destination_port_range="80",
                source_address_prefix="*",
                destination_address_prefix="*",
            ),
            network.SecurityRuleArgs(
                name="AllowHTTPS",
                priority=110,
                access="Allow",
                direction="Inbound",
                protocol="Tcp",
                source_port_range="*",
                destination_port_range="443",
                source_address_prefix="*",
                destination_address_prefix="*",
            ),
            network.SecurityRuleArgs(
                name="DenyAllInbound",
                priority=1000,
                access="Deny",
                direction="Inbound",
                protocol="*",
                source_port_range="*",
                destination_port_range="*",
                source_address_prefix="*",
                destination_address_prefix="*",
            ),
        ]
    )

# Virtual Network
vnet = None
if deploy_vnet:
    vnet = network.VirtualNetwork("drifttest-vnet",
        virtual_network_name="drifttest-vnet",
        resource_group_name=resource_group_name,
        location=location,
        tags=tags,
        address_space=network.AddressSpaceArgs(
            address_prefixes=["10.0.0.0/16"]
        ),
        subnets=[
            network.SubnetArgs(
                name="drifttest-subnet",
                address_prefix="10.0.0.0/24",
                private_endpoint_network_policies="Disabled",
                private_link_service_network_policies="Enabled",
            ),
            network.SubnetArgs(
                name="drifttest-private-subnet",
                address_prefix="10.0.1.0/24",
                private_endpoint_network_policies="Disabled",
                private_link_service_network_policies="Enabled",
            ),
            network.SubnetArgs(
                name="drifttest-private-subnet-2",
                address_prefix="10.0.2.0/24",
                private_endpoint_network_policies="Disabled",
                private_link_service_network_policies="Enabled",
            ),
        ],
        enable_ddos_protection=False
    )

# Storage Account
storage_account = None
if deploy_storage:
    storage_account_name = pulumi.Output.concat("drifttestsa", unique_suffix.result)
    storage_account = storage.StorageAccount("drifttest-storage",
        account_name=storage_account_name,
        resource_group_name=resource_group_name,
        location=location,
        tags=tags,
        sku=storage.SkuArgs(
            name="Standard_LRS"
        ),
        kind="StorageV2",
        access_tier="Hot",
        allow_blob_public_access=False,
        allow_shared_key_access=True,
        minimum_tls_version="TLS1_2",
        enable_https_traffic_only=True,
        is_hns_enabled=False,
        large_file_shares_state="Disabled",
        network_rule_set=storage.NetworkRuleSetArgs(
            default_action="Allow"
        )
    )

# App Service Plan
app_service_plan = None
if deploy_app_service_plan:
    app_service_plan = web.AppServicePlan("drifttest-asp",
        name="drifttest-asp",
        resource_group_name=resource_group_name,
        location=location,
        tags=tags,
        sku=web.SkuDescriptionArgs(
            name="F1",
            tier="Free"
        ),
        reserved=False,
        zone_redundant=False
    )

# Log Analytics Workspace
log_analytics = None
if deploy_log_analytics:
    log_analytics_name = pulumi.Output.concat("drifttest-law-", unique_suffix.result)
    log_analytics = operationalinsights.Workspace("drifttest-law",
        workspace_name=log_analytics_name,
        resource_group_name=resource_group_name,
        location=location,
        tags=tags,
        sku=operationalinsights.WorkspaceSkuArgs(
            name="PerGB2018"
        ),
        retention_in_days=30,
        features=operationalinsights.WorkspaceFeaturesArgs(
            enable_log_access_using_only_resource_permissions=True
        )
    )

# Key Vault
key_vault = None
if deploy_key_vault:
    # Get current client config
    client_config = pulumi.Output.from_input(azure_config.require("tenantId"))
    key_vault_name = pulumi.Output.concat("drifttest-kv", unique_suffix.result)
    key_vault = keyvault.Vault("drifttest-kv",
        vault_name=key_vault_name,
        resource_group_name=resource_group_name,
        location=location,
        tags=tags,
        properties=keyvault.VaultPropertiesArgs(
            tenant_id=azure_config.require("tenantId"),
            sku=keyvault.SkuArgs(
                family="A",
                name="standard"
            ),
            enabled_for_deployment=False,
            enabled_for_template_deployment=False,
            enabled_for_disk_encryption=False,
            enable_rbac_authorization=True,
            public_network_access="Enabled"
        )
    )

# Service Bus
service_bus_namespace = None
service_bus_queues = []
if deploy_service_bus:
    service_bus_namespace = servicebus.Namespace("drifttest-servicebus",
        namespace_name="drifttest-servicebus",
        resource_group_name=resource_group_name,
        location=location,
        tags=tags,
        sku=servicebus.SBSkuArgs(
            name="Basic",
            tier="Basic"
        ),
        disable_local_auth=False,
        public_network_access="Enabled",
        minimum_tls_version="1.2"
    )

    # Create queues
    queue_configs = [
        {"name": "orders", "max_delivery_count": 10, "lock_duration": "PT5M"},
        {"name": "deadletter", "max_delivery_count": 1, "lock_duration": "PT1M"},
        {"name": "notifications", "max_delivery_count": 5, "lock_duration": "PT2M"},
    ]

    for queue_config in queue_configs:
        queue = servicebus.Queue(f"queue-{queue_config['name']}",
            queue_name=queue_config["name"],
            namespace_name=service_bus_namespace.name,
            resource_group_name=resource_group_name,
            max_delivery_count=queue_config["max_delivery_count"],
            lock_duration=queue_config["lock_duration"],
            requires_duplicate_detection=False,
            requires_session=False,
            dead_lettering_on_message_expiration=False
        )
        service_bus_queues.append(queue)

# Exports
pulumi.export("resource_group_name", resource_group_name)
pulumi.export("unique_suffix", unique_suffix.result)

if nsg:
    pulumi.export("nsg_id", nsg.id)
    pulumi.export("nsg_name", nsg.name)

if vnet:
    pulumi.export("vnet_id", vnet.id)
    pulumi.export("vnet_name", vnet.name)

if storage_account:
    pulumi.export("storage_account_id", storage_account.id)
    pulumi.export("storage_account_name", storage_account.name)

if app_service_plan:
    pulumi.export("app_service_plan_id", app_service_plan.id)
    pulumi.export("app_service_plan_name", app_service_plan.name)

if log_analytics:
    pulumi.export("log_analytics_workspace_id", log_analytics.id)
    pulumi.export("log_analytics_workspace_name", log_analytics.name)

if key_vault:
    pulumi.export("key_vault_id", key_vault.id)
    pulumi.export("key_vault_name", key_vault.name)

if service_bus_namespace:
    pulumi.export("service_bus_namespace_id", service_bus_namespace.id)
    pulumi.export("service_bus_namespace_name", service_bus_namespace.name)
