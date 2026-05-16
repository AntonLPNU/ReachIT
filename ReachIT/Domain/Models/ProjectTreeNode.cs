// Represents a logical node in the ReachIT project explorer.
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations.Schema;
using System.IO;
using ReachIT.Domain.Enums;

namespace ReachIT.Domain.Models;

public class ProjectTreeNode
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? ProjectMetaId { get; set; }
    public Guid? ParentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty;
    public ProjectTreeNodeType NodeType { get; set; } = ProjectTreeNodeType.Folder;
    public bool IsDirectory { get; set; }
    public bool IsExternal { get; set; }
    public string? ExternalTargetPathOrUrl { get; set; }
    public bool IsExpanded { get; set; }
    [NotMapped]
    public bool IsSelected { get; set; }
    [NotMapped]
    public Guid? AttachedTaskId { get; set; }
    [NotMapped]
    public int AttachedTaskCount { get; set; }
    [NotMapped]
    public string? IconSource { get; set; }
    public ObservableCollection<ProjectTreeNode> Children { get; set; } = [];

    [NotMapped]
    public bool IsTaskNode => NodeType == ProjectTreeNodeType.Task;

    [NotMapped]
    public bool HasAttachedTasks => AttachedTaskCount > 0;

    [NotMapped]
    public string DisplayName
    {
        get
        {
            if (IsTaskNode || IsDirectory || IsExternal || NodeType is ProjectTreeNodeType.ProjectRoot or ProjectTreeNodeType.VirtualNode)
            {
                return Name;
            }

            var nameWithoutExtension = Path.GetFileNameWithoutExtension(Name);
            return string.IsNullOrWhiteSpace(nameWithoutExtension) ? Name : nameWithoutExtension;
        }
    }

    [NotMapped]
    public string IconGlyph
    {
        get
        {
            if (IsTaskNode)
            {
                return "T";
            }

        if (NodeType == ProjectTreeNodeType.WebLink)
        {
            return "WEB";
        }

            if (IsDirectory || NodeType is ProjectTreeNodeType.ProjectRoot or ProjectTreeNodeType.VirtualNode)
            {
                return string.Empty;
            }

            var namedGlyph = NamedFileIconGlyph;
            if (!string.IsNullOrWhiteSpace(namedGlyph))
            {
                return namedGlyph;
            }

            return EffectiveExtension switch
            {
                ".py" => "PY",
                ".cs" => "C#",
                ".xaml" => "UI",
                ".xml" => "<>",
                ".json" => "{}",
                ".md" => "MD",
                ".txt" => "TXT",
                ".log" => "LOG",
                ".ini" or ".toml" or ".config" or ".editorconfig" => "CFG",
                ".env" => "ENV",
                ".gitignore" or ".gitattributes" or ".gitmodules" => "GIT",
                ".lock" => "LOCK",
                ".doc" or ".docx" => "W",
                ".xls" or ".xlsx" => "X",
                ".ppt" or ".pptx" => "P",
                ".png" or ".jpg" or ".jpeg" or ".gif" or ".webp" or ".svg" => "IMG",
                ".html" or ".css" or ".js" or ".ts" => "</>",
                ".zip" or ".rar" or ".7z" => "ZIP",
                ".rit" => "R",
                _ => string.Empty
            };
        }
    }

    [NotMapped]
    public bool ShowIconGlyph => !string.IsNullOrWhiteSpace(IconGlyph);

    [NotMapped]
    public bool HasDownloadedIcon => !string.IsNullOrWhiteSpace(IconSource);

    [NotMapped]
    public string MaterialIconKind => ResolveMaterialIconKind();

    [NotMapped]
    public bool ShowMaterialIcon => !HasDownloadedIcon && !ShowFolderIcon && !string.IsNullOrWhiteSpace(MaterialIconKind);

    [NotMapped]
    public bool ShowTextIcon => !HasDownloadedIcon && !ShowMaterialIcon && ShowIconGlyph;

    [NotMapped]
    public bool ShowFolderIcon => !HasDownloadedIcon && (IsDirectory || NodeType is ProjectTreeNodeType.ProjectRoot or ProjectTreeNodeType.VirtualNode);

    [NotMapped]
    public bool ShowExtensionBadge => !HasDownloadedIcon && !ShowFolderIcon && !ShowMaterialIcon && !ShowIconGlyph && !string.IsNullOrWhiteSpace(ExtensionBadge);

    [NotMapped]
    public string IconBackground => NodeType switch
    {
        ProjectTreeNodeType.Task => "#6D2FCB",
        ProjectTreeNodeType.ProjectRoot => "#174B84",
        ProjectTreeNodeType.Folder or ProjectTreeNodeType.VirtualNode => "#123C68",
        ProjectTreeNodeType.WebLink or ProjectTreeNodeType.OfflinePage => "#0B6B6C",
        _ => EffectiveExtension switch
        {
            ".py" => "#1D5F37",
            ".cs" => "#663399",
            ".xaml" or ".xml" => "#2358A8",
            ".json" => "#A06412",
            ".md" or ".txt" or ".log" => "#516172",
            ".gitignore" or ".gitattributes" or ".gitmodules" => "#D94D1A",
            ".env" => "#236B55",
            ".ini" or ".toml" or ".config" or ".editorconfig" => "#31547A",
            ".lock" => "#6C4A8F",
            ".doc" or ".docx" or ".odt" or ".rtf" => "#185ABD",
            ".xls" or ".xlsx" or ".ods" or ".csv" or ".tsv" => "#107C41",
            ".ppt" or ".pptx" or ".odp" => "#C43E1C",
            ".png" or ".jpg" or ".jpeg" or ".gif" or ".webp" or ".svg" or ".bmp" or ".ico" or ".tif" or ".tiff" or ".psd" or ".ai" or ".fig" => "#B21F66",
            ".mp3" or ".wav" or ".ogg" or ".flac" or ".m4a" => "#7A4DC8",
            ".mp4" or ".mov" or ".avi" or ".mkv" or ".webm" => "#A83E78",
            ".html" or ".css" or ".scss" or ".sass" or ".less" or ".js" or ".jsx" or ".ts" or ".tsx" or ".vue" or ".svelte" => "#A15C00",
            ".zip" or ".rar" or ".7z" or ".tar" or ".tar.gz" or ".gz" or ".bz2" or ".xz" => "#765A18",
            ".pdf" => "#B3261E",
            ".sql" or ".db" or ".sqlite" or ".sqlite3" => "#1E6F8C",
            ".exe" or ".msi" or ".apk" or ".app" or ".bat" or ".cmd" or ".sh" => "#4E5F76",
            ".pem" or ".crt" or ".cer" or ".key" or ".pfx" => "#7C5A12",
            ".rit" => "#1E7892",
            _ => BuildExtensionColor(ExtensionBadge)
        }
    };

    [NotMapped]
    public string IconForeground => EffectiveExtension switch
    {
        ".json" or ".html" or ".css" or ".js" or ".ts" or ".zip" or ".rar" or ".7z" => "#FFE8B5",
        ".doc" or ".docx" or ".xls" or ".xlsx" or ".ppt" or ".pptx" => "#FFFFFF",
        _ => IsDarkColor(IconBackground) ? "#F3FAFF" : "#081322"
    };

    [NotMapped]
    public bool ShowSubtitle => !IsTaskNode && !string.IsNullOrWhiteSpace(RelativePath);

    [NotMapped]
    public string ExtensionBadge
    {
        get
        {
            if (IsDirectory || IsTaskNode || IsExternal)
            {
                return string.Empty;
            }

            var namedGlyph = NamedFileIconGlyph;
            if (!string.IsNullOrWhiteSpace(namedGlyph))
            {
                return namedGlyph;
            }

            var extension = EffectiveExtension.TrimStart('.').ToUpperInvariant();
            return extension.Length > 3 ? extension[..3] : extension;
        }
    }

    [NotMapped]
    public string EffectiveExtension
    {
        get
        {
            var fileName = Name.ToLowerInvariant();
            foreach (var knownExtension in KnownExtensions)
            {
                if (fileName.EndsWith(knownExtension, StringComparison.OrdinalIgnoreCase))
                {
                    return knownExtension;
                }
            }

            var directExtension = Path.GetExtension(Name).ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(directExtension))
            {
                var namedExtension = NamedFileEffectiveExtension;
                if (!string.IsNullOrWhiteSpace(namedExtension))
                {
                    return namedExtension;
                }
            }

            if (!directExtension.Equals(".txt", StringComparison.OrdinalIgnoreCase))
            {
                return directExtension;
            }

            var withoutTxt = Path.GetFileNameWithoutExtension(Name);
            var nestedExtension = Path.GetExtension(withoutTxt).ToLowerInvariant();
            return string.IsNullOrWhiteSpace(nestedExtension) ? directExtension : nestedExtension;
        }
    }

    private static readonly string[] KnownExtensions =
    [
        ".tar.gz",
        ".zip.txt",
        ".rar.txt",
        ".7z.txt",
        ".pdf.txt",
        ".docx.txt",
        ".xlsx.txt",
        ".pptx.txt",
        ".sqlite3",
        ".tar.gz",
        ".dockerfile",
        ".zip",
        ".rar",
        ".7z",
        ".tar",
        ".gz",
        ".bz2",
        ".xz",
        ".pdf",
        ".docx",
        ".doc",
        ".odt",
        ".rtf",
        ".xlsx",
        ".xls",
        ".ods",
        ".csv",
        ".tsv",
        ".pptx",
        ".ppt",
        ".odp",
        ".json",
        ".xml",
        ".yaml",
        ".yml",
        ".gitignore",
        ".gitattributes",
        ".gitmodules",
        ".editorconfig",
        ".config",
        ".lock",
        ".env",
        ".ini",
        ".toml",
        ".log",
        ".md",
        ".txt",
        ".html",
        ".css",
        ".scss",
        ".sass",
        ".less",
        ".js",
        ".jsx",
        ".ts",
        ".tsx",
        ".vue",
        ".svelte",
        ".py",
        ".cs",
        ".xaml",
        ".java",
        ".cpp",
        ".c",
        ".h",
        ".hpp",
        ".go",
        ".rs",
        ".php",
        ".rb",
        ".swift",
        ".kt",
        ".kts",
        ".ps1",
        ".sh",
        ".bat",
        ".cmd",
        ".sql",
        ".db",
        ".sqlite",
        ".png",
        ".jpg",
        ".jpeg",
        ".gif",
        ".webp",
        ".svg",
        ".bmp",
        ".ico",
        ".tif",
        ".tiff",
        ".psd",
        ".ai",
        ".fig",
        ".mp3",
        ".wav",
        ".ogg",
        ".flac",
        ".m4a",
        ".mp4",
        ".mov",
        ".avi",
        ".mkv",
        ".webm",
        ".exe",
        ".msi",
        ".apk",
        ".app",
        ".pem",
        ".crt",
        ".cer",
        ".key",
        ".pfx"
    ];

    [NotMapped]
    private string NamedFileIconGlyph
    {
        get
        {
            var name = Name.Trim().ToLowerInvariant();
            var relativePath = RelativePath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            var inGitFolder = relativePath.StartsWith($".git{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase);

            if (inGitFolder || name is "head" or "index" or "packed-refs" or "config" or "description" or "shallow" or "commit_editmsg" or "fetch_head" or "orig_head")
            {
                return "GIT";
            }

            if (name is "license" or "licence" or "copying" or "notice")
            {
                return "LIC";
            }

            if (name is "readme" or "changelog" or "changes" or "authors" or "contributors" or "credits")
            {
                return "TXT";
            }

            if (name is "makefile" or "dockerfile" or "containerfile" or "procfile" or "rakefile" or "gemfile")
            {
                return "CFG";
            }

            return string.Empty;
        }
    }

    [NotMapped]
    private string NamedFileEffectiveExtension
    {
        get
        {
            return NamedFileIconGlyph switch
            {
                "GIT" => ".gitignore",
                "LIC" or "TXT" => ".txt",
                "CFG" => ".config",
                _ => string.Empty
            };
        }
    }

    private string ResolveMaterialIconKind()
    {
        if (IsTaskNode)
        {
            return "ClipboardText";
        }

        if (NodeType == ProjectTreeNodeType.WebLink)
        {
            return "LinkVariant";
        }

        var namedGlyph = NamedFileIconGlyph;
        if (namedGlyph == "GIT")
        {
            return "Git";
        }

        if (namedGlyph == "LIC")
        {
            return "Certificate";
        }

        if (namedGlyph == "CFG")
        {
            return "FileCog";
        }

        if (namedGlyph == "TXT")
        {
            return "FileDocument";
        }

        return EffectiveExtension switch
        {
            ".gitignore" or ".gitattributes" or ".gitmodules" => "Git",
            ".md" => "LanguageMarkdown",
            ".txt" or ".log" or ".rtf" => "FileDocument",
            ".pdf" => "FilePdfBox",
            ".doc" or ".docx" or ".odt" => "FileWordBox",
            ".xls" or ".xlsx" or ".ods" or ".csv" or ".tsv" => "FileExcelBox",
            ".ppt" or ".pptx" or ".odp" => "FilePowerpointBox",
            ".json" => "CodeJson",
            ".xml" or ".xaml" => "FileXmlBox",
            ".yaml" or ".yml" or ".toml" or ".ini" or ".config" or ".editorconfig" or ".env" => "FileCog",
            ".lock" => "FileLock",
            ".pem" or ".crt" or ".cer" or ".key" or ".pfx" => "FileKey",
            ".sql" or ".db" or ".sqlite" or ".sqlite3" => "Database",
            ".png" or ".jpg" or ".jpeg" or ".gif" or ".webp" or ".svg" or ".bmp" or ".ico" or ".tif" or ".tiff" or ".psd" or ".ai" or ".fig" => "FileImage",
            ".mp3" or ".wav" or ".ogg" or ".flac" or ".m4a" => "FileMusic",
            ".mp4" or ".mov" or ".avi" or ".mkv" or ".webm" => "FileVideo",
            ".zip" or ".rar" or ".7z" or ".tar" or ".tar.gz" or ".gz" or ".bz2" or ".xz" => "FolderZip",
            ".html" or ".css" or ".scss" or ".sass" or ".less" or ".js" or ".jsx" or ".ts" or ".tsx" or ".vue" or ".svelte"
                or ".py" or ".cs" or ".java" or ".cpp" or ".c" or ".h" or ".hpp" or ".go" or ".rs" or ".php" or ".rb" or ".swift" or ".kt" or ".kts" => "FileCode",
            ".ps1" or ".sh" or ".bat" or ".cmd" => "Console",
            ".exe" or ".msi" or ".apk" or ".app" => "ApplicationCog",
            ".rit" => "FileCog",
            _ => string.IsNullOrWhiteSpace(ExtensionBadge) ? "FileQuestion" : "File"
        };
    }

    private static string BuildExtensionColor(string extensionBadge)
    {
        if (string.IsNullOrWhiteSpace(extensionBadge))
        {
            return "#2A3854";
        }

        var hash = 17;
        foreach (var ch in extensionBadge.ToUpperInvariant())
        {
            hash = (hash * 31) + ch;
        }

        var hue = Math.Abs(hash) % 360;
        return HslToHex(hue, 0.68, 0.42);
    }

    private static string HslToHex(int hue, double saturation, double lightness)
    {
        var chroma = (1 - Math.Abs((2 * lightness) - 1)) * saturation;
        var x = chroma * (1 - Math.Abs(((hue / 60d) % 2) - 1));
        var match = lightness - (chroma / 2);

        var (r1, g1, b1) = hue switch
        {
            < 60 => (chroma, x, 0d),
            < 120 => (x, chroma, 0d),
            < 180 => (0d, chroma, x),
            < 240 => (0d, x, chroma),
            < 300 => (x, 0d, chroma),
            _ => (chroma, 0d, x)
        };

        var r = (int)Math.Round((r1 + match) * 255);
        var g = (int)Math.Round((g1 + match) * 255);
        var b = (int)Math.Round((b1 + match) * 255);
        return $"#{r:X2}{g:X2}{b:X2}";
    }

    private static bool IsDarkColor(string hex)
    {
        if (hex.Length != 7 || hex[0] != '#')
        {
            return true;
        }

        var r = Convert.ToInt32(hex.Substring(1, 2), 16);
        var g = Convert.ToInt32(hex.Substring(3, 2), 16);
        var b = Convert.ToInt32(hex.Substring(5, 2), 16);
        var luminance = (0.299 * r) + (0.587 * g) + (0.114 * b);
        return luminance < 145;
    }
}
