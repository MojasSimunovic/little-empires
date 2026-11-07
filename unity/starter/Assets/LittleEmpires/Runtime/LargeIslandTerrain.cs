using LittleEmpires.Core;
using UnityEngine;

namespace LittleEmpires.Unity
{
    public sealed class LargeIslandTerrain : MonoBehaviour
    {
        public float CellSize = 6f;
        public Terrain Surface { get; private set; }
        private TerrainKind[,] _kinds;
        private int _width, _depth;
        private float[,] _elevations;

        public void Build(WorldSnapshot snapshot)
        {
            foreach (var tile in snapshot.Terrain)
            {
                _width = Mathf.Max(_width, tile.Position.X + 1);
                _depth = Mathf.Max(_depth, tile.Position.Z + 1);
            }
            _kinds = new TerrainKind[_width, _depth];
            foreach (var tile in snapshot.Terrain) _kinds[tile.Position.X, tile.Position.Z] = tile.Terrain;
            if (_width != 64 || _depth != 64)
            {
                _elevations = new float[_width, _depth];
                for (var z = 0; z < _depth; z++)
                    for (var x = 0; x < _width; x++)
                    {
                        var sum = 0f;
                        for (var dz = -2; dz <= 2; dz++)
                            for (var dx = -2; dx <= 2; dx++)
                                sum += RawHeight(x + dx, z + dz);
                        _elevations[x, z] = sum / 25;
                    }
            }
            var data = new TerrainData { heightmapResolution = 513, alphamapResolution = 256,
                size = new Vector3(_width * CellSize, 80, _depth * CellSize) };
            var heights = new float[513, 513];
            for (var z = 0; z < 513; z++)
                for (var x = 0; x < 513; x++)
                    heights[z, x] = Height(x / 512f * _width - .5f, z / 512f * _depth - .5f) / 80;
            data.SetHeights(0, 0, heights);
            var colors = new[] { new Color(.40f,.56f,.28f), new Color(.80f,.70f,.48f),
                new Color(.48f,.47f,.42f), new Color(.30f,.44f,.25f), new Color(.61f,.62f,.30f) };
            var layers = new TerrainLayer[colors.Length];
            for (var i = 0; i < layers.Length; i++)
            {
                var texture = new Texture2D(128, 128);
                for (var z = 0; z < 128; z++)
                    for (var x = 0; x < 128; x++)
                    {
                        var broad = Mathf.PerlinNoise(x * .055f + i * 7, z * .055f);
                        var grain = Mathf.PerlinNoise(x * .31f, z * .31f + i * 9);
                        texture.SetPixel(x, z, colors[i] *
                            Mathf.Lerp(.81f, 1.17f, broad * .7f + grain * .3f));
                    }
                texture.Apply();
                layers[i] = new TerrainLayer { diffuseTexture = texture, tileSize = new Vector2(14,14) };
            }
            data.terrainLayers = layers;
            var weights = new float[256,256,5];
            for (var z = 0; z < 256; z++) for (var x = 0; x < 256; x++)
            {
                var gx = x / 255f * _width - .5f;
                var gz = z / 255f * _depth - .5f;
                var h = Height(gx,gz);
                var shore = _width == 64 && _depth == 64
                    ? (float)LargeIslandGeography.SampleCoastalShape(gx, gz) : h;
                var beach = 1 - Mathf.SmoothStep(0,1,Mathf.InverseLerp(4,9,shore));
                var rock = Mathf.SmoothStep(0,1,Mathf.InverseLerp(19,29,h));
                var slope = Mathf.Abs(Height(gx + .35f, gz) - Height(gx - .35f, gz)) +
                            Mathf.Abs(Height(gx, gz + .35f) - Height(gx, gz - .35f));
                var cliff = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(5, 13, slope));
                beach *= 1 - cliff * .75f;
                rock = Mathf.Max(rock, cliff * .85f);
                weights[z,x,1] = beach;
                weights[z,x,2] = (1-beach)*rock;
                for(var dz=-2;dz<=2;dz++) for(var dx=-2;dx<=2;dx++) {
                    var sample=Kind(Mathf.RoundToInt(gx)+dx,Mathf.RoundToInt(gz)+dz);
                    var blendLayer=sample==TerrainKind.Forest?3:sample==TerrainKind.FertileLand?4:0;
                    weights[z,x,blendLayer]+=(1-beach)*(1-rock)/25f;
                }
            }
            data.SetAlphamaps(0,0,weights);
            var go = Terrain.CreateTerrainGameObject(data);
            go.name = "Continuous island terrain";
            go.transform.SetParent(transform,false);
            go.transform.position = new Vector3(-_width*CellSize/2,-8,-_depth*CellSize/2);
            Surface = go.GetComponent<Terrain>();
            Surface.materialTemplate = new Material(Shader.Find("Universal Render Pipeline/Terrain/Lit"));
            Surface.materialTemplate.enableInstancing = true;
            Surface.basemapDistance = 10000;
            Surface.drawInstanced = true;
            var ocean = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ocean.name = "Ocean";
            ocean.transform.SetParent(transform,false);
            ocean.transform.localScale = Vector3.one * (_width*CellSize);
            ocean.transform.position = Vector3.up * -4;
            Destroy(ocean.GetComponent<Collider>());
            var water = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            water.color = new Color(.20f,.47f,.51f);
            water.SetFloat("_Smoothness",.65f);
            ocean.GetComponent<Renderer>().sharedMaterial = water;
        }

        private TerrainKind Kind(int x, int z) => _kinds[Mathf.Clamp(x,0,_width-1),Mathf.Clamp(z,0,_depth-1)];
        private float PlotHeight(int x,int z) => _elevations[Mathf.Clamp(x,0,_width-1),Mathf.Clamp(z,0,_depth-1)];
        private float RawHeight(int x, int z)
        {
            if (Kind(x,z) == TerrainKind.Water) return 0;
            var hill = Mathf.PerlinNoise(x*.065f+17,z*.065f+31);
            return Kind(x,z) == TerrainKind.Mountain ? 19 + hill*23 :
                (float)LargeIslandGeography.TownElevation;
        }

        private float Height(float x, float z)
        {
            if (_width == 64 && _depth == 64)
                return (float)LargeIslandGeography.SampleElevation(x, z);
            var ix = Mathf.FloorToInt(x);
            var iz = Mathf.FloorToInt(z);
            // Broad flat plot interiors, with smooth transitions between them.
            var tx = Mathf.SmoothStep(0,1,Mathf.Clamp01((x-ix-.30f)/.40f));
            var tz = Mathf.SmoothStep(0,1,Mathf.Clamp01((z-iz-.30f)/.40f));
            return Mathf.Lerp(Mathf.Lerp(PlotHeight(ix,iz),PlotHeight(ix+1,iz),tx),
                Mathf.Lerp(PlotHeight(ix,iz+1),PlotHeight(ix+1,iz+1),tx),tz);
        }

        public Vector3 Position(GridPosition plot)
        {
            var point = new Vector3((plot.X+.5f-_width/2f)*CellSize,0,(plot.Z+.5f-_depth/2f)*CellSize);
            point.y = Surface.SampleHeight(point) + Surface.transform.position.y;
            return point;
        }

        public bool Pick(RaycastHit hit, out GridPosition plot)
        {
            var p = hit.point - Surface.transform.position;
            plot = new GridPosition(Mathf.FloorToInt(p.x/CellSize),Mathf.FloorToInt(p.z/CellSize));
            return hit.collider is TerrainCollider && plot.X >= 0 && plot.Z >= 0 && plot.X < _width && plot.Z < _depth;
        }
    }
}
