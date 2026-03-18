using System.IO;
using XsltCraft.Application.Interfaces;

public class TemplateHotReloadService
{
    private readonly FileSystemWatcher _watcher;
    private readonly ITemplateCache _cache;

    public TemplateHotReloadService(ITemplateCache cache)
    {
        _cache = cache;

        _watcher = new FileSystemWatcher("templates")
        {
            Filter = "*.xslt",
            IncludeSubdirectories = true,
            EnableRaisingEvents = true
        };

        _watcher.Changed += OnChanged;
    }

    private void OnChanged(object sender, FileSystemEventArgs e)
    {
        var id = Path.GetFileName(Path.GetDirectoryName(e.FullPath));

        _cache.Reload(id, e.FullPath);
    }
}