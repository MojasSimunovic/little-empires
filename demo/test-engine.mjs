import assert from 'node:assert/strict';
import {startingState, placeBuilding, upgradeBuilding, endTurn, housing, BUILDINGS, isValidSave} from './engine.mjs';
function runScenario(start, buildOrders, upgradeTurn=-1){
 const s={...startingState(), ...start}; const results=[];
 for(let turn=0;turn<8;turn++){
   for(const [id,x,z] of (buildOrders[turn]||[])) assert.equal(placeBuilding(s,id,x,z).ok,true);
   if(turn===upgradeTurn)assert.equal(upgradeBuilding(s,0,1).ok,true);
   results.push(endTurn(s));
 }
 return {state:s,reports:results};
}
const plan={0:[['cottage',0,1],['fishery',1,0]],1:[['farm',2,0]]};
const growth=runScenario({wood:80,stone:25,food:30,coins:100},plan);
assert.deepEqual(growth.reports.map(r=>r.newPopulation),[10,12,14,14,14,14,14,14]);
assert.deepEqual([growth.state.wood,growth.state.stone,growth.state.food,growth.state.coins],[25,12,10,70]);
const unhappy=runScenario({wood:80,stone:25,food:0,coins:100,happiness:75},plan);
assert.deepEqual(unhappy.reports.map(r=>r.newHappiness),[64,53,42,45,48,51,54,57]);
assert.deepEqual(unhappy.reports.map(r=>r.immigrants),[0,0,0,0,0,1,1,1]);
assert.equal(unhappy.state.food,27);
assert.equal(unhappy.state.population,11);
const upgrade=runScenario({wood:100,stone:40,food:60,coins:100},plan,3);
assert.deepEqual(upgrade.reports.map(r=>r.newPopulation),[10,12,14,14,16,18,20,20]);
assert.deepEqual([upgrade.state.wood,upgrade.state.stone,upgrade.state.food,upgrade.state.coins],[21,17,28,50]);
assert.equal(housing(upgrade.state),20);
assert.equal(upgrade.state.buildings.length,3);
assert.equal(upgrade.state.buildings[0].id,'townhouse');
assert.deepEqual(upgrade.reports[4].upgraded,['Cottage → Townhouse']);
const invalid=startingState();
assert.equal(placeBuilding(invalid,'townhouse',2,2).ok,false);
assert.equal(placeBuilding(invalid,'cottage',2,2).ok,true);
assert.equal(upgradeBuilding(invalid,2,2).ok,false);
assert.equal(placeBuilding(invalid,'farm',2,2).ok,false);
assert.equal(isValidSave(startingState()),true);
assert.equal(isValidSave({}),false);
for(const item of Object.values(BUILDINGS))assert.ok(item.name);
console.log('PASS: Three.js demo rules match C# eight-season growth, happiness and upgrade benchmarks.');