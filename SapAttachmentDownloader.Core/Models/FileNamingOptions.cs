namespace SapAttachmentDownloader.Core.Models;

public enum FileNamingMode
{
    Original,
    Custom,
}

public enum NamingSegmentType
{
    /// <summary>Value ist ein Feld-Key (siehe InvoiceFieldCatalog/FileNameBuilder), dessen Wert zur Laufzeit aufgeloest wird.</summary>
    Field,

    /// <summary>Value ist ein fest hinterlegter, vom Nutzer eingegebener Text.</summary>
    Text,
}

/// <summary>
/// Ein Baustein eines zusammengesetzten Datei- oder Ordnernamens - entweder ein Feld
/// oder freier Text. Wird sowohl von FileNamingOptions als auch von FolderNamingOptions
/// verwendet.
/// </summary>
public class NamingSegment
{
    public NamingSegmentType Type { get; set; } = NamingSegmentType.Field;
    public string Value { get; set; } = string.Empty;
}

/// <summary>
/// Steuert, wie Anhang-Dateinamen beim Download gebildet werden - entweder 1:1 aus SAP
/// (Original) oder zusammengesetzt aus einer geordneten Abfolge von Feldern und freiem
/// Text (Custom). Wird sowohl von WinForms als auch von ConsoleJob aus appsettings.json
/// gelesen, siehe FileNamingOptionsReader.
/// </summary>
public class FileNamingOptions
{
    public FileNamingMode Mode { get; set; } = FileNamingMode.Original;

    /// <summary>Geordnete Abfolge von Feldern/Text-Bausteinen; Reihenfolge = Reihenfolge im Dateinamen.</summary>
    public List<NamingSegment> Segments { get; set; } = new();

    /// <summary>Trennzeichen zwischen den Segmenten - beliebig lang, nicht auf ein Zeichen beschraenkt.</summary>
    public string Separator { get; set; } = " - ";

    /// <summary>.NET-Datumsformat fuer Datumsfelder (PostingDate, DocumentDate).</summary>
    public string DateFormat { get; set; } = "yyyyMMdd";
}
