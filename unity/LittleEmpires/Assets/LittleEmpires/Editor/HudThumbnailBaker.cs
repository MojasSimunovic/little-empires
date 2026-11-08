using System.IO;
using System.Linq;
using LittleEmpires.Core;
using UnityEditor;
using UnityEngine;

namespace LittleEmpires.Unity.Editor
{
    internal static class HudThumbnailBaker
    {
        private const string Folder = "Assets/LittleEmpires/Resources/Hud/Icons";
        private const int Size = 160;

        [MenuItem("Little Empires/Art/Bake HUD Building Thumbnails")]
        internal static void Bake()
        {
            var registry = AssetDatabase.FindAssets("t:BuildingPrefabRegistry")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<BuildingPrefabRegistry>)
                .OrderByDescending(asset => BuildingCatalog.All.Count(definition => asset.Find(definition.Id) != null))
                .First();
            Directory.CreateDirectory(Folder);
            var preview = new PreviewRenderUtility();
            try
            {
                preview.camera.orthographic = true;
                preview.camera.clearFlags = CameraClearFlags.SolidColor;
                preview.camera.backgroundColor = new Color(.24f, .32f, .25f);
                preview.camera.nearClipPlane = .1f;
                preview.camera.farClipPlane = 120f;
                preview.camera.transform.rotation = Quaternion.Euler(26f, 30f, 0f);
                preview.lights[0].transform.rotation = Quaternion.Euler(42f, 24f, 0f);
                preview.lights[0].intensity = 1.75f;
                preview.lights[1].intensity = .9f;
                preview.ambientColor = new Color(.59f, .58f, .53f);

                foreach (var building in BuildingCatalog.All.Where(definition => definition.CanBuildDirectly))
                {
                    var prefab = registry.Find(building.Id);
                    var instance = prefab != null ? preview.InstantiatePrefabInScene(prefab) : new GameObject(building.Name);
                    try
                    {
                        if (prefab == null)
                        {
                            preview.AddSingleGO(instance);
                            instance.AddComponent<IslandView>().PopulateFallbackBuilding(instance.transform, building.Id);
                        }
                        var renderers = instance.GetComponentsInChildren<MeshRenderer>();
                        var bounds = renderers[0].bounds;
                        foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                        var right = preview.camera.transform.right;
                        var up = preview.camera.transform.up;
                        preview.camera.orthographicSize = Mathf.Max(Project(bounds.extents, right), Project(bounds.extents, up)) * 1.08f;
                        preview.camera.transform.position = bounds.center - preview.camera.transform.forward * 30f;
                        preview.BeginPreview(new Rect(0, 0, Size, Size), GUIStyle.none);
                        preview.Render(allowScriptableRenderPipeline: true);
                        var target = (RenderTexture)preview.EndPreview();
                        var previous = RenderTexture.active;
                        var texture = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
                        try
                        {
                            RenderTexture.active = target;
                            texture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                            // Preview lighting reads darker at icon size than it does in the scene.
                            var pixels = texture.GetPixels();
                            for (var i = 0; i < pixels.Length; i++)
                                pixels[i] = Color.Lerp(pixels[i], pixels[i].gamma, .65f);
                            texture.SetPixels(pixels);
                            texture.Apply();
                            File.WriteAllBytes($"{Folder}/{building.Id}.png", texture.EncodeToPNG());
                        }
                        finally
                        {
                            RenderTexture.active = previous;
                            Object.DestroyImmediate(texture);
                        }
                    }
                    finally
                    {
                        Object.DestroyImmediate(instance);
                    }
                }
            }
            finally
            {
                preview.Cleanup();
            }
            AssetDatabase.Refresh();
        }

        private static float Project(Vector3 extent, Vector3 axis) =>
            Mathf.Abs(axis.x) * extent.x + Mathf.Abs(axis.y) * extent.y + Mathf.Abs(axis.z) * extent.z;
    }
}
