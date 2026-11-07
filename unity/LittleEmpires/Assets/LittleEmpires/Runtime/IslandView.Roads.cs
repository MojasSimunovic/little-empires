using System.Collections.Generic;
using System.Linq;
using LittleEmpires.Core;
using UnityEngine;

namespace LittleEmpires.Unity
{
    public sealed partial class IslandView
    {
        private readonly Dictionary<GridPosition, GameObject> _roadObjects = new Dictionary<GridPosition, GameObject>();
        private readonly Dictionary<GridPosition, string> _roadSignatures = new Dictionary<GridPosition, string>();
        private readonly List<GameObject> _ghosts = new List<GameObject>();
        private Transform _roadsRoot;
        private Transform _ghostRoot;
        private float RoadCell => LargeTerrain != null ? LargeTerrain.CellSize : 1f;

        public void UpdateRoads(WorldSnapshot state)
        {
            if (_roadsRoot == null) { _roadsRoot = new GameObject("Persistent roads").transform; _roadsRoot.SetParent(transform, false); }
            var roads = state.Roads.ToDictionary(r => r.Position);
            foreach (var road in state.Roads)
            {
                var p = road.Position;
                var directions = new[] { new GridPosition(1,0), new GridPosition(0,1), new GridPosition(-1,0), new GridPosition(0,-1) };
                var signature = road.Surface.ToString();
                foreach (var d in directions) signature += roads.ContainsKey(new GridPosition(p.X+d.X,p.Z+d.Z)) ? "1" : "0";
                if (_roadSignatures.TryGetValue(p, out var old) && old == signature) continue;
                if (_roadObjects.TryGetValue(p, out var previous)) { previous.SetActive(false); Destroy(previous); }
                var root = new GameObject(road.Surface + " road " + p.X + "," + p.Z);
                root.transform.SetParent(_roadsRoot, false);
                _roadObjects[p] = root;
                _roadSignatures[p] = signature;
                var color = road.Surface == RoadSurface.Stone ? new Color(.62f,.63f,.59f) : new Color(.58f,.44f,.29f);
                var center = WorldPosition(p) + Vector3.up * (LargeTerrain != null ? .025f : .205f);
                Patch(root.transform, center, RoadCell*.52f, RoadCell*.52f, color);
                foreach (var d in directions)
                    if (roads.ContainsKey(new GridPosition(p.X+d.X,p.Z+d.Z)))
                        Patch(root.transform, center + new Vector3(d.X,0,d.Z)*RoadCell*.375f,
                            RoadCell*(d.X!=0?.25f:.52f), RoadCell*(d.Z!=0?.25f:.52f), color);
            }
        }

        private GameObject Patch(Transform parent, Vector3 position, float width, float depth, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "Flush road surface";
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.rotation = Quaternion.Euler(90,0,0);
            go.transform.localScale = new Vector3(width, depth, 1);
            go.GetComponent<Collider>().enabled = false;
            Destroy(go.GetComponent<Collider>());
            var renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = MaterialFor(color);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        public void ShowRoadPreview(RoadPathPreview preview, IslandRoadNetwork network)
        {
            if (_ghostRoot == null) { _ghostRoot = new GameObject("Road preview (pooled)").transform; _ghostRoot.SetParent(transform,false); }
            var count = preview == null ? 0 : preview.Segments.Count;
            while (_ghosts.Count < count) _ghosts.Add(Patch(_ghostRoot, Vector3.zero, RoadCell*.65f, RoadCell*.65f, Color.green));
            for (var i=0;i<_ghosts.Count;i++)
            {
                var go = _ghosts[i];
                go.SetActive(i<count);
                if (i>=count) continue;
                var p = preview.Segments[i];
                go.transform.position = WorldPosition(p) + Vector3.up*.12f;
                var color = !preview.Succeeded ? new Color(.85f,.19f,.13f) : network.HasRoad(p) ? new Color(.55f,.60f,.58f) : new Color(.3f,.85f,.34f);
                go.GetComponent<Renderer>().sharedMaterial = MaterialFor(color);
            }
        }
    }
}
