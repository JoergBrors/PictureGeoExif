"use strict";
const map = L.map('map', {worldCopyJump:true}).setView([51.1657,10.4515],6);
let currentMarker=null, selectedMarker=null, imageMarkers=[], gridLayer=null, tiles=null;
let currentGridSize=100, gridEnabled=false;
const notice=document.getElementById('notice');
function send(message){if(window.chrome?.webview) window.chrome.webview.postMessage(message);}
function status(text){notice.textContent=text; notice.hidden=!text; send({type:'status',text:text||'Klicken Sie auf die Karte, um GPS-Koordinaten auszuwählen.'});}
const clickIcon=L.divIcon({className:'pin',html:'<div style="background:#e65d35;width:100%;height:100%;border-radius:50%"></div>',iconSize:[16,16]});
function popup(name,lat,lng){const node=document.createElement('div');node.textContent=`${name} — ${lat.toFixed(6)}, ${lng.toFixed(6)}`;return node;}
function valid(lat,lng){return Number.isFinite(lat)&&Number.isFinite(lng)&&Math.abs(lat)<=90&&Math.abs(lng)<=180;}
function configure(config){
 const uri=new URL(config.url.replace('{s}','a').replace('{z}','0').replace('{x}','0').replace('{y}','0'));
 const attributionUrl=new URL(config.attributionUrl);
 if(uri.protocol!=='https:'||attributionUrl.protocol!=='https:') {status('Kartenanbieter benötigt HTTPS.');return;}
 if(tiles)map.removeLayer(tiles);
 const link=document.createElement('a');link.href=attributionUrl.href;link.target='_blank';link.rel='noopener';link.textContent=config.attribution;
 tiles=L.tileLayer(config.url,{maxZoom:19,attribution:'© '+link.outerHTML,keepBuffer:1,updateWhenIdle:true}).addTo(map);
 tiles.on('tileerror',()=>status('Kartenkacheln nicht verfügbar. Verbindung oder Anbieter prüfen.'));
 tiles.on('tileload',()=>status(''));
 status('Kartenkacheln werden geladen …');
}
function clearCurrentMarker(){if(currentMarker)map.removeLayer(currentMarker);currentMarker=null;}
function setCurrentMarker(lat,lng){if(!valid(lat,lng))return;clearCurrentMarker();currentMarker=L.marker([lat,lng],{icon:clickIcon}).addTo(map).bindPopup(popup('Neue Koordinaten',lat,lng)).openPopup();map.setView([lat,lng],Math.max(map.getZoom(),13));}
map.on('click',e=>{const lat=e.latlng.lat,lng=((e.latlng.lng+180)%360+360)%360-180;if(!valid(lat,lng))return;
 clearCurrentMarker();currentMarker=L.marker([lat,lng],{icon:clickIcon}).addTo(map).bindPopup(popup('Neue Koordinaten',lat,lng)).openPopup();send({type:'coordinates',lat,lng});});
function addImageMarkers(markers){imageMarkers.forEach(m=>map.removeLayer(m));imageMarkers=[];
 markers.filter(m=>valid(m.lat,m.lng)).forEach(m=>{const marker=L.circleMarker([m.lat,m.lng],{radius:7,color:'#287e4c'}).addTo(map).bindPopup(popup(m.name,m.lat,m.lng));
 marker.on('click',()=>{setSelectedMarker(m.lat,m.lng,m.name);send({type:'select',id:m.id});});imageMarkers.push(marker);});
 if(imageMarkers.length)map.fitBounds(L.featureGroup(imageMarkers).getBounds().pad(0.1),{maxZoom:15});}
function setSelectedMarker(lat,lng,name){if(!valid(lat,lng))return;clearSelectedMarker();selectedMarker=L.circleMarker([lat,lng],{radius:10,color:'#1475ce'}).addTo(map).bindPopup(popup(name,lat,lng)).openPopup();map.setView([lat,lng],Math.max(map.getZoom(),13));}
function clearSelectedMarker(){if(selectedMarker)map.removeLayer(selectedMarker);selectedMarker=null;}
function updateGrid(enabled,size){gridEnabled=!!enabled;currentGridSize=Number(size);if(gridLayer)map.removeLayer(gridLayer);gridLayer=null;
 if(!gridEnabled||!Number.isFinite(currentGridSize)||currentGridSize<10)return;
 if(map.getZoom()<13){status('Raster erst ab Zoomstufe 13 sichtbar.');return;}
 const bounds=map.getBounds(), crs=L.CRS.EPSG3857;
 const a=crs.project(bounds.getSouthWest()),b=crs.project(bounds.getNorthEast());
 // Mercator scale correction around viewport centre; local metric grid, capped for UI responsiveness.
 const step=currentGridSize/Math.max(0.1,Math.cos(map.getCenter().lat*Math.PI/180));
 const x0=Math.floor(a.x/step)*step,y0=Math.floor(a.y/step)*step;
 const count=Math.ceil((b.x-x0)/step)+Math.ceil((b.y-y0)/step)+2;
 if(count>300){status('Raster zu dicht: weiter hineinzoomen oder Rasterweite erhöhen.');return;}
 gridLayer=L.layerGroup().addTo(map);
 for(let x=x0;x<=b.x;x+=step)L.polyline([crs.unproject(L.point(x,a.y)),crs.unproject(L.point(x,b.y))],{weight:1,color:'#367cab',interactive:false}).addTo(gridLayer);
 for(let y=y0;y<=b.y;y+=step)L.polyline([crs.unproject(L.point(a.x,y)),crs.unproject(L.point(b.x,y))],{weight:1,color:'#367cab',interactive:false}).addTo(gridLayer);
}
map.on('moveend',()=>updateGrid(gridEnabled,currentGridSize));
new ResizeObserver(()=>map.invalidateSize()).observe(document.getElementById('map'));
if(window.chrome?.webview)send({type:'ready'});else configure({url:'https://tile.openstreetmap.org/{z}/{x}/{y}.png',attribution:'OpenStreetMap contributors',attributionUrl:'https://www.openstreetmap.org/copyright'});
