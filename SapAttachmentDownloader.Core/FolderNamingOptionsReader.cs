using System.Text.Json;
using SapAttachmentDownloader.Core.Models;

namespace SapAttachmentDownloader.Core;

/// <summary>
/// Liest die optionale "FolderNaming"-Sektion aus appsettings.json (Geschwister von "Sap").
/// Fehlt die Sektion oder einzelne Properties, greifen die Defaults aus FolderNamingOptions -
/// das reproduziert exakt das bisherige fest verdrahtete Unterordner-Verhalten
/// (SupplierInvoice_Supplier), damit ein alter appsettings.json unveraendert weiterlaeuft.
/// </summary>
public static class FolderNamingOptionsReader
{
    public static FolderNamingOptions Read(JsonElement appSettingsRoot)
    {
        var result = new FolderNamingOptions();

        if (!appSettingsRoot.TryGetProperty("FolderNaming", out var folderNaming))
            return result;

        if (folderNaming.TryGetProperty("Mode", out var modeEl) &&
            modeEl.ValueKind == JsonValueKind.String &&
            Enum.TryParse<FolderNamingMode>(modeEl.GetString(), ignoreCase: true, out var mode))
        {
            result.Mode = mode;
        }

        var segments = NamingSegmentReader.ReadSegments(folderNaming);
        if (segments is not null)
            result.Segments = segments;

        if (folderNaming.TryGetProperty("Separator", out var sepEl) && sepEl.ValueKind == JsonValueKind.String)
            result.Separator = sepEl.GetString() ?? result.Separator;

        if (folderNaming.TryGetProperty("DateFormat", out var dateFormatEl) && dateFormatEl.ValueKind == JsonValueKind.String)
            result.DateFormat = dateFormatEl.GetString() ?? result.DateFormat;

        return result;
    }
}
