using System.Text.Json;
using System.Text.Json.Serialization;

namespace XsltCraft.Application.ThemeSlots.Catalog;

/// <summary><c>Template.SlotCatalog</c> (jsonb) serileştirmesi: camelCase, enum'lar kebab-case.</summary>
public static class ThemeSlotCatalogJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.KebabCaseLower) },
    };

    public static string Serialize(ThemeSlotCatalog catalog) => JsonSerializer.Serialize(catalog, Options);

    public static ThemeSlotCatalog? TryDeserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;
        try
        {
            return JsonSerializer.Deserialize<ThemeSlotCatalog>(json, Options);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
