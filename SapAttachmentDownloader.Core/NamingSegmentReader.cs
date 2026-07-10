using System.Text.Json;
using SapAttachmentDownloader.Core.Models;

namespace SapAttachmentDownloader.Core;

/// <summary>
/// Liest ein "Segments"-Array (siehe NamingSegment) aus appsettings.json - gemeinsam genutzt
/// von FileNamingOptionsReader und FolderNamingOptionsReader, da beide dasselbe
/// Feld/Text-Baustein-Format verwenden.
/// </summary>
internal static class NamingSegmentReader
{
    public static List<NamingSegment>? ReadSegments(JsonElement parent)
    {
        if (!parent.TryGetProperty("Segments", out var segmentsEl) || segmentsEl.ValueKind != JsonValueKind.Array)
            return null;

        var segments = new List<NamingSegment>();
        foreach (var segmentEl in segmentsEl.EnumerateArray())
        {
            if (segmentEl.ValueKind != JsonValueKind.Object) continue;

            var value = segmentEl.TryGetProperty("Value", out var valueEl) && valueEl.ValueKind == JsonValueKind.String
                ? valueEl.GetString() ?? ""
                : "";
            if (value.Length == 0) continue;

            var type = segmentEl.TryGetProperty("Type", out var typeEl) &&
                       typeEl.ValueKind == JsonValueKind.String &&
                       Enum.TryParse<NamingSegmentType>(typeEl.GetString(), ignoreCase: true, out var parsedType)
                ? parsedType
                : NamingSegmentType.Field;

            segments.Add(new NamingSegment { Type = type, Value = value });
        }
        return segments;
    }
}
