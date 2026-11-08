# Little Empires — settlement progression (Tier 3 reserved)

The first two tiers are now implemented in C#. This is a small single-island seasonal city-builder; no external islands need rendering.

| Milestone | Requirement | Unlock |
|---|---|---|
| Village | New game | Village Hall, Cottage, Farm, Fishery, Quarry, Lumber Camp, Marketplace |
| Growing Village | 20 residents | Cottage to Townhouse upgrade; 24 wood + 10 stone + 20 coins and two seasons |
| Town Hall eligibility | 50 residents | Village Hall to Town Hall upgrade; 60 wood + 25 stone + 300 coins and two seasons |
| Town | Town Hall upgrade fully completed | Sawmill, Food Processing Workshop, Stoneworks, Trading Harbor |

Town is not unlocked by population alone, nor on the first upgrade season. The completed Town Hall unlocks Town-tier construction; this privilege persists if population subsequently falls. Classic simulation fixtures retain legacy ungated behavior. Real Strategic mode enforces upgrades and new construction gates in the C# core, including placement preview.

## Town-tier economy

**Sawmill** — stable ID lumber-mill; two workers, four coins per season upkeep, road-linked +50% output to Lumber Camps within six tiles. This is the existing processor renamed, not a new duplicate building.

**Food Processing Workshop** — stable ID food-processor; three workers, five coins upkeep; road-linked +25% output to Farms and Fisheries. Existing production effect, new display name.

**Stoneworks** — ID stoneworks; cost 35 wood/20 stone/45 coins; two seasons; two workers, four coins upkeep; road-linked non-stacking +50% stone output to Quarries within six tiles.

**Trading Harbor** — ID trading-harbor; cost 45 wood/30 stone/100 coins, three seasons. Level shoreline plot, trading requires road-connected completed residence. Existing Marketplace trade engine; 120 bought+sold units combined per season compared with 30 for Marketplace. For now it uses identical fixed trade prices. Trading partners are abstracted; later the Harbor may show archipelago nation reports and foreign offers.

Processing bonuses apply only when both buildings are completed and operational and on the same road network within six Manhattan tiles; bonus rounding and non-stacking are unchanged.

## Space for Tier 3

The settlement progression logic uses the extensible SettlementTier type, each building's RequiredTier, and WorldSnapshot.Tier. In a future milestone, add a new tier and another earned progression event, not a new set of hardcoded build-menu if statements. Each new specialized building can declare its required tier.

Potential Tier 3 mechanics: planks from lumber, cut stone from quarries, preserved food, luxury goods, specialist workshops, higher housing needs, more sophisticated trade contracts and inter-nation supply reports.

Those specialized goods require deliberate changes to the Resource/inventory model and production/consumption pipelines, so they are **reserved, not implemented now**. Keep our current simple four-resource economy and one-island premise intact.

## Unity MCP handoff: art and Play Mode checks pending

The new two buildings currently use procedural visual stand-ins in IslandView. Create proper town-era prefabs in Unity using the warm cream plaster, timber, terracotta roof visual language of the five Cottage/Townhouse variants and the Hall. Keep each inside the existing single 6x6-unit plot, local root (0,0,0), rotation zero, scale one, ground-level pivot, and front door toward -Z. A full dock extending onto water needs an explicit footprint/shoreline enhancement later; do not trespass into neighboring tiles now.

Register prefabs in the existing Building Prefabs registry using these IDs exactly: lumber-mill, food-processor, stoneworks, trading-harbor. Preserve all cottage/townhouse and Hall references and Unity metadata. Do not overwrite local uncommitted Unity MCP changes.

Play Mode acceptance:
1. Village has four Town buildings visible but disabled. Attempting direct construction through C# rejects without spending resources.
2. Cottage upgrade requires 20 residents in Strategic mode; cost/2-season mechanics stay unchanged.
3. Reaching 50 permits starting Town Hall upgrade but does not unlock Town industries while upgrading.
4. Completing Town Hall upgrade unlocks all four Town industries; confirm locked UI becomes enabled without reopening the game.
5. Sawmill and Food Workshop keep their existing staffed, road-linked resource bonuses. Stoneworks adds +50% stone to a connected staffed Quarry, with no stacking.
6. Harbor can only be placed on a level shoreline, trades only once completed and road-connected to housing, uses 120-unit quota with reset each season.
7. Verify Town models, isometric scale, front orientation, materials, Console warnings, and performance; manually click trade, tax and upgrade controls.
8. Take screenshots and record test status. Only merge after checking Git working tree, CI, and Unity Play Mode.

This spec is for a bounded Tier 2 milestone, not authorization to implement Tier 3 yet.
