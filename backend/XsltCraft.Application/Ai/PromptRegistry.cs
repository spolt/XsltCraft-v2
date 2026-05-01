using System.Collections.ObjectModel;
using System.Reflection;
using System.Text.RegularExpressions;

namespace XsltCraft.Application.Ai;

public record PromptPattern(string Id, IReadOnlyList<string> Triggers, string Content);

public static class PromptRegistry
{
    private static readonly Assembly Asm = typeof(PromptRegistry).Assembly;
    private static readonly string AsmName = Asm.GetName().Name!;

    private static string? _identity;
    private static string? _constraints;
    private static IReadOnlyDictionary<string, PromptPattern>? _patterns;
    private static readonly object PatternsLock = new();

    public static string Identity => _identity ??= LoadCore("Identity.md");
    public static string Constraints => _constraints ??= LoadCore("Constraints.md");

    public static IReadOnlyDictionary<string, PromptPattern> Patterns
    {
        get
        {
            if (_patterns is not null) return _patterns;
            lock (PatternsLock)
            {
                if (_patterns is not null) return _patterns;
                _patterns = BuildPatternDictionary();
            }
            return _patterns;
        }
    }

    private static string LoadCore(string fileName)
    {
        var resourceName = $"{AsmName}.Prompts.Core.{fileName}";
        using var stream = Asm.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource not found: {resourceName}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static IReadOnlyDictionary<string, PromptPattern> BuildPatternDictionary()
    {
        var prefix = $"{AsmName}.Prompts.Patterns.";
        var dict = new Dictionary<string, PromptPattern>();

        foreach (var name in Asm.GetManifestResourceNames())
        {
            if (!name.StartsWith(prefix, StringComparison.Ordinal) || !name.EndsWith(".md", StringComparison.Ordinal))
                continue;

            using var stream = Asm.GetManifestResourceStream(name)!;
            using var reader = new StreamReader(stream);
            var pattern = ParsePattern(reader.ReadToEnd());
            dict[pattern.Id] = pattern;
        }

        return new ReadOnlyDictionary<string, PromptPattern>(dict);
    }

    private static readonly Regex FrontMatterRe = new(
        @"^---\r?\n(.*?)\r?\n---\r?\n(.*)",
        RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex IdRe = new(
        @"^id:\s*(.+)$",
        RegexOptions.Multiline | RegexOptions.Compiled);

    private static readonly Regex TriggersRe = new(
        @"^triggers:\s*\[([^\]]*)\]",
        RegexOptions.Multiline | RegexOptions.Compiled);

    private static PromptPattern ParsePattern(string raw)
    {
        var m = FrontMatterRe.Match(raw);
        if (!m.Success)
            throw new InvalidOperationException("Pattern file is missing YAML front-matter.");

        var frontMatter = m.Groups[1].Value;
        var content = m.Groups[2].Value.TrimStart('\r', '\n');

        var idMatch = IdRe.Match(frontMatter);
        if (!idMatch.Success)
            throw new InvalidOperationException("Pattern file is missing 'id' field in front-matter.");
        var id = idMatch.Groups[1].Value.Trim();

        var triggersMatch = TriggersRe.Match(frontMatter);
        string[] triggers = triggersMatch.Success
            ? triggersMatch.Groups[1].Value.Split(',')
                .Select(t => t.Trim())
                .Where(t => t.Length > 0)
                .ToArray()
            : [];

        return new PromptPattern(id, triggers, content);
    }
}
