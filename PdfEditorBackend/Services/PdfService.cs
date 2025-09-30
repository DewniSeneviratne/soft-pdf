using Aspose.Pdf;
using Aspose.Pdf.Devices;
using Aspose.Pdf.Text;
using System.IO.Compression;
using PdfEditorBackend.Models;
using System.Text.RegularExpressions;

namespace PdfEditorBackend.Services
{
    public class PdfService : IPdfService
    {
        private readonly string _wwwroot;
        private readonly string _uploads;
        private readonly string _processed;

        /// <summary>
        /// connect storage folders wwwroot/uploads and wwwroot/processed on service startup
        /// </summary>
        public PdfService(IWebHostEnvironment env)
        {
            _wwwroot = Path.Combine(env.ContentRootPath, "wwwroot");
            _uploads = Path.Combine(_wwwroot, "uploads");
            _processed = Path.Combine(_wwwroot, "processed");

            Directory.CreateDirectory(_uploads);
            Directory.CreateDirectory(_processed);
        }

        private string UploadedPath(string fileName) => Path.Combine(_uploads, fileName);
        private string ProcessedPath(string fileName) => Path.Combine(_processed, fileName);

        /// <summary>
        /// Finds a file by name, checking edited first and then uploads
        /// </summary>
        private string ResolveExistingPath(string fileName)
        {
            var inProcessed = ProcessedPath(fileName);
            if (File.Exists(inProcessed)) return inProcessed;

            var inUploads = UploadedPath(fileName);
            if (File.Exists(inUploads)) return inUploads;

            throw new FileNotFoundException("File not found in processed or uploads", fileName);
        }

        /// <summary>
        /// Saves an uploaded pdf to /wwwroot/uploads with a unique name and returns its page count
        /// </summary>
        public async Task<UploadResult> SaveUploadAsync(IFormFile file)
        {
            if (file.Length == 0) throw new InvalidOperationException("Empty file");

            var safeName = Path.GetFileNameWithoutExtension(file.FileName);
            var ext = ".pdf";
            var unique = $"{safeName}_{Guid.NewGuid():N}{ext}";
            var dest = UploadedPath(unique);

            using (var fs = new FileStream(dest, FileMode.CreateNew))
                await file.CopyToAsync(fs);

            using var doc = new Document(dest);
            return new UploadResult { FileName = unique, PageCount = doc.Pages.Count };
        }

        /// <summary>
        /// reads a pdf and returns its total number of pages
        /// </summary>
        public async Task<int> GetPageCountAsync(string fileName)
        {
            var path = ResolveExistingPath(fileName);
            using var doc = new Document(path);
            return await Task.FromResult(doc.Pages.Count);
        }

        /// <summary>
        /// renders a given page to png/jpeg at a requested DPI and returns the image bytes
        /// </summary>
        public async Task<byte[]> GetPageImageAsync(string fileName, int page, ImageFormat fmt, int dpi = 150)
        {
            var path = ResolveExistingPath(fileName);
            using var doc = new Document(path);

            if (page < 1 || page > doc.Pages.Count)
                throw new ArgumentOutOfRangeException(nameof(page), "Page out of range");

            using var ms = new MemoryStream();
            var res = new Resolution(dpi);

            if (fmt == ImageFormat.Png)
            {
                var device = new PngDevice(res);
                device.Process(doc.Pages[page], ms);
            }
            else
            {
                var device = new JpegDevice(res, 85);
                device.Process(doc.Pages[page], ms);
            }

            return await Task.FromResult(ms.ToArray());
        }

        /// <summary>
        /// Applies replacements/overlays/metadata to a pdf
        /// Saves as a new processed file if requested otherwise overwrites the original
        /// </summary>
        public async Task<string> ApplyEditsAsync(EditRequest req)
        {
            var src = ResolveExistingPath(req.FileName);

            using var doc = new Document(src);
            ApplyEditsInMemory(doc, req);

            string outName;
            if (req.SaveAsNew)
            {
                // prefer explicit title from metadata payload otherwise use document's current Title
                string? title = null;
                if (req.Metadata != null)
                {
                    foreach (var kv in req.Metadata)
                    {
                        if (string.Equals(kv.Key, "title", StringComparison.OrdinalIgnoreCase))
                        {
                            title = kv.Value; break;
                        }
                    }
                }
                if (string.IsNullOrWhiteSpace(title)) title = doc.Info?.Title;

                var baseName = !string.IsNullOrWhiteSpace(title)
                    ? SanitizeFileBase(title)
                    : SanitizeFileBase(CleanBaseName(req.FileName));

                outName = EnsureUnique($"{baseName}.pdf", _processed);
            }
            else
            {
                outName = req.FileName;
            }

            var dest = req.SaveAsNew ? ProcessedPath(outName) : src;

            doc.Save(dest);
            return await Task.FromResult(Path.GetFileName(dest));
        }

        /// <summary>
        /// Exports a pdf to
        /// - updated pdf,
        /// - DOCX, or
        /// - ZIP of page images (pgn/jpeg).
        /// </summary>
        public async Task<(byte[] bytes, string contentType, string downloadName)> ExportAsync(ExportRequest req)
        {
            var input = File.Exists(ProcessedPath(req.FileName)) ? ProcessedPath(req.FileName) : UploadedPath(req.FileName);
            if (!File.Exists(input)) throw new FileNotFoundException("File not found.", req.FileName);

            var baseName = CleanBaseName(req.FileName);

            switch (req.Format)
            {
                case ExportFormat.Pdf:
                    {
                        var bytes = await File.ReadAllBytesAsync(input);
                        return (bytes, "application/pdf", $"{baseName}.pdf");
                    }
                case ExportFormat.Docx:
                    {
                        using var doc = new Aspose.Pdf.Document(input);
                        var ms = new MemoryStream();
                        var opts = new Aspose.Pdf.DocSaveOptions { Format = Aspose.Pdf.DocSaveOptions.DocFormat.DocX };
                        doc.Save(ms, opts);
                        return (ms.ToArray(),
                                "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                                $"{baseName}.docx");
                    }
                case ExportFormat.Images:
                    {
                        using var doc = new Aspose.Pdf.Document(input);
                        var dpi = req.Dpi ?? 150;
                        var res = new Aspose.Pdf.Devices.Resolution(dpi);

                        using var zipMs = new MemoryStream();
                        using (var zip = new ZipArchive(zipMs, ZipArchiveMode.Create, leaveOpen: true))
                        {
                            for (int i = 1; i <= doc.Pages.Count; i++)
                            {
                                var ext = req.ImageFormat == ImageFormat.Png ? "png" : "jpg";
                                var entry = zip.CreateEntry($"{baseName}_p{i}.{ext}", CompressionLevel.Optimal);

                                using var pageMs = new MemoryStream();
                                if (req.ImageFormat == ImageFormat.Png)
                                    new Aspose.Pdf.Devices.PngDevice(res).Process(doc.Pages[i], pageMs);
                                else
                                    new Aspose.Pdf.Devices.JpegDevice(res, 85).Process(doc.Pages[i], pageMs);

                                pageMs.Position = 0;
                                using var es = entry.Open();
                                pageMs.CopyTo(es);
                            }
                        }

                        zipMs.Position = 0;
                        return (zipMs.ToArray(), "application/zip", $"{baseName}_images.zip");
                    }
                default: throw new NotSupportedException("Unknown export format.");
            }
        }

        /// <summary>
        /// Opens a stored pdf for download (uploads or processed)
        /// </summary>
        public FileStream OpenStoredFile(string area, string fileName)
        {
            var path = area.ToLowerInvariant() switch
            {
                "uploads" => UploadedPath(fileName),
                "processed" => ProcessedPath(fileName),
                _ => throw new ArgumentException("Invalid area.")
            };
            if (!File.Exists(path)) throw new FileNotFoundException("File not found.", fileName);
            return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        }

        /// <summary>
        /// Renders a one off page preview that includes unsaved edits (server-side), without writing a new PDF
        /// </summary>
        public async Task<byte[]> GetPreviewAfterEditsAsync(PreviewAfterEditsRequest req)
        {
            var src = ResolveExistingPath(req.FileName);
            using var doc = new Document(src);

            var tmp = new EditRequest
            {
                Overlays = req.Overlays,
                Replacements = req.Replacements,
                Metadata = req.Metadata
            };
            ApplyEditsInMemory(doc, tmp);

            if (req.Page < 1 || req.Page > doc.Pages.Count)
                throw new ArgumentOutOfRangeException(nameof(req.Page), "Page out of range.");

            using var ms = new MemoryStream();
            var res = new Resolution(req.Dpi);
            if (req.ImageFormat == ImageFormat.Png) new PngDevice(res).Process(doc.Pages[req.Page], ms);
            else new JpegDevice(res, 85).Process(doc.Pages[req.Page], ms);
            return await Task.FromResult(ms.ToArray());
        }

        /// <summary>
        /// Core routine that mutates an aspose document in memory using the requested edits
        /// </summary>
        private static void ApplyEditsInMemory(Document doc, EditRequest req)
        {
            // replacements
            if (req.Replacements?.Count > 0)
            {
                foreach (var r in req.Replacements)
                {
                    var absorber = new TextFragmentAbsorber(r.Find);
                    absorber.TextSearchOptions = new TextSearchOptions(r.CaseSensitive)
                    {
                        IsRegularExpressionUsed = false
                    };
                    doc.Pages.Accept(absorber);
                    foreach (TextFragment frag in absorber.TextFragments)
                        frag.Text = r.ReplaceWith;
                }
            }

            // overlays
            if (req.Overlays?.Count > 0)
            {
                foreach (var ov in req.Overlays)
                {
                    var page = doc.Pages[ov.Page];
                    var tf = new TextFragment(ov.Text) { Position = new Position(ov.X, ov.Y) };
                    tf.TextState.FontSize = ov.FontSize;
                    if (!string.IsNullOrWhiteSpace(ov.FontName))
                        tf.TextState.Font = FontRepository.FindFont(ov.FontName);
                    tf.TextState.ForegroundColor = ParseColor(ov.ColorHex);
                    page.Paragraphs.Add(tf);
                }
            }

            // metadata
            if (req.Metadata?.Count > 0)
            {
                foreach (var kv in req.Metadata)
                {
                    if (string.IsNullOrWhiteSpace(kv.Key)) continue;

                    var rawKey = kv.Key.Trim();
                    var val = kv.Value ?? string.Empty;
                    var key = rawKey.ToLowerInvariant();

                    try
                    {
                        switch (key)
                        {
                            case "title": doc.Info.Title = val; break;
                            case "author": doc.Info.Author = val; break;
                            case "subject": doc.Info.Subject = val; break;
                            case "keywords": doc.Info.Keywords = val; break;
                            case "creator": doc.Info.Creator = val; break;
                            case "producer": doc.Info.Producer = val; break;

                            default:
                                var safe = System.Text.RegularExpressions.Regex.Replace(rawKey, @"[^A-Za-z0-9_.-]", "");
                                if (string.IsNullOrWhiteSpace(safe)) break;

                                var xmpKey = safe.StartsWith("/") ? safe : "/" + safe;
                                try { doc.Metadata[xmpKey] = val; } catch { }
                                break;
                        }
                    }
                    catch
                    {
                       
                    }
                }
            }
        }

        /// <summary>
        /// Parses a color into an aspose Pdf color
        /// </summary>
        private static Aspose.Pdf.Color ParseColor(string hex)
        {
            hex = hex?.TrimStart('#') ?? "000000";
            if (hex.Length != 6) hex = "000000";
            var r = Convert.ToInt32(hex[..2], 16);
            var g = Convert.ToInt32(hex.Substring(2, 2), 16);
            var b = Convert.ToInt32(hex.Substring(4, 2), 16);
            return Aspose.Pdf.Color.FromRgb(System.Drawing.Color.FromArgb(r, g, b));
        }

        /// <summary>
        /// Removes the trailing guid noise aspose/ workflow adds and returns a clean base file name.
        /// </summary>
        public string CleanBaseName(string fileName)
        {
            var name = Path.GetFileNameWithoutExtension(fileName);
            name = Regex.Replace(
                name,
                @"((?:_edited)?_[0-9a-fA-F]{32})+$",
                "",
                RegexOptions.IgnoreCase
            );

            return string.IsNullOrWhiteSpace(name) ? "document" : name;
        }

        /// <summary>
        /// Reads both core pdf info and any metadata into a simple dictionary.
        /// </summary>
        public IDictionary<string, string?> ReadMetadata(string fileName)
        {
            var path = ResolveExistingPath(fileName);
            using var doc = new Aspose.Pdf.Document(path);

            var meta = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["Title"] = doc.Info.Title,
                ["Author"] = doc.Info.Author,
                ["Subject"] = doc.Info.Subject,
                ["Keywords"] = doc.Info.Keywords,
                ["Creator"] = doc.Info.Creator,
                ["Producer"] = doc.Info.Producer
            };

            foreach (var key in doc.Metadata.Keys)
            {
                var niceKey = (key ?? string.Empty).TrimStart('/');
                if (string.IsNullOrWhiteSpace(niceKey)) continue;
                if (meta.ContainsKey(niceKey)) continue;

                try
                {
                    var x = doc.Metadata[key];
                    meta[niceKey] = x?.ToString();
                }
                catch
                {
                   
                }
            }

            return meta;
        }

        /// <summary>
        /// normalizes a proposed file base
        /// </summary>
        private static string SanitizeFileBase(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "document";
            var cleaned = System.Text.RegularExpressions.Regex.Replace(s, @"[^A-Za-z0-9._\- ]+", "");
            cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"\s+", " ").Trim();
            return string.IsNullOrWhiteSpace(cleaned) ? "document" : cleaned;
        }

        /// <summary>
        /// If the pdf has a title, prefer it otherwise fall back to a cleaned base name from the file
        /// </summary>
        private string PreferTitleOrBase(string physicalPath, string fallbackFromName)
        {
            try
            {
                using var d = new Aspose.Pdf.Document(physicalPath);
                var title = d.Info?.Title;
                if (!string.IsNullOrWhiteSpace(title))
                    return SanitizeFileBase(title);
            }
            catch
            {
          
            }
            return SanitizeFileBase(CleanBaseName(fallbackFromName));
        }

        /// <summary>
        /// ensures a filename is unique within a directory
        /// </summary>
        private static string EnsureUnique(string fileName, string directory)
        {
            var name = Path.GetFileNameWithoutExtension(fileName);
            var ext = Path.GetExtension(fileName);
            var candidate = Path.Combine(directory, fileName);
            int i = 1;
            while (File.Exists(candidate))
            {
                candidate = Path.Combine(directory, $"{name} ({i++}){ext}");
            }
            return Path.GetFileName(candidate);
        }
    }
}
