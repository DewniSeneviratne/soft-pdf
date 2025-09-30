const KEY = 'uploadedFiles';
const CUR = 'currentFile';

export function addRecent(name, pages){
  const arr = JSON.parse(localStorage.getItem(KEY) || '[]');
  if(!arr.some(x=>x.name===name)) arr.unshift({name, pages});
  localStorage.setItem(KEY, JSON.stringify(arr.slice(0,25)));
}

export function getRecents(){
  return JSON.parse(localStorage.getItem(KEY) || '[]');
}

export function setCurrent(name){
  localStorage.setItem(CUR, name);
}

export function getCurrent(){
  return localStorage.getItem(CUR);
}
