using System;
using System.Collections.Generic;
using Unity.Collections;//for leakDetection
using UnityEngine;
using UnityEngine.Rendering;

namespace JenniferFluid
{
    public class FluidModel : IDisposable
    {
        public int NumParticles { get; private set; }

        public Bounds Bounds;

        private Vector4[] fluids_positions_array;
        public float Density { get; set; }

        public float Viscosity { get; set; }

        public float Dampning { get; set; }

        public float ParticleRadius { get; private set; }

        public float ParticleDiameter { get; private set; }

        public float ParticleMass { get; set; }

        public float ParticleVolume { get; private set; }

        public ComputeBuffer Pressures { get; private set; }

        public ComputeBuffer Densities { get; private set; }

        public ComputeBuffer Positions { get; private set; }

        public ComputeBuffer[] Predicted { get; private set; }

        public ComputeBuffer[] Velocities { get; private set; }

        private ComputeBuffer m_argsBuffer;
        
        public FluidModel(List<Vector4> Fluids_Positions, Bounds fluid_bounds, float radius, float density, Matrix4x4 RTS)
        {
            Unity.Collections.LowLevel.Unsafe.UnsafeUtility.SetLeakDetectionMode(NativeLeakDetectionMode.EnabledWithStackTrace);
            NumParticles = Fluids_Positions.Count;
            Bounds = fluid_bounds;
            fluids_positions_array = Fluids_Positions.ToArray();
            //Debug.Log(fluids_positions_array[0]);
            //Debug.Log(fluids_positions_array[1]);
            

            Density = density;
            Viscosity = 0.002f;
            Dampning = 0.0f;

            ParticleRadius = radius;
            ParticleVolume = (4.0f / 3.0f) * Mathf.PI * Mathf.Pow(radius, 3);
            ParticleMass = ParticleVolume * Density;
            ParticleDiameter =  ParticleRadius * 2.0f; 
            Densities = new ComputeBuffer(NumParticles, sizeof(float));
            Pressures = new ComputeBuffer(NumParticles, sizeof(float));

            CreateParticles(Fluids_Positions, RTS);
        }

        private void CreateParticles(List<Vector4> Fluids_Positions, Matrix4x4 RTS)
        {
            Vector4[] positions = new Vector4[NumParticles];
            Vector4[] predicted = new Vector4[NumParticles];
            Vector4[] velocities = new Vector4[NumParticles];

            //position and predicted == initial positions
            fluids_positions_array.CopyTo(positions, 0);
            fluids_positions_array.CopyTo(predicted, 0);
            //Debug.Log(positions[0]);
            //Debug.Log(positions[1]);
            Positions = new ComputeBuffer(NumParticles, 4 * sizeof(float));
            Positions.SetData(positions);

            Debug.Log(Positions.count + " fluid position count ");
            //Predicted and velocities use a double buffer as solver step
            //needs to read from many locations of buffer and write the result
            //in same pass. Could be removed if needed as long as buffer writes 
            //are atomic. Not sure if they are.

            //4 buffers * 20000+particles hmmm

            Predicted = new ComputeBuffer[2];
            Predicted[0] = new ComputeBuffer(NumParticles, 4 * sizeof(float));
            Predicted[0].SetData(predicted);
            Predicted[1] = new ComputeBuffer(NumParticles, 4 * sizeof(float));
            Predicted[1].SetData(predicted);

            Velocities = new ComputeBuffer[2];
            Velocities[0] = new ComputeBuffer(NumParticles, 4 * sizeof(float));
            Velocities[0].SetData(velocities);
            Velocities[1] = new ComputeBuffer(NumParticles, 4 * sizeof(float));
            Velocities[1].SetData(velocities);

            Debug.Log(Predicted[0].count + " Predicted[0] position count ");
            Debug.Log(Velocities[0].count + " Velocities[0] position count ");
        }
        /// <summary>
        /// Draws the mesh spheres when draw particles is enabled.
        /// </summary>
        public void Draw(Camera cam, Mesh mesh, Material material, int layer)//call on Each frame ?what's layer
        {
            //Debug.Log(mesh.GetIndexCount(0));
            if (m_argsBuffer == null)
                CreateArgBuffer(mesh.GetIndexCount(0));//2306

            material.SetBuffer("positions", Positions);
            material.SetColor("color", new Color(4.0f, 79.0f, 118.0f));//Color.white   //new Color(4.0f, 79.0f, 180.0f)
            material.SetFloat("diameter", ParticleDiameter);

            ShadowCastingMode castShadow = ShadowCastingMode.Off;
            bool recieveShadow = false;

            Graphics.DrawMeshInstancedIndirect(mesh, 0, material, Bounds, m_argsBuffer, 0, null, castShadow, recieveShadow, layer, cam);
        }

        public void Dispose()
        {

            if (Positions != null)
            {
                Positions.Release();
                Positions = null;
            }

            if (Densities != null)
            {
                Densities.Release();
                Densities = null;
            }

            if (Pressures != null)
            {
                Pressures.Release();
                Pressures = null;
            }

            CBUtility.Release(Predicted);
            CBUtility.Release(Velocities);
            CBUtility.Release(ref m_argsBuffer);
        }


        private void CreateArgBuffer(uint indexCount)
        {
            uint[] args = new uint[5] { 0, 0, 0, 0, 0 };
            args[0] = indexCount;//2306
            args[1] = (uint)NumParticles;//14448

            m_argsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
            m_argsBuffer.SetData(args);
        }

    }


}