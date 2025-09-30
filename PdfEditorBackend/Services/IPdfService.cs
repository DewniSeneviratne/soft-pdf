using PdfEditorBackend.Models;

namespace PdfEditorBackend.Services
{
    public interface IPdfService
    {
        Task<UploadResult> SaveUploadAsync(IFormFile file);
        Task<int> GetPageCountAsync(string fileName);
        Task<byte[]> GetPageImageAsync(string fileName, int page, ImageFormat fmt, int dpi = 150);

        Task<string> ApplyEditsAsync(EditRequest req);
        Task<(byte[] bytes, string contentType, string downloadName)> ExportAsync(ExportRequest req);
        FileStream OpenStoredFile(string area, string fileName);

        Task<byte[]> GetPreviewAfterEditsAsync(PreviewAfterEditsRequest req);
        string CleanBaseName(string fileName);
        IDictionary<string, string?> ReadMetadata(string fileName);
    }
}
