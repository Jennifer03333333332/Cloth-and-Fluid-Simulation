using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace JenniferFluid
{
    public class Main : MonoBehaviour
    {
        //Attributes
        //[Header("particle's radius")]
        [SerializeField]
        //particle's radius
        public float radius;
        [SerializeField]
        public float density;

        [Header("Settings")]
        [SerializeField]
        public const float timeStep = 1.0f / 60.0f;
        
        public bool m_drawFluidParticles = false;
        public bool m_drawBoundaryParticles = false;
        public bool m_startRunning = false;

        [Header("Materials")]
        [SerializeField]
        public Material volume_mat;

        public Mesh m_particleMesh;
        public Material m_fluidParticleMat;
        public Material m_boundaryParticleMat;

        [Header("Boundary")]
        [SerializeField]

        //actual boundary particles' bound
        public Vector3 min_boundarybounds;
        public Vector3 max_boundarybounds;

        public List<Bounds> Exclusion_list;// { get; private set; }

        public int NumBoundaryParticles;

        //real boundary particles' AABB
        private Bounds boundary_Bounds;


        public ComputeBuffer Boundary_pos_cbuffer { get; private set; }
        private ComputeBuffer boundary_argsBuffer;
        [Header("Fluid")]
        [SerializeField]
        public Vector3 min_realfluidbounds;
        public Vector3 max_realfluidbounds;

        public List<Bounds> Fluid_Exclusion_list;

        public int NumFluidParticles;
        
        private Bounds fluids_Bounds;

        public float fluid_spacing;
        




        private bool wasError;

        //List of particles' position
        private List<Vector4> Boundary_Positions;
        private List<Vector4> Fluids_Positions;
        

        private FluidModel m_fluid;

        private TimeStepFluidModel simulation;

        private RenderVolume m_volume;

        private void CreateBoundary(Vector3 min_boun_innerbounds, Vector3 max_boun_innerbounds)
        {
            //内部区域 AABB
            Bounds innerBounds = new Bounds();
            innerBounds.SetMinMax(min_boun_innerbounds, max_boun_innerbounds);
            Debug.Log("Boundary inner bounds = " + innerBounds.ToString());
            //在内部区域外面增加一圈边界粒子，厚度为内部粒子的1.2
            float thickness = 1.2f; //1f
            float diameter = radius * 2;
            Vector3 min_outerbounds = min_boun_innerbounds - new Vector3(diameter * thickness, diameter * thickness, diameter * thickness);
            Vector3 max_outerbounds = max_boun_innerbounds + new Vector3(diameter * thickness, diameter * thickness, diameter * thickness);

            Bounds outerBounds = new Bounds();
            outerBounds.SetMinMax(min_outerbounds, max_outerbounds);

            Debug.Log("Boundary outer bounds = " + outerBounds.ToString());
            //create a array of particles
            //evenly spaced between the inner and outer bounds.


            //粒子的直径被用做spacing
            //List of 边界AABB盒子，目前里面只有一个边界
            Exclusion_list = new List<Bounds>();
            Exclusion_list.Add(innerBounds);

            //CreateParticles. 以outerbounds 和 radius计算全部的粒子，排除掉inside innerbounds的粒子
            // 1 calculate the numbers of particles
            // ! (int) in C# == (int) in C++
            int numX = (int)((outerBounds.size.x + radius) / diameter);
            int numY = (int)((outerBounds.size.y + radius) / diameter);
            int numZ = (int)((outerBounds.size.z + radius) / diameter);
            //52,82,22

            //Position list in GPU would be better?
            Boundary_Positions = new List<Vector4>();
            float inf = float.PositiveInfinity;
            min_boundarybounds = new Vector3(inf, inf, inf);
            max_boundarybounds = new Vector3(-inf, -inf, -inf);

            for (int z = 0; z < numZ; z++)
            {
                for (int y = 0; y < numY; y++)
                {
                    for (int x = 0; x < numX; x++)
                    {
                        //each particle's position
                        Vector4 pos = new Vector4();
                        pos.x = diameter * x + outerBounds.min.x + radius;
                        pos.y = diameter * y + outerBounds.min.y + radius;
                        pos.z = diameter * z + outerBounds.min.z + radius;

                        bool exclude = false;
                        for (int i = 0; i < Exclusion_list.Count; i++)//目前only has one outerBound
                        {
                            //如果pos在内边界内,找下一个粒子
                            if (Exclusion_list[i].Contains(pos))
                            {
                                exclude = true;
                                break;
                            }
                        }
                        //如果pos不在内边界内, Boundary_Positions list增加
                        if (!exclude)
                        {
                            Boundary_Positions.Add(pos);
                            //calculate the range of real bound particles
                            if (pos.x < min_boundarybounds.x) min_boundarybounds.x = pos.x;
                            if (pos.y < min_boundarybounds.y) min_boundarybounds.y = pos.y;
                            if (pos.z < min_boundarybounds.z) min_boundarybounds.z = pos.z;

                            if (pos.x > max_boundarybounds.x) max_boundarybounds.x = pos.x;
                            if (pos.y > max_boundarybounds.y) max_boundarybounds.y = pos.y;
                            if (pos.z > max_boundarybounds.z) max_boundarybounds.z = pos.z;

                        }

                    }
                }
            }
            NumBoundaryParticles = Boundary_Positions.Count;
            Debug.Log("Boundary Particles = " + NumBoundaryParticles);
            min_boundarybounds -= new Vector3(radius, radius, radius);
            max_boundarybounds += new Vector3(radius, radius, radius);
            boundary_Bounds = new Bounds();
            boundary_Bounds.SetMinMax(min_boundarybounds, max_boundarybounds);
            Debug.Log("Actual Boundary bounds = " + boundary_Bounds.ToString());
            //Create Boundary Particles
            //m_boundary = new FluidBoundary(source, radius, density, Matrix4x4.identity);
            Vector4[] boundary_pos_array = Boundary_Positions.ToArray();

            Boundary_pos_cbuffer = new ComputeBuffer(Boundary_Positions.Count, 4 * sizeof(float));
            Boundary_pos_cbuffer.SetData(boundary_pos_array);
        }
        private void CreateBoundryPsi()//what's Psi?
        {
            float cellSize = radius * 4.0f;
            SmoothingKernel K = new SmoothingKernel(cellSize);
            //Debug.Log(cellSize);

            //Stuck here, wait for further dev
        }
        private void CreateFluid()
        {
            Vector3 min_fluidbounds = new Vector3(-8 + radius, 0 + radius, -1 + radius);
            Vector3 max_fluidbounds = new Vector3(-4 - radius, 8 - radius, 2 - radius);//(-4, 8, 2); just give the initial fluid's volume

            Bounds fluidbounds = new Bounds();
            fluidbounds.SetMinMax(min_fluidbounds, max_fluidbounds);
            Debug.Log("Fluid bounds = " + fluidbounds.ToString());


            //The source will create a array of particles
            //evenly spaced inside the bounds. 
            //Multiple the spacing by 0.9 to pack more
            //particles into bounds.

            //diameter*0.9 被用做spacing
            fluid_spacing = radius * 2.0f  * 0.9f;
            float half_fluid_spacing = fluid_spacing * 0.5f;
            Fluid_Exclusion_list = new List<Bounds>();

            int numX = (int)((fluidbounds.size.x + half_fluid_spacing) / fluid_spacing);
            int numY = (int)((fluidbounds.size.y + half_fluid_spacing) / fluid_spacing);
            int numZ = (int)((fluidbounds.size.z + half_fluid_spacing) / fluid_spacing);

            Fluids_Positions = new List<Vector4>();
            float inf = float.PositiveInfinity;
            min_realfluidbounds = new Vector3(inf, inf, inf);
            max_realfluidbounds = new Vector3(-inf, -inf, -inf);
            for (int z = 0; z < numZ; z++)
            {
                for (int y = 0; y < numY; y++)
                {
                    for (int x = 0; x < numX; x++)
                    {
                        //each particle's position
                        Vector4 pos = new Vector4();
                        pos.x = fluid_spacing * x + fluidbounds.min.x + half_fluid_spacing;
                        pos.y = fluid_spacing * y + fluidbounds.min.y + half_fluid_spacing;
                        pos.z = fluid_spacing * z + fluidbounds.min.z + half_fluid_spacing;

                        bool exclude = false;
                        for (int i = 0; i < Fluid_Exclusion_list.Count; i++)//目前only has one outerBound
                        {
                            //如果pos在内边界内,找下一个粒子
                            if (Fluid_Exclusion_list[i].Contains(pos))
                            {
                                exclude = true;
                                break;
                            }
                        }

                        //如果pos不在内边界内, Boundary positions list 增加
                        if (!exclude)
                        {
                            Fluids_Positions.Add(pos);
                            //calculate the range of real bound particles
                            if (pos.x < min_realfluidbounds.x) min_realfluidbounds.x = pos.x;
                            if (pos.y < min_realfluidbounds.y) min_realfluidbounds.y = pos.y;
                            if (pos.z < min_realfluidbounds.z) min_realfluidbounds.z = pos.z;

                            if (pos.x > max_realfluidbounds.x) max_realfluidbounds.x = pos.x;
                            if (pos.y > max_realfluidbounds.y) max_realfluidbounds.y = pos.y;
                            if (pos.z > max_realfluidbounds.z) max_realfluidbounds.z = pos.z;

                        }

                    }
                }
            }

            min_realfluidbounds -= new Vector3(half_fluid_spacing, half_fluid_spacing, half_fluid_spacing);
            max_realfluidbounds += new Vector3(half_fluid_spacing, half_fluid_spacing, half_fluid_spacing);

            fluids_Bounds = new Bounds();
            fluids_Bounds.SetMinMax(min_realfluidbounds, max_realfluidbounds);
            Debug.Log("Actual Fluid bounds = " + fluids_Bounds.ToString());


            NumFluidParticles = Fluids_Positions.Count;
            Debug.Log("Fluid Particles = " + NumFluidParticles);
            //Debug.Log(Fluids_Positions[0]);
            //Debug.Log(Fluids_Positions[1]);

            m_fluid = new FluidModel(Fluids_Positions, fluids_Bounds, half_fluid_spacing, density, Matrix4x4.identity);

            
        }
        // Start is called before the first frame update
        void Start()
        {
            //Init()
            radius = 0.1f;
            density = 1000.0f;
            Vector3 min_boun_innerbounds = new Vector3(-8, 0, -2);
            Vector3 max_boun_innerbounds = new Vector3(8, 10, 2);

            try
            {
                //1 Boundary particles
                CreateBoundary(min_boun_innerbounds, max_boun_innerbounds);
                CreateBoundryPsi();


                //2 Fluid particles
                CreateFluid();

                //?? 流体bounds等于边界bounds. m_fluid.Bounds = m_boundary.Bounds; 流体边界只有初始的那一块，改成boundary的体积
                //fluids_Bounds = new Bounds(boundary_Bounds.center, boundary_Bounds.size);

                //经测试，= 只是赋值不是同地址
                fluids_Bounds = boundary_Bounds;

                //m_solver = new FluidSolver(m_fluid, m_boundary);

                simulation = new TimeStepFluidModel(m_fluid, NumBoundaryParticles);

                //Water part.  Get a texture named Volume
                //m_volume = new RenderVolume(boundary_Bounds, radius);
                //m_volume.CreateMesh(volume_mat);
            }
            catch
            {
                wasError = true;
                throw;
            }




        }

        // Update is called once per frame
        void Update()
        {
            if (wasError) return;
            if (m_startRunning)
            {
                //simulation.StepPhysics(timeStep);
                //m_volume.FillVolume(m_fluid, m_solver.Hash, m_solver.Kernel);
            }

            if (m_drawFluidParticles)
            {
                m_fluid.Draw(Camera.main, m_particleMesh, m_fluidParticleMat, 0);

            }
            if (m_drawBoundaryParticles)//must move to boundary
            {
                uint[] args = new uint[5] { 0, 0, 0, 0, 0 };
                args[0] = m_particleMesh.GetIndexCount(0);
                args[1] = (uint)NumBoundaryParticles;

                boundary_argsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
                boundary_argsBuffer.SetData(args);
                //connect to surface shader of this mat
                m_boundaryParticleMat.SetBuffer("positions", Boundary_pos_cbuffer);
                m_boundaryParticleMat.SetColor("color", Color.blue);
                m_boundaryParticleMat.SetFloat("diameter", radius * 1.2f);

                ShadowCastingMode castShadow = ShadowCastingMode.Off;
                bool recieveShadow = false;

                Graphics.DrawMeshInstancedIndirect(m_particleMesh, 0, m_boundaryParticleMat, boundary_Bounds, boundary_argsBuffer, 0, null, castShadow, recieveShadow, 0, Camera.main);

            }


        }

        private void OnDestroy()
        {
            //m_boundary.Dispose();
            if (Boundary_pos_cbuffer != null)
            {
                Boundary_pos_cbuffer.Release();
                Boundary_pos_cbuffer = null;
            }
            CBUtility.Release(ref boundary_argsBuffer);

            m_fluid.Dispose();
            simulation.Dispose();
            //m_volume.Dispose();
        }

    }
}