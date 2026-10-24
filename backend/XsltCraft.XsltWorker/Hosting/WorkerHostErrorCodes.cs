namespace XsltCraft.XsltWorker.Hosting;

/// <summary>
/// Worker'ın motor yolunda ürettiği <c>ResponseFailure.ErrorCode</c> değerleri (Contracts'takilere ek;
/// havuz bunları yorumlamaz, yalnız iletir). Güvenlik/teşhis kodları (XsltSafety, Saxon) olduğu gibi geçer.
/// </summary>
internal static class WorkerHostErrorCodes
{
    /// <summary>Beklenmeyen .NET/Java hatası; ayrıntı sızdırılmaz.</summary>
    public const string Internal = "worker.internal";

    /// <summary>Gövde sayısı/kodlaması op ile uyuşmuyor.</summary>
    public const string BadRequest = "worker.bad_request";

    /// <summary>İstekteki <c>xsltSha256</c> gövdenin özetiyle eşleşmiyor (bozulma / protokol hatası).</summary>
    public const string Sha256Mismatch = "worker.sha256_mismatch";

    /// <summary>İşbirlikçi süre, dönüşüm başlamadan (ör. derleme sırasında) doldu.</summary>
    public const string CooperativeDeadline = "worker.cooperative_deadline";

    /// <summary>.NET yığın derinliği koruması (<see cref="InsufficientExecutionStackException"/>).</summary>
    public const string StackLimit = "worker.stack_limit";

    /// <summary>Çıktı geçerli UTF-8'e kodlanamadı (eşleşmemiş vekil karakter).</summary>
    public const string InvalidOutput = "worker.invalid_output";
}
