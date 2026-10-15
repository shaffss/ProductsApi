using System.Globalization;
using System.Text.RegularExpressions;

namespace Products.UnitTests;

// Baza wiedzy docs/knowledge (Open Knowledge Format v0.2): zgodność ze specyfikacją i aktualność odwołań -
// zmiana nazwy pliku w kodzie bez poprawki dokumentacji kończy się błędem testu
public sealed partial class KnowledgeBundleTests
{
    private static readonly string Bundle = FindBundle();

    public static TheoryData<string> Concepts() => Documents(isConcept: true);

    public static TheoryData<string> AllDocuments() => Documents(isConcept: null);

    [Theory]
    [MemberData(nameof(Concepts))]
    public void Concept_has_parseable_frontmatter_with_type(string document)
    {
        var frontmatter = Frontmatter(Read(document));

        Assert.NotNull(frontmatter);
        Assert.Matches(TypeKey(), frontmatter);
        // wartość bez cudzysłowu z ": " lub " #" YAML odczyta jako mapowanie lub komentarz
        Assert.All(PlainValues().Matches(frontmatter).Select(m => m.Groups[1].Value), value =>
            Assert.False(value.Contains(": ", StringComparison.Ordinal) || value.Contains(" #", StringComparison.Ordinal), value));
    }

    [Theory]
    [MemberData(nameof(AllDocuments))]
    public void Links_and_resources_point_to_existing_paths(string document)
    {
        var text = Read(document);
        var missing = Link().Matches(CodeBlock().Replace(text, ""))
            .Concat(Resource().Matches(Frontmatter(text) ?? ""))
            .Select(m => m.Groups[1].Value)
            .Where(target => !target.Contains("://", StringComparison.Ordinal) && !target.StartsWith('#'))
            .Where(target => !Exists(document, target))
            .ToList();

        Assert.Empty(missing);
    }

    // index.md bez nagłówka YAML - wyjątek: okf_version w katalogu głównym bazy (§8)
    [Fact]
    public void Index_files_have_no_frontmatter_except_bundle_version()
    {
        foreach (var index in Directory.EnumerateFiles(Bundle, "index.md", SearchOption.AllDirectories))
        {
            var frontmatter = Frontmatter(File.ReadAllText(index));
            if (Path.GetDirectoryName(index) == Bundle)
                Assert.Equal("okf_version: \"0.2\"", frontmatter);
            else
                Assert.Null(frontmatter);
        }
    }

    // log.md: wpisy pogrupowane datami YYYY-MM-DD, najnowsze na górze (§9)
    [Fact]
    public void Log_entries_are_grouped_by_date_newest_first()
    {
        foreach (var log in Directory.EnumerateFiles(Bundle, "log.md", SearchOption.AllDirectories))
        {
            var dates = DateHeading().Matches(File.ReadAllText(log))
                .Select(m => DateOnly.ParseExact(m.Groups[1].Value, "yyyy-MM-dd", CultureInfo.InvariantCulture))
                .ToList();

            Assert.NotEmpty(dates);
            Assert.Equal(dates.Distinct().OrderDescending(), dates);
        }
    }

    // isConcept: true - pojęcia (wszystko poza index.md i log.md), null - wszystkie pliki .md
    private static TheoryData<string> Documents(bool? isConcept)
    {
        var data = new TheoryData<string>();
        foreach (var file in Directory.EnumerateFiles(Bundle, "*.md", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            var reserved = Path.GetFileName(file) is "index.md" or "log.md";
            if (isConcept is null || isConcept != reserved)
                data.Add(Path.GetRelativePath(Bundle, file).Replace('\\', '/'));
        }
        return data;
    }

    private static string Read(string document) => File.ReadAllText(Path.Combine(Bundle, document));

    private static string? Frontmatter(string text) => FrontmatterBlock().Match(text) is { Success: true } match
        ? match.Groups[1].Value.ReplaceLineEndings("\n")
        : null;

    // linki "/..." względem katalogu głównego bazy, pozostałe względem dokumentu; ścieżki mogą wychodzić poza bazę (kod)
    private static bool Exists(string document, string target)
    {
        var path = target.Split('#')[0];
        var fullPath = path.StartsWith('/')
            ? Path.Combine(Bundle, path[1..])
            : Path.Combine(Path.GetDirectoryName(Path.Combine(Bundle, document))!, path);
        return File.Exists(fullPath) || Directory.Exists(fullPath);
    }

    private static string FindBundle()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var bundle = Path.Combine(directory.FullName, "docs", "knowledge");
            if (File.Exists(Path.Combine(bundle, "index.md")))
                return bundle;
        }
        throw new DirectoryNotFoundException($"docs/knowledge not found above {AppContext.BaseDirectory}");
    }

    [GeneratedRegex(@"\A---\r?\n(.*?)\r?\n---\r?\n", RegexOptions.Singleline)]
    private static partial Regex FrontmatterBlock();

    [GeneratedRegex(@"^type:[ \t]*\S", RegexOptions.Multiline)]
    private static partial Regex TypeKey();

    // wartości kluczy najwyższego poziomu bez cudzysłowu, listy i mapowania w nawiasach
    [GeneratedRegex(@"^[A-Za-z_]+:[ \t]+([^""'\[{\s].*)$", RegexOptions.Multiline)]
    private static partial Regex PlainValues();

    [GeneratedRegex(@"^resource:[ \t]*(\S+)[ \t]*$", RegexOptions.Multiline)]
    private static partial Regex Resource();

    [GeneratedRegex(@"\]\(([^)\s]+)\)")]
    private static partial Regex Link();

    [GeneratedRegex(@"^```.*?^```", RegexOptions.Singleline | RegexOptions.Multiline)]
    private static partial Regex CodeBlock();

    [GeneratedRegex(@"^## (\d{4}-\d{2}-\d{2})[ \t]*$", RegexOptions.Multiline)]
    private static partial Regex DateHeading();
}
