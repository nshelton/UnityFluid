using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VolumeRenderer : MonoBehaviour
{
    public ComputeShader RayTracingShader;
    public SimulationVolume m_simulation;

    private RenderTexture m_frameRender;

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        Render(destination);
    }

    private Camera _camera;
    private void Awake()
    {
        _camera = GetComponent<Camera>();
    }

    private void SetShaderParameters()
    {
        RayTracingShader.SetMatrix("_CameraToWorld", _camera.cameraToWorldMatrix);
        RayTracingShader.SetMatrix("_CameraInverseProjection", _camera.projectionMatrix.inverse);
        RayTracingShader.SetTexture(0, "_DensityTexture", m_simulation.Volume);
        RayTracingShader.SetTexture(0, "_DivergenceTexture", m_simulation.Divergence);
        RayTracingShader.SetTexture(0, "_PressureTexture", m_simulation.Pressure);

        RayTracingShader.SetFloat("_density", m_simulation.density);
        RayTracingShader.SetFloat("_shadowAmount", m_simulation.shadowAmount);

    }
 
    private void Render(RenderTexture destination)
    {
        // Make sure we have a current render target
        InitRenderTexture();
        SetShaderParameters();

        // Set the target and dispatch the compute shader
        RayTracingShader.SetTexture(0, "Result", m_frameRender);
        int threadGroupsX = Mathf.CeilToInt(Screen.width / 8.0f);
        int threadGroupsY = Mathf.CeilToInt(Screen.height / 8.0f);
        RayTracingShader.Dispatch(0, threadGroupsX, threadGroupsY, 1);

        // Blit the result texture to the screen
        Graphics.Blit(m_frameRender, destination);
    }

    private void InitRenderTexture()
    {
        if (m_frameRender == null || m_frameRender.width != Screen.width || m_frameRender.height != Screen.height)
        {
            // Release render texture if we already have one
            if (m_frameRender != null)
                m_frameRender.Release();

            // Get a render target for Ray Tracing
            m_frameRender = new RenderTexture(Screen.width, Screen.height, 0,
                RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            m_frameRender.enableRandomWrite = true;
            m_frameRender.Create();
        }
    }
}