namespace SapAttachmentDownloader.Core.Models;

public enum FolderNamingMode
{
    /// <summary>Keine Unterordner - alle Dateien landen direkt im Zielordner (OutputFolder).</summary>
    Flat,

    /// <summary>Je Rechnung ein Unterordner, zusammengesetzt aus Feldern/freiem Text.</summary>
    Custom,
}

/// <summary>
/// Steuert, wie der Zielordner je Rechnung gebildet wird. Default reproduziert das bisherige
/// fest verdrahtete Verhalten (Unterordner "Rechnungsnummer_Lieferant-Nr."), damit ein
/// appsettings.json ohne "FolderNaming"-Sektion unveraendert weiterlaeuft.
/// </summary>
public class FolderNamingOptions
{
    public FolderNamingMode Mode { get; set; } = FolderNamingMode.Custom;

    public List<NamingSegment> Segments { get; set; } = new()
    {
        new NamingSegment { Type = NamingSegmentType.Field, Value = "SupplierInvoice" },
        new NamingSegment { Type = NamingSegmentType.Field, Value = "Supplier" },
    };

    public string Separator { get; set; } = "_";

    public string DateFormat { get; set; } = "yyyyMMdd";
}
