export function downloadBlob(blob, filename){
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url; a.download = filename || 'download';
  document.body.appendChild(a); a.click(); a.remove();
  URL.revokeObjectURL(url);
}

// Convert click px (from top-left image) to PDF points (origin bottom-left)
export function pxToPdfPoint(xPx, yPxFromTop, imgHeightPx){
  // 1 CSS px maps to points on your preview
  const DPI = 150; // or import preview_dpi
  const ptPerPx = 72 / DPI;
  const xPt = xPx * ptPerPx;
  const yPt = (imgHeightPx - yPxFromTop) * ptPerPx;
  return { xPt, yPt };
}
