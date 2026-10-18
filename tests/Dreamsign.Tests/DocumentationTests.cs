using Xunit;
using System.Text.RegularExpressions;

namespace Dreamsign.Tests;

public sealed class DocumentationTests
{
    [Fact]
    public void LocalMarkdownLinksResolveToExistingFiles()
    {
        var root = ArchitectureTests.RepositoryRoot();
        var documents = Directory.EnumerateFiles(root, "*.md")
            .Concat(Directory.EnumerateFiles(Path.Combine(root, "docs"), "*.md", SearchOption.AllDirectories));
        var missing = new List<string>();

        foreach (var document in documents)
        {
            var markdown = File.ReadAllText(document);
            foreach (Match link in Regex.Matches(markdown, @"\[[^\]]*\]\(([^\s)]+)\)", RegexOptions.None, TimeSpan.FromSeconds(2)))
            {
                var target = link.Groups[1].Value;
                if (target.StartsWith('#') || Uri.TryCreate(target, UriKind.Absolute, out _))
                    continue;

                var relative = Uri.UnescapeDataString(target.Split('#')[0]);
                var path = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(document)!, relative));
                if (!File.Exists(path) && !Directory.Exists(path))
                    missing.Add(Path.GetRelativePath(root, document) + " -> " + target);
            }
        }

        Assert.Empty(missing);
    }
}
