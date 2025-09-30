import { applyEdits, getPageCount, pagePreviewUrl, previewAfterEdits } from './api.js';

import { PREVIEW_DPI } from './config.js';
import { addRecent, getCurrent, setCurrent } from './state.js';
import { pxToPdfPoint } from './utils.js';

let fileName = getCurrent();
// selection state for overlay labels
let selectedOverlayId = null;

if(!fileName){ alert('No file selected. Upload a PDF first.'); location.href='upload.html'; }

const thumbsEl = document.querySelector('#thumbs');
const stage = document.querySelector('#stage');
const pageImg = document.querySelector('#pageImg');
const pageBadge = document.querySelector('#pageBadge');
const goPrev = document.querySelector('#prev'); const goNext = document.querySelector('#next');

const txtText = document.querySelector('#ovText');
const txtSize = document.querySelector('#ovSize');
const txtColor = document.querySelector('#ovColor');
const findTxt = document.querySelector('#find');
const replTxt = document.querySelector('#replace');
const caseChk = document.querySelector('#case');
const wholeChk= document.querySelector('#whole');
const metaTitle = document.querySelector('#metaTitle');
const metaAuthor= document.querySelector('#metaAuthor');
const metaSubject=document.querySelector('#metaSubject');
const metaKeywords=document.querySelector('#metaKeywords');
const btnSave = document.querySelector('#saveEdits');
const btnReset = document.querySelector('#resetOverlay');

let pageCount = 0;
let currentPage = 1;

let overlays = []; 
let replacements = [];
const replList = document.querySelector('#replList');

init();

async function init(){
  pageCount = await getPageCount(fileName);
  renderThumbs();
  loadPage(1);
}

function renderThumbs(){
  thumbsEl.innerHTML='';
  for(let p=1;p<=pageCount;p++){
    const img = document.createElement('img');
    img.className='thumb';
    img.src = pagePreviewUrl(fileName, p, 'png', 72);
    img.alt=`Page ${p}`;
    img.onclick= ()=> loadPage(p);
    thumbsEl.appendChild(img);
  }
}

function selectThumb(){
  const nodes = [...thumbsEl.querySelectorAll('.thumb')];
  nodes.forEach((n,i)=> n.classList.toggle('active', i+1===currentPage));
}

async function loadPage(p){
  currentPage = Math.max(1, Math.min(pageCount, p));
  pageImg.src = pagePreviewUrl(fileName, currentPage, 'png', PREVIEW_DPI) + '&cb=' + Date.now();
  pageBadge.textContent = `${currentPage} / ${pageCount}`;
  selectThumb();
  renderOverlayDots();
}

stage.addEventListener('click', e=>{
  if(e.target.id!=='pageImg') return;
  const r = pageImg.getBoundingClientRect();
  const x = e.clientX - r.left;
  const y = e.clientY - r.top;
  const text = txtText.value?.trim() || 'New text';
  const size = parseFloat(txtSize.value||'16');
  const color = txtColor.value || '#111111';
  overlays.push({ page: currentPage, x, y, text, size, color });
  renderOverlayDots();
});

window.addEventListener('click', ev=>{
  // click outside a dot but inside stage unselect
  if(stage.contains(ev.target) && !ev.target.classList.contains('overlay-dot')){
    selectedOverlayId = null;
    updateOverlaySelection();
  }
});
function renderOverlayDots(){
  // clear
  [...stage.querySelectorAll('.overlay-dot')].forEach(n=>n.remove());

  const ptToPx = PREVIEW_DPI / 72;

  overlays.filter(o=>o.page===currentPage).forEach(o=>{
    if(!o.id) o.id = crypto.randomUUID(); // stable identity per overlay

    const dot = document.createElement('div');
    dot.className = 'overlay-dot';
    dot.dataset.id = o.id;
    dot.style.left = o.x + 'px';
    dot.style.top  = o.y + 'px';
    dot.textContent = o.text;
    dot.title = 'Drag to move • Double-click to edit • Del/Backspace to remove';
    dot.style.fontSize = (o.size * ptToPx) + 'px';
    dot.style.color = o.color;

    // selection
    dot.addEventListener('click', ev=>{
      ev.stopPropagation();               
      selectedOverlayId = o.id;
      updateOverlaySelection();
    });

    // inline edit
    dot.addEventListener('dblclick', ()=>{
      const nt = prompt('Change text:', o.text);
      if(nt !== null){
        o.text = nt;
        renderOverlayDots();
      }
    });

    // drag
    let dragging=false, dx=0, dy=0;
    dot.addEventListener('mousedown', ev=>{
      dragging=true; dx=ev.offsetX; dy=ev.offsetY; ev.preventDefault();
      selectedOverlayId = o.id; updateOverlaySelection();
    });
    window.addEventListener('mouseup', ()=> dragging=false);
    window.addEventListener('mousemove', ev=>{
      if(!dragging) return;
      const nr = pageImg.getBoundingClientRect();
      let nx = ev.clientX - nr.left - dx + dot.offsetWidth/2;
      let ny = ev.clientY - nr.top  - dy + dot.offsetHeight/2;
      nx = Math.max(0, Math.min(nr.width, nx));
      ny = Math.max(0, Math.min(nr.height, ny));
      dot.style.left = nx+'px'; dot.style.top = ny+'px';
      o.x = nx; o.y = ny;
    });

    stage.appendChild(dot);
  });

  updateOverlaySelection();
}

function updateOverlaySelection(){
  stage.querySelectorAll('.overlay-dot').forEach(n=>{
    n.classList.toggle('selected', n.dataset.id === selectedOverlayId);
  });
}


goPrev.onclick = ()=> loadPage(currentPage-1);
goNext.onclick = ()=> loadPage(currentPage+1);

document.querySelector('#addReplace').onclick = ()=>{
  const f = (findTxt.value||'').trim(); const w = (replTxt.value||'').trim();
  if(!f) return alert('Find text is required');
  replacements.push({ find:f, replaceWith:w, caseSensitive:!!caseChk.checked, wholeWord:!!wholeChk.checked });
  const li = document.createElement('div');
  li.className='pill'; li.textContent = `"${f}" → "${w||'(remove)'}"`;
  replList.appendChild(li);
  findTxt.value=''; replTxt.value='';
};

btnReset.onclick = ()=>{
  overlays = []; replacements = []; replList.innerHTML='';
  renderOverlayDots();
};

btnSave.onclick = async ()=>{
  const imgRect = pageImg.getBoundingClientRect();
  const stageH = imgRect.height;
  const ovPoints = overlays.map(o=>{
    const pt = pxToPdfPoint(o.x, o.y, stageH);
    return {
      page: o.page, x: +pt.xPt.toFixed(2), y: +pt.yPt.toFixed(2),
      text: o.text, fontSize: o.size, colorHex: o.color
    };
  });

  const metadata = {};
  if(metaTitle.value) metadata.Title = metaTitle.value;
  if(metaAuthor.value) metadata.Author = metaAuthor.value;
  if(metaSubject.value) metadata.Subject = metaSubject.value;
  if(metaKeywords.value) metadata.Keywords = metaKeywords.value;

  const payload = {
    fileName,
    saveAsNew: true,
    overlays: ovPoints,
    replacements: replacements.length ? replacements : undefined,
    metadata: Object.keys(metadata).length ? metadata : undefined
  };

  try{
    const res = await applyEdits(payload);
    fileName = res.fileName;                   // adopt new file
    setCurrent(fileName);
    addRecent(fileName, pageCount);
    alert('Edits applied');
    await loadPage(currentPage);               // refresh preview
  }catch(err){
    alert(err.message || 'Failed to apply edits');
  }
};
const btnPreview = document.querySelector('#previewEdits');

btnPreview.onclick = async ()=>{
  // build the same payload you send to save, but call preview
  const imgRect = pageImg.getBoundingClientRect();
  const stageH = imgRect.height;

  const ovPoints = overlays.map(o=>{
    const pt = pxToPdfPoint(o.x, o.y, stageH);
    return {
      page: o.page, x: +pt.xPt.toFixed(2), y: +pt.yPt.toFixed(2),
      text: o.text, fontSize: o.size, colorHex: o.color
    };
  });

  const metadata = {};
  if(metaTitle.value) metadata.Title = metaTitle.value;
  if(metaAuthor.value) metadata.Author = metaAuthor.value;
  if(metaSubject.value) metadata.Subject = metaSubject.value;
  if(metaKeywords.value) metadata.Keywords = metaKeywords.value;

  const payload = {
    fileName,
    page: currentPage,
    dpi: PREVIEW_DPI,
    imageFormat: 'Png',
    overlays: ovPoints,
    replacements: replacements.length ? replacements : undefined,
    metadata: Object.keys(metadata).length ? metadata : undefined
  };

  try{
    const blob = await previewAfterEdits(payload);
    // show the preview without saving a file
    pageImg.src = URL.createObjectURL(blob);
  }catch(err){
    alert(err.message || 'Preview failed');
  }
};
window.addEventListener('keydown', e=>{
  const isEditingField = ['INPUT','TEXTAREA'].includes(document.activeElement?.tagName);
  if((e.key === 'Delete' || e.key === 'Backspace') && !isEditingField && selectedOverlayId){
    overlays = overlays.filter(o => o.id !== selectedOverlayId);
    selectedOverlayId = null;
    renderOverlayDots();
    e.preventDefault();
  }
});