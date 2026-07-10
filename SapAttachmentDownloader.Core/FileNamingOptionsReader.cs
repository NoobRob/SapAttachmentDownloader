using System.Text.Json;
using SapAttachmentDownloader.Core.Models;

namespace SapAttachmentDownloader.Core;

/// <summary>
/// Liest die optionale "FileNaming"-Sektion aus appsettings.json (Geschwister von "Sap",
/// nicht darin verschachtelt). Fehlt die Sektion oder einzelne Properties, greifen die
/// Defaults aus FileNamingOptions - so bleibt ein alter appsettings.json ohne diese
/// Sektion lauffaehig (kein Crash im headless ConsoleJob).
/// </summary>
public static class FileNamingOptionsReader
{
    public static FileNamingOptions Read(JsonElement appSettingsRoot)
    {
        var result = new FileNamingOptions();

        if (!appSettingsRoot.TryGetProperty("FileNaming", out var fileNaming))
            return result;

        if (fileNaming.TryGetProperty("Mode", out var modeEl) &&
            modeEl.ValueKind == JsonValueKind.String &&
            Enum.TryParse<FileNamingMode>(modeEl.GetString(), ignoreCase: true, out var mode))
        {
            result.Mode = mode;
        }

        var segments = NamingSegmentReader.ReadSegments(fileNaming);
        if (segments is not null)
            result.Segments = segments;

        if (fileNaming.TryGetProperty("Separator", out var sepEl) && sepEl.ValueKind == JsonValueKind.String)
            result.Separator = sepEl.GetString() ?? result.Separator;

        if (fileNaming.TryGetProperty("DateFormat", out var dateFormatEl) && dateFormatEl.ValueKind == JsonValueKind.String)
            result.DateFormat = dateFormatEl.GetString() ?? result.DateFormat;

        return result;
    }
}
