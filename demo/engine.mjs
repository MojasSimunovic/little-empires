// Little Empires browser rules mirror. C# LittleEmpires.Core remains authoritative.
export const BUILDINGS = Object.freeze({
  'lumber-camp': { id:'lumber-camp', name:'Lumber Camp', symbol:'🌲', color:'#aa754f', cost:{wood:15, coins:10}, seasons:2, production:{wood:6}, housing:0, detail:'A renewable supply of timber for your growing settlement.' },
  fishery: { id:'fishery', name:'Fishery', symbol:'🐟', color:'#499ca7', cost:{wood:12, stone:3, coins:8}, seasons:1, production:{food:5}, housing:0, detail:'Provides a steady catch of fresh food each season.' },
  quarry: { id:'quarry', name:'Quarry', symbol:'⛏️', color:'#8b9ba0', cost:{wood:20, coins:15}, seasons:2, production:{stone:4}, housing:0, detail:'Extracts stone for stronger buildings and upgrades.' },
  farm: { id:'farm', name:'Farm', symbol:'🌾', color:'#d7ad64', cost:{wood:25, stone:5, coins:12}, seasons:2, production:{food:9}, housing:0, detail:'High-yield fields that help support more people.' },
  cottage: { id:'cottage', name:'Cottage', symbol:'🏠', color:'#b56958', cost:{wood:18, stone:5, coins:10}, seasons:1, production:{}, housing:6, detail:'A welcoming home with space for six more residents.' },
  townhouse: { id:'townhouse', name:'Townhouse', symbol:'🏘️', color:'#b86869', cost:{}, seasons:1, production:{}, housing:12, buildable:false, detail:'An upgraded home with twelve housing slots.' },
});
export const UPGRADE = Object.freeze({from:'cottage', to:'townhouse', cost:{wood:24, stone:10, coins:20}, seasons:2});
const resources = ['wood','stone','food','coins'];
const seasons = ['Spring','Summer','Autumn','Winter'];
export const costText = (cost) => Object.entries(cost).map(([r,v])=>`${v} ${r}`).join(' · ');
export const dateText = state => `${seasons[state.season]} · Year ${state.year}`;
export const startingState = () => ({year:1,season:0,wood:85,stone:35,food:30,coins:110,population:8,happiness:70,baseHousing:8,buildings:[],lastReport:null});
export const housing = state => state.baseHousing + state.buildings.reduce((sum,b)=>sum+(b.remaining===0?BUILDINGS[b.id].housing:0),0);
export const canAfford = (state,cost) => Object.entries(cost).every(([r,v])=>state[r]>=v);
const pay = (state,cost) => { for (const [r,v] of Object.entries(cost)) state[r]-=v; };
export function placeBuilding(state,id,x,z) {
  const def = BUILDINGS[id];
  if (!def || def.buildable===false) return {ok:false,reason:'This building cannot be placed directly.'};
  if (state.buildings.some(b=>b.x===x&&b.z===z)) return {ok:false,reason:'There is already a building here.'};
  if(!canAfford(state,def.cost)) return {ok:false,reason:'Not enough resources to begin construction.'};
  pay(state,def.cost);
  state.buildings.push({id,x,z,remaining:def.seasons,upgradeRemaining:0});
  return {ok:true,reason:`${def.name} construction started.`};
}
export function upgradeBuilding(state,x,z) {
  const b=state.buildings.find(b=>b.x===x&&b.z===z);
  if(!b || b.id!==UPGRADE.from || b.remaining!==0 || b.upgradeRemaining>0)
    return {ok:false,reason:'Only a completed Cottage can be upgraded.'};
  if(!canAfford(state,UPGRADE.cost)) return {ok:false,reason:'Not enough resources for the Townhouse upgrade.'};
  pay(state,UPGRADE.cost); b.upgradeRemaining=UPGRADE.seasons;
  return {ok:true,reason:'Upgrading Cottage into a Townhouse.'};
}
export function endTurn(state) {
  const from=dateText(state), before=Object.fromEntries(resources.map(r=>[r,state[r]]));
  const oldPopulation=state.population, oldHappiness=state.happiness;
  const completed=[],upgraded=[];
  // 1. Existing buildings produce. Buildings completed this turn cannot produce yet.
  for(const b of state.buildings) if(b.remaining===0)
    for(const [r,v] of Object.entries(BUILDINGS[b.id].production)) state[r]+=v;
  // 2. Existing residents eat. New arrivals eat beginning next season.
  const consumed=Math.min(state.food,oldPopulation), shortage=oldPopulation-consumed;
  state.food-=consumed;
  // 3. Construction and upgrades progress. Old cottage housing remains during upgrades.
  for(const b of state.buildings) {
    if(b.remaining>0) { if(--b.remaining===0) completed.push(BUILDINGS[b.id].name); }
    else if(b.upgradeRemaining>0) {
      if(--b.upgradeRemaining===0) { b.id=UPGRADE.to; upgraded.push('Cottage → Townhouse'); }
    }
  }
  // 4. Housing and feeding affect happiness.
  if(state.population>0) state.happiness=Math.max(0,Math.min(100,state.happiness+(shortage?-12:2)+(housing(state)>state.population?1:-1)));
  // 5. Immigration: limited by happiness, housing, and remaining food surplus.
  const happinessLimit=state.happiness<50?0:state.happiness<70?1:2;
  const immigrants=shortage?0:Math.min(happinessLimit,state.food,Math.max(0,housing(state)-state.population));
  state.population+=immigrants;
  // 6. Calendar rollover.
  if(state.season===3) {state.season=0; state.year+=1;} else state.season++;
  const report={from,to:dateText(state),delta:Object.fromEntries(resources.map(r=>[r,state[r]-before[r]])),consumed,shortage,immigrants,completed,upgraded,oldPopulation,oldHappiness,newPopulation:state.population,newHappiness:state.happiness};
  state.lastReport=report;
  return report;
}
export function isValidSave(value) {
  return value && Number.isInteger(value.year) && Number.isInteger(value.season) && value.season>=0 && value.season<=3 &&
    resources.every(r=>Number.isSafeInteger(value[r])&&value[r]>=0) &&
    Number.isInteger(value.population)&&value.population>=0 && Number.isInteger(value.happiness)&&value.happiness>=0&&value.happiness<=100 &&
    value.baseHousing===8 && Array.isArray(value.buildings) && value.buildings.every(b=>
      BUILDINGS[b.id] && Number.isInteger(b.x) && Number.isInteger(b.z) && Number.isInteger(b.remaining) && b.remaining>=0 &&
      Number.isInteger(b.upgradeRemaining)&&b.upgradeRemaining>=0 && !(b.id==='townhouse'&&b.remaining!==0));
}