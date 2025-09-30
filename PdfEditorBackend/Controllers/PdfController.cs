using Microsoft.AspNetCore.Mvc;
using PdfEditorBackend.Models;
using PdfEditorBackend.Services;

namespace PdfEditorBackend.Controllers
{
    /// <summary>
    /// Http api for uploading, previewing, editing, exporting, and inspecting pdf files.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class PdfController : ControllerBase
    {
        private readonly IPdfService _svc;

        /// <summary>
        /// Inject the pdf service that performs the actual document work.
        /// </summary>
        public PdfController(IPdfService svc) => _svc = svc;

        /// <summary>
        /// Uploads a pdf file to the server and returns its unique name and page count
        /// </summary>
        /// <remarks>
        /// Expects multipart/form data with a single file field containing a .pdf file
        /// </remarks>
        [HttpPost("upload")]
        [RequestSizeLimit(long.MaxValue)]
        public async Task<ActionResult<UploadResult>> Upload([FromForm] IFormFile file)
        {
            if (file == null || file.Length == 0) return BadRequest("No file.");
            if (!file.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                return BadRequest("Please upload a PDF.");

            var result = await _svc.SaveUploadAsync(file);
            return Ok(result);
        }

        /// <summary>
        /// Returns the total page count for a previously uploaded pdf
        /// </summary>
        [HttpGet("pagecount/{fileName}")]
        public async Task<ActionResult<int>> PageCount(string fileName)
        {
            var count = await _svc.GetPageCountAsync(fileName);
            return Ok(count);
        }

        /// <summary>
        /// Renders a single page preview as png or jpeg at the requested DPI.
        /// </summary>
        /// <param name="fileName">Stored file name </param>
        /// <param name="page">1 based page number to render </param>
        /// <param name="fmt">png or jpeg </param>
        /// <param name="dpi">rasterization dpi </param>
        [HttpGet("preview/{fileName}")]
        public async Task<IActionResult> Preview(string fileName, [FromQuery] int page = 1, [FromQuery] string fmt = "png", [FromQuery] int dpi = 150)
        {
            var format = fmt.Equals("jpeg", StringComparison.OrdinalIgnoreCase) ? ImageFormat.Jpeg : ImageFormat.Png;
            var bytes = await _svc.GetPageImageAsync(fileName, page, format, dpi);
            var ct = format == ImageFormat.Png ? "image/png" : "image/jpeg";
            return File(bytes, ct);
        }

        /// <summary>
        /// Applies text replacements, overlays, and metadata updates to a pdf
        /// </summary>
        [HttpPost("edit")]
        public async Task<ActionResult<object>> Edit([FromBody] EditRequest req)
        {
            if (!ModelState.IsValid) return ValidationProblem(ModelState);
            try
            {
                var outName = await _svc.ApplyEditsAsync(req);
                var display = _svc.CleanBaseName(outName);
                return Ok(new { fileName = outName, displayName = display, area = req.SaveAsNew ? "processed" : "uploads" });
            }
            catch (Exception ex)
            {
                return Problem(title: "Edit failed", detail: ex.ToString(), statusCode: 500);
            }
        }

        /// <summary>
        /// Exports the pdf as updated pdf, docx, or a zip of page images and returns the file bytes
        /// </summary>
        [HttpPost("export")]
        public async Task<IActionResult> Export([FromBody] ExportRequest req)
        {
            if (!ModelState.IsValid) return ValidationProblem(ModelState);
            var (bytes, contentType, downloadName) = await _svc.ExportAsync(req);
            return File(bytes, contentType, downloadName);
        }

        /// <summary>
        /// Streams a stored pdf for download, preferring /processed over /uploads if both exist
        /// </summary>
        [HttpGet("download/{fileName}")]
        public IActionResult Download(string fileName)
        {
            try
            {
                var fs = _svc.OpenStoredFile("processed", fileName);
                return File(fs, "application/pdf", fileName);
            }
            catch
            {
                var fs2 = _svc.OpenStoredFile("uploads", fileName);
                return File(fs2, "application/pdf", fileName);
            }
        }

        /// <summary>
        /// Renders a one off page preview that includes unsaved edits, without creating a new pdf
        /// </summary>
        [HttpPost("preview-after-edits")]
        public async Task<IActionResult> PreviewAfterEdits([FromBody] PreviewAfterEditsRequest req)
        {
            var bytes = await _svc.GetPreviewAfterEditsAsync(req);
            var ct = req.ImageFormat == ImageFormat.Png ? "image/png" : "image/jpeg";
            return File(bytes, ct);
        }

        /// <summary>
        /// Reads core pdf info plus XMP metadata as a flat dictionary.
        /// </summary>
        [HttpGet("metadata/{fileName}")]
        public ActionResult<IDictionary<string, string?>> ReadMetadata(string fileName)
        {
            var meta = _svc.ReadMetadata(fileName);
            return Ok(meta);
        }
    }
}
