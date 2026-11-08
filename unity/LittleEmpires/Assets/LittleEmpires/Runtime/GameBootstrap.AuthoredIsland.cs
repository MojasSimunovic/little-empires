using System.Collections.Generic;
using LittleEmpires.Core;
using UnityEngine;

namespace LittleEmpires.Unity
{
    public sealed partial class GameBootstrap
    {
        [SerializeField] private Terrain authoredTerrain;
        [SerializeField] private MeshFilter authoredRiver;
        [SerializeField] private Transform[] authoredDecorations;
        [SerializeField] private float authoredSeaLevel = -4f;

        private IslandMap CreateAuthoredMap()
        {
            const int size = 64;
            var data = authoredTerrain.terrainData;
            var origin = authoredTerrain.transform.position;
            var river = authoredRiver.sharedMesh.vertices;
            var tiles = new List<IslandTile>(size * size);
            var relief = new double[size, size];
            var paint = data.GetAlphamaps(0, 0, data.alphamapWidth, data.alphamapHeight);
            var crossings = new Dictionary<GridPosition, BridgeAxis>
            {
                [new GridPosition(44, 28)] = BridgeAxis.EastWest,
                [new GridPosition(45, 36)] = BridgeAxis.EastWest
            };

            float Sample(float x, float z) =>
                origin.y + data.GetInterpolatedHeight(x / size, z / size);

            for (var z = 0; z < size; z++)
            {
                var worldZ = origin.z + (z + .5f) * cellSize;
                var riverX = RiverCenterAt(worldZ, river);
                for (var x = 0; x < size; x++)
                {
                    var u = (x + .5f) / size;
                    var v = (z + .5f) / size;
                    var center = Sample(x + .5f, z + .5f);
                    var worldX = origin.x + (x + .5f) * cellSize;
                    var woodland = paint[Mathf.RoundToInt(v * (data.alphamapHeight - 1)),
                        Mathf.RoundToInt(u * (data.alphamapWidth - 1)), 1];

                    var kind = center <= authoredSeaLevel ? TerrainKind.Water :
                        Mathf.Abs(worldX - riverX) < cellSize * .5f ? TerrainKind.River :
                        center >= 3.5f ? TerrainKind.Mountain :
                        woodland > .42f ? TerrainKind.Forest :
                        x > 33 && x < 43 && z > 12 && z < 42 && center > 1.75f ? TerrainKind.FertileLand :
                        TerrainKind.Grassland;
                    tiles.Add(new IslandTile(new GridPosition(x, z), kind));

                    var low = center;
                    var high = center;
                    for (var dz = 0; dz <= 8; dz++)
                        for (var dx = 0; dx <= 8; dx++)
                        {
                            var y = Sample(x + .01f + dx * .98f / 8, z + .01f + dz * .98f / 8);
                            low = Mathf.Min(low, y);
                            high = Mathf.Max(high, y);
                        }
                    relief[x, z] = high - low;
                }
            }

            return new IslandMap(size, size, tiles, crossings, plot => relief[plot.X, plot.Z]);
        }

        private float RiverCenterAt(float worldZ, Vector3[] vertices)
        {
            for (var i = 2; i < vertices.Length; i += 2)
            {
                var previous = authoredRiver.transform.TransformPoint((vertices[i - 2] + vertices[i - 1]) * .5f);
                var next = authoredRiver.transform.TransformPoint((vertices[i] + vertices[i + 1]) * .5f);
                if (worldZ >= previous.z && worldZ <= next.z)
                    return Mathf.Lerp(previous.x, next.x, Mathf.InverseLerp(previous.z, next.z, worldZ));
            }

            return float.PositiveInfinity;
        }
    }

    public sealed partial class IslandView
    {
        private readonly Dictionary<GameObject, List<GridPosition>> _authoredNature =
            new Dictionary<GameObject, List<GridPosition>>();

        public void UseAuthoredDecorations(Transform[] groups)
        {
            var origin = LargeTerrain.Surface.transform.position;
            var cell = LargeTerrain.CellSize;
            foreach (var group in groups)
                foreach (Transform decoration in group)
                {
                    var renderers = decoration.GetComponentsInChildren<Renderer>();
                    if (renderers.Length == 0) continue;
                    var bounds = renderers[0].bounds;
                    foreach (var renderer in renderers)
                        bounds.Encapsulate(renderer.bounds);
                    var min = bounds.min - origin;
                    var max = bounds.max - origin;
                    var plots = new List<GridPosition>();
                    for (var z = Mathf.FloorToInt(min.z / cell); z <= Mathf.FloorToInt(max.z / cell); z++)
                        for (var x = Mathf.FloorToInt(min.x / cell); x <= Mathf.FloorToInt(max.x / cell); x++)
                            plots.Add(new GridPosition(x, z));
                    _authoredNature.Add(decoration.gameObject, plots);
                }
        }

        private void UpdateAuthoredNature(HashSet<GridPosition> occupied)
        {
            foreach (var decoration in _authoredNature)
            {
                var visible = !decoration.Value.Exists(occupied.Contains);
                if (decoration.Key.activeSelf != visible)
                    decoration.Key.SetActive(visible);
            }
        }
    }
}
