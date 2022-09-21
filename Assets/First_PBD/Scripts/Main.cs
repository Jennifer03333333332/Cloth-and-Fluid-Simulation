using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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
        [Header("Boundary")]
        [SerializeField]
        public Vector3 min_innerbounds;
        public Vector3 max_innerbounds;
        public Vector3 min_outerbounds;
        public Vector3 max_outerbounds;
        //in FluidBoundary.cs, to test if it's changed. Or it changed in real time
        public Vector3 min_boundarybounds;
        public Vector3 max_boundarybounds;
        [SerializeField]
        public List<Bounds> Exclusion_list;// { get; private set; }

        public int NumBoundaryParticles;


        [Header("Fluid")]
        [SerializeField]
        public Vector3 min_fluidbounds;
        public Vector3 max_fluidbounds;
        public List<Bounds> Fluid_Exclusion_list;
        public Vector3 min_realfluidbounds;
        public Vector3 max_realfluidbounds;
        private Bounds fluids_Bounds;

        public float fluid_spacing;
        public int NumFluidParticles;




        private bool wasError;

        //init Boundary
        private Bounds innerBounds;
        private Bounds outerBounds;

        //List of particles' position
        private List<Vector4> Boundary_Positions;
        private List<Vector4> Fluids_Positions;
        //real boundary particles' AABB
        private Bounds boundary_Bounds;

        private FluidBody m_fluid;

        public ComputeBuffer Boundary_pos_cbuffer { get; private set; }



        private void CreateBoundary()
        {
            //内部区域 AABB
            innerBounds = new Bounds();
            innerBounds.SetMinMax(min_innerbounds, max_innerbounds);

            //在内部区域外面增加一圈边界粒子，厚度为内部粒子的1.2
            float thickness = 1.2f; //1f
            float diameter = radius * 2;
            min_outerbounds = min_innerbounds - new Vector3(diameter * thickness, diameter * thickness, diameter * thickness);
            max_outerbounds = max_innerbounds + new Vector3(diameter * thickness, diameter * thickness, diameter * thickness);

            outerBounds = new Bounds();
            outerBounds.SetMinMax(min_outerbounds, max_outerbounds);

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
            Debug.Log("Boundary Particles = " + Boundary_Positions.Count);
            min_boundarybounds -= new Vector3(radius, radius, radius);
            max_boundarybounds += new Vector3(radius, radius, radius);
            boundary_Bounds = new Bounds();
            boundary_Bounds.SetMinMax(min_boundarybounds, max_boundarybounds);

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
            min_fluidbounds = new Vector3(-8 + radius, 0 + radius, -1 + radius);
            max_fluidbounds = new Vector3(-4 - radius, 8 - radius, 2 - radius);//(-4, 8, 2); just give the initial fluid's volume

            Bounds fluidbounds = new Bounds();
            fluidbounds.SetMinMax(min_fluidbounds, max_fluidbounds);

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
                        //如果pos不在内边界内, Boundary_Positions list增加
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
            


            NumFluidParticles = Fluids_Positions.Count;
            Debug.Log("Fluid Particles = " + NumFluidParticles);


            m_fluid = new FluidBody(Fluids_Positions, fluids_Bounds, half_fluid_spacing, density, Matrix4x4.identity);

            
        }
        // Start is called before the first frame update
        void Start()
        {
            //Init()
            radius = 0.1f;
            density = 1000.0f;
            min_innerbounds = new Vector3(-8, 0, -2);
            max_innerbounds = new Vector3(8, 10, 2);

            try
            {
                //1 Boundary particles
                CreateBoundary();
                CreateBoundryPsi();


                //2 Fluid particles
                CreateFluid();

                //?? 流体bounds等于边界bounds. m_fluid.Bounds = m_boundary.Bounds;
                fluids_Bounds = new Bounds(boundary_Bounds.center, boundary_Bounds.size);
                
                //m_solver = new FluidSolver(m_fluid, m_boundary);
                

                //m_volume = new RenderVolume(m_boundary.Bounds, radius);
                //m_volume.CreateMesh(m_volumeMat);
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
        }
    }
}