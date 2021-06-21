using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;

[ExecuteInEditMode]
public class PathTracingDemo : MonoBehaviour
{
    public RayTracingShader rayTracingShader = null;

    public RayTracingShader rayTracingShaderGBuffer = null;

    public Cubemap envTexture = null;

    [Range(1, 100)]
    public uint bounceCountOpaque = 5;

    [Range(1, 100)]
    public uint bounceCountTransparent = 8;
    
    private uint cameraWidth = 0;
    private uint cameraHeight = 0;
  
    private RenderTexture rayTracingOutput = null;
    private RenderTexture gBufferWorldNormals = null;
    private RenderTexture gBufferIntersectionT = null;

    private RayTracingAccelerationStructure rayTracingAccelerationStructure = null;

    private void CreateRayTracingAccelerationStructure()
    {
        if (rayTracingAccelerationStructure == null)
        {
            RayTracingAccelerationStructure.RASSettings settings = new RayTracingAccelerationStructure.RASSettings();
            settings.rayTracingModeMask = RayTracingAccelerationStructure.RayTracingModeMask.Everything;
            settings.managementMode = RayTracingAccelerationStructure.ManagementMode.Automatic;
            settings.layerMask = 255;

            rayTracingAccelerationStructure = new RayTracingAccelerationStructure(settings);
        }
    }

    private void ReleaseResources()
    {
        if (rayTracingAccelerationStructure != null)
        {
            rayTracingAccelerationStructure.Release();
            rayTracingAccelerationStructure = null;
        }

        if (rayTracingOutput != null)
        {
            rayTracingOutput.Release();
            rayTracingOutput = null;
        }

        if (gBufferWorldNormals != null)
        {
            gBufferWorldNormals.Release();
            gBufferWorldNormals = null;
        }

        if (gBufferIntersectionT != null)
        {
            gBufferIntersectionT.Release();
            gBufferIntersectionT = null;
        }

        cameraWidth = 0;
        cameraHeight = 0;
    }

    private void CreateResources()
    {
        CreateRayTracingAccelerationStructure();

        if (cameraWidth != Camera.main.pixelWidth || cameraHeight != Camera.main.pixelHeight)
        {
            {
                if (rayTracingOutput)
                    rayTracingOutput.Release();

                RenderTextureDescriptor rtDesc = new RenderTextureDescriptor()
                {
                    dimension = TextureDimension.Tex2D,
                    width = Camera.main.pixelWidth,
                    height = Camera.main.pixelHeight,
                    depthBufferBits = 0,
                    volumeDepth = 1,
                    msaaSamples = 1,
                    vrUsage = VRTextureUsage.OneEye,
                    graphicsFormat = GraphicsFormat.R32G32B32A32_SFloat,
                    enableRandomWrite = true,
                };

                rayTracingOutput = new RenderTexture(rtDesc);
                rayTracingOutput.Create();
            }

            {
                if (gBufferWorldNormals)
                    gBufferWorldNormals.Release();

                RenderTextureDescriptor rtDesc = new RenderTextureDescriptor()
                {
                    dimension = TextureDimension.Tex2D,
                    width = Camera.main.pixelWidth,
                    height = Camera.main.pixelHeight,
                    depthBufferBits = 0,
                    volumeDepth = 1,
                    msaaSamples = 1,
                    vrUsage = VRTextureUsage.OneEye,
                    graphicsFormat = GraphicsFormat.R32G32B32A32_SFloat,
                    enableRandomWrite = true,
                };

                gBufferWorldNormals = new RenderTexture(rtDesc);
                gBufferWorldNormals.Create();
            }

            {
                if (gBufferIntersectionT)
                    gBufferIntersectionT.Release();

                RenderTextureDescriptor rtDesc = new RenderTextureDescriptor()
                {
                    dimension = TextureDimension.Tex2D,
                    width = Camera.main.pixelWidth,
                    height = Camera.main.pixelHeight,
                    depthBufferBits = 0,
                    volumeDepth = 1,
                    msaaSamples = 1,
                    vrUsage = VRTextureUsage.OneEye,
                    graphicsFormat = GraphicsFormat.R32_SFloat,
                    enableRandomWrite = true,
                };

                gBufferIntersectionT = new RenderTexture(rtDesc);
                gBufferIntersectionT.Create();
            }

            cameraWidth = (uint)Camera.main.pixelWidth;
            cameraHeight = (uint)Camera.main.pixelHeight;
        }
    }

    void OnDestroy()
    {
        ReleaseResources();
    }

    void OnDisable()
    {
        ReleaseResources();
    }

    private void OnEnable()
    {
    }

    private void Update()
    {
        CreateResources();
    }

    [ImageEffectOpaque]
    void OnRenderImage(RenderTexture src, RenderTexture dest)
    {
        if (!SystemInfo.supportsRayTracing)
        {
            Debug.LogError("The RayTracing API is not supported by this GPU or by the current graphics API.");
            Graphics.Blit(src, dest);
            return;
        }

        if (!rayTracingShader || !rayTracingShaderGBuffer)
        {
            Debug.LogError("A raytrace shader was not set in the script!");
            Graphics.Blit(src, dest);
            return;
        }

        if (rayTracingAccelerationStructure == null)
            return;
        
        // Not really needed per frame if the scene is static.
        rayTracingAccelerationStructure.Build();

        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // Generate GBuffer for denoising input.
        rayTracingShaderGBuffer.SetShaderPass("PathTracingGBuffer");

        // Input
        rayTracingShaderGBuffer.SetAccelerationStructure(Shader.PropertyToID("g_AccelStruct"), rayTracingAccelerationStructure);
        rayTracingShaderGBuffer.SetFloat(Shader.PropertyToID("g_Zoom"), Mathf.Tan(Mathf.Deg2Rad * Camera.main.fieldOfView * 0.5f));
        rayTracingShaderGBuffer.SetFloat(Shader.PropertyToID("g_AspectRatio"), cameraWidth / (float)cameraHeight);

        // Output
        rayTracingShaderGBuffer.SetTexture(Shader.PropertyToID("g_WorldNormals"), gBufferWorldNormals);
        rayTracingShaderGBuffer.SetTexture(Shader.PropertyToID("g_IntersectionT"), gBufferIntersectionT);

        rayTracingShaderGBuffer.Dispatch("MainRayGenShader", (int)cameraWidth, (int)cameraHeight, 1, Camera.main);
    

        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // Path tracing
        rayTracingShader.SetShaderPass("PathTracing");

        Shader.SetGlobalInt(Shader.PropertyToID("g_BounceCountOpaque"), (int)bounceCountOpaque);
        Shader.SetGlobalInt(Shader.PropertyToID("g_BounceCountTransparent"), (int)bounceCountTransparent);

        // Input
        rayTracingShader.SetAccelerationStructure(Shader.PropertyToID("g_AccelStruct"), rayTracingAccelerationStructure);
        rayTracingShader.SetFloat(Shader.PropertyToID("g_Zoom"), Mathf.Tan(Mathf.Deg2Rad * Camera.main.fieldOfView * 0.5f));
        rayTracingShader.SetFloat(Shader.PropertyToID("g_AspectRatio"), cameraWidth / (float)cameraHeight);
        rayTracingShader.SetInt(Shader.PropertyToID("g_FrameIndex"), Time.frameCount);
        rayTracingShader.SetTexture(Shader.PropertyToID("g_EnvTex"), envTexture);

        // Output
        rayTracingShader.SetTexture(Shader.PropertyToID("g_Radiance"), rayTracingOutput);       

        rayTracingShader.Dispatch("MainRayGenShader", (int)cameraWidth, (int)cameraHeight, 1, Camera.main);
       
        Graphics.Blit(rayTracingOutput, dest);
    }
}
