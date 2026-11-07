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

        public GameObject Find(string buildingId)
        {
            foreach (var entry in entries)
                if (entry.BuildingId == buildingId)
                    return entry.Prefab;
            return null;
        }
    }
}
