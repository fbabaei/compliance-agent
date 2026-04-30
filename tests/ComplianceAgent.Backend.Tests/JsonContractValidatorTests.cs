using System.Text.Json;
using Xunit;

namespace ComplianceAgent.Backend.Tests;

public class JsonContractValidatorTests
{
    [Fact]
    public void Validate_ReturnsValid_ForExpectedShape()
    {
        var json = """
        {
          "arrangementId": null,
          "country": "Germany",
          "entities": ["ABC GmbH"],
          "description": "Financing agreement",
          "transactionType": null,
          "status": "draft"
        }
        """;

        var result = JsonContractValidator.Validate(json);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Validate_ReturnsInvalid_ForUnexpectedProperty()
    {
        var json = """
        {
          "arrangementId": null,
          "country": null,
          "entities": [],
          "description": null,
          "transactionType": null,
          "status": "draft",
          "extra": "not-allowed"
        }
        """;

        var result = JsonContractValidator.Validate(json);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Unexpected property 'extra'"));
    }

    [Fact]
    public void Validate_ReturnsInvalid_ForWrongEntitiesType()
    {
        var json = """
        {
          "arrangementId": null,
          "country": null,
          "entities": "ABC GmbH",
          "description": null,
          "transactionType": null,
          "status": "draft"
        }
        """;

        var result = JsonContractValidator.Validate(json);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Property 'entities' must be an array."));
    }

    [Fact]
    public void Validate_ReturnsInvalid_ForMalformedJson()
    {
        const string json = "{ \"arrangementId\": 1, ";

        var result = JsonContractValidator.Validate(json);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.StartsWith("Invalid JSON:", StringComparison.Ordinal));
    }
}
