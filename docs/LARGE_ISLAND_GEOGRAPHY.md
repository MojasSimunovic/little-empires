# LargeIsland geography control map

The fixed 64×64 island is authored in src/LittleEmpires.Core/LargeIslandGeography.cs.
Its 17×17 control strings run south to north and preserve the approved coast
and bay silhouette. The control field is a mask for the coast and northeastern
mountain, rather than the final city elevation. The separate biome strings
mark woodland with F and farmland with A.

IslandGenerator classifies the 64×64 map from this height source. Unity Terrain
samples the same elevation function at 513×513 resolution. TownElevation is
10 above the Terrain origin, or world Y = 2. Ordinary interior grassland,
farmland and woodland share this exact grade. The original coastline remains
at control value 4. Only the narrow coastal bank (control values 4–5.5) and the
single northeastern mountain region (values above 20) change height.
Beach paint still uses the original coastal mask, preserving its extent.
There is no rolling height variation on the city floor.

To reshape the island, edit the control strings, keep every row 17 characters
long, and inspect the playable LargeIsland scene. The scene uses six world
units per logical cell. Building placement, road commands and season logic
remain in WorldGameSession.

The flat-ground Play Mode check placed five adjacent Meadow Cottages, five
touching dirt-road segments and a Farm, then advanced two seasons. All six
buildings completed. The road network connected both ends of the cottage row.
All five cottage roots measured Y = 2.00061 (Terrain quantization), with zero
height variation at nine terrain samples under each footprint. Adjacent
cottage renderer bounds had a minimum 0.169-unit gap. The road bottoms met the
Terrain surface. Of 3,034 non-water cells, 2,673 (88.1%) have exactly the common
town elevation, 203 belong to the mountain, and 158 to the immediate coast.

Vegetation, new surface textures and a river with a valid gameplay crossing
remain a subsequent pass after flat-ground approval.
