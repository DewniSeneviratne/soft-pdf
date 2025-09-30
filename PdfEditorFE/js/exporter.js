import { doExport } from './api.js';
import { getCurrent } from './state.js';
import { downloadBlob } from './utils.js';

const fileName = getCurrent();
if(!fileName){ alert('No file selected. Upload a PDF first.'); location.href='upload.html'; }

const opts = document.querySelectorAll('.opt');
const fmtSelect = document.querySelector('#imgFmt');
const dpiInput = document.querySelector('#dpi');
const go = document.querySelector('#go');
let chosen = 'Pdf';

opts.forEach(o=>{
  o.addEventListener('click', ()=>{
    opts.forEach(x=>x.classList.remove('active')); o.classList.add('active');
    chosen = o.dataset.value;
    document.querySelector('#imagesOnly').style.display = (chosen==='Images')?'block':'none';
  });
});

// save helper that prefers native file picker when available
async function saveBlobAs(blob, suggestedName, chosen) {
  const hasNativePicker = 'showSaveFilePicker' in window;
  if (hasNativePicker) {
    // map export type to MIME/ extensions for better OS filters
    const typeConfig = {
      Pdf:  { desc: 'PDF',   mime: 'application/pdf', ext: ['.pdf'] },
      Docx: { desc: 'Word',  mime: 'application/vnd.openxmlformats-officedocument.wordprocessingml.document', ext: ['.docx'] },
      Images: { desc: 'ZIP', mime: 'application/zip', ext: ['.zip'] }
    }[chosen];

    try {
      const handle = await window.showSaveFilePicker({
        suggestedName,
        types: [{ description: typeConfig.desc, accept: { [typeConfig.mime]: typeConfig.ext } }]
      });
      const writable = await handle.createWritable();
      await writable.write(blob);
      await writable.close();
      return;
    } catch (err) {
 
    }
  }
  // Fallback- regular download (browser asks where to save if user has that preference)
  downloadBlob(blob, suggestedName);
}

go.addEventListener('click', async ()=>{
  const payload = {
    fileName,
    format: chosen,
    imageFormat: fmtSelect.value,
    dpi: parseInt(dpiInput.value||'150',10)
  };
  try{
    const { blob, suggestedName } = await doExport(payload);

    // prefer server-suggested name; otherwise build a clean one
    let downloadName = suggestedName;
    if(!downloadName){
      const cleanBase = fileName
        .replace(/\.(pdf)$/i, '')
        .replace(/((?:_edited)?_[0-9a-fA-F]{32})+$/i, '') || 'document';
      const ext = chosen==='Pdf'?'.pdf' : chosen==='Docx'?'.docx' : '.zip';
      downloadName = cleanBase + (chosen==='Images' ? '_images' : '') + ext;
    }

    await saveBlobAs(blob, downloadName, chosen);
  }catch(err){
    alert(err.message || 'Export failed');
  }
});
