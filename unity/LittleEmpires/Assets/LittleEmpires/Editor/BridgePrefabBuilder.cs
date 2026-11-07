using System.IO;
using UnityEditor;
using UnityEngine;

namespace LittleEmpires.Unity.Editor
{
    /// <summary>Assembles provisional CC0 Quaternius bridge art. Deck top is local Y=0; span follows X.</summary>
    public static class BridgePrefabBuilder
    {
        private const string Source="Assets/LittleEmpires/Art/Quaternius/Medieval Village MegaKit[Standard]/FBX/";
        private const string Destination="Assets/LittleEmpires/Art/Bridges/";
        [MenuItem("Little Empires/Create Quaternius Bridge Prefabs")]
        public static void Build()
        {
            Directory.CreateDirectory(Destination);
            AssetDatabase.Refresh();
            foreach(var stone in new[]{false,true})
            {
                var name=stone?"StoneRiverBridge":"WoodenRiverBridge";
                var root=new GameObject(name);
                var material=AssetDatabase.LoadAssetAtPath<Material>(Destination+name+".mat");
                if(material==null)
                {
                    material=new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    material.color=stone?new Color(.63f,.59f,.49f):new Color(.40f,.27f,.15f);
                    AssetDatabase.CreateAsset(material,Destination+name+".mat");
                }
                Fit(root.transform,"Floor_WoodDark",new Vector3(0,-.08f,0),new Vector3(6.2f,.16f,3.2f),material);
                foreach(var sign in new[]{-1,1})
                {
                    if(stone)
                    {
                        Fit(root.transform,"Wall_Arch",new Vector3(0,-1.05f,sign*1.43f),new Vector3(6.2f,2.1f,.35f),material);
                        Fit(root.transform,"Wall_UnevenBrick_Straight",new Vector3(0,.4f,sign*1.5f),new Vector3(6.2f,.8f,.3f),material);
                    }
                    else
                        Fit(root.transform,"Prop_WoodenFence_Single",new Vector3(0,.42f,sign*1.5f),new Vector3(6.1f,.84f,.16f),material);
                }
                PrefabUtility.SaveAsPrefabAsset(root,Destination+name+".prefab");
                Object.DestroyImmediate(root);
            }
            AssetDatabase.SaveAssets();
        }
        private static void Fit(Transform parent,string model,Vector3 center,Vector3 size,Material material)
        {
            var asset=AssetDatabase.LoadAssetAtPath<GameObject>(Source+model+".fbx");
            if(asset==null) throw new System.InvalidOperationException("Missing bridge piece "+model);
            var wrapper=new GameObject(model).transform;
            var instance=(GameObject)PrefabUtility.InstantiatePrefab(asset);
            instance.transform.SetParent(wrapper,false);
            var renderers=instance.GetComponentsInChildren<Renderer>();
            var bounds=renderers[0].bounds;
            foreach(var r in renderers) { bounds.Encapsulate(r.bounds); r.sharedMaterials=System.Array.ConvertAll(r.sharedMaterials,m=>material); }
            instance.transform.position-=bounds.center;
            wrapper.localScale=new Vector3(size.x/bounds.size.x,size.y/bounds.size.y,size.z/bounds.size.z);
            wrapper.SetParent(parent,false); wrapper.localPosition=center;
            foreach(var collider in instance.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(collider);
        }
    }
}
