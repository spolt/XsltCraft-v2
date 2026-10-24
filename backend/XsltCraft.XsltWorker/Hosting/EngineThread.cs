using System.Collections.Concurrent;

namespace XsltCraft.XsltWorker.Hosting;

/// <summary>
/// Tüm motor işinin (Saxon/.NET derleme DAHİL, dönüşüm, ısınma, test op'ları) koştuğu tek, uzun ömürlü,
/// <see cref="StackSizeBytes"/> yığınlı thread. Saxon <c>Isolated</c> profilinin trace derinlik sınırı (1000) bu
/// yığına göre seçildi; derleme anı özyinelemesi (sınırsız iç içe ifade) de burada koşar — yığın taşarsa süreç
/// ölür, Api öldürüp yeniden başlatır. Ana döngü (stdin/stdout) ayrı thread'de kalır, iş verip sonucu bekler.
/// Kuyruk tek tüketicili; worker zaten aynı anda tek istek alır.
/// </summary>
internal sealed class EngineThread : IDisposable
{
    public const int StackSizeBytes = 64 * 1024 * 1024;
    public const string ThreadName = "xslt-engine";

    private readonly BlockingCollection<IWorkItem> _queue = new();
    private readonly Thread _thread;

    public EngineThread()
    {
        _thread = new Thread(Loop, StackSizeBytes)
        {
            IsBackground = true, // süreç çıkışını bekletmez
            Name = ThreadName,
        };
        _thread.Start();
    }

    /// <summary>İşi motor thread'ine kuyruklar; sonuç/istisna dönen görevde.</summary>
    public Task<T> RunAsync<T>(Func<T> work)
    {
        ArgumentNullException.ThrowIfNull(work);
        var item = new WorkItem<T>(work);
        _queue.Add(item);
        return item.Task;
    }

    /// <summary>İşi motor thread'inde koşturur ve bitmesini bekler (açılış/ısınma).</summary>
    public T Run<T>(Func<T> work) => RunAsync(work).GetAwaiter().GetResult();

    public void Dispose() => _queue.CompleteAdding();

    private void Loop()
    {
        foreach (var item in _queue.GetConsumingEnumerable())
            item.Execute();
    }

    private interface IWorkItem
    {
        void Execute();
    }

    private sealed class WorkItem<T>(Func<T> work) : IWorkItem
    {
        // Devam kodu motor thread'inde koşmasın: ana döngü thread havuzunda sürer.
        private readonly TaskCompletionSource<T> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<T> Task => _completion.Task;

        public void Execute()
        {
            try
            {
                _completion.SetResult(work());
            }
            catch (Exception ex)
            {
                _completion.SetException(ex);
            }
        }
    }
}
