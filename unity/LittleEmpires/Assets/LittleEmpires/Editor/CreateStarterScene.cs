using System.IO;
using LittleEmpires.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LittleEmpires.Unity.Editor
{
    /// <summary>
    /// One-click setup for the first executable scene.
    /// Uses the currently configured Universal 3D project and creates no
    /// extra rendering-pipeline settings or external asset dependencies.
    /// </summary>
    public static class CreateStarterScene
    {
        private const string ScenePath = "Assets/LittleEmpires/Scenes/StarterIsland.unity";

        [MenuItem("Little Empires/Create Starter Island Scene")]
        public static void CreateScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            var scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Little Empires - Game");
            root.AddComponent<GameBootstrap>();

            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 200f;
            camera.clearFlags = CameraClearFlags.Skybox;

            var sunObject = new GameObject("Warm afternoon sun");
            var sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, .91f, .76f);
            sun.intensity = 1.25f;
            sun.transform.rotation = Quaternion.Euler(53f, -37f, 0f);

            const string folder = "Assets/LittleEmpires/Scenes";
            if (!AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder("Assets/LittleEmpires", "Scenes");

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(ScenePath, true)
            };
            AssetDatabase.Refresh();
            Selection.activeGameObject = root;

            EditorUtility.DisplayDialog("Little Empires",
                "StarterIsland is ready. Press Play!\n\n" +
                "Build: choose a tool in the left panel and click a tile.\n" +
                "R: dirt roads, T: stone roads, E: end season.\n" +
                "Right mouse: orbit; mouse wheel: zoom.\n\n" +
                "Use Edit > Project Settings > Player > Active Input Handling = Both " +
                "if Unity reports legacy input errors.",
                "Let's build");
        }
    }
}
