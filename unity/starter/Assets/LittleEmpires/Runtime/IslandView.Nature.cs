using System;
using System.Collections.Generic;
using LittleEmpires.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace LittleEmpires.Unity
{
    public sealed partial class IslandView
    {
        // Natural scenery is generated ONCE, separately from seasonally updated
        // buildings. A road or building can clear a plot without destroying assets.
        private readonly Dictionary<GridPosition, GameObject> _naturePlots =
            new Dictionary<GridPosition, GameObject>();
        private Transform _natureRoot;

        public void InitializeNature(WorldSnapshot state)
        {
            if (LargeTerrain == null || NaturePrefabs == null || _natureRoot != null)
                return;

            _natureRoot = new GameObject("Persistent Quaternius nature").transform;
            _natureRoot.SetParent(transform, false);

            foreach (var tile in state.Terrain)
            {
                var plot = tile.Position;
                var random = new System.Random(unchecked(plot.X * 73856093 ^
                    plot.Z * 19349663 ^ 0x48B071));
                switch (tile.Terrain)
                {
                    case TerrainKind.Forest:
                        // Connected woodland with uneven edges, not lone trees on every tile.
                        if (random.NextDouble() < .90)
                            AddNature(plot, NaturePrefabs.Trees, 7f + (float)random.NextDouble() * 3f, random);
                        if (random.NextDouble() < .58)
                            AddNature(plot, NaturePrefabs.Trees, 5.5f + (float)random.NextDouble() * 2f, random);
                        if (random.NextDouble() < .60)
                            AddNature(plot, NaturePrefabs.Bushes, 1.1f, random);
                        break;

                    case TerrainKind.Mountain:
                        if (random.NextDouble() < .72)
                            AddNature(plot, NaturePrefabs.Rocks, 2.4f + (float)random.NextDouble() * 2f, random);
                        if (random.NextDouble() < .26)
                            AddNature(plot, NaturePrefabs.Rocks, 1.4f, random);
                        break;

                    case TerrainKind.Grassland:
                        if (random.NextDouble() < .15)
                            AddNature(plot, NaturePrefabs.Grasses, .8f, random);
                        if (random.NextDouble() < .065)
                            AddNature(plot, NaturePrefabs.Flowers, .7f, random);
                        if (random.NextDouble() < .022)
                            AddNature(plot, NaturePrefabs.Bushes, 1.2f, random);
                        break;

                    case TerrainKind.FertileLand:
                        if (random.NextDouble() < .12)
                            AddNature(plot, NaturePrefabs.Grasses, .75f, random);
                        if (random.NextDouble() < .06)
                            AddNature(plot, NaturePrefabs.Flowers, .6f, random);
                        break;
                }
            }

            UpdateNatureVisibility(state);
        }

        private void AddNature(GridPosition plot, GameObject[] prefabs,
            float targetHeight, System.Random random)
        {
            if (prefabs == null || prefabs.Length == 0) return;
            var prefab = prefabs[random.Next(prefabs.Length)];
            if (prefab == null) return;

            if (!_naturePlots.TryGetValue(plot, out var group))
            {
                group = new GameObject("Scenery " + plot.X + "," + plot.Z);
                group.transform.SetParent(_natureRoot, false);
                _naturePlots.Add(plot, group);
            }

            var instance = Instantiate(prefab, group.transform, false);
            instance.name = prefab.name;
            var position = WorldPosition(plot);
            position.x += ((float)random.NextDouble() - .5f) * LargeTerrain.CellSize * .58f;
            position.z += ((float)random.NextDouble() - .5f) * LargeTerrain.CellSize * .58f;
            position.y = LargeTerrain.Surface.SampleHeight(position) +
                LargeTerrain.Surface.transform.position.y;
            instance.transform.position = position;
            instance.transform.rotation = Quaternion.Euler(0,
                (float)random.NextDouble() * 360f, 0);

            foreach (var collider in instance.GetComponentsInChildren<Collider>(true))
                collider.enabled = false;

            var renderers = instance.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return;
            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            // Normalize differing FBX import scales; preserve original materials.
            if (bounds.size.y > .001f)
            {
                var ratio = Mathf.Clamp(targetHeight / bounds.size.y, .03f, 15f);
                instance.transform.localScale *= ratio;
            }
            bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);
            instance.transform.position += Vector3.up * (position.y - bounds.min.y);

            if (targetHeight < 2f)
                foreach (var renderer in renderers)
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        private void UpdateNatureVisibility(WorldSnapshot state)
        {
            if (_natureRoot == null && _authoredNature.Count == 0) return;
            var occupied = new HashSet<GridPosition>();
            foreach (var building in state.Buildings)
                occupied.UnionWith(building.OccupiedPlots);
            foreach (var road in state.Roads)
                occupied.Add(road.Position);

            UpdateAuthoredNature(occupied);

            foreach (var entry in _naturePlots)
            {
                var visible = !occupied.Contains(entry.Key);
                if (entry.Value.activeSelf != visible)
                    entry.Value.SetActive(visible);
            }
        }
    }
}
