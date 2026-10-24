using System.Text.Json.Serialization;

namespace XsltCraft.XsltWorker.Contracts.Messages;

/// <summary>Başlık JSON'u için source-gen bağlamı (yansıma yok, trim/AOT dostu).</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip)]
[JsonSerializable(typeof(HelloHeader))]
[JsonSerializable(typeof(RequestHeader))]
[JsonSerializable(typeof(ResponseHeader))]
[JsonSerializable(typeof(ShutdownHeader))]
public sealed partial class WorkerJsonContext : JsonSerializerContext
{
}
