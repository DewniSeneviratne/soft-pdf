namespace PdfEditorBackend.Models
{
    public enum ExportFormat { Pdf, Docx, Images }
    public enum ImageFormat { Png, Jpeg }

    public class UploadResult
    {
        public string FileName { get; set; } = default!;
        public int PageCount { get; set; }
    }

    public class TextOverlay
    {
        public int Page { get; set; }
        public float X { get; set; }      
        public float Y { get; set; }
        public string Text { get; set; } = "";
        public float FontSize { get; set; } = 12;
        public string? FontName { get; set; }
        public string ColorHex { get; set; } = "#000000";
    }

    public class TextReplace
    {
        public string Find { get; set; } = "";
        public string ReplaceWith { get; set; } = "";
        public bool CaseSensitive { get; set; } = false;
        public bool WholeWord { get; set; } = false;
    }

    public class EditRequest
    {
        public string FileName { get; set; } = default!;
        public bool SaveAsNew { get; set; } = true;
        public List<TextOverlay>? Overlays { get; set; }
        public List<TextReplace>? Replacements { get; set; }
        public Dictionary<string, string>? Metadata { get; set; }
    }

    public class ExportRequest
    {
        public string FileName { get; set; } = default!;
        public ExportFormat Format { get; set; } = ExportFormat.Pdf;
        public ImageFormat ImageFormat { get; set; } = ImageFormat.Png;
        public int? Dpi { get; set; } = 150;
    }

    public class PreviewAfterEditsRequest
    {
        public string FileName { get; set; } = default!;
        public int Page { get; set; } = 1;
        public int Dpi { get; set; } = 150;
        public ImageFormat ImageFormat { get; set; } = ImageFormat.Png;

        public List<TextOverlay>? Overlays { get; set; }
        public List<TextReplace>? Replacements { get; set; }
        public Dictionary<string, string>? Metadata { get; set; }
    }
}
