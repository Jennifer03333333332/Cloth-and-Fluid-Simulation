using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
//test
using UnityEngine.Rendering;
namespace JenniferFluid
{
    public class TimeStepFluidModel: IDisposable
    {
        private const int THREADS = 128;
        private const int READ = 0;
        private const int WRITE = 1;
        //test
        bool capturing = true;
        //ThreadsGroups
        public int ThreadsGroups { get; private set; }

        public int SolverIterations { get; set; }

        public int ConstraintIterations { get; set; }
        public Grid m_grid { get; private set; }

        private FluidModel m_fluid;
        private BoundaryModel m_boundary;

        private ComputeShader m_shader;
        public SmoothingKernel Kernel { get; private set; }
        public Vector4[] debugarray;
        public TimeStepFluidModel(FluidModel model, BoundaryModel boundary)
        {
            SolverIterations = 2;
            ConstraintIterations = 2;

            m_fluid = model;
            m_boundary = boundary;

            float cellSize = model.ParticleRadius * 4.0f;
            int total = model.NumParticles + boundary.NumParticles;//14448 + 13808
            m_grid = new Grid(boundary.Bounds, total, cellSize);
            Kernel = new SmoothingKernel(cellSize);

            int numParticles = model.NumParticles;//14448
            ThreadsGroups = numParticles / THREADS;//112
            if (numParticles % THREADS != 0) ThreadsGroups++;

            m_shader = Resources.Load("Simulation") as ComputeShader;
        }

        public void Dispose()
        {
            m_grid.Dispose();
        }
        public void StepPhysics(float dt)
        {

            if (dt <= 0.0) return;
            if (SolverIterations <= 0 || ConstraintIterations <= 0) return;

            dt /= SolverIterations;
            //Set the Simulation.compute
            //About the FluidModel
            m_shader.SetInt("NumParticles", m_fluid.NumParticles);
            m_shader.SetVector("Gravity", new Vector3(0.0f, -9.81f, 0.0f));
            m_shader.SetFloat("Dampning", m_fluid.Dampning);
            m_shader.SetFloat("DeltaTime", dt);
            m_shader.SetFloat("Density", m_fluid.Density);
            m_shader.SetFloat("Viscosity", m_fluid.Viscosity);
            m_shader.SetFloat("ParticleMass", m_fluid.ParticleMass);
            //About Grid Kernel
            m_shader.SetFloat("KernelRadius", Kernel.Radius);
            m_shader.SetFloat("KernelRadius2", Kernel.Radius2);
            m_shader.SetFloat("Poly6Zero", Kernel.Poly6(Vector3.zero));
            m_shader.SetFloat("Poly6", Kernel.POLY6);
            m_shader.SetFloat("SpikyGrad", Kernel.SPIKY_GRAD);
            m_shader.SetFloat("ViscLap", Kernel.VISC_LAP);

            m_shader.SetFloat("HashScale", m_grid.InvCellSize);
            m_shader.SetVector("HashSize", m_grid.Bounds.size);
            m_shader.SetVector("HashTranslate", m_grid.Bounds.min);

            //Predicted and velocities use a double buffer as solver step
            //needs to read from many locations of buffer and write the result
            //in same pass. Could be removed if needed as long as buffer writes 
            //are atomic. Not sure if they are.

            for (int i = 0; i < SolverIterations; i++)
            {   //calculate the predicted v and pos, store in m_fluid.Predicted[WRITE] and m_fluid.Velocities[WRITE], then swap
                PredictPositions(dt);
                //[Read] before is [Write]
                m_grid.Process(m_fluid.Predicted[READ], m_boundary.Boundary_pos_cbuffer);

                ConstrainPositions();

                UpdateVelocities(dt);

                SolveViscosity();

                UpdatePositions();
            }

        }

        //calculate the predicted v and pos (PredictedWRITE and VelocitiesWRITE)
        //v' = v + (Gravity - Dampning)*dt
        //pos = pos + v'*dt
        private void PredictPositions(float dt)
        {
            //Find the kernel  (PredictPositions) in Simulation.compute
            int kernel = m_shader.FindKernel("PredictPositions");

            m_shader.SetBuffer(kernel, "Positions", m_fluid.Positions);
            m_shader.SetBuffer(kernel, "PredictedWRITE", m_fluid.Predicted[WRITE]);
            m_shader.SetBuffer(kernel, "VelocitiesREAD", m_fluid.Velocities[READ]);
            m_shader.SetBuffer(kernel, "VelocitiesWRITE", m_fluid.Velocities[WRITE]);

            m_shader.Dispatch(kernel, ThreadsGroups, 1, 1);
            //void PredictPositions(int id : SV_DispatchThreadID)

            //dt is each iteration time; Damping now = 0


            //Predicted and Velocities has double buffer, init() is the same
            Swap(m_fluid.Predicted);
            Swap(m_fluid.Velocities);
            
        }
        
        
        private void Swap(ComputeBuffer[] buffers)
        {
            ComputeBuffer tmp = buffers[0];
            buffers[0] = buffers[1];
            buffers[1] = tmp;
        }

        //in simulation.compute
        //ComputeDensity():using predicted pos, calculate Densities and Pressures
        //
        //SolveConstraint(): pos += SolveDensity(particlesid, pos, pressure)
        public void ConstrainPositions()
        {
            if (PIX.IsAttached() && capturing)
            {
                PIX.BeginGPUCapture();

            }
            int computeKernel = m_shader.FindKernel("ComputeDensity");
            int solveKernel = m_shader.FindKernel("SolveConstraint");

            m_shader.SetBuffer(computeKernel, "Densities", m_fluid.Densities);
            m_shader.SetBuffer(computeKernel, "Pressures", m_fluid.Pressures);
            m_shader.SetBuffer(computeKernel, "Boundary", m_boundary.Boundary_pos_cbuffer);
            m_shader.SetBuffer(computeKernel, "IndexMap", m_grid.IndexMap);
            m_shader.SetBuffer(computeKernel, "Table", m_grid.Table);

            m_shader.SetBuffer(solveKernel, "Pressures", m_fluid.Pressures);
            m_shader.SetBuffer(solveKernel, "Boundary", m_boundary.Boundary_pos_cbuffer);
            m_shader.SetBuffer(solveKernel, "IndexMap", m_grid.IndexMap);
            m_shader.SetBuffer(solveKernel, "Table", m_grid.Table);
            //for each iterations
            for (int i = 0; i < ConstraintIterations; i++)
            {
                m_shader.SetBuffer(computeKernel, "PredictedREAD", m_fluid.Predicted[READ]);
                m_shader.Dispatch(computeKernel, ThreadsGroups, 1, 1);

                m_shader.SetBuffer(solveKernel, "PredictedREAD", m_fluid.Predicted[READ]);
                m_shader.SetBuffer(solveKernel, "PredictedWRITE", m_fluid.Predicted[WRITE]);
                m_shader.Dispatch(solveKernel, ThreadsGroups, 1, 1);

                Swap(m_fluid.Predicted);
            }

            if (capturing)
            {
                capturing = false;
                PIX.EndGPUCapture();
            }

            //m_fluid.Predicted[READ].GetData(debugarray);
            //Debug.Log(debugarray[0]);
        }

        //v = (pos' - original pos)/dt
        private void UpdateVelocities(float dt)
        {
            int kernel = m_shader.FindKernel("UpdateVelocities");

            m_shader.SetBuffer(kernel, "Positions", m_fluid.Positions);
            m_shader.SetBuffer(kernel, "PredictedREAD", m_fluid.Predicted[READ]);
            m_shader.SetBuffer(kernel, "VelocitiesWRITE", m_fluid.Velocities[WRITE]);

            m_shader.Dispatch(kernel, ThreadsGroups, 1, 1);

            Swap(m_fluid.Velocities);
        }


        /// <summary>
        /// calculate viscosity
        /// then update velocity
        /// </summary>
        private void SolveViscosity()
        {
            int kernel = m_shader.FindKernel("SolveViscosity");

            m_shader.SetBuffer(kernel, "Densities", m_fluid.Densities);
            m_shader.SetBuffer(kernel, "Boundary", m_boundary.Boundary_pos_cbuffer);
            m_shader.SetBuffer(kernel, "IndexMap", m_grid.IndexMap);
            m_shader.SetBuffer(kernel, "Table", m_grid.Table);

            m_shader.SetBuffer(kernel, "PredictedREAD", m_fluid.Predicted[READ]);
            m_shader.SetBuffer(kernel, "VelocitiesREAD", m_fluid.Velocities[READ]);
            m_shader.SetBuffer(kernel, "VelocitiesWRITE", m_fluid.Velocities[WRITE]);

            m_shader.Dispatch(kernel, ThreadsGroups, 1, 1);

            Swap(m_fluid.Velocities);
        }
        
        /// <summary>
        /// simply set Positions = PredictedRead
        /// </summary>
        private void UpdatePositions()
        {
            int kernel = m_shader.FindKernel("UpdatePositions");

            m_shader.SetBuffer(kernel, "Positions", m_fluid.Positions);
            m_shader.SetBuffer(kernel, "PredictedREAD", m_fluid.Predicted[READ]);

            m_shader.Dispatch(kernel, ThreadsGroups, 1, 1);
        }

    }
}
