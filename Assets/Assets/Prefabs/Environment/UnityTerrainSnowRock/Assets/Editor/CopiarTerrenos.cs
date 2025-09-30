using UnityEngine;
using UnityEditor;

public class TerrainSubareaCopier : EditorWindow
{
    [Header("Terreno")]
    public Terrain terrain;

    [Header("Fuente (en metros, relativo al terreno)")]
    public float sourceX = 0f;
    public float sourceZ = 0f;
    public float sourceWidth = 100f;
    public float sourceHeight = 100f;

    [Header("Destino (posición en metros de la esquina inferior-izquierda)")]
    public float destX = 0f;
    public float destZ = 0f;

    [Header("Opciones")]
    public bool copyHeights = true;
    public bool copyTextures = true;
    public int edgeSmoothPixels = 0; // 0 = sin suavizado

    [MenuItem("Tools/Terrain/Copy/Paste Subarea")]
    public static void ShowWindow()
    {
        var w = GetWindow<TerrainSubareaCopier>("Copy/Paste Subarea");
        w.minSize = new Vector2(380, 300);
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Copiar sub-área de un Terrain (mismo Terrain)", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        terrain = (Terrain)EditorGUILayout.ObjectField("Terrain", terrain, typeof(Terrain), true);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Fuente (metros)", EditorStyles.boldLabel);
        sourceX = EditorGUILayout.FloatField("X (m)", sourceX);
        sourceZ = EditorGUILayout.FloatField("Z (m)", sourceZ);
        sourceWidth = EditorGUILayout.FloatField("Ancho (m)", sourceWidth);
        sourceHeight = EditorGUILayout.FloatField("Alto (m)", sourceHeight);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Destino (metros)", EditorStyles.boldLabel);
        destX = EditorGUILayout.FloatField("X (m)", destX);
        destZ = EditorGUILayout.FloatField("Z (m)", destZ);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Opciones", EditorStyles.boldLabel);
        copyHeights = EditorGUILayout.Toggle("Copiar alturas", copyHeights);
        copyTextures = EditorGUILayout.Toggle("Copiar texturas", copyTextures);
        edgeSmoothPixels = EditorGUILayout.IntSlider("Suavizar bordes (px)", edgeSmoothPixels, 0, 16);

        EditorGUILayout.Space();
        using (new EditorGUI.DisabledScope(terrain == null))
        {
            if (GUILayout.Button("Copy Heights"))
            {
                CopyHeightsBlock();
            }
            if (GUILayout.Button("Copy Textures"))
            {
                CopyTexturesBlock();
            }
            if (GUILayout.Button("Copy Heights + Textures"))
            {
                CopyHeightsBlock();
                CopyTexturesBlock();
            }
        }

        EditorGUILayout.HelpBox("Tip: El suavizado de bordes aplica una caída suave en los últimos N píxeles del bloque para minimizar costuras.", MessageType.Info);
    }

    // ------------------ UTILIDADES DE CONVERSIÓN ------------------

    struct MapRects
    {
        public int srcHX, srcHZ, srcHW, srcHH; // heightmap indices
        public int dstHX, dstHZ;
        public int srcAX, srcAZ, srcAW, srcAH; // alphamap indices
        public int dstAX, dstAZ;
    }

    MapRects BuildRects()
    {
        var td = terrain.terrainData;
        Vector3 size = td.size;

        // Clamp fuente en metros al área del terreno
        float fx = Mathf.Clamp(sourceX, 0, size.x);
        float fz = Mathf.Clamp(sourceZ, 0, size.z);
        float fw = Mathf.Clamp(sourceWidth, 0, size.x - fx);
        float fh = Mathf.Clamp(sourceHeight, 0, size.z - fz);

        // Clamp destino
        float dx = Mathf.Clamp(destX, 0, size.x);
        float dz = Mathf.Clamp(destZ, 0, size.z);

        // Convertir METROS -> ÍNDICES heightmap
        // Nota: heightmap tiene resolución N; índices válidos [0..N-1]
        int hRes = td.heightmapResolution;
        float hxPerM = (hRes - 1) / size.x; // proporcional en X
        float hzPerM = (hRes - 1) / size.z; // proporcional en Z

        int srcHX = Mathf.RoundToInt(fx * hxPerM);
        int srcHZ = Mathf.RoundToInt(fz * hzPerM);
        int srcHW = Mathf.Max(1, Mathf.RoundToInt(fw * hxPerM));
        int srcHH = Mathf.Max(1, Mathf.RoundToInt(fh * hzPerM));

        int dstHX = Mathf.RoundToInt(dx * hxPerM);
        int dstHZ = Mathf.RoundToInt(dz * hzPerM);

        // Convertir METROS -> ÍNDICES alphamap (texturas)
        int aResX = td.alphamapWidth;
        int aResZ = td.alphamapHeight;
        float axPerM = (aResX) / size.x;
        float azPerM = (aResZ) / size.z;

        int srcAX = Mathf.RoundToInt(fx * axPerM);
        int srcAZ = Mathf.RoundToInt(fz * azPerM);
        int srcAW = Mathf.Max(1, Mathf.RoundToInt(fw * axPerM));
        int srcAH = Mathf.Max(1, Mathf.RoundToInt(fh * azPerM));

        int dstAX = Mathf.RoundToInt(dx * axPerM);
        int dstAZ = Mathf.RoundToInt(dz * azPerM);

        // Ajustar si se sale por el borde al pegar
        // (para alturas)
        srcHW = Mathf.Min(srcHW, hRes - srcHX);
        srcHH = Mathf.Min(srcHH, hRes - srcHZ);
        int maxDstHW = hRes - dstHX;
        int maxDstHH = hRes - dstHZ;
        int copyHW = Mathf.Min(srcHW, maxDstHW);
        int copyHH = Mathf.Min(srcHH, maxDstHH);
        srcHW = copyHW; srcHH = copyHH;

        // (para texturas)
        srcAW = Mathf.Min(srcAW, aResX - srcAX);
        srcAH = Mathf.Min(srcAH, aResZ - srcAZ);
        int maxDstAW = aResX - dstAX;
        int maxDstAH = aResZ - dstAZ;
        int copyAW = Mathf.Min(srcAW, maxDstAW);
        int copyAH = Mathf.Min(srcAH, maxDstAH);
        srcAW = copyAW; srcAH = copyAH;

        return new MapRects
        {
            srcHX = srcHX, srcHZ = srcHZ, srcHW = srcHW, srcHH = srcHH,
            dstHX = dstHX, dstHZ = dstHZ,
            srcAX = srcAX, srcAZ = srcAZ, srcAW = srcAW, srcAH = srcAH,
            dstAX = dstAX, dstAZ = dstAZ
        };
    }

    // ------------------ COPIA DE ALTURAS ------------------

    void CopyHeightsBlock()
    {
        if (terrain == null) return;
        var td = terrain.terrainData;

        Undo.RegisterCompleteObjectUndo(td, "Copy Heights Subarea");

        var r = BuildRects();
        float[,] block = td.GetHeights(r.srcHX, r.srcHZ, r.srcHW, r.srcHH);

        if (edgeSmoothPixels > 0)
            ApplyEdgeFeather(block, edgeSmoothPixels);

        td.SetHeights(r.dstHX, r.dstHZ, block);

        Debug.Log($"[Terrain] Alturas copiadas {r.srcHW}x{r.srcHH} px heightmap → ({r.dstHX},{r.dstHZ}).");
        EditorUtility.SetDirty(td);
    }

    // Suavizado multiplicativo en los bordes del bloque (feather)
    void ApplyEdgeFeather(float[,] data, int border)
    {
        int w = data.GetLength(1);
        int h = data.GetLength(0);

        border = Mathf.Clamp(border, 1, Mathf.Min(w, h) / 2);

        for (int y = 0; y < h; y++)
        {
            float vy = Mathf.Min(y, h - 1 - y) / (float)border;
            vy = Mathf.Clamp01(vy);
            for (int x = 0; x < w; x++)
            {
                float vx = Mathf.Min(x, w - 1 - x) / (float)border;
                vx = Mathf.Clamp01(vx);
                float f = Mathf.Min(vx, vy);      // caída hacia los bordes
                // mezclamos la altura con la altura promedio del borde (0.5 = mantén, 0 = aplanar hacia los bordes)
                // Aquí usamos un leve atenuado sólo cerca de los bordes:
                float atten = Mathf.Lerp(0.85f, 1.0f, f);
                data[y, x] *= atten;
            }
        }
    }

    // ------------------ COPIA DE TEXTURAS (ALPHAMAPS) ------------------

    void CopyTexturesBlock()
    {
        if (terrain == null) return;
        var td = terrain.terrainData;

        if (td.alphamapLayers == 0)
        {
            Debug.LogWarning("El Terrain no tiene capas de textura (splat layers) configuradas.");
            return;
        }

        Undo.RegisterCompleteObjectUndo(td, "Copy Textures Subarea");

        var r = BuildRects();
        int layers = td.alphamapLayers;

        float[,,] srcAlpha = td.GetAlphamaps(r.srcAX, r.srcAZ, r.srcAW, r.srcAH);

        if (edgeSmoothPixels > 0)
            ApplyEdgeFeatherAlpha(srcAlpha, edgeSmoothPixels);

        td.SetAlphamaps(r.dstAX, r.dstAZ, srcAlpha);

        Debug.Log($"[Terrain] Texturas copiadas {r.srcAW}x{r.srcAH} px alphamap → ({r.dstAX},{r.dstAZ}).");
        EditorUtility.SetDirty(td);
    }

    void ApplyEdgeFeatherAlpha(float[,,] alpha, int border)
    {
        int w = alpha.GetLength(1);
        int h = alpha.GetLength(0);
        int layers = alpha.GetLength(2);

        border = Mathf.Clamp(border, 1, Mathf.Min(w, h) / 2);

        for (int y = 0; y < h; y++)
        {
            float vy = Mathf.Min(y, h - 1 - y) / (float)border;
            vy = Mathf.Clamp01(vy);
            for (int x = 0; x < w; x++)
            {
                float vx = Mathf.Min(x, w - 1 - x) / (float)border;
                vx = Mathf.Clamp01(vx);
                float f = Mathf.Min(vx, vy);
                float atten = Mathf.Lerp(0.85f, 1.0f, f);

                // atenuamos cada capa y renormalizamos
                float sum = 0f;
                for (int l = 0; l < layers; l++)
                {
                    alpha[y, x, l] *= atten;
                    sum += alpha[y, x, l];
                }
                if (sum > 0f)
                {
                    for (int l = 0; l < layers; l++)
                        alpha[y, x, l] /= sum; // renormalizar a 1
                }
            }
        }
    }
}

