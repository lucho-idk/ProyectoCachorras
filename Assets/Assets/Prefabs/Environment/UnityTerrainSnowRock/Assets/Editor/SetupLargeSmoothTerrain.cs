// SetupSnowTerrain1000.cs
// Unity 2021+ (C#)
// Crea/actualiza un Terreno 1000x1000 "nevado" con irregularidades suaves, colisiones y textura aplicada.
// Menú: Tools/Terrain/Build Snow Terrain 1000x1000
//
// Requisitos cumplidos:
// - Editor script con [MenuItem] que crea/actualiza escena y terreno automáticamente.
// - Terrain size = (1000, 100, 1000), heightmapResolution coherente (513).
// - Alturas suaves via fBM (Perlin + octavas) + suavizado final (blur).
// - Collider activo (Terrain incluye TerrainCollider).
// - Textura: intenta cargar Resources/Textures/snow.png; si no, usa fallback (Default-Particle o textura generada).
// - Tiling razonable (por defecto 40).
// - Guarda TerrainData en Assets/Terrain/Generated/SnowTerrain_1000.asset.
// - Idempotencia: si existe "SnowTerrain_1000" en escena, lo reutiliza/actualiza (sin duplicar).
//
// Copiar tal cual en un archivo llamado SetupSnowTerrain1000.cs
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using System.IO;

public class SetupSnowTerrain1000 : MonoBehaviour
{
    // ========= Parámetros editables =========
    public const int     seed               = 20250928;
    public const float   baseNoiseScale     = 0.006f;   // ~0.004–0.008
    public const int     octaves            = 4;        // 3–5
    public const float   persistence        = 0.56f;    // ~0.5–0.6
    public const float   lacunarity         = 2.0f;     // ~2.0
    public const float   overallHeightScale = 0.15f;    // relieve global (0..1 del alto Y del terrain)
    public const string  snowTexturePath    = "Resources/Textures/snow"; // sin extensión
    public const float   textureTiling      = 40f;      // 30–50 recomendado
    // =======================================

    private const string TerrainGOName   = "SnowTerrain_1000";
    private const string TerrainAssetDir = "Assets/Terrain/Generated";
    private const string TerrainAssetPath= TerrainAssetDir + "/SnowTerrain_1000.asset";
    private const string DefaultScenePath= "Assets/Scenes/SnowTerrain_1000.unity";

    private static readonly Vector3 kTerrainSize = new Vector3(1000f, 100f, 1000f);
    private const int kHeightmapResolution = 513; // coherente con 1000 (2^n + 1)

    [MenuItem("Tools/Terrain/Build Snow Terrain 1000x1000")]
    public static void BuildSnowTerrain()
    {
        // === 1) Escena: si no hay escena guardada, crear y guardar una nueva ===
        var active = SceneManager.GetActiveScene();
        if (!active.isLoaded || string.IsNullOrEmpty(active.path))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(DefaultScenePath));
            var newScene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            EditorSceneManager.SaveScene(newScene, DefaultScenePath);
            active = newScene;
        }

        // === 2) Buscar o crear GameObject de Terrain idempotente ===
        GameObject terrainGO = GameObject.Find(TerrainGOName);
        Terrain terrainCmp;
        TerrainData tData;

        if (terrainGO != null && terrainGO.TryGetComponent(out terrainCmp) && terrainCmp.terrainData != null)
        {
            // Reutilizar existente
            tData = terrainCmp.terrainData;
            // Asegurar tamaño correcto
            tData.size = kTerrainSize;
        }
        else
        {
            // Crear/obtener TerrainData como asset en ruta fija
            Directory.CreateDirectory(TerrainAssetDir);

            tData = AssetDatabase.LoadAssetAtPath<TerrainData>(TerrainAssetPath);
            if (tData == null)
            {
                tData = new TerrainData();
                tData.heightmapResolution = kHeightmapResolution;
                tData.size = kTerrainSize;
                AssetDatabase.CreateAsset(tData, TerrainAssetPath);
            }
            else
            {
                tData.heightmapResolution = kHeightmapResolution;
                tData.size = kTerrainSize;
            }

            // Crear GameObject del Terrain con su collider
            terrainGO = Terrain.CreateTerrainGameObject(tData);
            terrainGO.name = TerrainGOName;
            terrainCmp = terrainGO.GetComponent<Terrain>();
        }

        // Centrar el terreno en el origen: situar esquina en (-size/2, 0, -size/2)
        terrainGO.transform.position = new Vector3(-kTerrainSize.x * 0.5f, 0f, -kTerrainSize.z * 0.5f);

        // === 3) Generación de alturas (fBM Perlin + suavizado/blur) ===
        GenerateSmoothSnowHeights(tData);

        // === 4) Textura nevada (TerrainLayer) con fallback automático ===
        ApplySnowTextureLayer(tData);

        // === 5) Guardar cambios en assets y escena ===
        EditorUtility.SetDirty(tData);
        EditorUtility.SetDirty(terrainGO);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(active);
        EditorSceneManager.SaveScene(active);

        EditorUtility.DisplayDialog(
            "Terreno nevado listo",
            "Se creó/actualizó '" + TerrainGOName + "' (1000x1000, colisiones, nieve, ondulado suave).\n" +
            "Listo para usar con tu personaje (WASD, salto, cámara).",
            "OK"
        );
    }

    // ---------- Creación de TerrainData: alturas suaves de nieve ----------
    /// <summary>
    /// Generación del heightmap: fBM (Perlin + octavas) con altura media baja
    /// y 1–2 pasadas de blur para eliminar picos abruptos.
    /// </summary>
    private static void GenerateSmoothSnowHeights(TerrainData tData)
    {
        int res = tData.heightmapResolution;
        float[,] heights = new float[res, res];

        System.Random rng = new System.Random(seed);
        float offX = rng.Next(-100000, 100000);
        float offY = rng.Next(-100000, 100000);

        // fBM (fractal Brownian motion) Perlin
        for (int y = 0; y < res; y++)
        {
            for (int x = 0; x < res; x++)
            {
                float nx = (x + offX) * baseNoiseScale;
                float ny = (y + offY) * baseNoiseScale;

                float amp = 1f;
                float freq = 1f;
                float sum = 0f;

                for (int o = 0; o < Mathf.Max(1, octaves); o++)
                {
                    sum += Mathf.PerlinNoise(nx * freq, ny * freq) * amp;
                    amp *= Mathf.Clamp01(persistence);
                    freq *= Mathf.Max(1.01f, lacunarity);
                }

                // Normalización aproximada de fBM y curva suave (sin picos)
                float norm = 1f - Mathf.Pow(persistence, Mathf.Max(0, octaves - 1));
                float val = (norm > 0.0001f) ? (sum * (1f / norm)) : sum;
                val = Mathf.Pow(val, 1.1f);

                // Altura final baja (nieve ondulada) + clamp seguridad
                float h = Mathf.Clamp01(val * overallHeightScale);
                heights[y, x] = h;
            }
        }

        // Suavizado final (1–2 pasadas de box blur)
        BoxBlurHeightsInPlace(heights, passes: 2);

        tData.SetHeights(0, 0, heights);
    }

    /// <summary>
    /// Suavizado simple tipo box-blur de radio 1, repetido 'passes' veces.
    /// </summary>
    private static void BoxBlurHeightsInPlace(float[,] src, int passes = 1)
    {
        int resY = src.GetLength(0);
        int resX = src.GetLength(1);
        float[,] tmp = new float[resY, resX];

        for (int p = 0; p < passes; p++)
        {
            for (int y = 0; y < resY; y++)
            {
                for (int x = 0; x < resX; x++)
                {
                    float sum = 0f;
                    int count = 0;

                    for (int dy = -1; dy <= 1; dy++)
                    {
                        int yy = y + dy;
                        if (yy < 0 || yy >= resY) continue;
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int xx = x + dx;
                            if (xx < 0 || xx >= resX) continue;
                            sum += src[yy, xx];
                            count++;
                        }
                    }

                    tmp[y, x] = sum / Mathf.Max(1, count);
                }
            }

            // Copiar de tmp a src
            for (int y = 0; y < resY; y++)
                for (int x = 0; x < resX; x++)
                    src[y, x] = tmp[y, x];
        }
    }

    // ---------- Aplicación de textura/capa de nieve ----------
    /// <summary>
    /// Intenta cargar Resources/Textures/snow.png; si no existe, usa fallback:
    /// - Builtin "Default-Particle.psd" (si disponible).
    /// - Como último recurso, crea una textura 2x2 azulada/blanca temporal.
    /// Aplica TerrainLayer con tiling definido.
    /// </summary>
    private static void ApplySnowTextureLayer(TerrainData tData)
    {
        // 1) Intentar Resources/Textures/snow.png
        Texture2D tex = Resources.Load<Texture2D>("Textures/snow");

        // 2) Fallback a builtin "Default-Particle"
        if (tex == null)
        {
#if UNITY_2021_1_OR_NEWER
            // Nota: algunas versiones permiten cargar "Default-Particle.psd" como builtin extra resource.
            tex = AssetDatabase.GetBuiltinExtraResource<Texture2D>("Default-Particle.psd");
#endif
        }

        // 3) Último recurso: textura temporal generada por código (2x2 azulada/blanca)
        if (tex == null)
        {
            tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            Color snowA = new Color(0.90f, 0.95f, 1.00f, 1f); // blanco azulado
            Color snowB = new Color(0.95f, 0.98f, 1.00f, 1f);
            tex.SetPixels(new Color[] { snowA, snowB, snowB, snowA });
            tex.Apply();
        }

        // Crear/actualizar TerrainLayer principal
        TerrainLayer layer = null;
        if (tData.terrainLayers != null && tData.terrainLayers.Length > 0 && tData.terrainLayers[0] != null)
        {
            layer = tData.terrainLayers[0];
            layer.diffuseTexture = tex;
            layer.tileSize = new Vector2(textureTiling, textureTiling);
            EditorUtility.SetDirty(layer);
        }
        else
        {
            layer = new TerrainLayer();
            layer.diffuseTexture = tex;
            layer.tileSize = new Vector2(textureTiling, textureTiling);

            // Guardar TerrainLayer como sub-asset junto al TerrainData para evitar archivos sueltos
            AssetDatabase.AddObjectToAsset(layer, tData);
            AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(tData));

            tData.terrainLayers = new TerrainLayer[] { layer };
        }
    }
}
#endif



