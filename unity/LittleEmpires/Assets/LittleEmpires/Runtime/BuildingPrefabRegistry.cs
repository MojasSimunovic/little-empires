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

        [SerializeField] private GameObject[] farms = Array.Empty<GameObject>();

        public const int HousingVariantCount = 5;
        public const int FarmVariantCount = 2;

        public GameObject Find(string buildingId, int variant = 0)
        {
            var variants = buildingId == LittleEmpires.Core.BuildingCatalog.CottageId ? cottages :
                buildingId == LittleEmpires.Core.BuildingCatalog.TownhouseId ? townhouses :
                buildingId == LittleEmpires.Core.BuildingCatalog.FarmId ? farms : null;
            if (variants != null && variant >= 0 && variant < variants.Length &&
                variants[variant] != null)
                return variants[variant];

            foreach (var entry in entries)
                if (entry.BuildingId == buildingId)
                    return entry.Prefab;
            return null;
        }
    }
}
