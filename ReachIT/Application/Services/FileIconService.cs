using System.IO;
using System.Net.Http;
using ReachIT.Application.Contracts;
using ReachIT.Domain.Enums;
using ReachIT.Domain.Models;

namespace ReachIT.Application.Services;

public sealed class FileIconService : IFileIconService
{
    private static readonly IReadOnlyDictionary<string, IconDefinition> ExtensionIcons = new Dictionary<string, IconDefinition>(StringComparer.OrdinalIgnoreCase)
    {
        [".py"] = new("python", "https://cdn.jsdelivr.net/gh/devicons/devicon@latest/icons/python/python-original.svg"),
        [".cs"] = new("csharp", "https://cdn.jsdelivr.net/gh/devicons/devicon@latest/icons/csharp/csharp-original.svg"),
        [".js"] = new("javascript", "https://cdn.jsdelivr.net/gh/devicons/devicon@latest/icons/javascript/javascript-original.svg"),
        [".ts"] = new("typescript", "https://cdn.jsdelivr.net/gh/devicons/devicon@latest/icons/typescript/typescript-original.svg"),
        [".jsx"] = new("react", "https://cdn.jsdelivr.net/gh/devicons/devicon@latest/icons/react/react-original.svg"),
        [".tsx"] = new("react", "https://cdn.jsdelivr.net/gh/devicons/devicon@latest/icons/react/react-original.svg"),
        [".vue"] = new("vuejs", "https://cdn.jsdelivr.net/gh/devicons/devicon@latest/icons/vuejs/vuejs-original.svg"),
        [".svelte"] = new("svelte", "https://cdn.jsdelivr.net/gh/devicons/devicon@latest/icons/svelte/svelte-original.svg"),
        [".html"] = new("html5", "https://cdn.jsdelivr.net/gh/devicons/devicon@latest/icons/html5/html5-original.svg"),
        [".css"] = new("css3", "https://cdn.jsdelivr.net/gh/devicons/devicon@latest/icons/css3/css3-original.svg"),
        [".scss"] = new("sass", "https://cdn.jsdelivr.net/gh/devicons/devicon@latest/icons/sass/sass-original.svg"),
        [".sass"] = new("sass", "https://cdn.jsdelivr.net/gh/devicons/devicon@latest/icons/sass/sass-original.svg"),
        [".json"] = new("json", "https://cdn.jsdelivr.net/gh/devicons/devicon@latest/icons/json/json-original.svg"),
        [".xml"] = new("xml", "https://cdn.jsdelivr.net/gh/devicons/devicon@latest/icons/xml/xml-original.svg"),
        [".md"] = new("markdown", "https://cdn.jsdelivr.net/gh/devicons/devicon@latest/icons/markdown/markdown-original.svg"),
        [".java"] = new("java", "https://cdn.jsdelivr.net/gh/devicons/devicon@latest/icons/java/java-original.svg"),
        [".cpp"] = new("cplusplus", "https://cdn.jsdelivr.net/gh/devicons/devicon@latest/icons/cplusplus/cplusplus-original.svg"),
        [".c"] = new("c", "https://cdn.jsdelivr.net/gh/devicons/devicon@latest/icons/c/c-original.svg"),
        [".go"] = new("go", "https://cdn.jsdelivr.net/gh/devicons/devicon@latest/icons/go/go-original.svg"),
        [".rs"] = new("rust", "https://cdn.jsdelivr.net/gh/devicons/devicon@latest/icons/rust/rust-original.svg"),
        [".php"] = new("php", "https://cdn.jsdelivr.net/gh/devicons/devicon@latest/icons/php/php-original.svg"),
        [".rb"] = new("ruby", "https://cdn.jsdelivr.net/gh/devicons/devicon@latest/icons/ruby/ruby-original.svg"),
        [".swift"] = new("swift", "https://cdn.jsdelivr.net/gh/devicons/devicon@latest/icons/swift/swift-original.svg"),
        [".kt"] = new("kotlin", "https://cdn.jsdelivr.net/gh/devicons/devicon@latest/icons/kotlin/kotlin-original.svg"),
        [".ps1"] = new("powershell", "https://cdn.jsdelivr.net/gh/devicons/devicon@latest/icons/powershell/powershell-original.svg"),
        [".sh"] = new("bash", "https://cdn.jsdelivr.net/gh/devicons/devicon@latest/icons/bash/bash-original.svg"),
        [".sql"] = new("postgresql", "https://cdn.jsdelivr.net/gh/devicons/devicon@latest/icons/postgresql/postgresql-original.svg"),
        [".db"] = new("sqlite", "https://cdn.jsdelivr.net/gh/devicons/devicon@latest/icons/sqlite/sqlite-original.svg"),
        [".sqlite"] = new("sqlite", "https://cdn.jsdelivr.net/gh/devicons/devicon@latest/icons/sqlite/sqlite-original.svg"),
        [".sqlite3"] = new("sqlite", "https://cdn.jsdelivr.net/gh/devicons/devicon@latest/icons/sqlite/sqlite-original.svg"),
        [".yml"] = new("yaml", "https://cdn.jsdelivr.net/gh/devicons/devicon@latest/icons/yaml/yaml-original.svg"),
        [".yaml"] = new("yaml", "https://cdn.jsdelivr.net/gh/devicons/devicon@latest/icons/yaml/yaml-original.svg")
    };

    private readonly string _cacheDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ReachIT",
        "file-icons");

    private bool _cacheAttempted;

    public async Task EnsureCacheAsync(CancellationToken cancellationToken = default)
    {
        if (_cacheAttempted)
        {
            return;
        }

        _cacheAttempted = true;
        Directory.CreateDirectory(_cacheDirectory);

        using var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(5)
        };

        foreach (var icon in ExtensionIcons.Values.DistinctBy(x => x.FileName))
        {
            var targetPath = GetCachedPath(icon.FileName);
            if (File.Exists(targetPath))
            {
                continue;
            }

            try
            {
                await using var stream = await client.GetStreamAsync(icon.Url, cancellationToken).ConfigureAwait(false);
                await using var file = File.Create(targetPath);
                await stream.CopyToAsync(file, cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                // Icons are decorative. If the network is unavailable, ReachIT keeps the local fallback badges.
            }
        }
    }

    public string? GetIconPath(ProjectTreeNode node)
    {
        if (node.IsDirectory || node.IsExternal || node.NodeType is ProjectTreeNodeType.ProjectRoot or ProjectTreeNodeType.VirtualNode or ProjectTreeNodeType.Task)
        {
            return null;
        }

        var extension = node.EffectiveExtension;
        if (string.IsNullOrWhiteSpace(extension) || !ExtensionIcons.TryGetValue(extension, out var icon))
        {
            return null;
        }

        var path = GetCachedPath(icon.FileName);
        return File.Exists(path) ? path : null;
    }

    private string GetCachedPath(string fileName)
    {
        return Path.Combine(_cacheDirectory, fileName + ".svg");
    }

    private sealed record IconDefinition(string FileName, string Url);
}
