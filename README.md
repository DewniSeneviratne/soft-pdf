# soft-pdf
Setup

Backend (.NET 8)

cd PdfEditorBackend
dotnet restore
dotnet add package Aspose.PDF
dotnet run

Runs at e.g. https://localhost:7261
<br/><br/><br/>
Frontend (static HTML/JS)

In frontend/js/config.js set

export const API_BASE = 'https://localhost:7261/api/pdf';
export const PREVIEW_DPI = 150;
Open the index files with live server
<br/><br/><br/>
What it does

Upload PDF >> temp store on disk.

Preview pages as images.

Edit- add text overlays, find/replace text, update metadata.

Export- Updated PDF, Word (.docx), Images (ZIP) (one image per page).

Download- Browser prompts for save location (supports native picker in Chromium).
<br/><br/><br/>

Libraries

Aspose.PDF – PDF processing, page rendering, DOCX export.

ASP.NET Core Web API – backend.
<br/><br/><br/>
Assumptions / Notes

No DB. uses filesystem only.

Preview is image-based, edits are applied server-side on save or preview

Text replace is best-effort (depends on original PDF text layout).

<br/><br/><br/>
Quick Flow

Run backend.

Open frontend.

Upload >> Edit (overlay/replace/metadata) >> Preview or Save final.

Export >> choose format >> browser asks where to save.
