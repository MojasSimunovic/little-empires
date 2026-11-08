using System.Collections.Generic;
using System.Linq;
using LittleEmpires.Core;
using UnityEngine;

namespace LittleEmpires.Unity
{
    public sealed partial class IslandView
    {
        public GameObject WoodenBridgePrefab;
        public GameObject StoneBridgePrefab;
        private readonly Dictionary<GridPosition, GameObject> _roadObjects = new Dictionary<GridPosition, GameObject>();
        private readonly Dictionary<GridPosition, string> _roadSignatures = new Dictionary<GridPosition, string>();
        private readonly List<GameObject> _ghosts = new List<GameObject>();
        private readonly Dictionary<string,GameObject> _bridgeGhosts = new Dictionary<string,GameObject>();
        private readonly Dictionary<Color,Material> _previewMaterials = new Dictionary<Color,Material>();
        private Transform _roadsRoot;
        private Transform _ghostRoot;
        private float RoadCell => LargeTerrain != null ? LargeTerrain.CellSize : 1f;

        private readonly Dictionary<GridPosition, GameObject> _crossingMarkers =
            new Dictionary<GridPosition, GameObject>();
        private Transform _crossingsRoot;
        private bool _crossingsHighlighted;

        // A crossing is discoverable before a road is dragged over it.
        // Markers have no colliders so terrain picking still works.
        public void ConfigureCrossingMarkers(IReadOnlyDictionary<GridPosition, BridgeAxis> crossings)
        {
            if (crossings == null || crossings.Count == 0) return;
            if (_crossingsRoot == null)
            {
                _crossingsRoot = new GameObject("Bridge crossing bank posts").transform;
                _crossingsRoot.SetParent(transform, false);
            }
            foreach (var crossing in crossings)
            {
                if (_crossingMarkers.ContainsKey(crossing.Key)) continue;
                var marker = new GameObject("Crossing " + crossing.Key.X + "," + crossing.Key.Z);
                marker.transform.SetParent(_crossingsRoot, false);
                for (var side = -1; side <= 1; side += 2)
                {
                    var bank = new GridPosition(crossing.Key.X + side, crossing.Key.Z);
                    var position = WorldPosition(bank);
                    var post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    post.name = "Bridge location post";
                    post.transform.SetParent(marker.transform, false);
                    post.transform.position = position + new Vector3(-side * RoadCell * .35f, .25f, 0);
                    post.transform.localScale = new Vector3(.15f, .22f, .15f);
                    var collider = post.GetComponent<Collider>();
                    if (collider != null) Destroy(collider);
                }
                _crossingMarkers.Add(crossing.Key, marker);
            }
            UpdateCrossingMarkers();
        }

        public void SetCrossingMarkersHighlighted(bool highlighted)
        {
            if (_crossingsHighlighted == highlighted) return;
            _crossingsHighlighted = highlighted;
            UpdateCrossingMarkers();
        }

        private void UpdateCrossingMarkers()
        {
            var color = _crossingsHighlighted
                ? new Color(.97f, .78f, .28f)
                : new Color(.62f, .48f, .31f);
            foreach (var marker in _crossingMarkers)
            {
                marker.Value.SetActive(!_roadObjects.ContainsKey(marker.Key));
                foreach (var renderer in marker.Value.GetComponentsInChildren<Renderer>(true))
                    renderer.sharedMaterial = MaterialFor(color);
            }
        }


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
                if (road.Bridge != BridgeAxis.None)
                {
                    var prefab=road.Surface==RoadSurface.Stone?StoneBridgePrefab:WoodenBridgePrefab;
                    root.transform.position=BridgePosition(p);
                    root.transform.localScale=Vector3.one*(RoadCell/6f);
                    if(prefab!=null)
                    {
                        var instance=Instantiate(prefab,root.transform,false);
                        foreach(var collider in instance.GetComponentsInChildren<Collider>()) collider.enabled=false;
                    }
                    else Debug.LogError("Missing bridge prefab for "+road.Surface);
                    continue;
                }
                var color = road.Surface == RoadSurface.Stone ? new Color(.62f,.63f,.59f) : new Color(.58f,.44f,.29f);
                var center = WorldPosition(p) + Vector3.up * (LargeTerrain != null ? .025f : .205f);
                Patch(root.transform, center, RoadCell*.52f, RoadCell*.52f, color);
                foreach (var d in directions)
                    if (roads.ContainsKey(new GridPosition(p.X+d.X,p.Z+d.Z)))
                        Patch(root.transform, center + new Vector3(d.X,0,d.Z)*RoadCell*.375f,
                            RoadCell*(d.X!=0?.25f:.52f), RoadCell*(d.Z!=0?.25f:.52f), color);
            }
            UpdateCrossingMarkers();
            UpdateNatureVisibility(state);
        }

        private Vector3 BridgePosition(GridPosition p)
        {
            var center=WorldPosition(p);
            center.y=WorldPosition(new GridPosition(p.X-1,p.Z)).y+.025f;
            return center;
        }

        private Material PreviewMaterial(Color color)
        {
            if (_previewMaterials.TryGetValue(color, out var material)) return material;
            var template = Resources.Load<Material>("Hud/PlacementPreview");
            if (template == null) return MaterialFor(color);

            var tint = color;
            tint.a = .58f;
            material = new Material(template) { color = tint };
            _previewMaterials[color] = material;
            return material;
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

        public void ShowRoadPreview(RoadPathPreview preview, IslandRoadNetwork network, bool stone = false)
        {
            foreach(var ghost in _bridgeGhosts.Values) ghost.SetActive(false);
            if (_ghostRoot == null) { _ghostRoot = new GameObject("Road preview (pooled)").transform; _ghostRoot.SetParent(transform,false); }
            var count = preview == null ? 0 : preview.Segments.Count;
            while (_ghosts.Count < count) _ghosts.Add(Patch(_ghostRoot, Vector3.zero, RoadCell*.65f, RoadCell*.65f, Color.green));
            for (var i=0;i<_ghosts.Count;i++)
            {
                var go = _ghosts[i];
                go.SetActive(i<count);
                if (i>=count) continue;
                var p = preview.Segments[i];
                go.transform.position = (network.IsBridgeCrossing(p) ? BridgePosition(p) : WorldPosition(p)) + Vector3.up*.12f;
                var reused=network.HasRoad(p) && (!stone || network.Segments[p]==RoadSurface.Stone);
                var color = !preview.Succeeded ? new Color(.85f,.19f,.13f) : reused ? new Color(.55f,.60f,.58f) : new Color(.3f,.85f,.34f);
                go.GetComponent<Renderer>().sharedMaterial = PreviewMaterial(color);
                go.transform.localScale=new Vector3(RoadCell*(i==0 || i==count-1?.75f:.6f),RoadCell*(i==0 || i==count-1?.75f:.6f),1);
                if(network.IsBridgeCrossing(p))
                {
                    var prefab=stone?StoneBridgePrefab:WoodenBridgePrefab;
                    if(prefab==null) continue;
                    var key=p.X+","+p.Z+","+stone;
                    if(!_bridgeGhosts.TryGetValue(key,out var bridge))
                    {
                        bridge=Instantiate(prefab,_ghostRoot,false); _bridgeGhosts[key]=bridge;
                        foreach(var collider in bridge.GetComponentsInChildren<Collider>()) collider.enabled=false;
                    }
                    bridge.SetActive(true); bridge.transform.position=BridgePosition(p)+Vector3.up*.03f;
                    bridge.transform.localScale=Vector3.one*(RoadCell/6f);
                    foreach(var renderer in bridge.GetComponentsInChildren<Renderer>())
                        renderer.sharedMaterials=System.Array.ConvertAll(renderer.sharedMaterials,m=>PreviewMaterial(color));
                }
            }
        }
    }
}
