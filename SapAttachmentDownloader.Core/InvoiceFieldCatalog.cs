using System.Globalization;
using SapAttachmentDownloader.Core.Models;

namespace SapAttachmentDownloader.Core;

/// <summary>
/// Rechnungsfelder (InvoiceDocument), die sich fuer die Zusammensetzung von Datei- und
/// Ordnernamen eignen. Von FileNameBuilder (erweitert um Anhang-Felder) und
/// FolderNameBuilder (unveraendert, da ein Ordner mehrere Anhaenge einer Rechnung buendelt)
/// gemeinsam genutzt.
/// </summary>
public static class InvoiceFieldCatalog
{
    public record FieldDefinition(string Key, string DisplayName, Func<InvoiceDocument, string, string> Selector);

    public static readonly IReadOnlyList<FieldDefinition> Fields = new List<FieldDefinition>
    {
        new("SupplierInvoice", "Rechnungsnummer", (inv, _) => inv.SupplierInvoice),
        new("SupplierReference", "Lieferanten-Referenz", (inv, _) => inv.SupplierReference),
        new("Supplier", "Lieferant-Nr.", (inv, _) => inv.Supplier),
        new("SupplierName", "Lieferantenname", (inv, _) => inv.SupplierName),
        new("CompanyCode", "Buchungskreis", (inv, _) => inv.CompanyCode),
        new("FiscalYear", "Geschäftsjahr", (inv, _) => inv.FiscalYear),
        new("AccountingDocumentType", "Belegart", (inv, _) => inv.AccountingDocumentType),
        new("PostingDate", "Buchungsdatum", (inv, fmt) => inv.PostingDate?.ToString(fmt, CultureInfo.InvariantCulture) ?? ""),
        new("DocumentDate", "Belegdatum", (inv, fmt) => inv.DocumentDate?.ToString(fmt, CultureInfo.InvariantCulture) ?? ""),
        new("InvoiceGrossAmount", "Bruttobetrag", (inv, _) => inv.InvoiceGrossAmount?.ToString("F2", CultureInfo.InvariantCulture) ?? ""),
        new("DocumentCurrency", "Währung", (inv, _) => inv.DocumentCurrency),
        new("LinkedSAPObjectKey", "SAP-Schlüssel (Rechnungsnr.+Geschäftsjahr)", (inv, _) => inv.LinkedSAPObjectKey),
    };
}
