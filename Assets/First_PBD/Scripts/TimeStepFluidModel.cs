using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace JenniferFluid
{
    public class TimeStepFluidModel//: IDisposable ??
    {
        private const int THREADS = 128;
        private const int READ = 0;
        private const int WRITE = 1;

        //ThreadsGroups
        public int ThreadsGroups { get; private set; }

        public int SolverIterations { get; set; }

        public int ConstraintIterations { get; set; }

        private FluidModel m_fluid;
        private ComputeShader m_shader;
        public SmoothingKernel Kernel { get; private set; }

        public TimeStepFluidModel(FluidModel model, int NumBoundParticles)
        {


            SolverIterations = 2;
            ConstraintIterations = 2;

            m_fluid = model;
            //Boundary = boundary;

            float cellSize = model.ParticleRadius * 4.0f;
            int total = model.NumParticles + NumBoundParticles;//14448 + 13808
            //Hash = new GridHash(Boundary.Bounds, total, cellSize);
            Kernel = new SmoothingKernel(cellSize);

            int numParticles = model.NumParticles;//14448
            ThreadsGroups = numParticles / THREADS;//112
            if (numParticles % THREADS != 0) ThreadsGroups++;

            m_shader = Resources.Load("Simulation") as ComputeShader;
        }

        public void Dispose()
        {
            //Hash.Dispose();
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
            //About Kernel
            m_shader.SetFloat("KernelRadius", Kernel.Radius);
            m_shader.SetFloat("KernelRadius2", Kernel.Radius2);
            m_shader.SetFloat("Poly6Zero", Kernel.Poly6(Vector3.zero));
            m_shader.SetFloat("Poly6", Kernel.POLY6);
            m_shader.SetFloat("SpikyGrad", Kernel.SPIKY_GRAD);
            m_shader.SetFloat("ViscLap", Kernel.VISC_LAP);

            //m_shader.SetFloat("HashScale", Hash.InvCellSize);
            //m_shader.SetVector("HashSize", Hash.Bounds.size);
            //m_shader.SetVector("HashTranslate", Hash.Bounds.min);

            //Predicted and velocities use a double buffer as solver step
            //needs to read from many locations of buffer and write the result
            //in same pass. Could be removed if needed as long as buffer writes 
            //are atomic. Not sure if they are.

            for (int i = 0; i < SolverIterations; i++)
            {
                PredictPositions(dt);

                //Hash.Process(m_fluid.Predicted[READ], Boundary.Positions);

                //ConstrainPositions();

                //UpdateVelocities(dt);

                //SolveViscosity();

                //UpdatePositions();
            }

        }
        private void PredictPositions(float dt)
        {
            //Find the PredictPositions kernel
            int kernel = m_shader.FindKernel("PredictPositions");

            m_shader.SetBuffer(kernel, "Positions", m_fluid.Positions);
            m_shader.SetBuffer(kernel, "PredictedWRITE", m_fluid.Predicted[WRITE]);
            m_shader.SetBuffer(kernel, "VelocitiesREAD", m_fluid.Velocities[READ]);
            m_shader.SetBuffer(kernel, "VelocitiesWRITE", m_fluid.Velocities[WRITE]);

            m_shader.Dispatch(kernel, ThreadsGroups, 1, 1);
            //double buffer...
            Swap(m_fluid.Predicted);
            Swap(m_fluid.Velocities);
            
        }
        private void Swap(ComputeBuffer[] buffers)
        {
            ComputeBuffer tmp = buffers[0];
            buffers[0] = buffers[1];
            buffers[1] = tmp;
        }


        //public void ConstrainPositions()
        //{

        //    int computeKernel = m_shader.FindKernel("ComputeDensity");
        //    int solveKernel = m_shader.FindKernel("SolveConstraint");

        //    m_shader.SetBuffer(computeKernel, "Densities", m_fluid.Densities);
        //    m_shader.SetBuffer(computeKernel, "Pressures", m_fluid.Pressures);
        //    m_shader.SetBuffer(computeKernel, "Boundary", Boundary.Positions);
        //    m_shader.SetBuffer(computeKernel, "IndexMap", Hash.IndexMap);
        //    m_shader.SetBuffer(computeKernel, "Table", Hash.Table);

        //    m_shader.SetBuffer(solveKernel, "Pressures", m_fluid.Pressures);
        //    m_shader.SetBuffer(solveKernel, "Boundary", Boundary.Positions);
        //    m_shader.SetBuffer(solveKernel, "IndexMap", Hash.IndexMap);
        //    m_shader.SetBuffer(solveKernel, "Table", Hash.Table);

        //    for (int i = 0; i < ConstraintIterations; i++)
        //    {
        //        m_shader.SetBuffer(computeKernel, "PredictedREAD", m_fluid.Predicted[READ]);
        //        m_shader.Dispatch(computeKernel, ThreadsGroups, 1, 1);

        //        m_shader.SetBuffer(solveKernel, "PredictedREAD", m_fluid.Predicted[READ]);
        //        m_shader.SetBuffer(solveKernel, "PredictedWRITE", m_fluid.Predicted[WRITE]);
        //        m_shader.Dispatch(solveKernel, ThreadsGroups, 1, 1);

        //        Swap(m_fluid.Predicted);
        //    }
        //}
    }
}
