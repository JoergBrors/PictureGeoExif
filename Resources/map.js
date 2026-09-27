"use strict";
// Double click is reserved for placing a new GPS point, so it must not zoom.
const map = L.map('map', {worldCopyJump:true, doubleClickZoom:false}).setView([51.1657,10.4515],6);
const roadLayer=L.layerGroup().addTo(map), routeLayer=L.layerGroup().addTo(map), imageLayer=L.layerGroup().addTo(map);
let currentMarker=null, selectedMarker=null, gridLayer=null, tiles=null;
let currentGridSize=100, gridEnabled=false;
const notice=document.getElementById('notice');
const routeColors=['#d9480f','#1971c2','#2f9e44','#9c36b5','#e67700','#0c8599','#c2255c','#5c940d'];
function send(message){if(window.chrome?.webview) window.chrome.webview.postMessage(message);}
function status(text){notice.textContent=text; notice.hidden=!text; send({type:'status',text:text||'Doppelklick auf die Karte setzt neue GPS-Koordinaten.'});}
const clickIcon=L.divIcon({className:'pin',html:'<div style="background:#e65d35;width:100%;height:100%;border-radius:50%"></div>',iconSize:[16,16]});
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

// Names and coordinates are shown in the app above the map (no popups covering other points).
function clearCurrentMarker(){if(currentMarker)map.removeLayer(currentMarker);currentMarker=null;}
function setCurrentMarker(lat,lng){if(!valid(lat,lng))return;clearCurrentMarker();currentMarker=L.marker([lat,lng],{icon:clickIcon,interactive:false}).addTo(map);map.setView([lat,lng],Math.max(map.getZoom(),13));}
map.on('dblclick',e=>{const lat=e.latlng.lat,lng=((e.latlng.lng+180)%360+360)%360-180;if(!valid(lat,lng))return;
 clearCurrentMarker();currentMarker=L.marker([lat,lng],{icon:clickIcon,interactive:false}).addTo(map);send({type:'coordinates',lat,lng});});

function addImageMarkers(markers,fit){imageLayer.clearLayers();
 markers.filter(m=>valid(m.lat,m.lng)).forEach(m=>{const marker=L.circleMarker([m.lat,m.lng],{radius:7,color:'#287e4c',weight:2,fillOpacity:0.6}).addTo(imageLayer);
  marker.on('click',()=>{setSelectedMarker(m.lat,m.lng,false);send({type:'select',id:m.id});});
  marker.on('mouseover',()=>send({type:'hover',id:m.id}));
  marker.on('mouseout',()=>send({type:'hover',id:-1}));});
 if(fit&&imageLayer.getLayers().length)map.fitBounds(L.featureGroup(imageLayer.getLayers()).getBounds().pad(0.1),{maxZoom:17});}

function setSelectedMarker(lat,lng,center){if(!valid(lat,lng))return;clearSelectedMarker();
 selectedMarker=L.circleMarker([lat,lng],{radius:11,color:'#1475ce',weight:3,fill:false,interactive:false}).addTo(map);
 if(center!==false&&!map.getBounds().contains([lat,lng]))map.panTo([lat,lng]);}
function clearSelectedMarker(){if(selectedMarker)map.removeLayer(selectedMarker);selectedMarker=null;}

// routes: [{number, lengthMeters, points:[[lat,lng],...] (trunk, south→north / west→east), branches:[[attach,[lat,lng],...]]}]
function setRoutes(routes){routeLayer.clearLayers();
 routes.forEach(r=>{const pts=r.points.filter(p=>valid(p[0],p[1]));if(pts.length<2)return;
  const color=routeColors[(r.number-1)%routeColors.length];
  const line=L.polyline(pts,{color,weight:4,opacity:0.8}).addTo(routeLayer);
  L.circleMarker(pts[0],{radius:5,color,fill:true,fillOpacity:1,interactive:false}).addTo(routeLayer);
  L.circleMarker(pts[pts.length-1],{radius:5,color,fill:true,fillColor:'#fff',fillOpacity:1,interactive:false}).addTo(routeLayer);
  line.on('mouseover',()=>send({type:'routeHover',number:r.number}));
  line.on('mouseout',()=>send({type:'routeHover',number:0}));
  (r.branches||[]).forEach(b=>{const bp=b.filter(p=>valid(p[0],p[1]));if(bp.length<2)return;
   L.polyline(bp,{color,weight:3,opacity:0.8,dashArray:'8 4'}).addTo(routeLayer)
    .on('mouseover',()=>send({type:'routeHover',number:r.number})).on('mouseout',()=>send({type:'routeHover',number:0}));
   L.circleMarker(bp[0],{radius:3,color,fill:true,fillOpacity:1,interactive:false}).addTo(routeLayer);});});
 imageLayer.eachLayer(l=>l.bringToFront());}

// Road-matched routes: [{number, segments:[{onRoad, points:[[lat,lng],...]}]}]. Straight gaps = photo off the way network.
function setRoadRoutes(routes){roadLayer.clearLayers();
 routes.forEach(r=>{const color=routeColors[(r.number-1)%routeColors.length];
  r.segments.forEach(s=>{const pts=s.points.filter(p=>valid(p[0],p[1]));if(pts.length<2)return;
   const style=s.onRoad?{color,weight:s.branch?4:6,opacity:0.55}:{color:s.branch?color:'#868e96',weight:s.branch?3:3,opacity:0.9,dashArray:s.branch?'8 4':'6 6'};
   const line=L.polyline(pts,style).addTo(roadLayer);
   line.on('mouseover',()=>send({type:'routeHover',number:r.number}));
   line.on('mouseout',()=>send({type:'routeHover',number:0}));});});
 imageLayer.eachLayer(l=>l.bringToFront());}

function setLayerVisible(name,visible){const layer=name==='routes'?routeLayer:name==='images'?imageLayer:name==='road'?roadLayer:null;if(!layer)return;
 if(visible&&!map.hasLayer(layer))layer.addTo(map);if(!visible&&map.hasLayer(layer))map.removeLayer(layer);
 imageLayer.eachLayer(l=>l.bringToFront());}

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
