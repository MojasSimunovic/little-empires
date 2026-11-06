# Little Empires — C# first, Unity second

We will first implement and test the **simulation core in plain C#**, then hook it into a real Unity 6 island on a personal PC. The existing Three.js browser demo is a visual experiment only; its JavaScript game rules are a temporary mirror, not the canonical game rules.

## Selected design decisions

1. **Geography & placement (LE-020, issue #29).** Every seeded island must be playable and have at least one valid Quarry location. Quarries go on mountains; Lumber Camps on flat land neighboring forests; Fisheries near the shore; Farms on fertile land; housing on flat land. Cannot overlap buildings. Invalid placements do not spend resources. Seeded terrain is reproducible.
2. **Roads (LE-021, #30).** Dirt roads cost no resources or strategic action points; later upgrade each segment to stone for a stone cost. Road network becomes the backbone of logistical connectivity to production, the Market and eventual ports; do not require a complex pathfinding system from day one.
3. **Production chains (LE-022, #31).** Lumber Mill increases output from supported, nearby/connected Lumber Camps (proposed +50%). Food Processing Plant boosts connected Farms/Fisheries (proposed +25%). Bonuses have explicit caps and **do not stack**; start with familiar Wood/Food resource categories rather than exploding the item catalogue.
4. **Taxes (LE-023, #32).** Player-adjustable Low/Normal/High/Very High tax settings: more coin income at the expense of happiness, or less income with improved happiness. Tax season timing must be consistent with existing population and immigration.
5. **Marketplace (LE-024, #33).** Market is a building that enables buy/sell commands with price spread and inventory limits. Initial illustrative prices: Wood sell 3/buy 5; Stone 5/8; Food 2/4 coins. No free arbitrage; inventory and coin transactions must be atomic. Later add supply/demand, ports and external markets.
6. **Workforce and upkeep (LE-025, #34).** Buildings require workers and may incur operating costs; staffing determines whether and how much each building produces. Cap allocated workers by eligible population, and resolve the turn deterministically. Trade, taxation and wages should together create meaningful economic tradeoffs.
7. **Unity integration (LE-026, #35).** Import a Unity-compatible packaging of the C# core, render the seeded terrain with real game-world objects, show placement validation, collect player commands, and advance the actual `TurnEngine`. Do not duplicate the rules in Unity scripts. Add camera, UI, construction visuals and save/load later.

## Implementation status (current batch)

- **LE-020 (#29):** Seeded island and spatial placement rules — verified by user, complete.
- **LE-021 (#30):** Dirt/stone road system and connectivity — verified by user, complete.
- **LE-022 (#31):** Lumber Mill/Food Processing bonuses, bounded and non-stacking — committed, awaiting latest CI.
- **LE-023 (#32):** Tax policies and happiness effects — committed, awaiting latest CI.
- **LE-024 (#33):** Marketplace buy/sell, spreads, trade limits and connectivity — committed, awaiting latest CI.
- **LE-025 (#34):** Workforce allocation, production scaling and upkeep — committed, awaiting latest CI.
- **LE-026 (#35):** Real Unity scene and runtime compatibility pass — still pending installation on the personal PC. `WorldGameSession` and read-only `WorldSnapshot` have been implemented to reduce the integration work.

The integrated `strategic` console scenario and a separate `StrategicEconomyTests` suite exercise the advanced economy while leaving prior Classic scenarios intact. See [STRATEGIC_ECONOMY.md](STRATEGIC_ECONOMY.md).

## Development contract

- **C# is authoritative** for turns, terrain, building costs, road rules, taxes, population, trade and production.
- **All rules have automated tests**, including affordable/unaffordable, border cases, invalid spatial placement, no double-counting, timing and conservation of resources.
- Keep an independent **console simulator** and deterministic scenarios to balance the economy without Unity.
- Stable IDs, data-driven definitions and separation of simulation and presentation are preferred over scene-dependent logic.
- Continue using **GitHub Actions**, keeping changes small and verifiable.
- Keep existing **JavaScript demo** working where feasible; introducing new spatial rules in C# does not automatically modify the browser demo. A future browser/UI sync needs explicit save migration.
- Unity integration will need an explicit compatibility pass: `net8.0` and Unity assembly/runtime compatibility are **not assumed**.

## Order and dependencies

**Terrain → Roads → Production chains → Taxes → Marketplace → Workforce → Unity integration** is the working order. Tax or marketplace prototyping may move earlier if balancing reveals a need, but roads should precede distance- and connectivity-dependent industrial bonuses.

All numeric balance parameters are provisional and should be reviewed after a playable Unity scene exists.
