using LittleEmpires.Core;
using UnityEngine;

namespace LittleEmpires.Unity
{
    public sealed class FisheryWaterfront : MonoBehaviour
    {
        [SerializeField] private Transform skiff;
        [SerializeField] private Material planks;
        [SerializeField] private Material posts;
        [SerializeField] private Material rope;

        private static readonly GridPosition[] Directions =
        {
            new GridPosition(0, -1), new GridPosition(1, 0),
            new GridPosition(0, 1), new GridPosition(-1, 0)
        };

        private const float PierWidth = 1.46f;
        private const float BoatHalfLength = 1.97f;
        private const float BoatHalfWidth = .81f;
        private const float DeckStart = .23f;

        public void AlignToShore(LargeIslandTerrain terrain, GridPosition plot)
        {
            skiff.gameObject.SetActive(false);
            var origin = transform.position;
            var sea = terrain.SeaLevel;
            var halfCell = terrain.CellSize * .5f;
            var limit = halfCell + terrain.CellSize - .08f;
            var chosen = Vector3.zero;
            var shore = float.PositiveInfinity;
            var boatPosition = Vector3.zero;
            var hasBoat = false;
            var acrossPier = false;

            foreach (var offset in Directions)
            {
                if (!terrain.IsOcean(new GridPosition(plot.X + offset.X, plot.Z + offset.Z)))
                    continue;

                var direction = new Vector3(offset.X, 0, offset.Z);
                var right = Vector3.Cross(direction, Vector3.up);
                var edge = FindWater(terrain, origin, direction, right, halfCell, limit);
                if (float.IsPositiveInfinity(edge))
                    continue;

                var floating = FindBoatPosition(terrain, origin, direction, right,
                    edge, halfCell, limit, out var candidate, out var across);
                if (chosen != Vector3.zero && (hasBoat && !floating || hasBoat == floating && edge >= shore))
                    continue;

                chosen = direction;
                shore = edge;
                boatPosition = candidate;
                hasBoat = floating;
                acrossPier = across;
            }

            if (chosen == Vector3.zero)
                return;

            transform.rotation = Quaternion.LookRotation(-chosen);
            var end = Mathf.Min(limit, shore + 1.02f);
            if (hasBoat)
                end = Mathf.Clamp(Vector3.Dot(boatPosition - origin, chosen) +
                    (acrossPier ? -BoatHalfWidth - .12f : .35f), end, limit);

            var deckY = Mathf.Max(sea + .32f, origin.y + .24f);
            var lateral = transform.right;
            for (var distance = DeckStart; distance <= end; distance += .15f)
                for (var side = -1; side <= 1; side++)
                    deckY = Mathf.Max(deckY, terrain.GroundHeight(origin + chosen * distance +
                        lateral * (side * PierWidth * .5f)) + .13f);

            var deckHeight = deckY - origin.y;
            var pier = new GameObject("Shore-aligned fishing pier").transform;
            pier.SetParent(transform, false);
            var count = Mathf.CeilToInt((end - DeckStart) / .24f);
            var step = (end - DeckStart) / count;
            const float loadingWidth = 1.94f;
            for (var i = 0; i < count; i++)
            {
                var distance = DeckStart + (i + .5f) * step;
                var width = distance > end - .94f ? loadingWidth : PierWidth;
                Timber(pier, "Deck plank", new Vector3(0, deckHeight, -distance),
                    new Vector3(width, .105f, step - .013f), i % 5 == 0 ? posts : planks);
            }

            foreach (var side in new[] { -.61f, .61f })
            {
                Timber(pier, "Stringer", new Vector3(side, deckHeight - .105f, -(end + DeckStart) * .5f),
                    new Vector3(.12f, .15f, end - DeckStart), posts);
                for (var distance = end - .11f; distance >= DeckStart; distance -= 1.42f)
                {
                    var atHead = distance > end - .3f;
                    var pileSide = atHead ? Mathf.Sign(side) * .83f : side;
                    var world = origin + chosen * distance + lateral * pileSide;
                    var baseY = terrain.GroundHeight(world) - origin.y - .06f;
                    var topY = deckHeight + (atHead ? .31f : .08f);
                    Timber(pier, atHead ? "Mooring bollard" : "Jetty pile",
                        new Vector3(pileSide, (baseY + topY) * .5f, -distance),
                        new Vector3(.19f, topY - baseY, .19f), posts);
                }
            }

            if (!hasBoat)
                return;

            skiff.position = new Vector3(boatPosition.x, sea - .055f, boatPosition.z);
            var sideOfBoat = skiff.localPosition.x < 0 ? -1f : 1f;
            skiff.localRotation = Quaternion.Euler(0, acrossPier ? sideOfBoat * 90 : 0, 0);
            skiff.gameObject.SetActive(true);

            var tie = new Vector3(sideOfBoat * .83f, deckHeight + .20f, -end + .11f);
            var bow = transform.InverseTransformPoint(skiff.TransformPoint(new Vector3(0, .35f, -.79f)));
            var stern = transform.InverseTransformPoint(skiff.TransformPoint(new Vector3(0, .29f, .79f)));
            var cleat = (bow - tie).sqrMagnitude < (stern - tie).sqrMagnitude ? bow : stern;
            var delta = cleat - tie;
            var line = Timber(pier, "Mooring line", (cleat + tie) * .5f,
                new Vector3(.034f, .034f, delta.magnitude), rope);
            line.localRotation = Quaternion.LookRotation(delta);
        }

        private static float FindWater(LargeIslandTerrain terrain, Vector3 origin, Vector3 direction,
            Vector3 right, float start, float limit)
        {
            for (var d = start + .18f; d <= limit - .55f; d += .15f)
            {
                var point = origin + direction * d;
                if (terrain.GroundHeight(point) < terrain.SeaLevel - .055f &&
                    terrain.GroundHeight(point - right * .65f) < terrain.SeaLevel - .055f &&
                    terrain.GroundHeight(point + right * .65f) < terrain.SeaLevel - .055f)
                    return d;
            }

            return float.PositiveInfinity;
        }

        private static bool FindBoatPosition(LargeIslandTerrain terrain, Vector3 origin, Vector3 direction,
            Vector3 right, float shore, float halfCell, float limit, out Vector3 position, out bool acrossPier)
        {
            for (var d = Mathf.Max(halfCell, shore - .45f) + BoatHalfLength;
                d <= limit + 2.6f; d += .14f)
            {
                foreach (var side in new[] { 1.87f, -1.87f, 2.05f, -2.05f })
                {
                    var center = origin + direction * d + right * side;
                    if (!HasBoatClearance(terrain, center, direction, right))
                        continue;

                    position = center;
                    acrossPier = false;
                    return true;
                }
            }

            // A shallow beach may only have enough depth near the outer edge.
            // In that case the skiff ties across the head of the pier.
            for (var d = limit + .75f; d >= shore + BoatHalfWidth + .24f; d -= .14f)
            {
                foreach (var side in new[] { .95f, -.95f, 0f })
                {
                    var center = origin + direction * d + right * side;
                    if (!HasBoatClearance(terrain, center, right, direction))
                        continue;

                    position = center;
                    acrossPier = true;
                    return true;
                }
            }

            position = default;
            acrossPier = false;
            return false;
        }

        private static bool HasBoatClearance(LargeIslandTerrain terrain, Vector3 center,
            Vector3 length, Vector3 width)
        {
            for (var z = -1; z <= 1; z++)
                for (var x = -1; x <= 1; x++)
                {
                    var sample = center + length * (z * BoatHalfLength) + width * (x * BoatHalfWidth);
                    if (!terrain.IsOceanAt(sample) || terrain.GroundHeight(sample) >= terrain.SeaLevel - .07f)
                        return false;
                }

            return true;
        }

        private static Transform Timber(Transform parent, string name, Vector3 position, Vector3 size,
            Material material)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = name;
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = position;
            obj.transform.localScale = size;
            var collider = obj.GetComponent<Collider>();
            collider.enabled = false;
            Destroy(collider);
            obj.GetComponent<Renderer>().sharedMaterial = material;
            return obj.transform;
        }
    }
}
