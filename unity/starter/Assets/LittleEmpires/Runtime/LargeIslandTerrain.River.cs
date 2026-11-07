using LittleEmpires.Core;
using UnityEngine;

namespace LittleEmpires.Unity
{
    public sealed partial class LargeIslandTerrain
    {
        private Material _riverMaterial;
        private void BuildRiver()
        {
            const int steps = 384;
            var vertices = new Vector3[(steps+1)*2];
            var uv = new Vector2[vertices.Length];
            var triangles = new int[steps*6];
            for (var i=0;i<=steps;i++)
            {
                var z = i/(float)steps * (float)IslandRiver.SourceZ;
                var x = (float)IslandRiver.CenterX(z);
                var y = (float)IslandRiver.WaterElevation(x,z)-8;
                for (var side=0;side<2;side++)
                {
                    vertices[i*2+side] = new Vector3((x+.5f-_width/2f + (side==0?-.34f:.34f))*CellSize,y,(z+.5f-_depth/2f)*CellSize);
                    uv[i*2+side] = new Vector2(side,z*.6f);
                }
                if(i==steps) continue;
                var t=i*6; var v=i*2;
                triangles[t]=v; triangles[t+1]=v+2; triangles[t+2]=v+1;
                triangles[t+3]=v+1; triangles[t+4]=v+2; triangles[t+5]=v+3;
            }
            var mesh=new Mesh { name="Narrow flowing river", vertices=vertices, uv=uv, triangles=triangles };
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            var river=new GameObject("River - explicit core waterway"); river.transform.SetParent(transform,false);
            river.AddComponent<MeshFilter>().sharedMesh=mesh;
            _riverMaterial=new Material(Shader.Find("Universal Render Pipeline/Lit"));
            var texture=new Texture2D(32,128);
            texture.wrapMode=TextureWrapMode.Repeat;
            for(var y=0;y<128;y++) for(var x=0;x<32;x++)
            {
                var ripple=Mathf.Pow(.5f+.5f*Mathf.Sin(y/128f*Mathf.PI*8+x*.28f),12)*.035f;
                texture.SetPixel(x,y,new Color(.22f+ripple,.52f+ripple,.54f+ripple));
            }
            texture.Apply();
            _riverMaterial.mainTexture=texture; _riverMaterial.SetFloat("_Smoothness",.7f);
            river.AddComponent<MeshRenderer>().sharedMaterial=_riverMaterial;
        }
        private void Update()
        {
            if(_riverMaterial!=null) _riverMaterial.mainTextureOffset=new Vector2(0,Time.time*.12f);
        }
    }
}
