using Exsensic.Contracts.Enums;
using Exsensic.Core.Requirements;

namespace Exsensic.UnitTests.Requirements;

/// <summary>
/// Checks the requirement templates and every rule in RequirementValidator: unknown keys, required
/// fields, select options, number ranges, lengths and URLs.
/// </summary>
public class RequirementValidatorTests
{
    /// <summary>A complete, valid set of Photoshoot answers that each test changes one thing in.</summary>
    private static Dictionary<string, string> ValidPhotoshoot() => new()
    {
        ["shootType"] = "Product",
        ["location"] = "Studio",
        ["quantity"] = "25",
        ["description"] = "Clean white-background shots of our new range for the online store.",
    };

    /// <summary>Every contract category has a template, and each one asks for a required description.</summary>
    [Theory]
    [InlineData(ServiceCategory.Photoshoot)]
    [InlineData(ServiceCategory.Website)]
    [InlineData(ServiceCategory.Instagram)]
    [InlineData(ServiceCategory.TikTok)]
    public void For_EveryCategory_HasRequiredDescription(ServiceCategory category)
    {
        var template = RequirementTemplates.For(category);

        Assert.Equal(category, template.Category);
        Assert.Contains(template.Fields, f => f.Key == "description" && f.Required);
    }

    /// <summary>The Photoshoot template matches the brief exactly.</summary>
    [Fact]
    public void For_Photoshoot_MatchesBrief()
    {
        var fields = RequirementTemplates.For(ServiceCategory.Photoshoot).Fields.ToDictionary(f => f.Key);

        Assert.Equal(["Product", "Team", "Brand/lifestyle"], fields["shootType"].Options);
        Assert.Equal(["Studio", "On location"], fields["location"].Options);
        Assert.Equal(1m, fields["quantity"].Min);
        Assert.Equal(500m, fields["quantity"].Max);
        Assert.Equal(1000, fields["description"].MaxLength);
        Assert.Equal(500, fields["notes"].MaxLength);
        Assert.False(fields["notes"].Required);
    }

    /// <summary>A complete, valid set of answers produces no errors.</summary>
    [Fact]
    public void Validate_ValidAnswers_ReturnsNoErrors()
    {
        var errors = RequirementValidator.Validate(ServiceCategory.Photoshoot, ValidPhotoshoot());

        Assert.Empty(errors);
    }

    /// <summary>A key that isn't in the template is rejected, not silently ignored.</summary>
    [Fact]
    public void Validate_UnknownKey_ReturnsErrorForThatKey()
    {
        var answers = ValidPhotoshoot();
        answers["budget"] = "5000";

        var errors = RequirementValidator.Validate(ServiceCategory.Photoshoot, answers);

        Assert.Single(errors);
        Assert.True(errors.ContainsKey("budget"));
    }

    /// <summary>A missing or blank required answer is reported against that field.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_RequiredFieldMissingOrBlank_ReturnsError(string? description)
    {
        var answers = ValidPhotoshoot();
        if (description is null)
        {
            answers.Remove("description");
        }
        else
        {
            answers["description"] = description;
        }

        var errors = RequirementValidator.Validate(ServiceCategory.Photoshoot, answers);

        Assert.True(errors.ContainsKey("description"));
    }

    /// <summary>Leaving an optional field out is fine.</summary>
    [Fact]
    public void Validate_OptionalFieldMissing_ReturnsNoErrors()
    {
        var answers = ValidPhotoshoot();
        answers.Remove("notes");

        Assert.Empty(RequirementValidator.Validate(ServiceCategory.Photoshoot, answers));
    }

    /// <summary>A select answer must match one of the options exactly, including case.</summary>
    [Theory]
    [InlineData("Wedding")]
    [InlineData("product")]
    public void Validate_SelectValueNotInOptions_ReturnsError(string shootType)
    {
        var answers = ValidPhotoshoot();
        answers["shootType"] = shootType;

        var errors = RequirementValidator.Validate(ServiceCategory.Photoshoot, answers);

        Assert.True(errors.ContainsKey("shootType"));
    }

    /// <summary>Numbers outside the range, decimals and text are all rejected.</summary>
    [Theory]
    [InlineData("0")]
    [InlineData("501")]
    [InlineData("-3")]
    [InlineData("2.5")]
    [InlineData("ten")]
    public void Validate_InvalidQuantity_ReturnsError(string quantity)
    {
        var answers = ValidPhotoshoot();
        answers["quantity"] = quantity;

        var errors = RequirementValidator.Validate(ServiceCategory.Photoshoot, answers);

        Assert.True(errors.ContainsKey("quantity"));
    }

    /// <summary>The range limits themselves are allowed.</summary>
    [Theory]
    [InlineData("1")]
    [InlineData("500")]
    public void Validate_QuantityAtRangeLimit_ReturnsNoErrors(string quantity)
    {
        var answers = ValidPhotoshoot();
        answers["quantity"] = quantity;

        Assert.Empty(RequirementValidator.Validate(ServiceCategory.Photoshoot, answers));
    }

    /// <summary>Text longer than the field's maximum is rejected; text exactly at the maximum is allowed.</summary>
    [Theory]
    [InlineData(1000, false)]
    [InlineData(1001, true)]
    public void Validate_DescriptionLength_EnforcesMaximum(int length, bool expectError)
    {
        var answers = ValidPhotoshoot();
        answers["description"] = new string('a', length);

        var errors = RequirementValidator.Validate(ServiceCategory.Photoshoot, answers);

        Assert.Equal(expectError, errors.ContainsKey("description"));
    }

    /// <summary>Only absolute http and https addresses are accepted.</summary>
    [Theory]
    [InlineData("https://example.com", false)]
    [InlineData("http://example.com/about", false)]
    [InlineData("example.com", true)]
    [InlineData("javascript:alert(1)", true)]
    [InlineData("ftp://example.com", true)]
    public void Validate_WebsiteUrl_AcceptsOnlyHttpAndHttps(string url, bool expectError)
    {
        var answers = new Dictionary<string, string>
        {
            ["projectType"] = "Redesign",
            ["currentWebsite"] = url,
            ["description"] = "Refresh the look and add online booking.",
        };

        var errors = RequirementValidator.Validate(ServiceCategory.Website, answers);

        Assert.Equal(expectError, errors.ContainsKey("currentWebsite"));
    }

    /// <summary>No answers at all reports every required field, so the client sees them all at once.</summary>
    [Fact]
    public void Validate_NullAnswers_ReturnsErrorForEveryRequiredField()
    {
        var errors = RequirementValidator.Validate(ServiceCategory.Photoshoot, null);

        Assert.Equal(["description", "location", "quantity", "shootType"], errors.Keys.Order());
    }
}
