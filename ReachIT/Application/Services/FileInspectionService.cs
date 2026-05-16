using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Media.Imaging;
using System.Xml.Linq;
using ReachIT.Application.Contracts;
using ReachIT.Domain.Models;

namespace ReachIT.Application.Services;

public sealed class FileInspectionService : IFileInspectionService
{
    private const int MaxPreviewBytes = 256 * 1024;
    private const int MaxPreviewChars = 12000;
    private const int MaxArchiveEntriesPreview = 300;

    private static readonly HashSet<string> TextExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".md", ".tex", ".csv", ".html", ".css", ".js", ".ts", ".jsx", ".tsx",
        ".py", ".java", ".cs", ".cpp", ".c", ".h", ".json", ".xml", ".yaml", ".yml",
        ".sql", ".log", ".env", ".ini", ".toml", ".config", ".lock", ".gitignore",
        ".editorconfig", ".bat", ".sh", ".bash", ".zsh", ".ps1", ".xaml", ".rit", ".svg",
        ".hxx", ".hpp", ".cxx", ".inl", ".jxx", ".ixx", ".mm", ".m", ".f", ".for",
        ".pyi", ".cmake", ".ui", ".qrc", ".qss", ".dox", ".rst", ".nsh", ".nsi",
        ".in", ".am", ".pro", ".pri", ".template", ".tmpl", ".spec", ".desktop",
        ".plist", ".entitlements", ".gitattributes", ".gitmodules", ".clang-format",
        ".clang-format-ignore", ".clang-tidy", ".git-blame-ignore-revs", ".pylintrc",
        ".flake8", ".license", ".theme", ".conf", ".cfg", ".rc", ".def", ".vcproj",
        ".vcxproj", ".sln", ".cbp", ".lark", ".l", ".y", ".po", ".fcmat", ".fctb",
        ".fmf", ".sif", ".dat", ".dyn", ".inp", ".bdf", ".frd", ".unv", ".z88",
        ".geo", ".skf", ".pat", ".vtk", ".brep", ".iv", ".dxf", ".scad", ".csg",
        ".stp", ".step", ".p21", ".plmxml", ".uml"
    };

    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".webp", ".gif", ".svg", ".bmp", ".ico"
    };

    private static readonly HashSet<string> ZipArchiveExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".zip", ".jar", ".nupkg", ".vsix", ".docx", ".xlsx", ".pptx", ".odt", ".ods", ".odp",
        ".odg", ".fcstd", ".3mf", ".sh3d"
    };

    private static readonly HashSet<string> ArchiveExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".zip", ".jar", ".nupkg", ".vsix", ".docx", ".xlsx", ".pptx", ".odt", ".ods", ".odp",
        ".odg", ".fcstd", ".3mf", ".sh3d", ".rar", ".7z", ".tar", ".gz", ".tgz", ".bz2", ".xz"
    };

    private static readonly HashSet<string> AudioExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp3", ".wav", ".ogg", ".flac", ".m4a", ".aac", ".wma"
    };

    private static readonly HashSet<string> ModelExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".obj", ".stl", ".gltf", ".glb", ".fbx", ".dae", ".blend", ".3ds"
    };

    private static readonly HashSet<string> RiskyArchiveEntryExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".msi", ".bat", ".cmd", ".ps1", ".vbs", ".js", ".jse", ".wsf", ".scr",
        ".com", ".dll", ".sys", ".jar", ".apk", ".app", ".sh", ".desktop", ".lnk"
    };

    public async Task<FileInspectionResult> InspectAsync(string fullPath, CancellationToken cancellationToken = default)
    {
        var extension = Path.GetExtension(fullPath);
        var kind = GetKind(extension);

        try
        {
            if (TextExtensions.Contains(extension))
            {
                return new FileInspectionResult
                {
                    FileKind = kind,
                    ReadCapability = "Text preview",
                    PreviewText = await ReadTextPreviewAsync(fullPath, cancellationToken).ConfigureAwait(false),
                    HasTextPreview = true
                };
            }

            if (extension.Equals(".rtf", StringComparison.OrdinalIgnoreCase))
            {
                return new FileInspectionResult
                {
                    FileKind = kind,
                    ReadCapability = "RTF text preview",
                    PreviewText = StripRtf(await ReadTextPreviewAsync(fullPath, cancellationToken).ConfigureAwait(false)),
                    HasTextPreview = true
                };
            }

            if (extension.Equals(".docx", StringComparison.OrdinalIgnoreCase))
            {
                return CreateTextResult(kind, "Word text extraction", ExtractOpenXmlText(fullPath, "word/document.xml", "w:t"));
            }

            if (extension.Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
            {
                return CreateTextResult(kind, "Excel shared strings extraction", ExtractOpenXmlText(fullPath, "xl/sharedStrings.xml", "t"));
            }

            if (extension.Equals(".pptx", StringComparison.OrdinalIgnoreCase))
            {
                return CreateTextResult(kind, "PowerPoint slide text extraction", ExtractManyOpenXmlTexts(fullPath, "ppt/slides/", "a:t"));
            }

            if (extension is ".odt" or ".ods" or ".odp")
            {
                return CreateTextResult(kind, "OpenDocument text extraction", ExtractOpenDocumentText(fullPath));
            }

            if (ImageExtensions.Contains(extension))
            {
                return InspectImage(fullPath, kind, extension);
            }

            if (ZipArchiveExtensions.Contains(extension))
            {
                return InspectZipArchive(fullPath, kind);
            }

            if (ArchiveExtensions.Contains(extension))
            {
                return InspectArchiveMetadata(fullPath, kind);
            }

            if (AudioExtensions.Contains(extension))
            {
                return new FileInspectionResult
                {
                    FileKind = kind,
                    ReadCapability = "Audio preview",
                    HasAudioPreview = true,
                    Message = "Audio recognized. ReachIT does not execute embedded content; playback is handled by the Windows media pipeline."
                };
            }

            if (ModelExtensions.Contains(extension))
            {
                return new FileInspectionResult
                {
                    FileKind = kind,
                    ReadCapability = "3D model metadata",
                    HasModelPreview = true,
                    PreviewText = BuildModelSummary(fullPath, extension),
                    Message = "3D model recognized. Interactive rendering is not enabled yet; metadata preview is safe."
                };
            }

            return InspectBinaryMetadata(fullPath, kind, extension);
        }
        catch (Exception ex)
        {
            return new FileInspectionResult
            {
                FileKind = kind,
                ReadCapability = "Read failed",
                Message = $"Could not inspect this file: {ex.Message}"
            };
        }
    }

    private static FileInspectionResult CreateTextResult(string kind, string capability, string text)
    {
        return new FileInspectionResult
        {
            FileKind = kind,
            ReadCapability = capability,
            PreviewText = TrimPreview(text),
            HasTextPreview = !string.IsNullOrWhiteSpace(text),
            Message = string.IsNullOrWhiteSpace(text) ? "No readable text was found in this document." : string.Empty
        };
    }

    private static async Task<string> ReadTextPreviewAsync(string fullPath, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var bytesToRead = (int)Math.Min(stream.Length, MaxPreviewBytes);
        var buffer = new byte[bytesToRead];
        var read = await stream.ReadAsync(buffer.AsMemory(0, bytesToRead), cancellationToken).ConfigureAwait(false);
        var preview = Encoding.UTF8.GetString(buffer, 0, read);

        if (stream.Length > MaxPreviewBytes)
        {
            preview += Environment.NewLine + Environment.NewLine + "--- Preview truncated at 256 KB ---";
        }

        return preview;
    }

    private static string ExtractOpenXmlText(string fullPath, string entryName, string elementName)
    {
        using var archive = ZipFile.OpenRead(fullPath);
        var entry = archive.GetEntry(entryName);
        if (entry is null)
        {
            return string.Empty;
        }

        using var stream = entry.Open();
        var document = XDocument.Load(stream);
        return string.Join(Environment.NewLine, document.Descendants().Where(x => x.Name.LocalName == elementName.Split(':').Last()).Select(x => x.Value));
    }

    private static string ExtractManyOpenXmlTexts(string fullPath, string entryPrefix, string elementName)
    {
        using var archive = ZipFile.OpenRead(fullPath);
        var localName = elementName.Split(':').Last();
        var parts = new List<string>();
        foreach (var entry in archive.Entries.Where(x => x.FullName.StartsWith(entryPrefix, StringComparison.OrdinalIgnoreCase) && x.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)))
        {
            using var stream = entry.Open();
            var document = XDocument.Load(stream);
            parts.AddRange(document.Descendants().Where(x => x.Name.LocalName == localName).Select(x => x.Value));
        }

        return string.Join(Environment.NewLine, parts);
    }

    private static string ExtractOpenDocumentText(string fullPath)
    {
        using var archive = ZipFile.OpenRead(fullPath);
        var entry = archive.GetEntry("content.xml");
        if (entry is null)
        {
            return string.Empty;
        }

        using var stream = entry.Open();
        var document = XDocument.Load(stream);
        return string.Join(Environment.NewLine, document.Descendants().Where(x => !x.HasElements).Select(x => x.Value).Where(x => !string.IsNullOrWhiteSpace(x)));
    }

    private static FileInspectionResult InspectImage(string fullPath, string kind, string extension)
    {
        if (extension.Equals(".svg", StringComparison.OrdinalIgnoreCase))
        {
            return new FileInspectionResult
            {
                FileKind = kind,
                ReadCapability = "SVG text preview",
                PreviewText = File.ReadAllText(fullPath),
                HasTextPreview = true,
                Message = "SVG is text-based vector graphics."
            };
        }

        if (extension.Equals(".webp", StringComparison.OrdinalIgnoreCase))
        {
            return new FileInspectionResult
            {
                FileKind = kind,
                ReadCapability = "External app",
                Message = "WEBP support depends on installed Windows codecs. Use Open if preview is unavailable."
            };
        }

        var decoder = BitmapDecoder.Create(new Uri(fullPath), BitmapCreateOptions.DelayCreation, BitmapCacheOption.OnLoad);
        var frame = decoder.Frames.FirstOrDefault();
        var details = frame is null
            ? "Image recognized."
            : $"Image recognized. Size: {frame.PixelWidth}x{frame.PixelHeight}px.";

        return new FileInspectionResult
        {
            FileKind = kind,
            ReadCapability = "Image preview",
            HasImagePreview = true,
            Message = details
        };
    }

    private static FileInspectionResult InspectZipArchive(string fullPath, string kind)
    {
        var lines = new List<string>
        {
            "Safe archive inspection",
            "ReachIT lists archive metadata without extracting files or running anything.",
            string.Empty
        };

        var warnings = new List<string>();
        long totalCompressed = 0;
        long totalUncompressed = 0;
        var shown = 0;

        using var archive = ZipFile.OpenRead(fullPath);
        lines.Add($"Entries: {archive.Entries.Count}");
        lines.Add(string.Empty);

        foreach (var entry in archive.Entries)
        {
            totalCompressed += Math.Max(0, entry.CompressedLength);
            totalUncompressed += Math.Max(0, entry.Length);

            var entryName = entry.FullName.Replace('\\', '/');
            if (IsUnsafeArchivePath(entryName))
            {
                warnings.Add($"Path traversal or absolute path: {entry.FullName}");
            }

            var extension = Path.GetExtension(entryName);
            if (RiskyArchiveEntryExtensions.Contains(extension))
            {
                warnings.Add($"Executable/script entry: {entry.FullName}");
            }

            if (entry.CompressedLength > 0 && entry.Length / (double)entry.CompressedLength > 100)
            {
                warnings.Add($"High compression ratio: {entry.FullName}");
            }

            if (shown < MaxArchiveEntriesPreview)
            {
                var marker = entryName.EndsWith('/') ? "[folder]" : "[file]";
                lines.Add($"{marker} {entry.FullName}  ({FormatSize(entry.Length)}, compressed {FormatSize(entry.CompressedLength)})");
                shown++;
            }
        }

        if (archive.Entries.Count > MaxArchiveEntriesPreview)
        {
            lines.Add(string.Empty);
            lines.Add($"--- Preview truncated after {MaxArchiveEntriesPreview} entries ---");
        }

        lines.Insert(3, $"Total uncompressed: {FormatSize(totalUncompressed)}");
        lines.Insert(4, $"Total compressed: {FormatSize(totalCompressed)}");
        lines.Insert(5, string.Empty);

        var message = warnings.Count == 0
            ? "No obvious archive path or executable-entry risks detected."
            : "Archive warnings: " + string.Join(" | ", warnings.Take(8)) + (warnings.Count > 8 ? $" | +{warnings.Count - 8} more" : string.Empty);

        if (warnings.Count > 0)
        {
            lines.Insert(6, "Warnings:");
            foreach (var warning in warnings.Take(20))
            {
                lines.Insert(7, "- " + warning);
            }

            lines.Insert(Math.Min(27, lines.Count), string.Empty);
        }

        return new FileInspectionResult
        {
            FileKind = kind,
            ReadCapability = "Archive listing only",
            PreviewText = TrimPreview(string.Join(Environment.NewLine, lines)),
            HasArchivePreview = true,
            Message = message
        };
    }

    private static FileInspectionResult InspectArchiveMetadata(string fullPath, string kind)
    {
        var fileInfo = new FileInfo(fullPath);
        return new FileInspectionResult
        {
            FileKind = kind,
            ReadCapability = "Archive metadata only",
            PreviewText = string.Join(Environment.NewLine,
            [
                "Safe archive metadata",
                "ReachIT did not extract or execute this archive.",
                "Listing is available for ZIP-based containers only. Use a trusted archive tool in a sandbox if you need deeper inspection.",
                string.Empty,
                $"Name: {fileInfo.Name}",
                $"Size: {FormatSize(fileInfo.Length)}",
                $"Modified: {fileInfo.LastWriteTime:yyyy-MM-dd HH:mm:ss}"
            ]),
            HasArchivePreview = true,
            Message = "Archive recognized. No extraction was performed."
        };
    }

    private static FileInspectionResult InspectBinaryMetadata(string fullPath, string kind, string extension)
    {
        var fileInfo = new FileInfo(fullPath);
        var header = ReadHeaderHex(fullPath);
        var capability = extension.ToLowerInvariant() switch
        {
            ".qm" => "Qt translation binary metadata",
            ".ttf" or ".woff2" => "Font metadata",
            ".dll" or ".exe" or ".msi" => "Executable metadata only",
            ".pdf" => "PDF metadata only",
            ".xcf" or ".psd" or ".icns" => "Design asset metadata",
            ".jt" or ".glb" or ".fbx" => "3D binary metadata",
            _ => "Binary metadata"
        };

        var safety = extension.ToLowerInvariant() is ".dll" or ".exe" or ".msi"
            ? "Security note: executable content is never launched by preview."
            : "Security note: preview reads metadata only and does not execute embedded content.";

        return new FileInspectionResult
        {
            FileKind = kind,
            ReadCapability = capability,
            PreviewText = string.Join(Environment.NewLine,
            [
                "Binary / structured file preview",
                safety,
                string.Empty,
                $"Name: {fileInfo.Name}",
                $"Extension: {extension}",
                $"Size: {FormatSize(fileInfo.Length)}",
                $"Created: {fileInfo.CreationTime:yyyy-MM-dd HH:mm:ss}",
                $"Modified: {fileInfo.LastWriteTime:yyyy-MM-dd HH:mm:ss}",
                $"Header bytes: {header}"
            ]),
            HasTextPreview = true,
            Message = "Metadata-only preview. Use Open only if you trust this file."
        };
    }

    private static string ReadHeaderHex(string fullPath)
    {
        try
        {
            using var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var length = (int)Math.Min(32, stream.Length);
            var buffer = new byte[length];
            var read = stream.Read(buffer, 0, length);
            return read == 0
                ? "(empty)"
                : string.Join(' ', buffer.Take(read).Select(b => b.ToString("X2")));
        }
        catch
        {
            return "(unavailable)";
        }
    }

    private static bool IsUnsafeArchivePath(string entryName)
    {
        if (string.IsNullOrWhiteSpace(entryName))
        {
            return false;
        }

        return entryName.StartsWith("/", StringComparison.Ordinal)
               || entryName.Contains(":/", StringComparison.Ordinal)
               || entryName.Split('/', StringSplitOptions.RemoveEmptyEntries).Any(part => part == "..");
    }

    private static string BuildModelSummary(string fullPath, string extension)
    {
        var fileInfo = new FileInfo(fullPath);
        var lines = new List<string>
        {
            $"Format: {extension.TrimStart('.').ToUpperInvariant()}",
            $"Size: {FormatSize(fileInfo.Length)}",
            "Preview mode: metadata only"
        };

        if (extension.Equals(".obj", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var vertices = 0;
                var faces = 0;
                foreach (var line in File.ReadLines(fullPath).Take(20000))
                {
                    if (line.StartsWith("v ", StringComparison.Ordinal)) vertices++;
                    if (line.StartsWith("f ", StringComparison.Ordinal)) faces++;
                }

                lines.Add($"Vertices scanned: {vertices}");
                lines.Add($"Faces scanned: {faces}");
            }
            catch
            {
                lines.Add("Could not scan OBJ counters.");
            }
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string FormatSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        var kb = bytes / 1024d;
        if (kb < 1024) return $"{kb:F2} KB";
        var mb = kb / 1024d;
        if (mb < 1024) return $"{mb:F2} MB";
        return $"{mb / 1024d:F2} GB";
    }

    private static string StripRtf(string rtf)
    {
        var text = Regex.Replace(rtf, @"\\'[0-9a-fA-F]{2}", " ");
        text = Regex.Replace(text, @"\\[a-zA-Z]+\d* ?", string.Empty);
        text = Regex.Replace(text, @"[{}]", string.Empty);
        return TrimPreview(text);
    }

    private static string TrimPreview(string value)
    {
        return value.Length <= MaxPreviewChars
            ? value
            : value[..MaxPreviewChars] + Environment.NewLine + Environment.NewLine + "--- Preview truncated ---";
    }

    private static string GetKind(string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            ".txt" or ".docx" or ".odt" or ".rtf" or ".md" or ".tex" => "Text / document",
            ".xlsx" or ".xls" or ".csv" or ".ods" => "Spreadsheet",
            ".pptx" or ".ppt" or ".odp" => "Presentation",
            ".png" or ".jpg" or ".jpeg" or ".webp" or ".gif" or ".svg" or ".bmp" or ".ico" => "Image",
            ".mp3" or ".wav" or ".ogg" or ".flac" or ".m4a" => "Audio",
            ".mp4" or ".mov" or ".avi" or ".mkv" or ".webm" => "Video",
            ".zip" or ".rar" or ".7z" or ".tar" or ".gz" or ".tgz" or ".bz2" or ".xz" or ".3mf" or ".sh3d" => "Archive",
            ".fcstd" => "FreeCAD document",
            ".html" or ".css" or ".js" or ".ts" or ".jsx" or ".tsx" or ".py" or ".pyi" or ".java" or ".cs" or ".cpp" or ".c" or ".h" or ".hpp" or ".hxx" or ".cxx" or ".inl" => "Code",
            ".ui" or ".qrc" or ".qss" or ".qm" or ".fcmat" or ".fctb" or ".fcmacro" => "FreeCAD / Qt resource",
            ".cmake" or ".in" or ".am" or ".pro" or ".nsh" or ".nsi" or ".rc" or ".def" or ".sln" or ".vcproj" or ".vcxproj" => "Build / project file",
            ".json" or ".xml" or ".yaml" or ".yml" or ".sql" or ".db" or ".sqlite" or ".sqlite3" or ".parquet" or ".log" => "Data",
            ".vtk" or ".brep" or ".iv" or ".dxf" or ".stp" or ".step" or ".p21" or ".inp" or ".bdf" or ".frd" or ".unv" or ".z88" => "CAD / FEM data",
            ".psd" or ".ai" or ".fig" or ".xcf" or ".odg" or ".blend" or ".fbx" or ".obj" or ".stl" or ".glb" or ".gltf" or ".dae" or ".3ds" or ".jt" => "Design / 3D",
            ".ttf" or ".woff2" => "Font",
            ".env" or ".ini" or ".toml" or ".config" or ".lock" or ".gitignore" or ".editorconfig" => "Project config",
            ".exe" or ".msi" or ".apk" or ".app" or ".bat" or ".sh" or ".dll" or ".desktop" => "Executable / script",
            _ => "Unknown"
        };
    }
}
