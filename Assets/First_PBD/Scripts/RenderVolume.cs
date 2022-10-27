using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace JenniferFluid {

    public class RenderVolume : MonoBehaviour
    {
        private const int THREADS = 8;//???

        public GameObject m_mesh;

        //From RenderVolume()
        public Bounds bounds;

        public Bounds world_bounds;
        public RenderTexture new_VolumeTexture { get; private set; }


        public RenderVolume(Bounds bounds, float pixelSize)//boundary bounds, particle's radius
        {
            //PixelSize = pixelSize;
            world_bounds = bounds;
            //1 recalculate the bounds for volume

            //Vector3 min, max;
            //min.x = bounds.min.x;
            //min.y = bounds.min.y;
            //min.z = bounds.min.z;

            //max.x = min.x + (float)Math.Ceiling(bounds.size.x / PixelSize);
            //max.y = min.y + (float)Math.Ceiling(bounds.size.y / PixelSize);
            //max.z = min.z + (float)Math.Ceiling(bounds.size.z / PixelSize);

            //Bounds = new Bounds();
            //Bounds.SetMinMax(min, max);

            //int width = (int)Bounds.size.x;
            //int height = (int)Bounds.size.y;
            //int depth = (int)Bounds.size.z;

            //int groupsX = width / THREADS;
            //if (width % THREADS != 0) groupsX++;

            //int groupsY = height / THREADS;
            //if (height % THREADS != 0) groupsY++;

            //int groupsZ = depth / THREADS;
            //if (depth % THREADS != 0) groupsZ++;

            //Groups = new Vector3Int(groupsX, groupsY, groupsZ);
            ////Volume = a texture(boundary.fluid.x and y)
            //Volume = new RenderTexture(width, height, 0, RenderTextureFormat.RFloat, RenderTextureReadWrite.Linear);
            //Volume.dimension = TextureDimension.Tex3D;
            //Volume.volumeDepth = depth;
            //Volume.useMipMap = false;
            //Volume.enableRandomWrite = true;
            //Volume.wrapMode = TextureWrapMode.Clamp;
            //Volume.filterMode = FilterMode.Bilinear;
            //Volume.Create();

            //m_shader = Resources.Load("ComputeVolume") as ComputeShader;
        }



        /// <summary>
        /// A inverted cube (material culls front) needs
        /// to be draw for the ray tracing of the volume.
        /// </summary>
        public void CreateMesh(Material material)
        {
            //Create a cube

            //material that has been assigned in the editor
            m_mesh = GameObject.CreatePrimitive(PrimitiveType.Cube);
            m_mesh.GetComponent<MeshRenderer>().sharedMaterial = material;


            Debug.Log(bounds.ToString());
            m_mesh.transform.position = world_bounds.center;
            m_mesh.transform.localScale = world_bounds.size;

            //Unity has 3d material???
            material.SetVector("Translate", m_mesh.transform.position);
            material.SetVector("Scale", m_mesh.transform.localScale);
            material.SetTexture("Volume", new_VolumeTexture);
            material.SetVector("Size", bounds.size);

        }


        public void Dispose()
        {
            if (m_mesh != null)
            {
                GameObject.DestroyImmediate(m_mesh);
                m_mesh = null;
            }
        }


    }


}
