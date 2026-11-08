using System;
using UnityEngine;

namespace LittleEmpires.Unity
{
    /// <summary>
    /// Explicit, build-safe references to the Quaternius nature models already
    /// imported into this project. No AssetDatabase or editor-only runtime loading.
    /// </summary>
    [CreateAssetMenu(menuName = "Little Empires/Nature Prefab Catalog")]
    public sealed class NaturePrefabCatalog : ScriptableObject
    {
        public GameObject[] Trees = Array.Empty<GameObject>();
        public GameObject[] Bushes = Array.Empty<GameObject>();
        public GameObject[] Rocks = Array.Empty<GameObject>();
        public GameObject[] Grasses = Array.Empty<GameObject>();
        public GameObject[] Flowers = Array.Empty<GameObject>();
    }
}
