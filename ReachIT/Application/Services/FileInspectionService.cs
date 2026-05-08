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

    private static readonly HashSet<string> TextExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".md", ".tex", ".csv", ".html", ".css", ".js", ".ts", ".jsx", ".tsx",
        ".py", ".java", ".cs", ".cpp", ".c", ".h", ".json", ".xml", ".yaml", ".yml",
        ".sql", ".log", ".env", ".ini", ".toml", ".config", ".lock", ".gitignore",
        ".editorconfig", ".bat", ".sh", ".ps1", ".xaml", ".rit", ".svg", ".obj"
    };

    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".webp", ".gif", ".svg", ".bmp", ".ico"
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

            return new FileInspectionResult
            {
                FileKind = kind,
                ReadCapability = "External app",
                Message = $"ReachIT recognizes {extension}, but direct preview is not implemented yet. Use Open to work with this file in its native app."
            };
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
            ".zip" or ".rar" or ".7z" or ".tar" or ".gz" => "Archive",
            ".html" or ".css" or ".js" or ".ts" or ".jsx" or ".tsx" or ".py" or ".java" or ".cs" or ".cpp" or ".c" or ".h" => "Code",
            ".json" or ".xml" or ".yaml" or ".yml" or ".sql" or ".db" or ".sqlite" or ".sqlite3" or ".parquet" or ".log" => "Data",
            ".psd" or ".ai" or ".fig" or ".blend" or ".fbx" or ".obj" or ".stl" or ".glb" or ".gltf" => "Design / 3D",
            ".env" or ".ini" or ".toml" or ".config" or ".lock" or ".gitignore" or ".editorconfig" => "Project config",
            ".exe" or ".msi" or ".apk" or ".app" or ".bat" or ".sh" => "Executable / script",
            _ => "Unknown"
        };
    }
}
