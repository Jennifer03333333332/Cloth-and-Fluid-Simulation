using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;


namespace JenniferFluid
{
    public class BoundaryModel : IDisposable
    {

        public int NumParticles { get; private set; }

        public Bounds Bounds;
        private Vector4[] boundary_pos_array;
        public ComputeBuffer Boundary_pos_cbuffer { get; private set; }

        private ComputeBuffer boundary_argsBuffer;

        public float radius;
        public BoundaryModel(List<Vector4> Boundary_Positions, Bounds boundary_bounds, float b_radius, float density)
        {
            NumParticles = Boundary_Positions.Count;
            boundary_pos_array = Boundary_Positions.ToArray();
            Bounds = boundary_bounds;
            radius = b_radius;

            Boundary_pos_cbuffer = new ComputeBuffer(NumParticles, 4 * sizeof(float));
            Boundary_pos_cbuffer.SetData(boundary_pos_array);

            CreateBoundryPsi();
        }

        private void CreateBoundryPsi()//what's Psi?
        {
            float cellSize = radius * 4.0f;
            SmoothingKernel K = new SmoothingKernel(cellSize);
            //Debug.Log(cellSize);

            //Stuck here, wait for further dev
        }
        public void Draw(Camera cam, Mesh mesh, Material material, int layer)
        {

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

