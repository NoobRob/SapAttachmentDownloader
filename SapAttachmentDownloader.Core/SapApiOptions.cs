namespace SapAttachmentDownloader.Core;

/// <summary>
/// Verbindungs- und Filterparameter fuer den Zugriff auf die SAP S/4HANA Cloud OData-APIs.
/// </summary>
public class SapApiOptions
{
    /// <summary>z.B. https://my433264-api.s4hana.cloud.sap (ohne trailing slash)</summary>
    public string Host { get; set; } = string.Empty;

    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    /// <summary>Buchungskreis, z.B. "1010"</summary>
    public string CompanyCode { get; set; } = string.Empty;

    /// <summary>Geschaeftsjahr, z.B. "2026"</summary>
    public string FiscalYear { get; set; } = string.Empty;

    /// <summary>
    /// Belegarten, die als Eingangsrechnung gelten (RE, KR, KN -
    /// siehe gemeinsame Analyse der Belegarten fuer FUCHS).
    /// </summary>
    public List<string> InvoiceDocumentTypes { get; set; } = new() { "RE", "KR", "KN" };

    /// <summary>
    /// BusinessObjectTypeName fuer den Attachment-Service.
    /// Fuer SupplierInvoice/Rechnungspruefung (RBKP): "BUS2081".
    /// (Fruehere Annahme "BKPF" war falsch - Anhaenge haengen an der SupplierInvoice, nicht am Buchungsbeleg.)
    /// </summary>
    public string BusinessObjectTypeName { get; set; } = "BUS2081";

    /// <summary>Zielordner fuer heruntergeladene Anhaenge.</summary>
    public string OutputFolder { get; set; } = "Downloads";
}
