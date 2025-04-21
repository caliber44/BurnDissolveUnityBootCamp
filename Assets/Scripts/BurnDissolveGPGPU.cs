using UnityEngine;
using UnityEngine.UI;

public class BurnDissolveGPU : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ComputeShader m_computeShader;
    [SerializeField] private MeshRenderer m_burnDissolveMeshRenderer;
    [SerializeField] private Material m_burnMaterial;
    [SerializeField] private Camera m_mainCamera;
    [SerializeField] private LayerMask m_clickLayer;
    [SerializeField] private ParticleSystem m_burnParticles;

    [Header("Buffer Settings")]
    [SerializeField] private int m_bufferSize = 1024; // Size of the texture (assuming square)

    [Header("Burn Settings")]
    [SerializeField] private Vector2 m_planeSize = new Vector2(5f, 5f); // Physical size of the object
    [SerializeField] private float m_burnRadius = 20f; // Size of burn brush
    [SerializeField] private float m_burnSpeed = 0.5f; // Decay rate of burn effect
    [SerializeField] private Vector2Int m_spreadMinMax = new Vector2Int(3, 7); // Min/max spread range

    [Header("Debug")]
    [SerializeField] private RawImage m_debugMaskImage; // UI element to show mask texture
    [SerializeField] private RawImage m_debugAlphaImage; // UI element to show alpha texture
    [SerializeField] private bool m_showDebug = true;

    // Compute shader resources
    private RenderTexture m_maskTexture;    // Current burn state
    private RenderTexture m_tempTexture;    // Temporary buffer for spreading
    private RenderTexture m_alphaTexture;   // Accumulated burn effect

    // Compute shader kernels
    private int m_burnDissolveKernel;
    private int m_copyTempBufferKernel;
    private int m_initBurnKernel;

    // Thread group counts (calculated based on texture size)
    private int m_threadGroupsX;
    private int m_threadGroupsY;

    // Raycasting for interactive burn
    private Ray m_ray;
    private RaycastHit m_rayHitInfo;
    private Vector2 m_uvPoint;

    private void Start()
    {
        Debug.Log("Drag on paper to burn");
        Debug.Log("Press Space to reset");

        Initialize();
    }
    private void Initialize()
    {
        Application.targetFrameRate = 60;

        // Initialize m_ray and UV point
        m_ray = new Ray(Vector3.zero, Vector3.zero);
        m_uvPoint = Vector2.zero;

        m_burnDissolveMeshRenderer.material = m_burnMaterial;

        // Setup compute resources
        InitializeComputeResources();

        // Configure particle system
        SetupParticleSystem();

        // Calculate thread group counts
        m_threadGroupsX = Mathf.CeilToInt(m_bufferSize / 8.0f);
        m_threadGroupsY = Mathf.CeilToInt(m_bufferSize / 8.0f);
    }
    private void InitializeComputeResources()
    {
        // Create render textures
        m_maskTexture = CreateRenderTexture(m_bufferSize, m_bufferSize);
        m_tempTexture = CreateRenderTexture(m_bufferSize, m_bufferSize);
        m_alphaTexture = CreateRenderTexture(m_bufferSize, m_bufferSize);

        // Clear textures
        ClearRenderTexture(m_maskTexture);
        ClearRenderTexture(m_tempTexture);
        ClearRenderTexture(m_alphaTexture);

        // Get compute shader kernels
        m_burnDissolveKernel = m_computeShader.FindKernel("BurnDissolve");
        m_copyTempBufferKernel = m_computeShader.FindKernel("BurnDissolveCopyTempBuffer");
        m_initBurnKernel = m_computeShader.FindKernel("InitBurn");

        // Bind textures to compute shader
        BindTexturesToComputeShader();

        // Set static parameters
        m_computeShader.SetInt("_BufferSize", m_bufferSize);
        m_computeShader.SetInts("_SpreadMinMax", new int[] { m_spreadMinMax.x, m_spreadMinMax.y });

        // Set material textures
        if (m_burnMaterial != null)
        {
            m_burnMaterial.SetTexture("_Mask", m_maskTexture);
            m_burnMaterial.SetTexture("_Alpha", m_alphaTexture);
        }
    }
    private void BindTexturesToComputeShader()
    {
        // Main kernel - burn and spread
        m_computeShader.SetTexture(m_burnDissolveKernel, "_Mask", m_maskTexture);
        m_computeShader.SetTexture(m_burnDissolveKernel, "_TempMask", m_tempTexture);
        m_computeShader.SetTexture(m_burnDissolveKernel, "_Alpha", m_alphaTexture);

        // Copy kernel - transfer spread values
        m_computeShader.SetTexture(m_copyTempBufferKernel, "_Mask", m_maskTexture);
        m_computeShader.SetTexture(m_copyTempBufferKernel, "_TempMask", m_tempTexture);

        // Init burn kernel
        m_computeShader.SetTexture(m_initBurnKernel, "_Mask", m_maskTexture);
        m_computeShader.SetTexture(m_initBurnKernel, "_Alpha", m_alphaTexture);
    }
    private RenderTexture CreateRenderTexture(int width, int height)
    {
        RenderTexture rt = new RenderTexture(width, height, 0, RenderTextureFormat.R16)
        {
            enableRandomWrite = true,  // Required for compute shader writes
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        rt.Create();
        return rt;
    }
    private void ClearRenderTexture(RenderTexture rt)
    {
        // Save current render target
        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = rt;

        // Clear to transparent black
        GL.Clear(true, true, Color.clear);

        // Restore previous render target
        RenderTexture.active = prev;
    }
    private void SetupParticleSystem()
    {
        //if (m_burnParticles != null)
        //{
        //    ParticleSystem.ShapeModule shape = m_burnParticles.shape;
        //    shape.texture = m_maskTexture;

        //    ParticleSystem.MainModule main = m_burnParticles.main;
        //    main.loop = true;

        //    m_burnParticles.Play();
        //}
    }
    private void Update()
    {
        // Run the burn simulation
        ProcessBurning();

        // Update debug views
        UpdateDebugViews();

        // Check for reset input
        if (Input.GetKeyDown(KeyCode.Space))
            ResetBurnDissolve();
    }
    private void UpdateDebugViews()
    {
        if (!m_showDebug)
            return;

        if (m_debugMaskImage != null)
            m_debugMaskImage.texture = m_maskTexture;

        if (m_debugAlphaImage != null)
            m_debugAlphaImage.texture = m_alphaTexture;
    }
    private void ProcessBurning()
    {
        // Update dynamic parameters
        m_computeShader.SetFloat("_DeltaTime", Time.deltaTime);
        m_computeShader.SetFloat("_BurnSpeed", m_burnSpeed);

        // Dispatch compute shader - first burn and spread
        m_computeShader.Dispatch(m_burnDissolveKernel, m_threadGroupsX, m_threadGroupsY, 1);

        // Dispatch compute shader - then copy temp buffer
        m_computeShader.Dispatch(m_copyTempBufferKernel, m_threadGroupsX, m_threadGroupsY, 1);
    }
    private void ResetBurnDissolve()
    {
        ClearRenderTexture(m_maskTexture);
        ClearRenderTexture(m_tempTexture);
        ClearRenderTexture(m_alphaTexture);
    }
    private void OnMouseDrag()
    {
        DoBurn();
    }
    private void DoBurn()
    {
        if (RayCheck())
        {
            // Convert world position to UV coordinates
            RayhitWorldPointToUV();

            // Draw burn at UV position
            DrawBurnAtPosition(m_uvPoint);
        }
    }
    private void DrawBurnAtPosition(Vector2 position)
    {
        // Set parameters for the burn position and size
        m_computeShader.SetFloats("_BurnPosition", new float[] { position.x, position.y });
        m_computeShader.SetFloat("_BurnRadius", m_burnRadius);

        // Dispatch the compute shader to draw the burn
        m_computeShader.Dispatch(m_initBurnKernel, m_threadGroupsX, m_threadGroupsY, 1);
    }
    private void RayhitWorldPointToUV()
    {
        Vector3 normalisedPoint = transform.InverseTransformPoint(m_rayHitInfo.point);
        normalisedPoint.x += m_planeSize.x;
        normalisedPoint.z += m_planeSize.y;
        normalisedPoint.x /= (m_planeSize.x * 2);
        normalisedPoint.z /= (m_planeSize.y * 2);

        m_uvPoint.x = 1f - normalisedPoint.x;
        m_uvPoint.y = 1f - normalisedPoint.z;
    }
    private bool RayCheck()
    {
        Vector3 mousePos = Input.mousePosition;

        mousePos.z = 10f;

        m_ray.origin = m_mainCamera.ScreenToWorldPoint(mousePos);
        m_ray.direction = (m_ray.origin - m_mainCamera.transform.position).normalized;

        if (Physics.Raycast(m_ray, out m_rayHitInfo, Mathf.Infinity, m_clickLayer))
        {
            return m_rayHitInfo.collider.gameObject == gameObject;
        }

        return false;
    }
    private void OnDisable()
    {
        // Clean up render textures
        if (m_maskTexture != null)
        {
            m_maskTexture.Release();
            m_maskTexture = null;
        }

        if (m_tempTexture != null)
        {
            m_tempTexture.Release();
            m_tempTexture = null;
        }

        if (m_alphaTexture != null)
        {
            m_alphaTexture.Release();
            m_alphaTexture = null;
        }
    }
}