import * as THREE from 'three';
import { OrbitControls } from 'three/addons/controls/OrbitControls.js';
import { BUILDINGS, UPGRADE, startingState, dateText, housing, canAfford, costText, placeBuilding, upgradeBuilding, endTurn, isValidSave } from './engine.mjs';

const $ = selector => document.querySelector(selector);
const STORAGE = 'little-empires-three-demo-v1';
let state = startingState();
try { const saved=JSON.parse(localStorage.getItem(STORAGE)||'null'); if(isValidSave(saved))state=saved; } catch {}
let activeBuild = null, selected = null, hover = null, dragging = false, pointerStart = null;
let toastTimer;
const seasonsIcons = ['🌱','☀️','🍂','❄️'];
const pretty = r => ({wood:'Wood',stone:'Stone',food:'Food',coins:'Coins'}[r]);
const toast = message => { const e=$('#toast');e.textContent=message;e.classList.add('visible');clearTimeout(toastTimer);toastTimer=setTimeout(()=>e.classList.remove('visible'),2800); };
const save = () => { try {localStorage.setItem(STORAGE,JSON.stringify(state));}catch{} };
const buildingAt=(x,z)=>state.buildings.find(b=>b.x===x&&b.z===z);

const host = $('#canvasHost');
const scene = new THREE.Scene(); scene.background = new THREE.Color('#95c0c4'); scene.fog = new THREE.Fog('#95c0c4',32,80);
const camera = new THREE.PerspectiveCamera(36,1,.1,150);
const camHome = new THREE.Vector3(25,23,26); camera.position.copy(camHome);
const renderer = new THREE.WebGLRenderer({antialias:true,alpha:false,powerPreference:'high-performance'});
renderer.setPixelRatio(Math.min(window.devicePixelRatio||1,2));
renderer.shadowMap.enabled = true; renderer.shadowMap.type=THREE.PCFSoftShadowMap;
renderer.outputColorSpace=THREE.SRGBColorSpace;
renderer.toneMapping=THREE.ACESFilmicToneMapping;
renderer.toneMappingExposure=1.48;
host.appendChild(renderer.domElement);
const controls = new OrbitControls(camera,renderer.domElement);
controls.target.set(0,0.2,0);controls.enableDamping=true;controls.dampingFactor=.08;controls.minDistance=17;controls.maxDistance=54;controls.minPolarAngle=.32;controls.maxPolarAngle=1.25;controls.enablePan=false;controls.update();
scene.add(new THREE.HemisphereLight('#d7eff6','#59795c',2.5));
const sun=new THREE.DirectionalLight('#fff3d6',3);sun.position.set(-14,25,16);sun.castShadow=true;sun.shadow.mapSize.set(2048,2048);sun.shadow.camera.left=-23;sun.shadow.camera.right=23;sun.shadow.camera.top=23;sun.shadow.camera.bottom=-23;sun.shadow.camera.near=1;sun.shadow.camera.far=80;sun.shadow.bias=-.0004;scene.add(sun);
const land=new THREE.Group();scene.add(land);
const water=new THREE.Mesh(new THREE.PlaneGeometry(160,160),new THREE.MeshStandardMaterial({color:'#6faeb5',roughness:.72,metalness:.06}));water.rotation.x=-Math.PI/2;water.position.y=-.2;water.receiveShadow=true;scene.add(water);
const waterRing=new THREE.Mesh(new THREE.RingGeometry(12.4,12.8,64),new THREE.MeshBasicMaterial({color:'#c4e8d9',transparent:true,opacity:.18,side:THREE.DoubleSide}));waterRing.rotation.x=-Math.PI/2;waterRing.position.y=-.13;waterRing.scale.set(1.2,.85,1);scene.add(waterRing);
const mat=(color,roughness=.85)=>new THREE.MeshStandardMaterial({color,roughness});
const mats={soil:mat('#8f7962'),sand:mat('#d7c49b'),stone:mat('#9dabb0'),wood:mat('#9c7458'),darkWood:mat('#6c5048'),roof:mat('#b96658'),roofAlt:mat('#d27e63'),wall:mat('#f3dab0'),wallAlt:mat('#ead1ac'),glass:mat('#80c4c0'),crop:mat('#e4ba6e'),green:mat('#81ad77'),rock:mat('#a2ada8'),bark:mat('#866b55'),leaf:mat('#5c956b'),leaf2:mat('#83af70'),leaf3:mat('#6a9e73'),track:mat('#c7b394')};
const tileMap=new Map(), tiles=[];const tileSize=1.72;
const key=(x,z)=>`${x},${z}`;
function seedRand(x,z,k=0){let n=Math.sin(x*127.1+z*311.7+k*43.37)*43758.5453;return n-Math.floor(n);}
function addMesh(group,geo,material,x=0,y=0,z=0,cast=true){const mesh=new THREE.Mesh(geo,material);mesh.position.set(x,y,z);mesh.castShadow=cast;mesh.receiveShadow=true;group.add(mesh);return mesh;}
function box(group,w,h,d,material,x=0,y=0,z=0){return addMesh(group,new THREE.BoxGeometry(w,h,d),material,x,y,z);}
function cone(group,r,h,material,x,y,z,n=5){return addMesh(group,new THREE.ConeGeometry(r,h,n),material,x,y,z);}
function tree(group,px,pz,s=1){const g=new THREE.Group();g.position.set(px,0,pz);box(g,.13*s,.45*s,.13*s,mats.bark,0,.23*s,0);const crown=cone(g,.47*s,1.12*s,seedRand(px,pz)>0.55?mats.leaf:mats.leaf2,0,.95*s,0,6);crown.rotation.y=.3;group.add(g);return g;}
function flowers(group, x,z){for(let j=0;j<5;j++){const a=j*2.4;addMesh(group,new THREE.SphereGeometry(.032,4,4),mat('#f8e8b2'),x+Math.cos(a)*.34,.055,z+Math.sin(a)*.27,false);}}
for(let x=-7;x<=7;x++)for(let z=-7;z<=7;z++){
  const radial=(x*x/(6.85*6.85)+z*z/(6.1*6.1));
  if(radial>.98+(seedRand(x,z)-.5)*.19)continue;
  const edge=radial>.71;const height=.58+(seedRand(x,z,1)-.5)*.21;
  const group=new THREE.Group();group.position.set(x*tileSize,0,z*tileSize);land.add(group);
  const soil=box(group,tileSize*.99,height,tileSize*.99,edge?mats.sand:mats.soil,0,(height-.06)/2-.02,0);
  const grassColor=edge?(seedRand(x,z,3)>.56?'#dbcb9b':'#c5bd89'):(seedRand(x,z,4)>.57?'#8cb780':'#91ba7f');
  const topMat=mat(grassColor);
  const top=box(group,tileSize*.995,.14,tileSize*.995,topMat,0,height,0);top.castShadow=false;
  const actualY=height+.07;
  const hit=addMesh(group,new THREE.PlaneGeometry(tileSize*.97,tileSize*.97),new THREE.MeshBasicMaterial({transparent:true,opacity:0,depthWrite:false,side:THREE.DoubleSide}),0,actualY+.017,0,false);
  hit.rotation.x=-Math.PI/2;hit.userData={x,z};tiles.push(hit);
  const deco=new THREE.Group();deco.position.y=actualY;group.add(deco);
  if(!edge && (x*x+z*z)>5 && seedRand(x,z,5)>.62){tree(deco,(seedRand(x,z,6)-.5)*.6,(seedRand(x,z,7)-.5)*.6,.62+seedRand(x,z,8)*.38);}
  else if(seedRand(x,z,9)>.66&&radial<.66)flowers(deco,.1,-.12);
  if(edge&&seedRand(x,z,10)>.84){let stone=cone(deco,.22,.22,mats.rock,.15,.12,-.1,6);stone.rotation.z=.45;}
  tileMap.set(key(x,z),{x,z,height:actualY,deco,group});
}
// Decorative founding settlement. Founding housing is modeled in the rules as baseHousing=8.
function drawFounders(){const center=tileMap.get(key(0,0));if(!center)return;const g=new THREE.Group();g.position.set(0,center.height,0);land.add(g);
  box(g,1.18,.86,1.04,mats.wallAlt,0,.43,0);box(g,1.3,.12,1.1,mats.wood,0,.1,0);
  const roof=cone(g,1.02,.83,mats.roofAlt,0,1.13,0,4);roof.rotation.y=Math.PI/4;
  box(g,.24,.53,.04,mats.darkWood,0,.33,.54);box(g,.19,.17,.045,mats.glass,-.34,.65,.55);
  const spire=cone(g,.14,.4,mats.roof,0,1.67,0,4);g.add(spire);
  for(let i=0;i<4;i++){const a=i*Math.PI*.5+.35;tree(g,Math.cos(a)*1.15,Math.sin(a)*1.15,.36);}
  center.deco.visible=false;
}
drawFounders();
function buildingVisual(def,b){const g=new THREE.Group();
  box(g,1.24,.12,1.24,mats.stone,0,.055,0);
  if(def.id==='cottage'||def.id==='townhouse'){
    const tall=def.id==='townhouse';box(g,1,.74+(tall?.49:0),.95,tall?mats.wall:mats.wallAlt,0,.46+(tall?.2:0),0);
    let roof=cone(g,.82,.65,tall?mats.roofAlt:mats.roof,0,(tall?1.48:1.06),0,4);roof.rotation.y=Math.PI/4;
    box(g,.24,.48,.06,mats.darkWood,0,.31,.49);
    for(const side of [-1,1]){box(g,.16,.17,.06,mats.glass,side*.29,.63,.5);if(tall)box(g,.16,.15,.06,mats.glass,side*.29,1.07,.5);}
    if(tall){box(g,.2,.39,.2,mats.wood,.27,1.47,-.22);}
  }else if(def.id==='fishery'){
    box(g,1.2,.3,.84,mats.wood,0,.2,0);box(g,.9,.55,.74,mats.wall,0,.62,0);
    let roof=cone(g,.82,.55,mats.roof,0,1.16,0,4);roof.rotation.y=Math.PI/4;
    for(const side of [-1,1])box(g,.19,.06,.95,mats.darkWood,side*.52,.23,.51);
    const rod=box(g,.05,1.1,.05,mats.darkWood,.56,.78,.4);rod.rotation.z=-.3;
  }else if(def.id==='farm'){
    box(g,1.23,.11,1.23,mats.soil,0,.12,0);
    for(let r=0;r<5;r++)for(let j=0;j<5;j++){
      const bx=(r-2)*.19,bz=(j-2)*.18;cone(g,.1,.26,r%2===0?mats.crop:mats.green,bx,.3,bz,4);
    }
    box(g,.43,.44,.45,mats.wall,-.44,.35,-.46);let roof=cone(g,.43,.33,mats.roofAlt,-.44,.77,-.46,4);roof.rotation.y=Math.PI/4;
  }else if(def.id==='quarry'){
    box(g,1.15,.26,1.15,mats.soil,0,.18,0);
    for(let i=0;i<7;i++){const r=.2+.17*seedRand(i,4),a=i*2.2;const rock=cone(g,r,.38+seedRand(i,10)*.4,mats.rock,Math.cos(a)*.4,.33,Math.sin(a)*.4,6);rock.rotation.z=.2;}
    box(g,.07,.62,.07,mats.wood,-.48,.61,-.43);box(g,.57,.08,.07,mats.wood,-.2,.94,-.43);
  }else if(def.id==='lumber-camp'){
    box(g,.8,.6,.82,mats.wood,0,.39,-.1);let roof=cone(g,.72,.55,mats.roof,0,.97,-.1,4);roof.rotation.y=Math.PI/4;
    for(let i=0;i<4;i++){const log=addMesh(g,new THREE.CylinderGeometry(.12,.12,.72,8),mats.bark,(i-1.5)*.27,.25,.51);log.rotation.z=Math.PI/2;}
    tree(g,-.45,.29,.43);
  }
  if(b.remaining>0){g.traverse(obj=>{if(obj.isMesh&&obj.material){obj.material=obj.material.clone();obj.material.transparent=true;obj.material.opacity=.64;}});
    for(const side of [-.7,.7]){box(g,.06,1.36,.06,mats.wood,side,.68,-.68);box(g,.06,1.36,.06,mats.wood,side,.68,.68);}
    box(g,1.47,.05,1.47,mats.wood,0,.78,0);
  }else if(b.upgradeRemaining>0){box(g,1.47,.05,1.47,mats.wood,0,1.2,0);for(const side of [-.68,.68]){box(g,.055,1.48,.055,mats.wood,side,.74,-.68);box(g,.055,1.48,.055,mats.wood,side,.74,.68);}}
  return g;
}
const buildingsGroup=new THREE.Group();land.add(buildingsGroup);
function rebuildBuildings(){while(buildingsGroup.children.length){const child=buildingsGroup.children[0];buildingsGroup.remove(child);child.traverse(o=>{if(o.isMesh){o.geometry?.dispose();if(o.material&&o.material!==mats.wood){/* shared materials must remain alive */}}});}
 for(const item of tileMap.values())item.deco.visible=true;
 tileMap.get(key(0,0)).deco.visible=false;
 for(const b of state.buildings){const tile=tileMap.get(key(b.x,b.z));if(!tile)continue;tile.deco.visible=false;const g=buildingVisual(BUILDINGS[b.id],b);g.position.set(b.x*tileSize,tile.height,b.z*tileSize);buildingsGroup.add(g);}
}
const hoverMesh=new THREE.Mesh(new THREE.PlaneGeometry(tileSize*.95,tileSize*.95),new THREE.MeshBasicMaterial({color:'#c5e5aa',transparent:true,opacity:.3,depthWrite:false,side:THREE.DoubleSide}));hoverMesh.rotation.x=-Math.PI/2;hoverMesh.visible=false;land.add(hoverMesh);
const selectedMesh=new THREE.Mesh(new THREE.RingGeometry(.7,.81,32),new THREE.MeshBasicMaterial({color:'#fce6b0',transparent:true,opacity:.95,side:THREE.DoubleSide}));selectedMesh.rotation.x=-Math.PI/2;selectedMesh.visible=false;land.add(selectedMesh);
const raycaster=new THREE.Raycaster(), pointer=new THREE.Vector2();
function hitTile(e){const r=renderer.domElement.getBoundingClientRect();pointer.x=((e.clientX-r.left)/r.width)*2-1;pointer.y=-((e.clientY-r.top)/r.height)*2+1;raycaster.setFromCamera(pointer,camera);const hit=raycaster.intersectObjects(tiles,false)[0];return hit?.object?.userData||null;}
function setHover(t){hover=t;const tile=t&&tileMap.get(key(t.x,t.z));hoverMesh.visible=!!tile&&!(t.x===0&&t.z===0);if(tile){hoverMesh.position.set(t.x*tileSize,tile.height+.055,t.z*tileSize);hoverMesh.material.color.set(activeBuild?'#d2e8a2':'#f5e6c5');hoverMesh.material.opacity=activeBuild?.34:.18;}renderer.domElement.style.cursor=tile?'pointer':'grab';}
function setSelected(t){selected=t;const tile=t&&tileMap.get(key(t.x,t.z));selectedMesh.visible=!!tile;if(tile)selectedMesh.position.set(t.x*tileSize,tile.height+.055,t.z*tileSize);renderUI();}
renderer.domElement.addEventListener('pointerdown',e=>{pointerStart={x:e.clientX,y:e.clientY};dragging=false;});
renderer.domElement.addEventListener('pointermove',e=>{if(pointerStart&&Math.hypot(e.clientX-pointerStart.x,e.clientY-pointerStart.y)>7)dragging=true;if(!pointerStart||!dragging)setHover(hitTile(e));});
renderer.domElement.addEventListener('pointerup',e=>{const shouldClick=pointerStart&&!dragging&&e.button===0;pointerStart=null;dragging=false;if(!shouldClick)return;const t=hitTile(e);if(!t)return;
 if(t.x===0&&t.z===0){toast('The village hall is home to your founding residents.');return;}
 const existing=buildingAt(t.x,t.z);
 if(existing){activeBuild=null;setSelected(t);toast(`${BUILDINGS[existing.id].name} selected.`);return;}
 if(activeBuild){const result=placeBuilding(state,activeBuild,t.x,t.z);if(result.ok){save();rebuildBuildings();setSelected(t);toast(result.reason);activeBuild=null;}else toast(result.reason);renderUI();return;}
 setSelected(t);toast('Empty plot. Choose a building from the left to start construction.');
});
renderer.domElement.addEventListener('pointerleave',()=>setHover(null));
function drawBuildingMenu(){const list=$('#buildingList');list.innerHTML='';for(const d of Object.values(BUILDINGS).filter(d=>d.buildable!==false)){
  const b=document.createElement('button');b.className='build-card'+(activeBuild===d.id?' active':'');
  const affordable=canAfford(state,d.cost);b.innerHTML=`<span class="build-icon">${d.symbol}</span><span><span class="build-title">${d.name}</span><span class="build-sub">${d.seasons} season${d.seasons>1?'s':''} · ${d.housing?'+'+d.housing+' housing':Object.entries(d.production).map(([r,v])=>'+'+v+' '+r).join('')}</span><span class="build-cost">${costText(d.cost)}</span>${!affordable?'<span class="build-desc">Resources needed</span>':''}</span>`;
  b.addEventListener('click',()=>{activeBuild=activeBuild===d.id?null:d.id;setSelected(null);renderUI();toast(activeBuild?`Place ${d.name} on an empty plot.`:'Building placement cancelled.');});list.appendChild(b);
}}
function renderInspector(){const el=$('#inspector');if(!selected){el.innerHTML='<p class="muted">Click an existing building to see its details and available upgrades.</p>';return;}
 const b=buildingAt(selected.x,selected.z);if(!b){el.innerHTML=`<p class="muted">Empty plot (${selected.x}, ${selected.z}).<br>Select a building to place it here.</p>`;return;}
 const d=BUILDINGS[b.id],progress=b.remaining?`Under construction · ${b.remaining} season${b.remaining>1?'s':''} left`:b.upgradeRemaining?`Upgrading · ${b.upgradeRemaining} season${b.upgradeRemaining>1?'s':''} left`:'Operational';
 el.innerHTML=`<h3 class="inspector-title">${d.symbol} ${d.name}</h3><p class="inspector-meta">${d.detail}<br><strong>Status:</strong> ${progress}<br><strong>Effect:</strong> ${d.housing?d.housing+' housing capacity':Object.entries(d.production).map(([r,v])=>'+'+v+' '+r+'/season').join(', ')}</p>`;
 if(b.id==='cottage'&&b.remaining===0){const btn=document.createElement('button');const affordable=canAfford(state,UPGRADE.cost);btn.className='inspector-action';btn.disabled=b.upgradeRemaining>0||!affordable;btn.textContent=b.upgradeRemaining>0?'Upgrade in progress':`UPGRADE TO TOWNHOUSE · ${costText(UPGRADE.cost)}`;btn.addEventListener('click',()=>{const result=upgradeBuilding(state,b.x,b.z);toast(result.reason);if(result.ok){save();rebuildBuildings();}renderUI();});el.appendChild(btn);if(!affordable&&b.upgradeRemaining===0){const note=document.createElement('p');note.className='muted';note.textContent='Gather more resources to upgrade.';el.appendChild(note);}}
}
function renderUI(){const m=$('#metrics');m.innerHTML=[['🪵','wood'],['🪨','stone'],['🍎','food'],['🪙','coins']].map(([icon,k])=>`<div class="metric"><span class="metric-icon">${icon}</span><span><small class="metric-name">${pretty(k)}</small><strong class="metric-value">${state[k]}</strong></span></div>`).join('');
 $('#seasonTitle').textContent=['Spring','Summer','Autumn','Winter'][state.season];$('#yearLabel').textContent=`Year ${state.year}`;$('#seasonIcon').textContent=seasonsIcons[state.season];
 $('#populationStat').textContent=`${state.population} / ${housing(state)}`;$('#populationFill').style.width=`${Math.min(100,state.population/housing(state)*100)||0}%`;
 $('#happinessStat').textContent=`${state.happiness} / 100`;$('#happinessFill').style.width=state.happiness+'%';$('#foodNeed').textContent='−'+state.population;
 const foodProduction=state.buildings.filter(b=>b.remaining===0).reduce((s,b)=>s+(BUILDINGS[b.id].production.food||0),0);$('#foodProduction').textContent='+'+foodProduction+' food';
 const h=$('#selectHint');h.innerHTML=activeBuild?`<span class="hint-glow">✦</span><span>PLACING ${BUILDINGS[activeBuild].name.toUpperCase()} · Click an empty tile</span>`:'<span class="hint-glow">✦</span><span>Choose a building on the left, then click an empty plot to place it.</span>';
 drawBuildingMenu();renderInspector();
}
function showModal(title,eyebrow,html){$('#modalEyebrow').textContent=eyebrow;$('#modalTitle').textContent=title;$('#modalContent').innerHTML=html;$('#modal').classList.remove('hidden');}
function statDelta(n){return (n>0?'+':'')+n;}
$('#endTurn').addEventListener('click',()=>{const r=endTurn(state);save();rebuildBuildings();renderUI();
 const events=[...r.completed.map(b=>'🏗️ '+b+' construction completed'),...r.upgraded.map(b=>'🏡 '+b+' upgrade completed')];
 if(r.shortage)events.push('⚠️ Food shortage: '+r.shortage+' meals missing');if(r.immigrants)events.push('🧳 '+r.immigrants+' new residents joined your settlement');
 if(!events.length)events.push('🍃 A peaceful season passes on Greenshore Island.');
 showModal('Another season, another story.','SEASON REPORT · '+r.from.toUpperCase(),`<div class="report-sub">Your settlement enters <b>${r.to}</b>.</div><div class="report-grid"><div class="report-cell"><small>Population</small><b>${r.oldPopulation} → ${r.newPopulation}</b></div><div class="report-cell"><small>Happiness</small><b>${r.oldHappiness} → ${r.newHappiness}</b></div><div class="report-cell"><small>Food change</small><b class="${r.delta.food<0?'report-red':'report-green'}">${statDelta(r.delta.food)}</b></div><div class="report-cell"><small>New arrivals</small><b class="report-green">+${r.immigrants}</b></div></div>${events.map(s=>`<div class="event-line">${s}</div>`).join('')}`);
});
$('#modalClose').addEventListener('click',()=>$('#modal').classList.add('hidden'));$('#modalContinue').addEventListener('click',()=>$('#modal').classList.add('hidden'));
$('#modal').addEventListener('click',e=>{if(e.target===$('#modal'))$('#modal').classList.add('hidden');});
$('#aboutBtn').addEventListener('click',()=>showModal('Welcome to Greenshore Island.','LITTLE EMPIRES · FIRST PLAYABLE',`<div class="report-sub">This is a small Three.js sandbox to get a feel for the building, seasonal economy and settlement progression. It is not the Unity game yet.</div><div class="event-line">🏡 Build cottages to provide housing, then upgrade them into townhouses.</div><div class="event-line">🌾 Build food producers to keep residents fed and happy.</div><div class="event-line">✦ End seasons to finish construction, collect resources and attract immigrants.</div><div class="event-line">🖱️ Drag to rotate the camera, scroll to zoom, click a building to inspect it.</div><div class="event-line">💾 Your island saves automatically in this browser.</div><div class="event-line">⚙️ The browser runs a JavaScript mirror of our C# rules — we'll keep Unity/C# authoritative.</div>`));
$('#resetGame').addEventListener('click',()=>{if(!confirm('Start a fresh island? Your current sandbox will be reset.'))return;state=startingState();activeBuild=null;selected=null;selectedMesh.visible=false;save();rebuildBuildings();renderUI();toast('A new chapter begins.');});
$('#zoomIn').addEventListener('click',()=>{camera.position.lerp(controls.target,.14);controls.update();});
$('#zoomOut').addEventListener('click',()=>{camera.position.sub(controls.target).multiplyScalar(1.14).add(controls.target);controls.update();});
$('#resetCamera').addEventListener('click',()=>{camera.position.copy(camHome);controls.target.set(0,.2,0);controls.update();toast('Camera reset.');});
window.addEventListener('keydown',e=>{if(e.key==='Escape'){activeBuild=null;$('#modal').classList.add('hidden');renderUI();}});
function resize(){const w=host.clientWidth,h=host.clientHeight;if(!w||!h)return;camera.aspect=w/h;camera.updateProjectionMatrix();renderer.setSize(w,h,false);}
new ResizeObserver(resize).observe(host);
rebuildBuildings();renderUI();resize();
function animate(){requestAnimationFrame(animate);controls.update();const t=performance.now()*.00025;waterRing.material.opacity=.18+Math.sin(t*4)*.04;renderer.render(scene,camera);}animate();