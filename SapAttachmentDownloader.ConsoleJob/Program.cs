using System.Text.Json;
using SapAttachmentDownloader.Core;

// -----------------------------------------------------------------------------------------
// Konsolen-Job: dieselbe Core-Logik wie die WinForms-App, aber ohne UI - gedacht als Basis
// fuer eine Windows-Aufgabenplanung (Task Scheduler) oder spaeter einen Windows-Dienst.
//
// Aufruf z.B. per Aufgabenplanung, taeglich nachts:
//   SapAttachmentDownloader.ConsoleJob.exe
//
// Kennwort bewusst NICHT in appsettings.json, sondern per Umgebungsvariable:
//   setx SAP_API_PASSWORD "..."
// (Fuer den produktiven Einsatz waere die Windows Credential Manager / DPAPI die sauberere
// Loesung - das ist hier als naechster Ausbauschritt gedacht, kein Blocker fuer den Start.)
// -----------------------------------------------------------------------------------------

var basePath = AppContext.BaseDirectory;
var options = LoadOptions(Path.Combine(basePath, "appsettings.json"));

options.Password = Environment.GetEnvironmentVariable("SAP_API_PASSWORD")
    ?? throw new InvalidOperationException(
        "Umgebungsvariable SAP_API_PASSWORD ist nicht gesetzt. " +
        "Siehe README.md, Abschnitt 'Konsolen-Job / geplante Aufgabe'.");

var stateFilePath = Path.Combine(basePath, "downloaded-documents.json");
var alreadyDownloaded = LoadState(stateFilePath);

Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Start. Bereits bekannt: {alreadyDownloaded.Count} Belege.");

using var client = new SapODataClient(options);
var invoiceService = new InvoiceListService(client, options);
var attachmentService = new AttachmentDownloadService(client, options);

var invoices = await invoiceService.GetInvoicesAsync(new Progress<string>(Console.WriteLine));

var newDownloads = 0;
var errors = 0;

foreach (var invoice in invoices)
{
    // Kernidee fuer den zyklischen Lauf: Belege, die wir schon einmal erfolgreich
    // heruntergeladen haben, werden uebersprungen - nur neue/geaenderte Belege kosten API-Calls.
    if (alreadyDownloaded.Contains(invoice.LinkedSAPObjectKey))
        continue;

    try
    {
        var originals = await attachmentService.GetOriginalsAsync(invoice.LinkedSAPObjectKey);
        if (originals.Count == 0)
        {
            // Kein Anhang (heute) - beim naechsten Lauf erneut pruefen, NICHT als erledigt markieren.
            continue;
        }

        var docFolder = Path.Combine(options.OutputFolder,
            $"{invoice.SupplierInvoice}_{invoice.Supplier}");

        foreach (var original in originals)
        {
            var savedPath = await attachmentService.DownloadAsync(original, docFolder);
            Console.WriteLine($"  heruntergeladen: {savedPath}");
            newDownloads++;
        }

        alreadyDownloaded.Add(invoice.LinkedSAPObjectKey);
    }
    catch (Exception ex)
    {
        errors++;
        Console.WriteLine($"  FEHLER bei Rechnung {invoice.SupplierInvoice}: {ex.Message}");
    }
}

SaveState(stateFilePath, alreadyDownloaded);

Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Ende. " +
                   $"{newDownloads} neue Datei(en), {errors} Fehler.");

return errors == 0 ? 0 : 1; // Exit-Code fuer die Aufgabenplanung (ungleich 0 = Fehler im Log sichtbar)

// --- lokale Hilfsfunktionen ---

static SapApiOptions LoadOptions(string path)
{
    using var doc = JsonDocument.Parse(File.ReadAllText(path));
    var sap = doc.RootElement.GetProperty("Sap");

    return new SapApiOptions
    {
        Host = sap.GetProperty("Host").GetString() ?? "",
        Username = sap.GetProperty("Username").GetString() ?? "",
        CompanyCode = sap.GetProperty("CompanyCode").GetString() ?? "",
        FiscalYear = sap.GetProperty("FiscalYear").GetString() ?? "",
        BusinessObjectTypeName = sap.GetProperty("BusinessObjectTypeName").GetString() ?? "BKPF",
        InvoiceDocumentTypes = sap.GetProperty("InvoiceDocumentTypes")
            .EnumerateArray().Select(e => e.GetString() ?? "").ToList(),
        OutputFolder = sap.GetProperty("OutputFolder").GetString() ?? "Downloads",
    };
}

static HashSet<string> LoadState(string path) =>
    File.Exists(path)
        ? JsonSerializer.Deserialize<HashSet<string>>(File.ReadAllText(path)) ?? new()
        : new();

static void SaveState(string path, HashSet<string> state) =>
    File.WriteAllText(path, JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true }));
