using XsltCraft.Application.Ai;

namespace XsltCraft.Application.Tests.Ai;

public class PromptRegistryTests
{
    [Fact]
    public void Identity_IsNotEmpty()
        => Assert.NotEmpty(PromptRegistry.Identity);

    [Fact]
    public void Constraints_IsNotEmpty()
        => Assert.NotEmpty(PromptRegistry.Constraints);

    [Theory]
    [InlineData("invoice-note")]
    [InlineData("supplier-party-person")]
    [InlineData("customer-party-person")]
    [InlineData("supplier-party-address")]
    [InlineData("customer-party-address")]
    [InlineData("invoice-header")]
    [InlineData("invoice-line")]
    [InlineData("legal-monetary-total")]
    public void Pattern_LoadsWithNonEmptyContentAndTriggers(string patternId)
    {
        Assert.True(PromptRegistry.Patterns.ContainsKey(patternId),
            $"Pattern '{patternId}' embedded resource'dan yüklenemedi.");

        var p = PromptRegistry.Patterns[patternId];
        Assert.NotEmpty(p.Content);
        Assert.NotEmpty(p.Triggers);
    }

    [Fact]
    public void Patterns_ContainsAllEightPatterns()
        => Assert.Equal(8, PromptRegistry.Patterns.Count);
}
