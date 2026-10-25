using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.EntityFrameworkCore;

using XsltCraft.Application.DTO;
using XsltCraft.Application.Imaging;
using XsltCraft.Application.Interfaces;
using XsltCraft.Application.Preview;
using XsltCraft.Application.Themes;
using XsltCraft.Infrastructure.Persistence;
using XsltCraft.Infrastructure.Storage;

namespace XsltCraft.Api.Preview;

/// <summary>
/// Grid Image bloklarındaki <c>assetId</c>'leri storage'dan okuyup base64 data URI'ya çevirir — önizleme,
/// kaydedilmemiş grid indirmesi, kayıtlı şablon önizlemesi ve <c>TemplateController.Download</c> tek kaynaktan.
/// Görseller HER ZAMAN gömülür; generator hiçbir URL'ye düşmez — burada çözülemeyen asset için <c>&lt;img&gt;</c>
/// üretilmez.
/// <para><b>Yetki kuralı (T3.1 IDOR):</b> asset gömülür ⇔ (a) kullanıcının kendi asset'i
/// (<c>OwnerId == userId</c>) ∨ (b) kullanıcı yetkili (Editör/Admin — <c>IsPrivileged</c>) ∨ (c) asset, kullanıcının
/// <see cref="IThemeAccessGuard"/> ile erişebildiği yayınlanmış bir hazır temanın BlockTree'sindeki bir Image
/// bloğunda referanslanıyor ve temanın sahibi asset'in sahibi (ya da tema sahipsiz — admin yüklemesi). (c),
/// temadan klonlanan şablonların (Clone yalnız BlockTree'yi kopyalar; <c>assetId</c> tema sahibinde kalır)
/// görsellerini kapsar — mevcut klonlar da migration'sız düzelir. Başkasının özel asset'i gömülmez.</para>
/// </summary>
public sealed class PreviewAssetEmbedder(
    AppDbContext db, IStorageService storage, IEntitlementService entitlements, IThemeAccessGuard themeAccess)
{
    private static readonly JsonSerializerOptions ConfigJson = new() { PropertyNameCaseInsensitive = true };

    /// <summary>Kayıtlı BlockTree JSON'u (V1/V2 — ikisinde de <c>blocks</c> sözlüğü) için <see cref="BuildAsync"/>.</summary>
    public Task<Dictionary<string, string>> BuildFromJsonAsync(string? blockTreeJson, Guid userId, CancellationToken ct = default) =>
        BuildAsync(ReadBlocks(blockTreeJson), userId, ct);

    public async Task<Dictionary<string, string>> BuildAsync(
        IReadOnlyDictionary<string, BlockDto> blocks, Guid userId, CancellationToken ct = default)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var ids = ImageAssetIds(blocks.Values)
            .Select(id => (Raw: id, Ok: Guid.TryParse(id, out var g), Id: g))
            .Where(x => x.Ok)
            .ToList();
        if (ids.Count == 0)
            return result;

        var guids = ids.Select(x => x.Id).Distinct().ToList();
        var assets = await db.Assets.AsNoTracking()
            .Where(a => guids.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, ct);

        bool? privileged = null;
        Dictionary<Guid, HashSet<Guid?>>? themeRefs = null;
        foreach (var (raw, _, id) in ids)
        {
            if (!assets.TryGetValue(id, out var asset))
                continue;

            if (asset.OwnerId != userId)
            {
                privileged ??= (await entitlements.GetAsync(userId, ct)).IsPrivileged;
                if (privileged != true)
                {
                    themeRefs ??= await AccessibleThemeAssetRefsAsync(userId, ct);
                    if (!themeRefs.TryGetValue(id, out var themeOwners)
                        || !(themeOwners.Contains(null) || themeOwners.Contains(asset.OwnerId)))
                        continue;
                }
            }

            try
            {
                if (!await storage.ExistsAsync(asset.FilePath))
                    continue;

                await using var stream = await storage.ReadAsync(asset.FilePath);
                using var ms = new MemoryStream();
                await stream.CopyToAsync(ms, ct);
                result[raw] = $"data:{ImageUpload.ServeMimeType(asset.FilePath)};base64,{Convert.ToBase64String(ms.ToArray())}";
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // atla — generator bu blokta <img> üretmez
            }
        }

        return result;
    }

    /// <summary>
    /// Kullanıcının erişebildiği yayınlanmış temaların BlockTree'lerinde referanslanan asset id'leri → tema sahipleri.
    /// Tema kataloğu küçüktür; jsonb üzerinde metin araması yerine bellekte ayrıştırılır (tam id eşleşmesi).
    /// </summary>
    private async Task<Dictionary<Guid, HashSet<Guid?>>> AccessibleThemeAssetRefsAsync(Guid userId, CancellationToken ct)
    {
        var refs = new Dictionary<Guid, HashSet<Guid?>>();
        var themes = await db.Templates.AsNoTracking()
            .Where(t => t.IsFreeTheme && t.BlockTree != null)
            .ToListAsync(ct);

        foreach (var theme in themes)
        {
            if (await themeAccess.CheckAsync(theme, userId, ct) != ThemeAccessDecision.Allowed)
                continue;

            foreach (var raw in ImageAssetIds(ReadBlocks(theme.BlockTree).Values))
            {
                if (!Guid.TryParse(raw, out var assetId))
                    continue;
                if (!refs.TryGetValue(assetId, out var owners))
                    refs[assetId] = owners = [];
                owners.Add(theme.OwnerId);
            }
        }

        return refs;
    }

    private static IEnumerable<string> ImageAssetIds(IEnumerable<BlockDto> blocks) =>
        blocks
            .Where(b => b.Type == "Image" && b.Config.ValueKind == JsonValueKind.Object)
            .Select(ReadAssetId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id!)
            .Distinct(StringComparer.OrdinalIgnoreCase);

    private static IReadOnlyDictionary<string, BlockDto> ReadBlocks(string? blockTreeJson)
    {
        if (string.IsNullOrWhiteSpace(blockTreeJson))
            return new Dictionary<string, BlockDto>();
        try
        {
            return JsonSerializer.Deserialize<BlocksOnly>(blockTreeJson, ConfigJson)?.Blocks
                   ?? new Dictionary<string, BlockDto>();
        }
        catch (JsonException)
        {
            return new Dictionary<string, BlockDto>();
        }
    }

    private static string? ReadAssetId(BlockDto block)
    {
        try
        {
            return JsonSerializer.Deserialize<ImageAssetIdOnly>(block.Config.GetRawText(), ConfigJson)?.AssetId;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private sealed class BlocksOnly
    {
        [JsonPropertyName("blocks")]
        public Dictionary<string, BlockDto>? Blocks { get; set; }
    }

    private sealed class ImageAssetIdOnly
    {
        [JsonPropertyName("assetId")]
        public string? AssetId { get; set; }
    }
}
