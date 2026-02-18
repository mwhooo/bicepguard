#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using FluentAssertions;
using BicepGuard.Models;
using BicepGuard.Services;
using Newtonsoft.Json.Linq;

namespace BicepGuard.Tests.Services;

public class ComparisonServiceTests
{
    [Fact]
    public void Constructor_WithoutIgnoreService_ShouldInitializeSuccessfully()
    {
        // Act
        var service = new ComparisonService();

        // Assert
        service.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithIgnoreService_ShouldInitializeSuccessfully()
    {
        // Arrange
        var ignoreService = new DriftIgnoreService("/nonexistent/path/drift-ignore.json");

        // Act
        var service = new ComparisonService(ignoreService);

        // Assert
        service.Should().NotBeNull();
    }

    [Fact]
    public void CompareResources_WithEmptyLiveResources_ShouldHasDrift()
    {
        // Arrange
        var service = new ComparisonService();
        var expectedTemplate = new JObject
        {
            ["resources"] = new JArray
            {
                new JObject
                {
                    ["type"] = "Microsoft.Storage/storageAccounts",
                    ["name"] = "mystorage",
                    ["location"] = "eastus",
                    ["kind"] = "StorageV2",
                    ["sku"] = new JObject { ["name"] = "Standard_LRS" },
                    ["properties"] = new JObject()
                }
            }
        };
        var liveResources = new List<AzureResource>();

        // Act
        var result = service.CompareResources(expectedTemplate, liveResources);

        // Assert
        result.Should().NotBeNull();
        result.HasDrift.Should().BeTrue();
        result.ResourceDrifts.Should().NotBeEmpty();
    }

    [Fact]
    public void CompareResources_WithMatchingResources_ShouldHaveNoDrift()
    {
        // Arrange
        var service = new ComparisonService();
        var expectedTemplate = new JObject
        {
            ["resources"] = new JArray
            {
                new JObject
                {
                    ["type"] = "Microsoft.Storage/storageAccounts",
                    ["name"] = "mystorage",
                    ["location"] = "eastus",
                    ["sku"] = new JObject { ["name"] = "Standard_LRS" },
                    ["properties"] = new JObject { ["accessTier"] = "Hot" }
                }
            }
        };
        var liveResources = new List<AzureResource>
        {
            new AzureResource
            {
                Type = "Microsoft.Storage/storageAccounts",
                Name = "mystorage",
                Id = "/subscriptions/sub123/resourceGroups/rg/providers/Microsoft.Storage/storageAccounts/mystorage",
                Properties = new Dictionary<string, object?>
                {
                    ["location"] = "eastus",
                    ["sku"] = new Dictionary<string, object?> { ["name"] = "Standard_LRS" },
                    ["properties.accessTier"] = "Hot"
                }
            }
        };

        // Act
        var result = service.CompareResources(expectedTemplate, liveResources);

        // Assert
        result.Should().NotBeNull();
        result.ResourceDrifts.Should().BeEmpty();
        result.HasDrift.Should().BeFalse();
    }

    [Fact]
    public void CompareResources_WithPropertyMismatch_ShouldDetectDrift()
    {
        // Arrange
        var service = new ComparisonService();
        var expectedTemplate = new JObject
        {
            ["resources"] = new JArray
            {
                new JObject
                {
                    ["type"] = "Microsoft.Storage/storageAccounts",
                    ["name"] = "mystorage",
                    ["location"] = "eastus",
                    ["sku"] = new JObject { ["name"] = "Standard_LRS" },
                    ["properties"] = new JObject { ["accessTier"] = "Hot" }
                }
            }
        };
        var liveResources = new List<AzureResource>
        {
            new AzureResource
            {
                Type = "Microsoft.Storage/storageAccounts",
                Name = "mystorage",
                Id = "/subscriptions/sub123/resourceGroups/rg/providers/Microsoft.Storage/storageAccounts/mystorage",
                Properties = new Dictionary<string, object?>
                {
                    ["location"] = "eastus",
                    ["properties.accessTier"] = "Cool"  // Mismatch
                }
            }
        };

        // Act
        var result = service.CompareResources(expectedTemplate, liveResources);

        // Assert
        result.Should().NotBeNull();
        result.HasDrift.Should().BeTrue();
        result.ResourceDrifts.Should().HaveCount(1);
        result.ResourceDrifts[0].PropertyDrifts.Should().NotBeEmpty();
        result.ResourceDrifts[0].PropertyDrifts.Any(p => p.Type == DriftType.Modified).Should().BeTrue();
    }

    [Fact]
    public void CompareResources_WithMissingResourceProperty_ShouldDetectDrift()
    {
        // Arrange
        var service = new ComparisonService();
        var expectedTemplate = new JObject
        {
            ["resources"] = new JArray
            {
                new JObject
                {
                    ["type"] = "Microsoft.Storage/storageAccounts",
                    ["name"] = "mystorage",
                    ["location"] = "eastus",
                    ["sku"] = new JObject { ["name"] = "Standard_LRS" },
                    ["properties"] = new JObject { ["accessTier"] = "Hot", ["httpsOnly"] = true }
                }
            }
        };
        var liveResources = new List<AzureResource>
        {
            new AzureResource
            {
                Type = "Microsoft.Storage/storageAccounts",
                Name = "mystorage",
                Id = "/subscriptions/sub123/resourceGroups/rg/providers/Microsoft.Storage/storageAccounts/mystorage",
                Properties = new Dictionary<string, object?>
                {
                    ["location"] = "eastus",
                    ["properties.accessTier"] = "Hot"
                    // httpsOnly property missing
                }
            }
        };

        // Act
        var result = service.CompareResources(expectedTemplate, liveResources);

        // Assert
        result.Should().NotBeNull();
        result.HasDrift.Should().BeTrue();
        result.ResourceDrifts.Should().HaveCount(1);
        result.ResourceDrifts[0].PropertyDrifts.Should().NotBeEmpty();
        result.ResourceDrifts[0].PropertyDrifts.Any(p => p.Type == DriftType.Missing).Should().BeTrue();
    }

    [Fact]
    public void CompareResources_WithMultipleResources_ShouldCompareAll()
    {
        // Arrange
        var service = new ComparisonService();
        var expectedTemplate = new JObject
        {
            ["resources"] = new JArray
            {
                new JObject
                {
                    ["type"] = "Microsoft.Storage/storageAccounts",
                    ["name"] = "storage1",
                    ["location"] = "eastus",
                    ["sku"] = new JObject { ["name"] = "Standard_LRS" },
                    ["properties"] = new JObject()
                },
                new JObject
                {
                    ["type"] = "Microsoft.KeyVault/vaults",
                    ["name"] = "keyvault1",
                    ["location"] = "eastus",
                    ["properties"] = new JObject()
                }
            }
        };
        var liveResources = new List<AzureResource>
        {
            new AzureResource
            {
                Type = "Microsoft.Storage/storageAccounts",
                Name = "storage1",
                Id = "/subscriptions/sub123/resourceGroups/rg/providers/Microsoft.Storage/storageAccounts/storage1",
                Properties = new Dictionary<string, object?> { ["location"] = "eastus" }
            },
            new AzureResource
            {
                Type = "Microsoft.KeyVault/vaults",
                Name = "keyvault1",
                Id = "/subscriptions/sub123/resourceGroups/rg/providers/Microsoft.KeyVault/vaults/keyvault1",
                Properties = new Dictionary<string, object?> { ["location"] = "westus" }  // Mismatch
            }
        };

        // Act
        var result = service.CompareResources(expectedTemplate, liveResources);

        // Assert
        result.Should().NotBeNull();
        result.ResourceDrifts.Should().NotBeEmpty();
        // Expected: keyvault location mismatch detected
    }

    [Fact]
    public void CompareResources_WithNoResourcesInTemplate_ShouldReturnNoDrift()
    {
        // Arrange
        var service = new ComparisonService();
        var expectedTemplate = new JObject
        {
            ["resources"] = new JArray()  // Empty resources
        };
        var liveResources = new List<AzureResource>
        {
            new AzureResource
            {
                Type = "Microsoft.Storage/storageAccounts",
                Name = "storage1",
                Id = "/subscriptions/sub123/resourceGroups/rg/providers/Microsoft.Storage/storageAccounts/storage1",
                Properties = new Dictionary<string, object?> { ["location"] = "eastus" }
            }
        };

        // Act
        var result = service.CompareResources(expectedTemplate, liveResources);

        // Assert
        result.Should().NotBeNull();
        result.ResourceDrifts.Should().BeEmpty();
        result.HasDrift.Should().BeFalse();
    }

    [Fact]
    public void CompareResources_WithLocationMismatch_ShouldDetectPropertyDrift()
    {
        // Arrange
        var service = new ComparisonService();
        var expectedTemplate = new JObject
        {
            ["resources"] = new JArray
            {
                new JObject
                {
                    ["type"] = "Microsoft.Storage/storageAccounts",
                    ["name"] = "mystorage",
                    ["location"] = "eastus",
                    ["sku"] = new JObject { ["name"] = "Standard_LRS" },
                    ["properties"] = new JObject()
                }
            }
        };
        var liveResources = new List<AzureResource>
        {
            new AzureResource
            {
                Type = "Microsoft.Storage/storageAccounts",
                Name = "mystorage",
                Id = "/subscriptions/sub123/resourceGroups/rg/providers/Microsoft.Storage/storageAccounts/mystorage",
                Properties = new Dictionary<string, object?>
                {
                    ["location"] = "westus"  // Different location
                }
            }
        };

        // Act
        var result = service.CompareResources(expectedTemplate, liveResources);

        // Assert
        result.Should().NotBeNull();
        result.HasDrift.Should().BeTrue();
        result.ResourceDrifts.Should().HaveCount(1);
        var drift = result.ResourceDrifts[0];
        drift.PropertyDrifts.Should().NotBeEmpty();
        drift.PropertyDrifts.Any(p => p.PropertyPath == "location" && p.Type == DriftType.Modified).Should().BeTrue();
    }

    [Fact]
    public void CompareResources_WithSkuMismatch_ShouldDetectDrift()
    {
        // Arrange
        var service = new ComparisonService();
        var expectedTemplate = new JObject
        {
            ["resources"] = new JArray
            {
                new JObject
                {
                    ["type"] = "Microsoft.Storage/storageAccounts",
                    ["name"] = "mystorage",
                    ["location"] = "eastus",
                    ["sku"] = new JObject { ["name"] = "Standard_LRS" },
                    ["properties"] = new JObject()
                }
            }
        };
        var liveResources = new List<AzureResource>
        {
            new AzureResource
            {
                Type = "Microsoft.Storage/storageAccounts",
                Name = "mystorage",
                Id = "/subscriptions/sub123/resourceGroups/rg/providers/Microsoft.Storage/storageAccounts/mystorage",
                Properties = new Dictionary<string, object?>
                {
                    ["location"] = "eastus",
                    ["sku"] = new Dictionary<string, object?> { ["name"] = "Standard_GRS" }  // Different SKU
                }
            }
        };

        // Act
        var result = service.CompareResources(expectedTemplate, liveResources);

        // Assert
        result.Should().NotBeNull();
        result.HasDrift.Should().BeTrue();
        result.ResourceDrifts.Should().HaveCount(1);
        result.ResourceDrifts[0].PropertyDrifts.Any(p => p.PropertyPath == "sku").Should().BeTrue();
    }

    [Fact]
    public void CompareResources_WithArmExpressionInLocation_ShouldSkipComparison()
    {
        // Arrange
        var service = new ComparisonService();
        var expectedTemplate = new JObject
        {
            ["resources"] = new JArray
            {
                new JObject
                {
                    ["type"] = "Microsoft.Storage/storageAccounts",
                    ["name"] = "mystorage",
                    ["location"] = "[parameters('location')]",  // ARM expression
                    ["sku"] = new JObject { ["name"] = "Standard_LRS" },
                    ["properties"] = new JObject()
                }
            }
        };
        var liveResources = new List<AzureResource>
        {
            new AzureResource
            {
                Type = "Microsoft.Storage/storageAccounts",
                Name = "mystorage",
                Id = "/subscriptions/sub123/resourceGroups/rg/providers/Microsoft.Storage/storageAccounts/mystorage",
                Properties = new Dictionary<string, object?>
                {
                    ["location"] = "anyregion",  // Any region should match since expected is parameterized
                    ["sku"] = new Dictionary<string, object?> { ["name"] = "Standard_LRS" }
                }
            }
        };

        // Act
        var result = service.CompareResources(expectedTemplate, liveResources);

        // Assert
        result.Should().NotBeNull();
        // ARM expressions should be skipped, so no location drift detected
        result.ResourceDrifts.Should().BeEmpty();
    }

    [Fact]
    public void CompareResources_WithTagsMismatch_ShouldDetectDrift()
    {
        // Arrange
        var service = new ComparisonService();
        var expectedTemplate = new JObject
        {
            ["resources"] = new JArray
            {
                new JObject
                {
                    ["type"] = "Microsoft.Storage/storageAccounts",
                    ["name"] = "mystorage",
                    ["location"] = "eastus",
                    ["sku"] = new JObject { ["name"] = "Standard_LRS" },
                    ["tags"] = new JObject { ["environment"] = "prod", ["team"] = "platform" },
                    ["properties"] = new JObject()
                }
            }
        };
        var liveResources = new List<AzureResource>
        {
            new AzureResource
            {
                Type = "Microsoft.Storage/storageAccounts",
                Name = "mystorage",
                Id = "/subscriptions/sub123/resourceGroups/rg/providers/Microsoft.Storage/storageAccounts/mystorage",
                Properties = new Dictionary<string, object?>
                {
                    ["location"] = "eastus",
                    ["tags"] = new Dictionary<string, object?> { ["environment"] = "dev" }  // Different value
                }
            }
        };

        // Act
        var result = service.CompareResources(expectedTemplate, liveResources);

        // Assert
        result.Should().NotBeNull();
        result.HasDrift.Should().BeTrue();
        result.ResourceDrifts.Should().HaveCount(1);
        result.ResourceDrifts[0].PropertyDrifts.Any(p => p.PropertyPath == "tags" && p.Type == DriftType.Modified).Should().BeTrue();
    }

    [Fact]
    public void CompareResources_WithResultSummary_ShouldGenerateNonEmptySummary()
    {
        // Arrange
        var service = new ComparisonService();
        var expectedTemplate = new JObject
        {
            ["resources"] = new JArray
            {
                new JObject
                {
                    ["type"] = "Microsoft.Storage/storageAccounts",
                    ["name"] = "mystorage",
                    ["location"] = "eastus",
                    ["sku"] = new JObject { ["name"] = "Standard_LRS" },
                    ["properties"] = new JObject { ["httpsOnly"] = true }
                }
            }
        };
        var liveResources = new List<AzureResource>
        {
            new AzureResource
            {
                Type = "Microsoft.Storage/storageAccounts",
                Name = "mystorage",
                Id = "/subscriptions/sub123/resourceGroups/rg/providers/Microsoft.Storage/storageAccounts/mystorage",
                Properties = new Dictionary<string, object?>
                {
                    ["location"] = "eastus",
                    ["properties.httpsOnly"] = false  // Mismatch
                }
            }
        };

        // Act
        var result = service.CompareResources(expectedTemplate, liveResources);

        // Assert
        result.Should().NotBeNull();
        result.Summary.Should().NotBeNullOrWhiteSpace();
    }
}
