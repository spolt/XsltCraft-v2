using XsltCraft.Domain.Entities;

namespace XsltCraft.Application.ThemeSlots.Catalog;

/// <summary>Kullanıcıya sunulan yuva seçeneği.</summary>
/// <param name="Removable">Temadaki görselin yeri ve kullanıcı görseli eklemeden kaldırabilir.</param>
public sealed record ThemeSlotOptionDto(string Id, string Label, string Group, string? DefaultFor, int Order, bool Removable);

public sealed record ThemeAnchorDto(string Id, string Label, bool Found, int? Line);

public sealed record ThemeSlotReportItemDto(string Id, string Label, string Group, string? DefaultFor, int Order,
    bool Conditional, string? LegacyId, bool Inserted, int Line, bool Removable);

public sealed record ThemeSlotWarningDto(string Code, string Message, string? SlotId);

/// <param name="Role">logo | signature | design.</param>
public sealed record ThemeImageReportDto(int Index, string Role, int Line, string? SlotId, bool Removable, bool Stripped);

/// <summary>Admin'e yükleme sonrası gösterilen analiz raporu.</summary>
public sealed record ThemeSlotReportDto(string Status, DateTime AnalyzedAt, IReadOnlyList<ThemeAnchorDto> Anchors,
    IReadOnlyList<ThemeSlotReportItemDto> Slots, IReadOnlyList<string> OccupiedHolders, OverridesSummary? Overrides,
    IReadOnlyList<ThemeSlotWarningDto> Warnings, IReadOnlyList<ThemeImageReportDto> Images);

/// <summary>Saklanan kataloğu kullanıcı/admin görünümlerine çevirir (etiketler burada eklenir).</summary>
public interface IThemeSlotProjector
{
    IReadOnlyList<ThemeSlotOptionDto> ForUser(string? catalogJson, DocumentType type);
    ThemeSlotReportDto? ForAdmin(string? catalogJson, DocumentType type);
    IReadOnlyDictionary<ThemeAssetKind, string> Defaults(string? catalogJson);
    IReadOnlySet<string> ConditionalSlots(string? catalogJson);
}

public sealed class ThemeSlotProjector : IThemeSlotProjector
{
    public IReadOnlyList<ThemeSlotOptionDto> ForUser(string? catalogJson, DocumentType type) =>
        ThemeSlotCatalogJson.TryDeserialize(catalogJson)?.Slots
            .OrderBy(s => s.Order)
            .Select(s => new ThemeSlotOptionDto(s.Id, ThemeSlotLabels.Slot(s.Id, type), ThemeSlotLabels.Group(s.Id),
                KindValue(s.DefaultFor), s.Order, s.Removable))
            .ToList()
        ?? [];

    public ThemeSlotReportDto? ForAdmin(string? catalogJson, DocumentType type)
    {
        var c = ThemeSlotCatalogJson.TryDeserialize(catalogJson);
        if (c is null)
            return null;

        return new ThemeSlotReportDto(
            Status: c.Status.ToString().ToLowerInvariant(),
            AnalyzedAt: c.AnalyzedAt,
            Anchors: c.Anchors.Select(a => new ThemeAnchorDto(a.Id, ThemeSlotLabels.Anchor(a.Id, type), a.Found, a.Line)).ToList(),
            Slots: c.Slots.OrderBy(s => s.Order).Select(s => new ThemeSlotReportItemDto(s.Id, ThemeSlotLabels.Slot(s.Id, type),
                ThemeSlotLabels.Group(s.Id), KindValue(s.DefaultFor), s.Order, s.Conditional, s.LegacyId, s.Inserted, s.Line, s.Removable)).ToList(),
            OccupiedHolders: c.OccupiedHolders,
            Overrides: c.Overrides,
            Warnings: c.Warnings.Select(w => new ThemeSlotWarningDto(w.Code, w.Message, w.SlotId)).ToList(),
            Images: (c.Images ?? []).Select(i => new ThemeImageReportDto(i.Index, RoleValue(i.Role), i.Line, i.SlotId, i.Removable, i.Stripped)).ToList());
    }

    public IReadOnlyDictionary<ThemeAssetKind, string> Defaults(string? catalogJson) =>
        ThemeSlotCatalogJson.TryDeserialize(catalogJson)?.Slots
            .Where(s => s.DefaultFor is not null)
            .ToDictionary(s => s.DefaultFor!.Value, s => s.Id)
        ?? new Dictionary<ThemeAssetKind, string>();

    public IReadOnlySet<string> ConditionalSlots(string? catalogJson) =>
        ThemeSlotCatalogJson.TryDeserialize(catalogJson)?.Slots.Where(s => s.Conditional).Select(s => s.Id).ToHashSet()
        ?? [];

    public static string RoleValue(ThemeImageRole role) => role switch
    {
        ThemeImageRole.Logo => "logo",
        ThemeImageRole.Signature => "signature",
        _ => "design",
    };

    public static bool TryParseRole(string? value, out ThemeImageRole role)
    {
        role = ThemeImageRole.Design;
        switch (value?.Trim().ToLowerInvariant())
        {
            case "logo": role = ThemeImageRole.Logo; return true;
            case "signature": role = ThemeImageRole.Signature; return true;
            case "design": return true;
            default: return false;
        }
    }

    private static string? KindValue(ThemeAssetKind? kind) => kind switch
    {
        ThemeAssetKind.Logo => "logo",
        ThemeAssetKind.Signature => "signature",
        _ => null,
    };
}
