namespace SapAttachmentDownloader.Core.Models;

/// <summary>
/// Eine Eingangsrechnung (SupplierInvoice / RBKP), wie sie aus
/// API_SUPPLIERINVOICE_PROCESS_SRV (Entity A_SupplierInvoice) gelesen wird.
/// Ein Datensatz pro Rechnung - kein Dedup mehr noetig, anders als beim
/// vorherigen Ansatz ueber die BKPF-Item-Cube.
/// </summary>
public class InvoiceDocument
{
    /// <summary>Rechnungsnummer, z.B. "5105600186" - das ist die "Rechnungsnummer" aus der Fiori-App.</summary>
    public string SupplierInvoice { get; set; } = string.Empty;
    public string FiscalYear { get; set; } = string.Empty;
    public string CompanyCode { get; set; } = string.Empty;
    public string AccountingDocumentType { get; set; } = string.Empty;
    public DateTime? PostingDate { get; set; }
    public DateTime? DocumentDate { get; set; }

    /// <summary>Lieferant (Business-Partner-Nummer, Feld "InvoicingParty").</summary>
    public string Supplier { get; set; } = string.Empty;

    /// <summary>Nicht Teil von A_SupplierInvoice - optionaler Ausbauschritt ueber die BP-API. Aktuell leer.</summary>
    public string SupplierName { get; set; } = string.Empty;

    /// <summary>Rechnungsnummer aus Sicht des Lieferanten (Feld "SupplierInvoiceIDByInvcgParty").</summary>
    public string SupplierReference { get; set; } = string.Empty;

    public decimal? InvoiceGrossAmount { get; set; }
    public string DocumentCurrency { get; set; } = string.Empty;

    /// <summary>
    /// Wird fuer die Attachment-Suche (GetAllOriginals) benoetigt.
    /// Fuer BUS2081 (SupplierInvoice): SupplierInvoice (10-stellig, fuehrende Nullen) + FiscalYear.
    /// KEIN Buchungskreis im Schluessel (anders als bei BKPF)!
    /// </summary>
    public string LinkedSAPObjectKey =>
        $"{SupplierInvoice.PadLeft(10, '0')}{FiscalYear}";

    // Wird beim Anhang-Check in der GUI befuellt, nicht Teil der SAP-Antwort:
    public int AttachmentCount { get; set; } = -1; // -1 = noch nicht geprueft
    public string Status { get; set; } = string.Empty;
}
