using System.Diagnostics;

namespace XsltCraft.Infrastructure.Xslt.Worker;

/// <summary>Bir worker süreci için başlatma bilgisi üretir (her çağrı yeni, boş çalışma dizinleri hazırlar).</summary>
public interface IWorkerProcessStartInfoFactory
{
    WorkerLaunchSpec Create();
}

/// <summary>
/// Başlatma bilgisi + worker'a özel boş dizinler (çıkışta silinir).
/// </summary>
/// <param name="StartInfo">Ortamı temizlenmiş, stdio yönlendirilmiş başlatma bilgisi.</param>
/// <param name="WorkingDirectory">Boş CWD (göreli URI'ler hiçbir şeye ulaşmasın).</param>
/// <param name="TempDirectory">TMPDIR/HOME/TEMP/TMP için boş dizin.</param>
public sealed record WorkerLaunchSpec(ProcessStartInfo StartInfo, string WorkingDirectory, string TempDirectory);
