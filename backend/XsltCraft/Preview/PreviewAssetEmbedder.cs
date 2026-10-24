using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.EntityFrameworkCore;

using XsltCraft.Application.DTO;
using XsltCraft.Application.Imaging;
using XsltCraft.Application.Interfaces;
using XsltCraft.Application.Preview;
using XsltCraft.Infrastructure.Persistence;
using XsltCraft.Infrastructure.Storage;

namespace XsltCraft.Api.Preview;

/// <summary>
/// Grid Image bloklarındaki <c>assetId</c>'leri storage'dan okuyup base64 data URI'ya çevirir
/// (önizleme + kaydedilmemiş grid indirmesi).
/// <para><b>IDOR kuralı:</b> yalnız kullanıcının kendi asset'i (<c>OwnerId == userId</c>) ya da yetkili
/// (Editör/Admin — <c>IsPrivileged</c>) kullanıcı gömülür. Başkasının asset'i sessizce atlanır; generator
/// o blokta URL yedeğine düşer (<c>/api/assets/{id}/serve</c> zaten başka sahibe 403 döner).</para>
/// </summary>
public sealed class PreviewAssetEmbedder(AppDbContext db, IStorageService storage, IEntitlementService entitlements)
{
    private static readonly JsonSerializerOptions ConfigJson = new() { PropertyNameCaseInsensitive = true };

    public async Task<Dictionary<string, string>> BuildAsync(
        IReadOnlyDictionary<string, BlockDto> blocks, Guid userId, CancellationToken ct = default)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var ids = blocks.Values
            .Where(b => b.Type == "Image" && b.Config.ValueKind == JsonValueKind.Object)
            .Select(ReadAssetId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(id => (Raw: id!, Ok: Guid.TryParse(id, out var g), Id: g))
            .Where(x => x.Ok)
            .ToList();
        if (ids.Count == 0)
            return result;

        var guids = ids.Select(x => x.Id).Distinct().ToList();
        var assets = await db.Assets.AsNoTracking()
            .Where(a => guids.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, ct);

        bool? privileged = null;
        foreach (var (raw, _, id) in ids)
        {
            if (!assets.TryGetValue(id, out var asset))
                continue;

            if (asset.OwnerId != userId)
            {
                privileged ??= (await entitlements.GetAsync(userId, ct)).IsPrivileged;
                if (privileged != true)
                    continue;
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
                // atla — generator URL yedeğine düşer
            }
        }

        return result;
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

    private sealed class ImageAssetIdOnly
    {
        [JsonPropertyName("assetId")]
        public string? AssetId { get; set; }
    }
}
