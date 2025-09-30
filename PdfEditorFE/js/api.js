import { API_BASE } from './config.js';

export async function uploadPdf(file){
  const fd = new FormData(); fd.append('file', file);
  const res = await fetch(`${API_BASE}/upload`, { method:'POST', body:fd });
  if(!res.ok) throw new Error('Upload failed');
  return res.json();
}

export async function getPageCount(fileName){
  const res = await fetch(`${API_BASE}/pagecount/${encodeURIComponent(fileName)}`);
  if(!res.ok) throw new Error('Failed to get page count');
  return res.json();
}

export function pagePreviewUrl(fileName, page, fmt='png', dpi=150){
  return `${API_BASE}/preview/${encodeURIComponent(fileName)}?page=${page}&fmt=${fmt}&dpi=${dpi}`;
}

export async function applyEdits(payload){
  const res = await fetch(`${API_BASE}/edit`, {
    method:'POST', headers:{'Content-Type':'application/json'}, body: JSON.stringify(payload)
  });
  if(!res.ok) throw new Error('Edit failed');
  return res.json();
}

export async function doExport(payload){
  const res = await fetch(`${API_BASE}/export`, {
    method:'POST', headers:{'Content-Type':'application/json'}, body: JSON.stringify(payload)
  });
  if(!res.ok) throw new Error('Export failed');
  const blob = await res.blob();
  const cd = res.headers.get('Content-Disposition') || res.headers.get('content-disposition');
  let suggested = null;
  if(cd){
    const m = cd.match(/filename="?([^"]+)"?/i);
    if(m) suggested = m[1];
  }
  return { blob, suggestedName: suggested };
}

export function directDownload(fileName){
  window.open(`${API_BASE.replace('/api/pdf','')}/api/pdf/download/${encodeURIComponent(fileName)}`,'_blank');
}
export async function previewAfterEdits(payload){
  const res = await fetch(`${API_BASE}/preview-after-edits`, {
    method:'POST',
    headers:{ 'Content-Type':'application/json' },
    body: JSON.stringify(payload)
  });
  if(!res.ok) throw new Error('Preview failed');
  return res.blob();
}
