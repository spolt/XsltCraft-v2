using System.Text.Json.Serialization;

using XsltCraft.Application.ThemeSlots.Overrides;

namespace XsltCraft.Application.ThemeSlots.Compose;

/// <summary>Görsel bloğundaki JSON öğesi. Alan adları <see cref="OverridesBlockParser"/>'ın okuduklarıyla aynı; eski
/// bloklardaki <c>xs</c>/<c>keep</c> yalnız okunur, yazılmaz.</summary>
internal sealed record OverrideItem(
    [property: JsonPropertyName("k")] string Kind,
    [property: JsonPropertyName("slot")] string? Slot,
    [property: JsonPropertyName("fb")] IReadOnlyList<string> Fallback,
    [property: JsonPropertyName("src")] string Src,
    [property: JsonPropertyName("w")] int? Width,
    [property: JsonPropertyName("h")] int? Height,
    [property: JsonPropertyName("a")] string Align);

/// <summary>Dosyadaki statik görselle dolan yuvalar.</summary>
internal static class ThemeAssetSlotRules
{
    public const string SlotOccupiedCode = "slot-occupied";

    public static ThemeComposeWarning SlotOccupied(ThemeAssetKind kind) =>
        new(SlotOccupiedCode, $"{ThemeAssetComposer.KindLabel(kind)} için seçilen konumda dosyadaki bir görsel var; bu konum atlandı.", kind);
}
