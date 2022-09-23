using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FirstRender : MonoBehaviour
{

    //compute shader related
    private ComputeShader cshader;//need to assign it in unity editor
    private RenderTexture TestTexture;
    //private ComputeBuffer resultBuffer;

    private const int THREADS = 8;

    private Renderer m_Renderer;

    // Start is called before the first frame update
    void Start()
    {
        //Target renderer
        m_Renderer = GameObject.Find("m_Cube").GetComponent<Renderer>();

        //Create new texture
        TestTexture = new RenderTexture(256, 256, 1);
        TestTexture.enableRandomWrite = true;
        TestTexture.Create();
        //Load shader file
        cshader = Resources.Load("First_Compute") as ComputeShader;//Test.compute must under the resources folder

        //Connect to the compute shader
        //1 create a integer to store the identifier of our kernel
        int computeKernel = cshader.FindKernel("CSMain");
        cshader.SetTexture(computeKernel, "Result", TestTexture);
        //because Threads*Threadsgroup = texture's length/width
        cshader.Dispatch(computeKernel, TestTexture.width / THREADS, TestTexture.height / THREADS, 1);


        //Assign the texture to material
        m_Renderer.material.SetTexture("_MainTex", TestTexture);
    }

    //Compute shader needs dispose




}
