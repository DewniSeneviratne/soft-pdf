import { uploadPdf } from './api.js';
import { addRecent, setCurrent } from './state.js';

const drop = document.querySelector('#drop');
const fileInput = document.querySelector('#file');
const btn = document.querySelector('#btnUpload');
const bar = document.querySelector('#bar');

drop.addEventListener('dragover', e=>{ e.preventDefault(); drop.classList.add('drag'); });
drop.addEventListener('dragleave', ()=> drop.classList.remove('drag'));
drop.addEventListener('drop', async e=>{
  e.preventDefault(); drop.classList.remove('drag');
  if(!e.dataTransfer.files?.length) return;
  await handle(e.dataTransfer.files[0]);
});
fileInput.addEventListener('change', async e=>{
  if(!fileInput.files?.length) return;
  await handle(fileInput.files[0]);
});
btn.addEventListener('click', ()=> fileInput.click());

async function handle(file){
  if(!file.name.toLowerCase().endsWith('.pdf')){ alert('Please choose a PDF.'); return; }
  bar.style.width='0';
  await fakeProgress();
  try{
    const res = await uploadPdf(file);
    addRecent(res.fileName, res.pageCount);
    setCurrent(res.fileName);
    alert(`Uploaded Pages: ${res.pageCount}`);
    window.location.href = 'edit.html';
  }catch(err){
    alert(err.message || 'Upload failed');
  }finally{
    bar.style.width='0';
  }
}

async function fakeProgress(){
  // purely cosmetic real fetch doesn't give progress without xhr plumbing
  let w=0; return new Promise(r=>{
    const t = setInterval(()=>{
      w += Math.random()*12; if(w>96) w=96;
      bar.style.width = w+'%';
    },100);
    setTimeout(()=>{ clearInterval(t); bar.style.width='100%'; setTimeout(r,200)}, 1200);
  });
}
