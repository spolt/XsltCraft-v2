namespace XsltCraft.Domain.Entities;

public class FeatureFlag
{
    public string Key { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public string? Value { get; set; }
    public DateTime UpdatedAt { get; set; }
}
