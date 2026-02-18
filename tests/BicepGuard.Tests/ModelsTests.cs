using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Xunit;
using FluentAssertions;
using BicepGuard.Models;

namespace BicepGuard.Tests.Models;

public class DriftDetectionResultTests
{
    [Fact]
    public void Constructor_ShouldInitializeWithDefaults()
    {
        // Act
        var result = new DriftDetectionResult();

        // Assert
        result.HasDrift.Should().BeFalse();
        result.ResourceDrifts.Should().BeEmpty();
        result.ResourceDrifts.Should().NotBeNull();
        result.Summary.Should().BeEmpty();
        result.DetectedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void HasDrift_ShouldBeSettable()
    {
        // Arrange
        var result = new DriftDetectionResult();

        // Act
        result.HasDrift = true;

        // Assert
        result.HasDrift.Should().BeTrue();
    }

    [Fact]
    public void ResourceDrifts_ShouldAllowAddingItems()
    {
        // Arrange
        var result = new DriftDetectionResult();
        var drift = new ResourceDrift { ResourceName = "storage1", ResourceType = "Microsoft.Storage/storageAccounts" };

        // Act
        result.ResourceDrifts.Add(drift);

        // Assert
        result.ResourceDrifts.Should().HaveCount(1);
        result.ResourceDrifts[0].ResourceName.Should().Be("storage1");
    }

    [Fact]
    public void Summary_CanBeSet()
    {
        // Arrange
        var result = new DriftDetectionResult();

        // Act
        result.Summary = "1 resource with drift detected";

        // Assert
        result.Summary.Should().Be("1 resource with drift detected");
    }

    [Fact]
    public void DetectedAt_CanBeSet()
    {
        // Arrange
        var result = new DriftDetectionResult();
        var customDate = new DateTime(2026, 1, 1, 12, 0, 0);

        // Act
        result.DetectedAt = customDate;

        // Assert
        result.DetectedAt.Should().Be(customDate);
    }

    [Fact]
    public void MultipleResourceDrifts_CanBeAdded()
    {
        // Arrange
        var result = new DriftDetectionResult();

        // Act
        result.ResourceDrifts.Add(new ResourceDrift { ResourceName = "res1" });
        result.ResourceDrifts.Add(new ResourceDrift { ResourceName = "res2" });
        result.ResourceDrifts.Add(new ResourceDrift { ResourceName = "res3" });

        // Assert
        result.ResourceDrifts.Should().HaveCount(3);
    }
}

public class ResourceDriftTests
{
    [Fact]
    public void Constructor_ShouldInitializeWithDefaults()
    {
        // Act
        var drift = new ResourceDrift();

        // Assert
        drift.ResourceType.Should().BeEmpty();
        drift.ResourceName.Should().BeEmpty();
        drift.ResourceId.Should().BeEmpty();
        drift.PropertyDrifts.Should().BeEmpty();
        drift.PropertyDrifts.Should().NotBeNull();
    }

    [Fact]
    public void HasDrift_WithNoPropertyDrifts_ShouldReturnFalse()
    {
        // Arrange
        var drift = new ResourceDrift { ResourceName = "storage1" };

        // Act & Assert
        drift.HasDrift.Should().BeFalse();
    }

    [Fact]
    public void HasDrift_WithPropertyDrifts_ShouldReturnTrue()
    {
        // Arrange
        var drift = new ResourceDrift
        {
            ResourceName = "storage1",
            PropertyDrifts = new List<PropertyDrift>
            {
                new() { PropertyPath = "properties.accessTier", Type = DriftType.Modified }
            }
        };

        // Act & Assert
        drift.HasDrift.Should().BeTrue();
    }

    [Fact]
    public void HasDrift_WithMultiplePropertyDrifts_ShouldReturnTrue()
    {
        // Arrange
        var drift = new ResourceDrift
        {
            ResourceName = "storage1",
            PropertyDrifts = new List<PropertyDrift>
            {
                new() { PropertyPath = "properties.accessTier", Type = DriftType.Modified },
                new() { PropertyPath = "tags.environment", Type = DriftType.Extra }
            }
        };

        // Act & Assert
        drift.HasDrift.Should().BeTrue();
    }

    [Fact]
    public void Properties_CanBeSet()
    {
        // Arrange
        var drift = new ResourceDrift();

        // Act
        drift.ResourceType = "Microsoft.Storage/storageAccounts";
        drift.ResourceName = "mystorageaccent";
        drift.ResourceId = "/subscriptions/12345/resourceGroups/myResourceGroup/providers/Microsoft.Storage/storageAccounts/mystorageaccount";

        // Assert
        drift.ResourceType.Should().Be("Microsoft.Storage/storageAccounts");
        drift.ResourceName.Should().Be("mystorageaccent");
        drift.ResourceId.Should().StartWith("/subscriptions/");
    }

    [Fact]
    public void PropertyDrifts_CanBeAddedAndRemoved()
    {
        // Arrange
        var drift = new ResourceDrift();
        var prop1 = new PropertyDrift { PropertyPath = "prop1" };
        var prop2 = new PropertyDrift { PropertyPath = "prop2" };

        // Act
        drift.PropertyDrifts.Add(prop1);
        drift.PropertyDrifts.Add(prop2);
        drift.PropertyDrifts.Remove(prop1);

        // Assert
        drift.PropertyDrifts.Should().HaveCount(1);
        drift.PropertyDrifts[0].PropertyPath.Should().Be("prop2");
    }
}

public class PropertyDriftTests
{
    [Fact]
    public void Constructor_ShouldInitializeWithDefaults()
    {
        // Act
        var drift = new PropertyDrift();

        // Assert
        drift.PropertyPath.Should().BeEmpty();
        drift.ExpectedValue.Should().BeNull();
        drift.ActualValue.Should().BeNull();
        drift.Type.Should().Be(DriftType.Missing); // Default enum value when not set
    }

    [Theory]
    [InlineData(DriftType.Missing)]
    [InlineData(DriftType.Extra)]
    [InlineData(DriftType.Modified)]
    public void Type_ShouldAllowAllDriftTypes(DriftType driftType)
    {
        // Arrange & Act
        var drift = new PropertyDrift { Type = driftType };

        // Assert
        drift.Type.Should().Be(driftType);
    }

    [Fact]
    public void PropertyPath_CanBeSet()
    {
        // Arrange
        var drift = new PropertyDrift();

        // Act
        drift.PropertyPath = "properties.accessTier";

        // Assert
        drift.PropertyPath.Should().Be("properties.accessTier");
    }

    [Fact]
    public void ExpectedValue_CanBeSetToString()
    {
        // Arrange
        var drift = new PropertyDrift();

        // Act
        drift.ExpectedValue = "Hot";

        // Assert
        drift.ExpectedValue.Should().Be("Hot");
    }

    [Fact]
    public void ActualValue_CanBeSetToString()
    {
        // Arrange
        var drift = new PropertyDrift();

        // Act
        drift.ActualValue = "Cool";

        // Assert
        drift.ActualValue.Should().Be("Cool");
    }

    [Fact]
    public void Values_CanBeSetToComplexObjects()
    {
        // Arrange
        var drift = new PropertyDrift();
        var expectedObj = new { setting = "value" };
        var actualObj = new { setting = "different" };

        // Act
        drift.ExpectedValue = expectedObj;
        drift.ActualValue = actualObj;

        // Assert
        drift.ExpectedValue.Should().NotBeNull();
        drift.ActualValue.Should().NotBeNull();
    }

    [Fact]
    public void DriftWithAllProperties_ShouldBeComplete()
    {
        // Arrange & Act
        var drift = new PropertyDrift
        {
            PropertyPath = "properties.encrypted",
            ExpectedValue = true,
            ActualValue = false,
            Type = DriftType.Modified
        };

        // Assert
        drift.PropertyPath.Should().Be("properties.encrypted");
        drift.ExpectedValue.Should().Be(true);
        drift.ActualValue.Should().Be(false);
        drift.Type.Should().Be(DriftType.Modified);
    }
}

public class DeploymentResultTests
{
    [Fact]
    public void Constructor_ShouldInitializeWithDefaults()
    {
        // Act
        var result = new DeploymentResult();

        // Assert
        result.Success.Should().BeFalse();
        result.DeploymentName.Should().BeEmpty();
        result.ErrorMessage.Should().BeNull();
        result.DeployedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Success_ShouldBeSettable()
    {
        // Arrange
        var result = new DeploymentResult();

        // Act
        result.Success = true;

        // Assert
        result.Success.Should().BeTrue();
    }

    [Fact]
    public void DeploymentName_CanBeSet()
    {
        // Arrange
        var result = new DeploymentResult();

        // Act
        result.DeploymentName = "deploy-20260218-001";

        // Assert
        result.DeploymentName.Should().Be("deploy-20260218-001");
    }

    [Fact]
    public void ErrorMessage_CanBeSetOnFailure()
    {
        // Arrange
        var result = new DeploymentResult();

        // Act
        result.Success = false;
        result.ErrorMessage = "Resource group not found";

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Resource group not found");
    }

    [Fact]
    public void SuccessfulDeployment_ShouldHaveNoError()
    {
        // Arrange & Act
        var result = new DeploymentResult
        {
            Success = true,
            DeploymentName = "deploy-001",
            ErrorMessage = null
        };

        // Assert
        result.Success.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void DeployedAt_CanBeSet()
    {
        // Arrange
        var result = new DeploymentResult();
        var customDate = new DateTime(2026, 2, 18, 10, 30, 0);

        // Act
        result.DeployedAt = customDate;

        // Assert
        result.DeployedAt.Should().Be(customDate);
    }
}

public class DriftIgnoreConfigurationTests
{
    [Fact]
    public void Constructor_ShouldInitializeWithDefaults()
    {
        // Act
        var config = new DriftIgnoreConfiguration();

        // Assert
        config.IgnorePatterns.Should().NotBeNull();
    }

    [Fact]
    public void IgnorePatterns_ShouldBeAccessible()
    {
        // Arrange
        var config = new DriftIgnoreConfiguration();

        // Act
        var patterns = config.IgnorePatterns;

        // Assert
        patterns.Should().NotBeNull();
        patterns.Description.Should().BeEmpty();
        patterns.Resources.Should().BeEmpty();
        patterns.GlobalPatterns.Should().BeEmpty();
    }
}

public class DriftIgnorePatternsTests
{
    [Fact]
    public void Constructor_ShouldInitializeWithDefaults()
    {
        // Act
        var patterns = new DriftIgnorePatterns();

        // Assert
        patterns.Description.Should().BeEmpty();
        patterns.Resources.Should().BeEmpty();
        patterns.GlobalPatterns.Should().BeEmpty();
    }

    [Fact]
    public void Resources_CanHaveRulesAdded()
    {
        // Arrange
        var patterns = new DriftIgnorePatterns();
        var rule = new ResourceIgnoreRule
        {
            ResourceType = "Microsoft.Storage/storageAccounts",
            Reason = "Managed by third party"
        };

        // Act
        patterns.Resources.Add(rule);

        // Assert
        patterns.Resources.Should().HaveCount(1);
        patterns.Resources[0].ResourceType.Should().Be("Microsoft.Storage/storageAccounts");
    }

    [Fact]
    public void GlobalPatterns_CanBeAdded()
    {
        // Arrange
        var patterns = new DriftIgnorePatterns();
        var globalPattern = new GlobalIgnorePattern
        {
            PropertyPattern = "tags.*",
            Reason = "Tags managed separately"
        };

        // Act
        patterns.GlobalPatterns.Add(globalPattern);

        // Assert
        patterns.GlobalPatterns.Should().HaveCount(1);
        patterns.GlobalPatterns[0].PropertyPattern.Should().Be("tags.*");
    }
}

public class ResourceIgnoreRuleTests
{
    [Fact]
    public void Constructor_ShouldInitializeWithDefaults()
    {
        // Act
        var rule = new ResourceIgnoreRule();

        // Assert
        rule.ResourceType.Should().BeEmpty();
        rule.Reason.Should().BeEmpty();
        rule.IgnoredProperties.Should().BeEmpty();
        rule.Conditions.Should().BeEmpty();
    }

    [Fact]
    public void IgnoredProperties_CanBeAdded()
    {
        // Arrange
        var rule = new ResourceIgnoreRule();

        // Act
        rule.IgnoredProperties.Add("tags");
        rule.IgnoredProperties.Add("metadata");

        // Assert
        rule.IgnoredProperties.Should().HaveCount(2);
        rule.IgnoredProperties.Should().Contain("tags");
    }

    [Fact]
    public void Conditions_CanBeSet()
    {
        // Arrange
        var rule = new ResourceIgnoreRule();

        // Act
        rule.Conditions.Add("tags.managed", "true");
        rule.Conditions.Add("location", "eastus");

        // Assert
        rule.Conditions.Should().HaveCount(2);
        rule.Conditions["tags.managed"].Should().Be("true");
    }

    [Fact]
    public void CompleteIgnoreRule_ShouldHaveAllProperties()
    {
        // Arrange & Act
        var rule = new ResourceIgnoreRule
        {
            ResourceType = "Microsoft.Storage/storageAccounts",
            Reason = "Third party managed",
            IgnoredProperties = new List<string> { "tags", "metadata" },
            Conditions = new Dictionary<string, string> { { "managed", "true" } }
        };

        // Assert
        rule.ResourceType.Should().Be("Microsoft.Storage/storageAccounts");
        rule.Reason.Should().Be("Third party managed");
        rule.IgnoredProperties.Should().HaveCount(2);
        rule.Conditions.Should().HaveCount(1);
    }
}

public class GlobalIgnorePatternTests
{
    [Fact]
    public void Constructor_ShouldInitializeWithDefaults()
    {
        // Act
        var pattern = new GlobalIgnorePattern();

        // Assert
        pattern.PropertyPattern.Should().BeEmpty();
        pattern.Reason.Should().BeEmpty();
    }

    [Fact]
    public void PropertyPattern_CanBeSet()
    {
        // Arrange
        var pattern = new GlobalIgnorePattern();

        // Act
        pattern.PropertyPattern = "tags.*";

        // Assert
        pattern.PropertyPattern.Should().Be("tags.*");
    }

    [Fact]
    public void Reason_CanBeSet()
    {
        // Arrange
        var pattern = new GlobalIgnorePattern();

        // Act
        pattern.Reason = "Tags managed in separate system";

        // Assert
        pattern.Reason.Should().Be("Tags managed in separate system");
    }
}

public class EnumTests
{
    [Fact]
    public void OutputFormat_ShouldHaveAllExpectedValues()
    {
        // Arrange & Act & Assert
        var values = Enum.GetValues(typeof(OutputFormat)).Cast<OutputFormat>().ToList();
        
        values.Should().Contain(OutputFormat.Console);
        values.Should().Contain(OutputFormat.Json);
        values.Should().Contain(OutputFormat.Html);
        values.Should().Contain(OutputFormat.Markdown);
        values.Should().HaveCount(4);
    }

    [Fact]
    public void DeploymentScope_ShouldHaveAllExpectedValues()
    {
        // Arrange & Act & Assert
        var values = Enum.GetValues(typeof(DeploymentScope)).Cast<DeploymentScope>().ToList();
        
        values.Should().Contain(DeploymentScope.ResourceGroup);
        values.Should().Contain(DeploymentScope.Subscription);
        values.Should().HaveCount(2);
    }

    [Fact]
    public void DriftType_ShouldHaveAllExpectedValues()
    {
        // Arrange & Act & Assert
        var values = Enum.GetValues(typeof(DriftType)).Cast<DriftType>().ToList();
        
        values.Should().Contain(DriftType.Missing);
        values.Should().Contain(DriftType.Extra);
        values.Should().Contain(DriftType.Modified);
        values.Should().HaveCount(3);
    }
}
