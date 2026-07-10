using SapAttachmentDownloader.Core.Models;

namespace SapAttachmentDownloader.Core;

/// <summary>
/// Baut den Zielordner fuer die Anhaenge einer Rechnung - entweder direkt der konfigurierte
/// Zielordner (Flat, alle Dateien in einem gemeinsamen Pfad) oder ein daraus abgeleiteter
/// Unterordner, zusammengesetzt aus Rechnungsfeldern/freiem Text (Custom). Reine Funktion,
/// kein SAP-Zugriff noetig.
/// </summary>
public static class FolderNameBuilder
{
    /// <summary>Nur Rechnungsfelder - ein Ordner buendelt alle Anhaenge einer Rechnung, Anhang-Felder ergeben hier keinen Sinn.</summary>
    public static readonly IReadOnlyList<InvoiceFieldCatalog.FieldDefinition> Catalog = InvoiceFieldCatalog.Fields;

    public static string Build(string outputFolder, InvoiceDocument invoice, FolderNamingOptions naming)
    {
        if (naming.Mode == FolderNamingMode.Flat || naming.Segments.Count == 0)
            return outputFolder;

        var parts = naming.Segments
            .Select(segment => segment.Type == NamingSegmentType.Text
                ? segment.Value
                : Catalog.FirstOrDefault(f => f.Key == segment.Value)?.Selector(invoice, naming.DateFormat) ?? "")
            .Where(value => !string.IsNullOrWhiteSpace(value));

        var subfolder = SanitizeFolderName(string.Join(naming.Separator, parts));
        return string.IsNullOrWhiteSpace(subfolder)
            ? outputFolder
            : Path.Combine(outputFolder, subfolder);
    }

    private static string SanitizeFolderName(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name;
    }
}
