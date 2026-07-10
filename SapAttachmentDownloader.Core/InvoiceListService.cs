using System.Globalization;
using System.Text.Json;
using SapAttachmentDownloader.Core.Models;

namespace SapAttachmentDownloader.Core;

/// <summary>
/// Liest Eingangsrechnungen ueber API_SUPPLIERINVOICE_PROCESS_SRV (Entity A_SupplierInvoice).
/// Liefert bereits einen Datensatz pro Rechnung - keine Dedup-Logik mehr noetig
/// (anders als beim fruesheren Ansatz ueber die BKPF-Item-Cube).
/// </summary>
public class InvoiceListService
{
    private const string ServicePath = "/sap/opu/odata/sap/API_SUPPLIERINVOICE_PROCESS_SRV";
    private const string EntitySet = "A_SupplierInvoice";

    private readonly SapODataClient _client;
    private readonly SapApiOptions _options;

    public InvoiceListService(SapODataClient client, SapApiOptions options)
    {
        _client = client;
        _options = options;
    }

    public async Task<List<InvoiceDocument>> GetInvoicesAsync(
        IProgress<string>? progress = null, CancellationToken ct = default)
    {
        var filter = BuildFilter();
        var select = "SupplierInvoice,FiscalYear,CompanyCode,AccountingDocumentType," +
                     "PostingDate,DocumentDate,InvoicingParty,SupplierInvoiceIDByInvcgParty," +
                     "InvoiceGrossAmount,DocumentCurrency";

        var relativePath =
            $"{ServicePath}/{EntitySet}?$filter={Uri.EscapeDataString(filter)}&$select={select}&$format=json";

        var invoices = new List<InvoiceDocument>();
        string? nextPath = relativePath;
        var page = 1;

        while (nextPath != null)
        {
            progress?.Report($"Lade Seite {page}...");
            var json = await _client.GetJsonAsync(nextPath, ct);
            using var jsonDoc = JsonDocument.Parse(json);
            var d = jsonDoc.RootElement.GetProperty("d");

            foreach (var item in d.GetProperty("results").EnumerateArray())
            {
                invoices.Add(new InvoiceDocument
                {
                    SupplierInvoice = GetString(item, "SupplierInvoice"),
                    FiscalYear = GetString(item, "FiscalYear"),
                    CompanyCode = GetString(item, "CompanyCode"),
                    AccountingDocumentType = GetString(item, "AccountingDocumentType"),
                    PostingDate = ParseODataDate(GetString(item, "PostingDate")),
                    DocumentDate = ParseODataDate(GetString(item, "DocumentDate")),
                    Supplier = GetString(item, "InvoicingParty"),
                    SupplierReference = GetString(item, "SupplierInvoiceIDByInvcgParty"),
                    InvoiceGrossAmount = GetDecimal(item, "InvoiceGrossAmount"),
                    DocumentCurrency = GetString(item, "DocumentCurrency"),
                });
            }

            // Klassisches OData V2: Paging ueber "__next" im "d"-Objekt.
            nextPath = d.TryGetProperty("__next", out var nextEl)
                ? StripHost(nextEl.GetString())
                : null;
            page++;
        }

        progress?.Report($"{invoices.Count} Rechnungen gefunden.");
        return invoices.OrderBy(v => v.PostingDate).ThenBy(v => v.SupplierInvoice).ToList();
    }

    private string BuildFilter()
    {
        var typeFilter = string.Join(" or ",
            _options.InvoiceDocumentTypes.Select(t =>
                $"AccountingDocumentType eq '{SapODataClient.ODataLiteral(t)}'"));

        return $"CompanyCode eq '{SapODataClient.ODataLiteral(_options.CompanyCode)}' " +
               $"and FiscalYear eq '{SapODataClient.ODataLiteral(_options.FiscalYear)}' " +
               $"and ({typeFilter})";
    }

    private static string GetString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind != JsonValueKind.Null
            ? value.GetString() ?? string.Empty
            : string.Empty;

    private static decimal? GetDecimal(JsonElement element, string property)
    {
        var raw = GetString(element, property);
        return decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    /// <summary>OData V2 liefert Datumsangaben im Format "/Date(1772409600000)/".</summary>
    private static DateTime? ParseODataDate(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return null;
        var digits = raw.Trim('/').Replace("Date(", "").Replace(")", "");
        if (long.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out var ms))
        {
            return DateTimeOffset.FromUnixTimeMilliseconds(ms).UtcDateTime;
        }
        return null;
    }

    /// <summary>__next liefert eine absolute URL - wir brauchen nur den Pfad+Query fuer GetJsonAsync.</summary>
    private static string? StripHost(string? absoluteUrl)
    {
        if (string.IsNullOrEmpty(absoluteUrl)) return null;
        var uri = new Uri(absoluteUrl);
        return uri.PathAndQuery;
    }
}
