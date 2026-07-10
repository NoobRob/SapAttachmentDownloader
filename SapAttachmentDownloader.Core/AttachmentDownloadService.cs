using System.Text.Json;
using SapAttachmentDownloader.Core.Models;

namespace SapAttachmentDownloader.Core;

/// <summary>
/// Findet Anhaenge zu einem FI-Beleg (GetAllOriginals) und laedt sie ueber
/// AttachmentContentSet(...)/$value herunter.
/// </summary>
public class AttachmentDownloadService
{
    private const string ServicePath = "/sap/opu/odata/sap/API_CV_ATTACHMENT_SRV";

    private readonly SapODataClient _client;
    private readonly SapApiOptions _options;

    public AttachmentDownloadService(SapODataClient client, SapApiOptions options)
    {
        _client = client;
        _options = options;
    }

    public async Task<List<AttachmentOriginal>> GetOriginalsAsync(
        string linkedSapObjectKey, CancellationToken ct = default)
    {
        var relativePath =
            $"{ServicePath}/GetAllOriginals" +
            $"?BusinessObjectTypeName='{Uri.EscapeDataString(SapODataClient.ODataLiteral(_options.BusinessObjectTypeName))}'" +
            $"&LinkedSAPObjectKey='{Uri.EscapeDataString(SapODataClient.ODataLiteral(linkedSapObjectKey))}'" +
            $"&$format=json";

        var json = await _client.GetJsonAsync(relativePath, ct);
        using var jsonDoc = JsonDocument.Parse(json);
        var d = jsonDoc.RootElement.GetProperty("d");

        // Function Imports mit Collection-Rueckgabe liefern "d.results", genau wie ein EntitySet.
        var results = d.TryGetProperty("results", out var resultsEl)
            ? resultsEl
            : d; // Fallback, falls das Gateway ein einzelnes Objekt statt einer Collection liefert.

        var originals = new List<AttachmentOriginal>();
        if (results.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in results.EnumerateArray())
            {
                originals.Add(MapOriginal(item));
            }
        }
        else if (results.ValueKind == JsonValueKind.Object)
        {
            originals.Add(MapOriginal(results));
        }

        return originals;
    }

    private static AttachmentOriginal MapOriginal(JsonElement item) => new()
    {
        DocumentInfoRecordDocType = GetString(item, "DocumentInfoRecordDocType"),
        DocumentInfoRecordDocNumber = GetString(item, "DocumentInfoRecordDocNumber"),
        DocumentInfoRecordDocVersion = GetString(item, "DocumentInfoRecordDocVersion"),
        DocumentInfoRecordDocPart = GetString(item, "DocumentInfoRecordDocPart"),
        LogicalDocument = GetString(item, "LogicalDocument"),
        ArchiveDocumentID = GetString(item, "ArchiveDocumentID"),
        LinkedSAPObjectKey = GetString(item, "LinkedSAPObjectKey"),
        BusinessObjectTypeName = GetString(item, "BusinessObjectTypeName"),
        FileName = GetString(item, "FileName"),
        FileSize = GetString(item, "FileSize"),
        MimeType = GetString(item, "MimeType"),
    };

    /// <summary>
    /// Laedt den Binaerinhalt eines Anhangs herunter und speichert ihn im Zielordner unter
    /// desiredFileName (Namensbildung, z.B. via FileNameBuilder.Build, liegt beim Aufrufer -
    /// dieser Service kennt nur Download-Mechanik, keine Naming-Strategie).
    /// </summary>
    public async Task<string> DownloadAsync(
        AttachmentOriginal original, string destinationFolder, string desiredFileName, CancellationToken ct = default)
    {
        Directory.CreateDirectory(destinationFolder);

        var keyPredicate =
            $"DocumentInfoRecordDocType='{Enc(original.DocumentInfoRecordDocType)}'," +
            $"DocumentInfoRecordDocNumber='{Enc(original.DocumentInfoRecordDocNumber)}'," +
            $"DocumentInfoRecordDocPart='{Enc(original.DocumentInfoRecordDocPart)}'," +
            $"DocumentInfoRecordDocVersion='{Enc(original.DocumentInfoRecordDocVersion)}'," +
            $"LogicalDocument='{Enc(original.LogicalDocument)}'," +
            $"ArchiveDocumentID='{Enc(original.ArchiveDocumentID)}'," +
            $"LinkedSAPObjectKey='{Enc(original.LinkedSAPObjectKey)}'," +
            $"BusinessObjectTypeName='{Enc(original.BusinessObjectTypeName)}'";

        var relativePath = $"{ServicePath}/AttachmentContentSet({keyPredicate})/$value";

        var bytes = await _client.GetBytesAsync(relativePath, ct);

        var fileName = SanitizeFileName(desiredFileName);

        var fullPath = Path.Combine(destinationFolder, fileName);
        fullPath = MakeUnique(fullPath);

        await File.WriteAllBytesAsync(fullPath, bytes, ct);
        return fullPath;
    }

    private static string Enc(string value) => Uri.EscapeDataString(SapODataClient.ODataLiteral(value));

    private static string GetString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind != JsonValueKind.Null
            ? value.GetString() ?? string.Empty
            : string.Empty;

    private static string SanitizeFileName(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name;
    }

    private static string MakeUnique(string fullPath)
    {
        if (!File.Exists(fullPath)) return fullPath;

        var dir = Path.GetDirectoryName(fullPath)!;
        var name = Path.GetFileNameWithoutExtension(fullPath);
        var ext = Path.GetExtension(fullPath);
        var i = 1;
        string candidate;
        do
        {
            candidate = Path.Combine(dir, $"{name}_{i}{ext}");
            i++;
        } while (File.Exists(candidate));

        return candidate;
    }
}
