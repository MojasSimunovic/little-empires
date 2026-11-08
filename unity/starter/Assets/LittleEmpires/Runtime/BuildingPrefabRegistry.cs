using System;
using UnityEngine;

namespace LittleEmpires.Unity
{
    [CreateAssetMenu(menuName = "Little Empires/Building Prefab Registry")]
    public sealed class BuildingPrefabRegistry : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public string BuildingId;
            public GameObject Prefab;
        }

        [SerializeField] private Entry[] entries = Array.Empty<Entry>();

        [SerializeField] private GameObject[] cottages = Array.Empty<GameObject>();
        [SerializeField] private GameObject[] townhouses = Array.Empty<GameObject>();

        public const int HousingVariantCount = 5;

        public GameObject Find(string buildingId, int housingVariant = 0)
        {
            var variants = buildingId == LittleEmpires.Core.BuildingCatalog.CottageId ? cottages :
                buildingId == LittleEmpires.Core.BuildingCatalog.TownhouseId ? townhouses : null;
            if (variants != null && housingVariant >= 0 && housingVariant < variants.Length &&
                variants[housingVariant] != null)
                return variants[housingVariant];

            foreach (var entry in entries)
                if (entry.BuildingId == buildingId)
                    return entry.Prefab;
            return null;
        }
    }
}
