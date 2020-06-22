using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SimulationVolume : MonoBehaviour
{



    public ComputeShader m_shader;
    [Range(0, 1)] public float m_viscosity = 0.1f;
    [Range(0,10)] public float m_timeScale = 0.1f;
    [Range(0, 1)] public float m_decayVelocity = 0.1f;
    [Range(0, 1)] public float m_decayDensity = 0.1f;
    public int m_resolution = 128;

    [Range(0, 30)] public int m_diffuseIterations = 10;
    [Range(0, 50)] public int m_jacobiIterations = 10;
    [Range(0, 50)] public float m_velocity = 10;

    [Header("rendering")]
    public float density;
    public float shadowAmount;

    private RenderTexture m_volumeTexturePrevious;

    private RenderTexture m_volumeVelocityA;
    private RenderTexture m_volumeVelocityB;
    
    private RenderTexture m_volumePressureA;
    private RenderTexture m_volumePressureB;
    private RenderTexture m_volumeDivergence;

    public RenderTexture Volume { get { return m_volumeVelocityA; } }
    public RenderTexture Divergence { get { return m_volumeDivergence; } }
    public RenderTexture Pressure { get { return m_volumePressureA; } }

    private Dictionary<string, int>  m_kernelMap = new Dictionary<string, int>();
    private string[] m_kernelNames = new string[] { 
        "Init", 
        "Advect", 
        "Inject", 
        "Diffuse",

        "Divergence",
        "ProjectField",
        "Pressure",
        "Clear",
        "BoundaryCondition"

    };
    private int m_blockDim;


    RenderTexture CreateVolumeTexture(RenderTextureFormat format)
    {
        RenderTexture tex = new RenderTexture(
            m_resolution, m_resolution, m_resolution, format, RenderTextureReadWrite.Default);
        tex.dimension = UnityEngine.Rendering.TextureDimension.Tex3D;
        tex.volumeDepth = m_resolution;
        tex.enableRandomWrite = true;
        tex.depth = 0;
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        tex.Create();
        return tex;
    }
    void Awake()
    {
        // Texture holds velocity, amount
        m_volumeVelocityA = CreateVolumeTexture(RenderTextureFormat.ARGBFloat);
        m_volumeVelocityB = CreateVolumeTexture(RenderTextureFormat.ARGBFloat);

        m_volumePressureA = CreateVolumeTexture(RenderTextureFormat.RFloat);
        m_volumePressureB = CreateVolumeTexture(RenderTextureFormat.RFloat);
        m_volumeDivergence = CreateVolumeTexture(RenderTextureFormat.RFloat);

        foreach (string name in m_kernelNames)
            m_kernelMap[name] = m_shader.FindKernel(name);
    }

    public void SwapBuffers()
    {
        RenderTexture tmp = m_volumeVelocityB;
        m_volumeVelocityB = m_volumeVelocityA;
        m_volumeVelocityA = tmp;
    }

    public void SetUniforms()
    {
        m_shader.SetFloat("_dt", Time.deltaTime * m_timeScale);
        m_shader.SetInt("_resolution", m_resolution);
        m_shader.SetFloat("_viscosity", m_viscosity);
        m_shader.SetVector("_decay", new Vector3(m_decayVelocity, m_decayDensity, 0f));
        m_shader.SetFloat("_time", Time.time);
        m_shader.SetFloat("_velocity", m_velocity);
    }

    private void Start()
    {
        m_blockDim = Mathf.CeilToInt(m_resolution / 4);

        SetUniforms();

        m_shader.SetTexture(m_kernelMap["Init"], "_Destination", m_volumeVelocityA);
        m_shader.Dispatch(m_kernelMap["Init"], m_blockDim, m_blockDim, m_blockDim);
    }


    void Update()
    {
        SetUniforms();

        m_shader.SetTexture(m_kernelMap["Advect"], "_Source", m_volumeVelocityA);
        m_shader.SetTexture(m_kernelMap["Advect"], "_Destination", m_volumeVelocityB);
        m_shader.Dispatch(m_kernelMap["Advect"], m_blockDim, m_blockDim, m_blockDim);
        SwapBuffers();

        if ( Input.GetKey(KeyCode.Space))
        {
            m_shader.SetTexture(m_kernelMap["Inject"], "_Source", m_volumeVelocityA);
            m_shader.SetTexture(m_kernelMap["Inject"], "_Destination", m_volumeVelocityB);
            m_shader.Dispatch(m_kernelMap["Inject"], m_blockDim, m_blockDim, m_blockDim);
            SwapBuffers();
        }

        //diffuse
        for (int i = 0; i < m_diffuseIterations; i++)
        {
            m_shader.SetTexture(m_kernelMap["Diffuse"], "_Source", m_volumeVelocityA);
            m_shader.SetTexture(m_kernelMap["Diffuse"], "_Destination", m_volumeVelocityB);
            m_shader.Dispatch(m_kernelMap["Diffuse"], m_blockDim, m_blockDim, m_blockDim);

            m_shader.SetTexture(m_kernelMap["Diffuse"], "_Source", m_volumeVelocityB);
            m_shader.SetTexture(m_kernelMap["Diffuse"], "_Destination", m_volumeVelocityA);
            m_shader.Dispatch(m_kernelMap["Diffuse"], m_blockDim, m_blockDim, m_blockDim);

        }



        //calculate divergence
        m_shader.SetTexture(m_kernelMap["Divergence"], "_Source", m_volumeVelocityA);
        m_shader.SetTexture(m_kernelMap["Divergence"], "_DestinationDivergence", m_volumeDivergence);
        m_shader.Dispatch(m_kernelMap["Divergence"], m_blockDim, m_blockDim, m_blockDim);



        //solve pressure
        m_shader.SetTexture(m_kernelMap["Pressure"], "_SourceDivergence", m_volumeDivergence);
        
        m_shader.SetTexture(m_kernelMap["Clear"], "_DestinationPressure", m_volumePressureA);
        m_shader.Dispatch(m_kernelMap["Clear"], m_blockDim, m_blockDim, m_blockDim);


        for (int i = 0; i < m_jacobiIterations; i++)
        {

            m_shader.SetTexture(m_kernelMap["Pressure"], "_SourcePressure", m_volumePressureA);
            m_shader.SetTexture(m_kernelMap["Pressure"], "_DestinationPressure", m_volumePressureB);
            m_shader.Dispatch(m_kernelMap["Pressure"], m_blockDim, m_blockDim, m_blockDim);

            m_shader.SetTexture(m_kernelMap["Pressure"], "_SourcePressure", m_volumePressureB);
            m_shader.SetTexture(m_kernelMap["Pressure"], "_DestinationPressure", m_volumePressureA);
            m_shader.Dispatch(m_kernelMap["Pressure"], m_blockDim, m_blockDim, m_blockDim);

        }

        //project 
        m_shader.SetTexture(m_kernelMap["ProjectField"], "_SourcePressure", m_volumePressureA);
        m_shader.SetTexture(m_kernelMap["ProjectField"], "_Source", m_volumeVelocityA);
        m_shader.SetTexture(m_kernelMap["ProjectField"], "_Destination", m_volumeVelocityB);
        m_shader.Dispatch(m_kernelMap["ProjectField"], m_blockDim, m_blockDim, m_blockDim);

        m_shader.SetTexture(m_kernelMap["BoundaryCondition"], "_Destination", m_volumeVelocityA);
        m_shader.Dispatch(m_kernelMap["BoundaryCondition"], m_blockDim, m_blockDim, m_blockDim);

        SwapBuffers();

    }

}
