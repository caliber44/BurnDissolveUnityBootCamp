using UnityEngine;
public class BurnDissolveImproved : MonoBehaviour
{
    [SerializeField] private MeshRenderer m_burnDissolveMeshRenderer;
    [SerializeField] private Camera m_camera;
    [SerializeField] private LayerMask m_clickLayer;
    [SerializeField] private Vector2Int m_bufferSize;
    [SerializeField] private Vector2 m_planeSize;
    [SerializeField] private float m_burnSize;
    [SerializeField] private float m_burnEdge;
    [SerializeField] private Vector2Int m_spreadMinMax;
    [SerializeField] private ParticleSystem m_burnParticles;
    [SerializeField] private Material m_burnMaterial;

    private Ray m_ray;
    private RaycastHit m_rayHitInfo;
    private float[,] m_buffer;
    private float[,] m_tempBuffer;
    private Vector2 m_uvPoint;
    private Texture2D m_mask;
    private Texture2D m_alpha;
    private Color32[] m_maskValues;
    private Color32[] m_alphaValues;

    private void Start()
    {
        Debug.Log("Drag on paper to burn");
        Debug.Log("Press Space to reset");

        Init();
    }
    private void OnMouseDrag()
    {
        DoBurn();
    }
    private void Init()
    {
        Application.targetFrameRate = 60;

        m_ray = new Ray(Vector3.zero, Vector3.zero);
        m_uvPoint = Vector3.zero;

        InitBuffer();
        InitTexture();
        SetParticleSystem();
    }
    private void InitBuffer() 
    {
        m_buffer = new float[m_bufferSize.x, m_bufferSize.y];
        m_tempBuffer = new float[m_bufferSize.x, m_bufferSize.y];
        
        ClearBuffer(m_buffer);
        ClearBuffer(m_tempBuffer);
    }
    private void InitTexture() 
    {
        m_burnDissolveMeshRenderer.material = m_burnMaterial;

        m_mask = new Texture2D(m_bufferSize.x, m_bufferSize.y, TextureFormat.Alpha8, false);
        m_maskValues = new Color32[m_bufferSize.x * m_bufferSize.y];

        m_burnDissolveMeshRenderer.material.SetTexture("_Mask", m_mask);

        m_alpha = new Texture2D(m_bufferSize.x, m_bufferSize.y, TextureFormat.Alpha8, false);
        m_alphaValues = new Color32[m_bufferSize.x * m_bufferSize.y];

        m_burnDissolveMeshRenderer.material.SetTexture("_Alpha", m_alpha);
    }
    private void ResetBurnDissolve()
    {
        ClearBuffer(m_buffer);
        ClearBuffer(m_tempBuffer);
        ResetMaskValues();
    }
    private void Update()
    {
        UpdateBufferValues();
        UpdateMaskValues();
        CheckInput();
    }
    private void SetParticleSystem() 
    {
        ParticleSystem.ShapeModule shape = m_burnParticles.shape;
        shape.texture = m_mask;

        ParticleSystem.MainModule main = m_burnParticles.main;
        main.loop = true;
        
        m_burnParticles.Play();
    }
    private void CheckInput() 
    {
        if(Input.GetKeyDown(KeyCode.Space)) ResetBurnDissolve();
    }
    private void ResetMaskValues() 
    {
        for (int i = 0; i < m_maskValues.Length; i++)
        {
            m_maskValues[i].a = 0;
            m_alphaValues[i].a = 0;
        }

        m_mask.SetPixels32(m_maskValues);
        m_mask.Apply();

        m_alpha.SetPixels32(m_alphaValues);
        m_alpha.Apply();
    }
    private void ClearBuffer(float[,] buffer) 
    {
        for (int x = 0; x < m_bufferSize.x; x++)
        {
            for (int y = 0; y < m_bufferSize.y; y++)
            {
                buffer[x, y] = 0;
            }
        }
    }
    private void UpdateMaskValues() 
    {
        int x;
        int y;
        byte value;

        for (int i = 0; i < m_maskValues.Length; i++) 
        {        
            x = i % m_bufferSize.x; 
            y = i / m_bufferSize.x;

            value = (byte)(m_buffer[x, y] * 255);

            m_maskValues[i].a = value;

            if (value > m_alphaValues[i].a)
            {
                m_alphaValues[i].a = value;
            }
        }

        m_mask.SetPixels32(m_maskValues);
        m_mask.Apply();

        m_alpha.SetPixels32(m_alphaValues);
        m_alpha.Apply();
    }
    private void DoBurn()
    {
        if (RayCheck())
        {
            RayhitWorldPointToUV();
            SetBufferValues(m_uvPoint.x * m_bufferSize.x, m_uvPoint.y * m_bufferSize.y, 1, m_burnSize,m_buffer);
        }
    }
    private void SpreadBurn(int x, int y) 
    {
        int spreadSizeX = Random.Range(m_spreadMinMax.x, m_spreadMinMax.y + 1);
        int spreadSizeY = Random.Range(m_spreadMinMax.x, m_spreadMinMax.y + 1);

        int wX = x - (spreadSizeX / 2);
        int wY = y - (spreadSizeY / 2);

        for (int i = 0; i < spreadSizeX; i++) 
        {
            for (int k = 0; k < spreadSizeY; k++) 
            {
                if (wX + i < 0 || wX + i >= m_bufferSize.x) continue;
                if (wY + k < 0 || wY + k >= m_bufferSize.y) continue;

                if (m_buffer[wX + i, wY + k] == 0 && AlphaCheck(wX + i, wY + k, 1))
                {
                    SetBufferValue(wX + i, wY + k, 1f,m_tempBuffer);
                }
            }
        }        
    }
    private void UpdateBufferValues() 
    {
        for (int x = 0; x < m_bufferSize.x; x++)
        {
            for (int y = 0; y < m_bufferSize.y; y++)
            {            
                if(m_buffer[x, y] == 0) continue;

                if(m_buffer[x,y] == 1) SpreadBurn(x, y);

                SetBufferValue(x, y,m_buffer[x,y] -= m_burnEdge * Time.deltaTime,m_buffer);
            }
        }

        CopyTempSpreadBurnValues();
    }
    private void CopyTempSpreadBurnValues() 
    {
        for (int x = 0; x < m_bufferSize.x; x++)
        {
            for (int y = 0; y < m_bufferSize.y; y++)
            {
                if (m_tempBuffer[x,y] == 1 && m_buffer[x,y] == 0) 
                {
                    SetBufferValue(x, y, 1, m_buffer);
                }
            }
        }

        ClearBuffer(m_tempBuffer);
    }
    private void SetBufferValues(float x, float y, float value, float size, float[,] buffer) 
    {
        x -= size / 4;
        y -= size / 4;

        for(int k = 0; k < size; k++) 
        {                 
            for(int j = 0; j < size; j++) 
            {
                if (AlphaCheck((k - (k / 2)) + x, (j - (j / 2)) + y))
                {
                    SetBufferValue((k - (k / 2)) + x, (j - (j / 2)) + y, value, buffer);
                }
            }
        }
    }
    private void SetBufferValue(float x, float y, float value, float[,] buffer)
    {
        SetBufferValue(Mathf.FloorToInt(x),Mathf.FloorToInt(y),value, buffer);
    }
    private void SetBufferValue(int x, int y, float value, float[,] buffer) 
    {
        if (x < 0 || x >= m_bufferSize.x) return;
        if (y < 0 || y >= m_bufferSize.y) return;

        if (value < 0) { value = 0; } else if (value > 1){ value = 1; }

        buffer[x, y] = value;
    }
    private bool AlphaCheck(float x, float y, float cutoff = 250)
    {
        int j = Mathf.FloorToInt(x);
        int k = Mathf.FloorToInt(y);

        if (j < 0 || j >= m_bufferSize.x) return false;
        if (k < 0 || k >= m_bufferSize.y) return false;

        return m_alphaValues[k * m_bufferSize.x + j].a < cutoff;
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
        mousePos.z = m_camera.transform.position.z;

        m_ray.origin = m_camera.ScreenToWorldPoint(mousePos);
        m_ray.direction = (m_ray.origin - m_camera.transform.position).normalized;

        if( Physics.Raycast(m_ray, out m_rayHitInfo, Mathf.Infinity, m_clickLayer)) 
        {
            return m_rayHitInfo.collider.gameObject == gameObject;
        }

        return false;
    }
}
