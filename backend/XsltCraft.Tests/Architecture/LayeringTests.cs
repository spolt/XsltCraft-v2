using System.Runtime.CompilerServices;
using System.Xml.Linq;

namespace XsltCraft.Tests.Architecture;

/// <summary>
/// Katman ve tedarik zinciri kuralları (ADR 008 §1, §9) — csproj ve kaynak dosyaları okunarak bloke edilir:
/// IKVM/Maven yalnız XsltCraft.Xslt.Saxon'da; Application/Domain/Contracts Saxon'a bağlı değil; Xslt.Saxon yalnız
/// Application'a bağlı; Worker ↛ Infrastructure; Api ↛ Xslt.Saxon (doğrudan); <c>net.sf.saxon</c> yalnız
/// Xslt.Saxon kaynaklarında (test projeleri hariç); Saxon-HE 10.9 NuGet paketi hiçbir yerde yok.
/// </summary>
public sealed class LayeringTests
{
    private const string SaxonProject = "XsltCraft.Xslt.Saxon";

    private static readonly string[] SaxonFreeProjects =
        ["XsltCraft.Application", "XsltCraft.Domain", "XsltCraft.XsltWorker.Contracts"];

    private static readonly Lazy<string> Backend = new(() => FindBackendDir());

    [Fact]
    public void MavenAndIkvm_OnlyInXsltSaxon()
    {
        foreach (var (name, doc) in Projects())
        {
            var maven = doc.Descendants("MavenReference").Count();
            var ikvm = PackageReferences(doc).Count(p => p.StartsWith("IKVM", StringComparison.OrdinalIgnoreCase));
            var ikvmItems = doc.Descendants("IkvmReference").Count();

            if (name == SaxonProject)
            {
                Assert.True(maven > 0, "Xslt.Saxon MavenReference bildirmeli");
                Assert.Contains("IKVM.Maven.Sdk", PackageReferences(doc));
            }
            else
            {
                Assert.True(maven == 0, $"{name}: MavenReference yalnız {SaxonProject}'ta olabilir");
                Assert.True(ikvm == 0, $"{name}: IKVM PackageReference yalnız {SaxonProject}'ta olabilir");
                Assert.True(ikvmItems == 0, $"{name}: IkvmReference yalnız {SaxonProject}'ta olabilir");
            }
        }
    }

    [Fact]
    public void ApplicationDomainContracts_HaveNoSaxonDependency()
    {
        var projects = Projects().ToDictionary(p => p.Name, p => p.Doc);
        foreach (var name in SaxonFreeProjects)
        {
            Assert.True(projects.ContainsKey(name), $"{name}.csproj bulunamadı");
            var doc = projects[name];
            Assert.DoesNotContain(ProjectReferences(doc), r => r is SaxonProject or "XsltCraft.Infrastructure" or "XsltCraft.XsltWorker");
            Assert.DoesNotContain(PackageReferences(doc), p => p.Contains("Saxon", StringComparison.OrdinalIgnoreCase) || p.StartsWith("IKVM", StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void XsltSaxon_ReferencesOnlyApplication()
    {
        var doc = Projects().Single(p => p.Name == SaxonProject).Doc;
        Assert.Equal(["XsltCraft.Application"], ProjectReferences(doc));
        Assert.Equal("true", doc.Descendants("RestorePackagesWithLockFile").SingleOrDefault()?.Value);
        Assert.True(File.Exists(Path.Combine(Backend.Value, SaxonProject, "packages.lock.json")), "packages.lock.json yok");

        var hashes = File.ReadAllLines(Path.Combine(Backend.Value, SaxonProject, "maven-hashes.txt"))
            .Where(l => l.Length > 0 && !l.StartsWith('#')).ToList();
        Assert.Contains(hashes, l => l.Contains("Saxon-HE-12.10.jar", StringComparison.Ordinal));
        Assert.All(hashes, l => Assert.Matches("^[0-9a-f]{64} ", l));
    }

    [Fact]
    public void Worker_DoesNotReferenceInfrastructure()
    {
        var doc = Projects().Single(p => p.Name == "XsltCraft.XsltWorker").Doc;
        Assert.DoesNotContain("XsltCraft.Infrastructure", ProjectReferences(doc));
        Assert.DoesNotContain("XsltCraft.Api", ProjectReferences(doc));
    }

    [Fact]
    public void Api_DoesNotReferenceXsltSaxonDirectly()
    {
        var doc = Projects().Single(p => p.Name == "XsltCraft.Api").Doc;
        Assert.DoesNotContain(SaxonProject, ProjectReferences(doc));
    }

    [Fact]
    public void NetSfSaxon_OnlyInXsltSaxonSources()
    {
        var offenders = new List<string>();
        foreach (var dir in Directory.EnumerateDirectories(Backend.Value))
        {
            var project = Path.GetFileName(dir);
            if (project == SaxonProject || project.EndsWith(".Tests", StringComparison.Ordinal) || project is "spike" or "build")
                continue;

            foreach (var file in Directory.EnumerateFiles(dir, "*.cs", SearchOption.AllDirectories))
            {
                if (IsBuildOutput(file))
                    continue;
                if (File.ReadAllText(file).Contains("net.sf.saxon", StringComparison.Ordinal))
                    offenders.Add(Path.GetRelativePath(Backend.Value, file));
            }
        }

        Assert.True(offenders.Count == 0, "net.sf.saxon yalnız XsltCraft.Xslt.Saxon/ kaynaklarında: " + string.Join(", ", offenders));
    }

    [Fact]
    public void NoSaxon10_InAnyCsproj()
    {
        foreach (var (name, doc) in Projects())
        {
            // Saxon-HE 10.9 NuGet paketi (IKVM 8.1 / .NET Framework) net10'da çalışmıyor (ADR 007).
            Assert.DoesNotContain(PackageReferences(doc), p => p.Equals("Saxon-HE", StringComparison.OrdinalIgnoreCase));

            foreach (var maven in doc.Descendants("MavenReference"))
            {
                var include = (string?)maven.Attribute("Include") ?? string.Empty;
                var version = (string?)maven.Attribute("Version") ?? maven.Element("Version")?.Value;
                if (include.Contains("Saxon-HE", StringComparison.OrdinalIgnoreCase))
                    Assert.True(version == "12.10", $"{name}: Saxon-HE sürümü 12.10'a sabit olmalı (bulunan {version})");
            }
        }
    }

    // ---- yardımcılar --------------------------------------------------------------------------------

    private static IEnumerable<(string Name, XDocument Doc)> Projects()
    {
        foreach (var csproj in Directory.EnumerateFiles(Backend.Value, "*.csproj", SearchOption.AllDirectories))
        {
            if (IsBuildOutput(csproj))
                continue;
            yield return (Path.GetFileNameWithoutExtension(csproj), XDocument.Load(csproj));
        }
    }

    private static List<string> ProjectReferences(XDocument doc) =>
        doc.Descendants("ProjectReference")
            .Select(e => Path.GetFileNameWithoutExtension(((string?)e.Attribute("Include") ?? string.Empty).Replace('\\', '/')))
            .ToList();

    private static List<string> PackageReferences(XDocument doc) =>
        doc.Descendants("PackageReference").Select(e => (string?)e.Attribute("Include") ?? string.Empty).ToList();

    private static bool IsBuildOutput(string path)
    {
        var sep = Path.DirectorySeparatorChar;
        return path.Contains($"{sep}bin{sep}", StringComparison.OrdinalIgnoreCase) ||
               path.Contains($"{sep}obj{sep}", StringComparison.OrdinalIgnoreCase);
    }

    // artifacts-path ile çıktı repodan ayrı olabilir: önce derleme anındaki kaynak yolu, sonra çıktı/CWD'den yukarı.
    private static string FindBackendDir([CallerFilePath] string sourceFile = "")
    {
        var candidates = new[] { Path.GetDirectoryName(sourceFile), AppContext.BaseDirectory, Directory.GetCurrentDirectory() };
        foreach (var start in candidates)
        {
            for (var dir = string.IsNullOrEmpty(start) ? null : new DirectoryInfo(start); dir is not null; dir = dir.Parent)
            {
                if (File.Exists(Path.Combine(dir.FullName, "XsltCraft.slnx")))
                    return dir.FullName;
                var nested = Path.Combine(dir.FullName, "backend");
                if (File.Exists(Path.Combine(nested, "XsltCraft.slnx")))
                    return nested;
            }
        }

        throw new InvalidOperationException("backend/XsltCraft.slnx bulunamadı.");
    }
}
