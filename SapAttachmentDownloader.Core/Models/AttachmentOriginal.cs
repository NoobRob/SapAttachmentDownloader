namespace SapAttachmentDownloader.Core.Models;

/// <summary>
/// Ein einzelner Anhang, wie er von der Function Import "GetAllOriginals"
/// des Service API_CV_ATTACHMENT_SRV zurueckgegeben wird. Die ersten acht Felder
/// bilden zusammen den Schluessel der Entity "AttachmentContentSet" und werden
/// 1:1 fuer den Download-Call (.../$value) benoetigt.
/// </summary>
public class AttachmentOriginal
{
    public string DocumentInfoRecordDocType { get; set; } = string.Empty;
    public string DocumentInfoRecordDocNumber { get; set; } = string.Empty;
    public string DocumentInfoRecordDocVersion { get; set; } = string.Empty;
    public string DocumentInfoRecordDocPart { get; set; } = string.Empty;
    public string LogicalDocument { get; set; } = string.Empty;
    public string ArchiveDocumentID { get; set; } = string.Empty;
    public string LinkedSAPObjectKey { get; set; } = string.Empty;
    public string BusinessObjectTypeName { get; set; } = string.Empty;

    public string FileName { get; set; } = string.Empty;
    public string FileSize { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
}
