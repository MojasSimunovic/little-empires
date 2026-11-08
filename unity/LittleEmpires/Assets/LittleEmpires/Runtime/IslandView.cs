using System;
using System.Collections.Generic;
using LittleEmpires.Core;
using UnityEngine;

namespace LittleEmpires.Unity
{
    /// <summary>
    /// Prototype renderer. All visuals are derived from a WorldSnapshot;
    /// no economic, road or building-placement rules live here.
    /// Replace generated geometry with MegaKit prefabs later.
    /// </summary>
    public sealed partial class IslandView : MonoBehaviour
    {
        private readonly Dictionary<string, Material> _materials = new Dictionary<string, Material>();
        private readonly HashSet<string> _missingPrefabs = new HashSet<string>();
        public LargeIslandTerrain LargeTerrain;
        public BuildingPrefabRegistry BuildingPrefabs;
        public NaturePrefabCatalog NaturePrefabs;
        private readonly Dictionary<int, GameObject> _buildingObjects = new Dictionary<int, GameObject>();
        private readonly Dictionary<int, string> _buildingSignatures = new Dictionary<int, string>();
        private Transform _contentRoot;
        private GameObject _selection;
        private readonly List<GameObject> _footprintPreview = new List<GameObject>();
        private float _offsetX;
        private float _offsetZ;

        public Bounds LandBounds { get; private set; }

        public Vector3 WorldPosition(GridPosition position)
        {
            return LargeTerrain != null ? LargeTerrain.Position(position) : new Vector3(position.X - _offsetX, 0f, position.Z - _offsetZ);
        }

        public void Rebuild(WorldSnapshot state)
        {
            // LargeIsland's terrain, selection marker and completed houses persist.
            // Only buildings whose visible state actually changed are recreated.
            if (LargeTerrain != null && _contentRoot != null)
            {
                UpdateRoads(state);
                SyncBuildings(state);
                UpdateNatureVisibility(state);
                return;
            }

            _buildingObjects.Clear();
            _buildingSignatures.Clear();
            _selection = null;
            if (_contentRoot != null)
            {
                // Remove the old scene graph on each season/command.
                _contentRoot.gameObject.SetActive(false);
                Destroy(_contentRoot.gameObject);
            }

            var root = new GameObject("Island - generated from C#");
            root.transform.SetParent(transform, false);
            _contentRoot = root.transform;

            var width = 0;
            var depth = 0;
            foreach (var tile in state.Terrain)
            {
                width = Mathf.Max(width, tile.Position.X + 1);
                depth = Mathf.Max(depth, tile.Position.Z + 1);
            }

            _offsetX = (width - 1) * .5f;
            _offsetZ = (depth - 1) * .5f;

            var land = new HashSet<GridPosition>();
            foreach (var tile in state.Terrain)
                if (tile.Terrain != TerrainKind.Water)
                    land.Add(tile.Position);

            var landBounds = new Bounds();
            var first = true;
            foreach (var plot in land)
            {
                var bounds = new Bounds(WorldPosition(plot) + Vector3.up * .75f,
                    new Vector3(LargeTerrain != null ? LargeTerrain.CellSize : 1.8f, LargeTerrain != null ? 12f : 2.4f, LargeTerrain != null ? LargeTerrain.CellSize : 1.8f));
                if (first) landBounds = bounds;
                else landBounds.Encapsulate(bounds);
                first = false;
            }
            LandBounds = first
                ? new Bounds(Vector3.zero, new Vector3(width, 2f, depth))
                : landBounds;

            if (LargeTerrain == null) {
            var ocean = Cube(_contentRoot, "Open water", new Vector3(0, -.51f, 0),
                new Vector3(width + 200f, .5f, depth + 200f),
                new Color(.22f, .52f, .57f), false);
            ocean.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            foreach (var tile in state.Terrain)
                DrawTile(tile, land);


            }
            UpdateRoads(state);

            SyncBuildings(state);

            _selection = new GameObject("Selected plot");
            _selection.transform.SetParent(_contentRoot, false);
            var marker = new Color(.98f, .86f, .33f);
            for (var side = -1; side <= 1; side += 2)
            {
                Cube(_selection.transform, "Edge", new Vector3(side * .47f, 0, 0),
                    new Vector3(.035f, .015f, .97f), marker, false);
                Cube(_selection.transform, "Edge", new Vector3(0, 0, side * .47f),
                    new Vector3(.9f, .015f, .035f), marker, false);
            }
            // Selection should outline an entire logical plot (6 world units on LargeIsland).
            if (LargeTerrain != null)
                _selection.transform.localScale = Vector3.one * LargeTerrain.CellSize;
            _selection.SetActive(false);
        }

        private Vector3 FootprintCenter(GridPosition anchor, int width, int height)
        {
            var first = WorldPosition(anchor);
            var last = WorldPosition(new GridPosition(anchor.X + width - 1, anchor.Z + height - 1));
            return (first + last) * .5f;
        }

        public void ShowFootprintPreview(GridPosition? anchor, int width = 1, int height = 1, bool valid = false)
        {
            var count = anchor.HasValue ? width * height : 0;
            var color = valid ? new Color(.29f, .77f, .34f) : new Color(.87f, .24f, .17f);
            var cellSize = LargeTerrain != null ? LargeTerrain.CellSize : 1f;
            for (var i = 0; i < count; i++)
            {
                if (i == _footprintPreview.Count)
                {
                    var marker = Cube(transform, "Building footprint preview", Vector3.zero,
                        new Vector3(cellSize * .93f, .035f, cellSize * .93f), color, false);
                    _footprintPreview.Add(marker);
                }
                var cell = new GridPosition(anchor.Value.X + i % width, anchor.Value.Z + i / width);
                var preview = _footprintPreview[i];
                preview.transform.position = WorldPosition(cell) + Vector3.up * .15f;
                preview.GetComponent<Renderer>().sharedMaterial = PreviewMaterial(color);
                preview.SetActive(true);
            }
            for (var i = count; i < _footprintPreview.Count; i++)
                _footprintPreview[i].SetActive(false);
        }

        public void Highlight(GridPosition? position, int width = 1, int height = 1)
        {
            if (_selection == null) return;
            _selection.SetActive(position.HasValue);
            if (position.HasValue)
            {
                var cellSize = LargeTerrain != null ? LargeTerrain.CellSize : 1f;
                _selection.transform.localScale = new Vector3(width * cellSize, cellSize, height * cellSize);
                _selection.transform.position = FootprintCenter(position.Value, width, height) + Vector3.up * .26f;
            }
        }

        private void DrawTile(IslandTile tile, HashSet<GridPosition> land)
        {
            var point = WorldPosition(tile.Position);
            var water = tile.Terrain == TerrainKind.Water;
            var coastal = !water && (
                !land.Contains(new GridPosition(tile.Position.X - 1, tile.Position.Z)) ||
                !land.Contains(new GridPosition(tile.Position.X + 1, tile.Position.Z)) ||
                !land.Contains(new GridPosition(tile.Position.X, tile.Position.Z - 1)) ||
                !land.Contains(new GridPosition(tile.Position.X, tile.Position.Z + 1)));

            if (coastal)
            {
                Cylinder(_contentRoot, "Shallows", point + Vector3.down * .25f,
                    new Vector3(1.79f, .016f, 1.79f), new Color(.34f, .65f, .62f));
                Cylinder(_contentRoot, "Sandy shore", point + Vector3.down * .12f,
                    new Vector3(1.52f, .08f, 1.52f), new Color(.76f, .68f, .48f));
            }

            Color ground;
            switch (tile.Terrain)
            {
                case TerrainKind.Water: ground = new Color(.18f, .58f, .7f); break;
                case TerrainKind.Forest: ground = new Color(.49f, .62f, .37f); break;
                case TerrainKind.FertileLand: ground = new Color(.54f, .65f, .37f); break;
                case TerrainKind.Mountain: ground = new Color(.51f, .62f, .40f); break;
                default: ground = new Color(.53f, .64f, .39f); break;
            }

            var noise = Mathf.PerlinNoise(point.x * .17f + 50f, point.z * .17f + 50f);
            ground *= Mathf.Lerp(.93f, 1.07f, noise);
            ground.a = 1f;
            var groundY = water ? -.45f : 0f;
            var cube = Cube(_contentRoot, "Ground " + tile.Position.X + "," + tile.Position.Z,
                point + new Vector3(0, groundY, 0),
                new Vector3(1f, .38f, 1f), ground, true);
            var target = cube.AddComponent<IslandTileTarget>();
            target.Position = tile.Position;
            var renderer = cube.GetComponent<Renderer>();
            renderer.enabled = !water;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            if (tile.Terrain == TerrainKind.Forest)
            {
                Tree(point + new Vector3(-.2f, .18f, .14f), .75f);
                Tree(point + new Vector3(.23f, .18f, -.12f), .53f);
            }
            else if (tile.Terrain == TerrainKind.Mountain)
            {
                Cube(_contentRoot, "Rock", point + new Vector3(0, .56f, 0),
                    new Vector3(.77f, .8f, .75f), new Color(.53f, .56f, .54f), false);
                Cube(_contentRoot, "Rock peak", point + new Vector3(.16f, 1.04f, -.07f),
                    new Vector3(.46f, .3f, .49f), new Color(.72f, .73f, .68f), false);
            }
            else if (tile.Terrain == TerrainKind.FertileLand)
            {
                for (var i = 0; i < 3; i++)
                    Cube(_contentRoot, "Farm soil detail",
                        point + new Vector3(-.29f + i * .28f, .205f, 0),
                        new Vector3(.11f, .025f, .75f),
                        new Color(.54f, .46f, .28f), false);
            }
            else if (tile.Terrain == TerrainKind.Grassland &&
                     (tile.Position.X * 17 + tile.Position.Z * 31) % 7 == 0)
            {
                Flower(point + new Vector3(.19f, .22f, -.19f));
            }
        }

        private void DrawRoad(RoadView road)
        {
            var center = WorldPosition(road.Position);
            var stone = road.Surface == RoadSurface.Stone;
            var roadScale = LargeTerrain != null
                ? new Vector3(LargeTerrain.CellSize, .045f, LargeTerrain.CellSize)
                : new Vector3(.88f, .045f, .88f);
            var roadHeight = LargeTerrain != null ? .0225f : .22f;
            Cube(_contentRoot, stone ? "Stone road" : "Dirt road",
                center + new Vector3(0, roadHeight, 0), roadScale,
                stone ? new Color(.66f, .67f, .64f) : new Color(.65f, .51f, .35f),
                false);
        }

        private void SyncBuildings(WorldSnapshot state)
        {
            var seen = new HashSet<int>();
            var housingCount = 0;
            var farmCount = 0;
            foreach (var building in state.Buildings)
            {
                // Snapshots retain settlement order, including the same entry after an upgrade.
                var isHousing = building.Id == BuildingCatalog.CottageId ||
                    building.Id == BuildingCatalog.TownhouseId;
                var variant = isHousing ? housingCount++ % BuildingPrefabRegistry.HousingVariantCount :
                    building.Id == BuildingCatalog.FarmId ? farmCount++ % BuildingPrefabRegistry.FarmVariantCount : 0;
                if (!building.Position.HasValue) continue;
                seen.Add(building.Index);
                var p = building.Position.Value;
                // These fields, not season/year or worker priority, determine the mesh.
                var signature = building.Id + "|" + p.X + "," + p.Z + "|" +
                    building.IsComplete + "|" + building.IsUpgrading + "|" + variant;
                if (_buildingSignatures.TryGetValue(building.Index, out var previous) &&
                    previous == signature && _buildingObjects.TryGetValue(building.Index, out var existing) &&
                    existing != null)
                    continue;

                if (_buildingObjects.TryGetValue(building.Index, out var old) && old != null)
                {
                    old.SetActive(false);
                    Destroy(old);
                }
                _buildingObjects[building.Index] = DrawBuilding(building, variant);
                _buildingSignatures[building.Index] = signature;
            }

            var missing = new List<int>();
            foreach (var key in _buildingObjects.Keys)
                if (!seen.Contains(key)) missing.Add(key);
            foreach (var key in missing)
            {
                if (_buildingObjects[key] != null) Destroy(_buildingObjects[key]);
                _buildingObjects.Remove(key);
                _buildingSignatures.Remove(key);
            }
        }

        private GameObject DrawBuilding(BuildingView building, int variant)
        {
            var center = FootprintCenter(building.Position.Value, building.Width, building.Height);
            var home = new GameObject(building.Name + " #" + building.Index);
            home.transform.SetParent(_contentRoot, false);
            home.transform.position = center;
            var root = home.transform;
            if (LargeTerrain != null) root.localScale = Vector3.one * LargeTerrain.CellSize;

            if (!building.IsComplete)
            {
                Cube(root, "Construction foundation",
                    new Vector3(0, .095f, 0), new Vector3(.82f * building.Width, .19f, .8f * building.Height),
                    new Color(.73f, .64f, .45f), false);
                for (var i = -1; i <= 1; i += 2)
                    Cube(root, "Scaffolding", new Vector3(i * .34f, .52f, .36f),
                        new Vector3(.06f, .85f, .06f), new Color(.48f, .31f, .18f), false);
                return home;
            }

            var prefab = BuildingPrefabs != null ? BuildingPrefabs.Find(building.Id, variant) : null;
            if (prefab != null)
            {
                // The cottage's 5.84 x 5.56 unit footprint fits one large-island plot.
                // Its door faces -Z in the source prefab, toward the camera and road.
                var visual = Instantiate(prefab, root, false);
                visual.name = prefab.name;
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.identity;
                visual.transform.localScale = Vector3.one / root.localScale.x;
                if (LargeTerrain != null && building.Id == BuildingCatalog.FisheryId &&
                    visual.TryGetComponent<FisheryWaterfront>(out var waterfront))
                    waterfront.AlignToShore(LargeTerrain, building.Position.Value);
                foreach (var collider in visual.GetComponentsInChildren<Collider>())
                    collider.enabled = false;
                return home;
            }
            if (BuildingPrefabs != null && _missingPrefabs.Add(building.Id))
                Debug.LogWarning("No building prefab registered for " + building.Id + "; using procedural visuals.");

            PopulateFallbackBuilding(root, building.Id);
            return home;
        }

        public void PopulateFallbackBuilding(Transform root, string buildingId)
        {
            switch (buildingId)
            {
                case BuildingCatalog.CottageId:
                case BuildingCatalog.TownhouseId:
                    House(root, buildingId == BuildingCatalog.TownhouseId);
                    break;
                case BuildingCatalog.FarmId:
                    Farm(root);
                    break;
                case BuildingCatalog.LumberCampId:
                    House(root, false, .65f);
                    LogPile(root);
                    break;
                case BuildingCatalog.LumberMillId:
                    House(root, false, .75f);
                    LogPile(root);
                    Cube(root, "Mill chimney", new Vector3(.25f, 1.11f, -.2f),
                        new Vector3(.17f, .48f, .18f), new Color(.64f, .58f, .48f), false);
                    break;
                case BuildingCatalog.FisheryId:
                    // The procedural cottage's walls start at y=.23 local units.
                    // Give them a real plinth, rather than leaving a gap above terrain.
                    Cube(root, "Shoreline foundation",
                        new Vector3(0, .115f, 0), new Vector3(.8f, .23f, .8f),
                        new Color(.63f, .59f, .50f), false);
                    House(root, false, .72f);
                    Cube(root, "Fishing pier", new Vector3(.36f, .26f, 0),
                        new Vector3(.45f, .12f, .36f), new Color(.54f, .37f, .24f), false);
                    break;
                case BuildingCatalog.QuarryId:
                    Cube(root, "Mine shed", new Vector3(0, .25f, 0),
                        new Vector3(.6f, .5f, .6f), new Color(.52f, .37f, .25f), false);
                    Cube(root, "Rock stacks", new Vector3(-.24f, .125f, -.26f),
                        new Vector3(.33f, .25f, .32f), new Color(.67f, .67f, .64f), false);
                    break;
                case BuildingCatalog.FoodProcessorId:
                    House(root, false, .8f);
                    Cube(root, "Smokehouse chimney", new Vector3(.23f, 1.17f, -.19f),
                        new Vector3(.16f, .6f, .16f), new Color(.65f, .54f, .46f), false);
                    break;
                case BuildingCatalog.StoneworksId:
                    House(root, false, .63f);
                    Cube(root, "Stoneworking slab", new Vector3(-.27f, .28f, -.34f),
                        new Vector3(.26f, .16f, .2f), new Color(.7f, .7f, .65f), false);
                    Cube(root, "Stone block stack", new Vector3(.28f, .28f, -.34f),
                        new Vector3(.24f, .22f, .22f), new Color(.67f, .66f, .63f), false);
                    break;
                case BuildingCatalog.TradingHarborId:
                    Market(root);
                    Cube(root, "Harbor wharf", new Vector3(0, .235f, -.35f),
                        new Vector3(.87f, .13f, .25f), new Color(.47f, .32f, .22f), false);
                    Cube(root, "Trade crates", new Vector3(-.27f, .42f, -.18f),
                        new Vector3(.24f, .25f, .23f), new Color(.56f, .42f, .26f), false);
                    break;
                case BuildingCatalog.MarketplaceId:
                    Market(root);
                    break;
                default:
                    House(root, false);
                    break;
            }
        }

        private void House(Transform root, bool tall, float size = .8f)
        {
            var height = tall ? 1.12f : .73f;
            Cube(root, "Cream plaster", new Vector3(0, .23f + height / 2f, 0),
                new Vector3(size, height, size * .8f),
                new Color(.9f, .82f, .65f), false);
            var roofHeight = .25f + height;
            var roof = new Color(.72f, .36f, .24f);
            var left = Cube(root, "Terracotta roof left",
                new Vector3(-.21f, roofHeight + .12f, 0),
                new Vector3(.52f, .12f, .84f), roof, false);
            left.transform.localRotation = Quaternion.Euler(0, 0, 25f);
            var right = Cube(root, "Terracotta roof right",
                new Vector3(.21f, roofHeight + .12f, 0),
                new Vector3(.52f, .12f, .84f), roof, false);
            right.transform.localRotation = Quaternion.Euler(0, 0, -25f);
            Cube(root, "Oak door", new Vector3(.03f, .38f, -.415f),
                new Vector3(.2f, .42f, .03f), new Color(.43f, .27f, .17f), false);
            Cube(root, "Warm window", new Vector3(-.23f, .62f, -.419f),
                new Vector3(.18f, .18f, .028f), new Color(.95f, .81f, .48f), false);
            Cube(root, "Chimney", new Vector3(.22f, roofHeight + .22f, .15f),
                new Vector3(.14f, .36f, .14f), new Color(.6f, .56f, .49f), false);
        }

        private void Farm(Transform root)
        {
            Cube(root, "Farm shed", new Vector3(-.22f, .4f, -.25f),
                new Vector3(.47f, .45f, .42f), new Color(.82f, .71f, .51f), false);
            for (var i = 0; i < 3; i++)
                Cube(root, "Wheat rows", new Vector3(-.24f + i * .23f, .24f, .21f),
                    new Vector3(.11f, .1f, .38f), new Color(.87f, .77f, .39f), false);
        }

        private void LogPile(Transform root)
        {
            for (var i = 0; i < 3; i++)
                Cube(root, "Stacked logs", new Vector3(-.25f + .13f * i, .28f, -.43f),
                    new Vector3(.11f, .12f, .28f), new Color(.54f, .31f, .18f), false);
        }

        private void Market(Transform root)
        {
            Cube(root, "Market platform", new Vector3(0, .25f, 0),
                new Vector3(.82f, .12f, .74f), new Color(.67f, .52f, .35f), false);
            for (var i = -1; i <= 1; i += 2)
                for (var j = -1; j <= 1; j += 2)
                    Cube(root, "Canopy post", new Vector3(i * .31f, .61f, j * .27f),
                        new Vector3(.06f, .65f, .06f), new Color(.43f, .29f, .19f), false);
            Cube(root, "Cloth canopy", new Vector3(0, .97f, 0),
                new Vector3(.85f, .09f, .78f), new Color(.73f, .33f, .31f), false);
            Cube(root, "Market goods", new Vector3(.1f, .39f, -.13f),
                new Vector3(.3f, .18f, .24f), new Color(.85f, .66f, .29f), false);
        }

        private void Tree(Vector3 origin, float scale)
        {
            var trunk = Cylinder(_contentRoot, "Tree trunk",
                origin + new Vector3(0, .22f * scale, 0),
                new Vector3(.12f * scale, .22f * scale, .12f * scale),
                new Color(.43f, .33f, .22f));
            var canopy = Sphere(_contentRoot, "Leaf canopy",
                origin + new Vector3(0, .58f * scale, 0),
                Vector3.one * (.6f * scale), new Color(.24f, .46f, .29f));
        }

        private void Flower(Vector3 center)
        {
            Sphere(_contentRoot, "Wildflower", center,
                Vector3.one * .075f, new Color(.96f, .85f, .66f));
        }

        private GameObject Cube(Transform parent, string name, Vector3 position,
            Vector3 scale, Color color, bool collidable)
        {
            return Primitive(parent, name, PrimitiveType.Cube, position,
                scale, color, collidable);
        }

        private GameObject Cylinder(Transform parent, string name, Vector3 position,
            Vector3 scale, Color color)
        {
            return Primitive(parent, name, PrimitiveType.Cylinder, position,
                scale, color, false);
        }

        private GameObject Sphere(Transform parent, string name, Vector3 position,
            Vector3 scale, Color color)
        {
            return Primitive(parent, name, PrimitiveType.Sphere, position,
                scale, color, false);
        }

        private GameObject Primitive(Transform parent, string name,
            PrimitiveType type, Vector3 position, Vector3 scale,
            Color color, bool collidable)
        {
            var piece = GameObject.CreatePrimitive(type);
            piece.name = name;
            piece.transform.SetParent(parent, false);
            piece.transform.localPosition = position;
            piece.transform.localScale = scale;
            var collider = piece.GetComponent<Collider>();
            if (collider != null) collider.enabled = collidable;

            var renderer = piece.GetComponent<Renderer>();
            renderer.sharedMaterial = MaterialFor(color);
            return piece;
        }

        private Material MaterialFor(Color color)
        {
            var key = ColorUtility.ToHtmlStringRGB(color);
            if (_materials.TryGetValue(key, out var material))
                return material;
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            material = new Material(shader);
            material.color = color;
            material.SetFloat("_Smoothness", .12f);
            _materials[key] = material;
            return material;
        }
    }

    public sealed class IslandTileTarget : MonoBehaviour
    {
        public GridPosition Position;
    }
}
