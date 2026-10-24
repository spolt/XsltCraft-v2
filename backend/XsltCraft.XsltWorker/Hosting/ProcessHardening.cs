namespace XsltCraft.XsltWorker.Hosting;

/// <summary>Süreç düzeyi sertleştirme (açılışta, istek işlemeden önce).</summary>
internal static class ProcessHardening
{
    public static void Apply()
    {
        PreferOomKill();
    }

    /// <summary>
    /// Linux: bellek baskısında çekirdek OOM katili Api yerine önce worker'ı seçsin.
    /// Değeri yükseltmek ayrıcalık gerektirmez; hata (konteyner kısıtı vb.) yutulur.
    /// </summary>
    private static void PreferOomKill()
    {
        if (!OperatingSystem.IsLinux())
            return;

        try
        {
            File.WriteAllText("/proc/self/oom_score_adj", "1000");
        }
        catch (Exception)
        {
            // En iyi çaba: yazılamazsa Api tarafı bellek bekçisi yine geçerli.
        }
    }
}
