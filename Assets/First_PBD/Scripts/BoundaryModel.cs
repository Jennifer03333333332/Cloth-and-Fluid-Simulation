using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;


namespace JenniferFluid
{
    public class BoundaryModel : IDisposable
    {
        private const int THREADS = 128;
        public int NumParticles { get; private set; }

        public Bounds Bounds;
        public float radius;
        public float Density { get; private set; }

        private Vector4[] boundary_pos_array;
        public ComputeBuffer Boundary_pos_cbuffer { get; private set; }
        //arguments, length = 5
        private ComputeBuffer boundary_argsBuffer;

        
        public BoundaryModel(List<Vector4> Boundary_Positions, Bounds boundary_bounds, float b_radius, float density)
        {
            NumParticles = Boundary_Positions.Count;
            boundary_pos_array = Boundary_Positions.ToArray();
            Bounds = boundary_bounds;
            radius = b_radius;
            Density = density;//1000.0f;
            Boundary_pos_cbuffer = new ComputeBuffer(NumParticles, 4 * sizeof(float));
            Boundary_pos_cbuffer.SetData(boundary_pos_array);

            CreateBoundryPsi();
        }

        
        //now it's just a const
        private void CreateBoundryPsi()//what's Psi?
        {
            float cellSize = radius * 4.0f;
            SmoothingKernel K = new SmoothingKernel(cellSize);
            //Debug.Log(cellSize);

            //Stuck here, wait for further dev
            Grid grid = new Grid(Bounds, NumParticles, cellSize);
            grid.Process(Boundary_pos_cbuffer);
            //BoundaryModel.compute
            //Target: Boundary_pos_cbuffer[id].w = psi;
            //psi = Density * volume which is mass??
            ComputeShader shader = Resources.Load("BoundaryModel") as ComputeShader;
            //Target: Boundary_pos_cbuffer[id].w = psi;
            //psi = Density * volume which is mass??
            int kernel = shader.FindKernel("ComputePsi");

            shader.SetFloat("Density", Density);
            shader.SetFloat("KernelRadiuse", K.Radius);
            shader.SetFloat("KernelRadius2", K.Radius2);
            shader.SetFloat("Poly6", K.POLY6);
            shader.SetFloat("Poly6Zero", K.Poly6(Vector3.zero));
            shader.SetInt("NumParticles", NumParticles);

            shader.SetFloat("HashScale", grid.InvCellSize);
            shader.SetVector("HashSize", grid.Bounds.size);
            shader.SetVector("HashTranslate", grid.Bounds.min);
            shader.SetBuffer(kernel, "IndexMap", grid.IndexMap);
            shader.SetBuffer(kernel, "Table", grid.Table);

            shader.SetBuffer(kernel, "Boundary", Boundary_pos_cbuffer);

            int groups = NumParticles / THREADS;
            if (NumParticles % THREADS != 0) groups++;

            //Fills the boundarys psi array so the fluid can
            //collide against it smoothly. The original computes
            //the phi for each boundary particle based on the
            //density of the boundary but I find the fluid 
            //leaks out so Im just using a const value.

            shader.Dispatch(kernel, groups, 1, 1);

            grid.Dispose();
        }
        public void Draw(Camera cam, Mesh mesh, Material material, int layer)
        {
            //create boundary_argsBuffer, length = 5
            //{mesh's indexCount, NumParticles, 0, 0, 0}
            uint[] args = new uint[5] { 0, 0, 0, 0, 0 };
            args[0] = mesh.GetIndexCount(0);
            args[1] = (uint)NumParticles;

            boundary_argsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
            boundary_argsBuffer.SetData(args);
            //connect to surface shader of this mat
            material.SetBuffer("positions", Boundary_pos_cbuffer);
            material.SetColor("color", Color.blue);
            material.SetFloat("diameter", radius * 2.0f);

            ShadowCastingMode castShadow = ShadowCastingMode.Off;
            bool recieveShadow = false;
            //generate this mesh many times on the position lists
            Graphics.DrawMeshInstancedIndirect(mesh, 0, material, Bounds, boundary_argsBuffer, 0, null, castShadow, recieveShadow, 0, Camera.main);

        }
        public void Dispose()
        {
            if (Boundary_pos_cbuffer != null)
            {
                Boundary_pos_cbuffer.Release();
                Boundary_pos_cbuffer = null;
            }

            CBUtility.Release(ref boundary_argsBuffer);

        }
    }

}

