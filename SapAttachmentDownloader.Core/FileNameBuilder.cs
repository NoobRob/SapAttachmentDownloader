using SapAttachmentDownloader.Core.Models;

namespace SapAttachmentDownloader.Core;

/// <summary>
/// Baut den Ziel-Dateinamen fuer einen Anhang - entweder 1:1 aus SAP (Original) oder
/// zusammengesetzt aus ausgewaehlten Rechnungs-/Anhang-Feldern (Custom). Reine Funktion,
/// kein SAP-Zugriff noetig, damit unabhaengig von AttachmentDownloadService testbar.
/// </summary>
public static class FileNameBuilder
{
    public record FieldDefinition(string Key, string DisplayName, Func<InvoiceDocument, AttachmentOriginal, string, string> Selector);

    /// <summary>
    /// Verfuegbare Felder, in der Reihenfolge, in der sie in der Feldauswahl angezeigt werden:
    /// alle Rechnungsfelder aus InvoiceFieldCatalog plus zwei Anhang-spezifische Felder.
    /// Status/AttachmentCount sind bewusst ausgeschlossen: Status ist zum Zeitpunkt der
    /// Namensbildung noch nicht gesetzt, AttachmentCount ist reine Laufzeit-Metadaten.
    /// </summary>
    public static readonly IReadOnlyList<FieldDefinition> Catalog = InvoiceFieldCatalog.Fields
        .Select(f => new FieldDefinition(f.Key, f.DisplayName, (inv, _, fmt) => f.Selector(inv, fmt)))
        .Concat(new List<FieldDefinition>
        {
            new("OriginalFileName", "Original-Dateiname (ohne Endung)", (_, orig, _) => Path.GetFileNameWithoutExtension(orig.FileName)),
            new("ArchiveDocumentID", "Archiv-Dokument-ID", (_, orig, _) => orig.ArchiveDocumentID),
        })
        .ToList();

    public static string Build(InvoiceDocument invoice, AttachmentOriginal original, FileNamingOptions naming)
    {
        var extension = GetExtension(original);

        if (naming.Mode == FileNamingMode.Original || naming.Segments.Count == 0)
        {
            return string.IsNullOrWhiteSpace(original.FileName)
                ? $"{original.LinkedSAPObjectKey}_{original.ArchiveDocumentID}{extension}"
                : original.FileName;
        }

        var parts = naming.Segments
            .Select(segment => segment.Type == NamingSegmentType.Text
                ? segment.Value
                : Catalog.FirstOrDefault(f => f.Key == segment.Value)?.Selector(invoice, original, naming.DateFormat) ?? "")
            .Where(value => !string.IsNullOrWhiteSpace(value));

        var baseName = string.Join(naming.Separator, parts);
        if (string.IsNullOrWhiteSpace(baseName))
            baseName = $"{original.LinkedSAPObjectKey}_{original.ArchiveDocumentID}";

        return baseName + extension;
    }

    private static string GetExtension(AttachmentOriginal original)
    {
        var extension = Path.GetExtension(original.FileName);
        if (!string.IsNullOrEmpty(extension))
            return extension;

        return original.MimeType.ToLowerInvariant() switch
        {
            "application/pdf" => ".pdf",
            "image/tiff" => ".tif",
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            _ => "",
        };
    }
}
