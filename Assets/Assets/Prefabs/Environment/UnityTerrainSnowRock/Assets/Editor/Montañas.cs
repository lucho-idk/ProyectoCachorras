// Assets/Prefabs/environment/BuildMountainousTerrain500.cs
// Terreno 500x500 con montañas y BORDES QUE BAJAN A y=0 (clamp duro).
// No crea ground plane (asumo que ya tenés uno en Y=0).
// Menú: Tools/Terrain/Build Mountainous Terrain 500x500

using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;

namespace Juego.Tools
{
    public static class BuildMountainousTerrain500
    {
        // ---------- Parámetros principales ----------
        const string TerrainName = "MountainTerrain_500";
        static readonly Vector3 TerrainSize = new Vector3(500f, 100f, 500f);
        const int HeightmapRes = 513;
        const string DataFolder = "Assets/Terrain/Generated/";
        const string TextureFolder = "Assets/Terrain/Generated/Textures/";
        const string DefaultTexPath = TextureFolder + "GrassLike.png";
        const string TerrainDataAssetPath = DataFolder + "MountainTerrain_500.asset";

        // Ruido / forma
        const int Seed = 20250929;
        const int Octaves = 5;
        const float BaseFrequency = 0.0025f;
        const float Lacunarity = 2.05f;
        const float Persistence = 0.48f;
        const float RidgeFrequency = 0.0032f;
        const float RidgeSharpness = 1.35f;
        const float MountainThreshold = 0.35f;

        // Suavizado global (de picos, no de bordes)
        const int SmoothPasses = 2;
        const float SmoothStrength = 0.5f;

        // Cima de referencia
        static readonly Vector2 ReferencePeak01 = new Vector2(0.68f, 0.37f);
        const float ReferencePeakRadius01 = 0.10f;
        const float ReferencePeakBoost = 0.55f;

        // Caída a los bordes
        // Porcentaje del tamaño que será “rampa” hacia el suelo (0..0.49)
        const float EdgeFalloffPercent = 0.10f;  // ~10% → ~50 unidades en 500
        const float EdgeHardness = 2.0f;         // curva más marcada
        // Anillo de píxeles desde el borde que se CLAMPEA a 0 exacto, después de todo
        const int EdgeClampPixels = 6;

        [MenuItem("Tools/Terrain/Build Mountainous Terrain 500x500")]
        public static void Build()
        {
            EnsureFolder(DataFolder);
            EnsureFolder(TextureFolder);

            Texture2D tex = EnsureDefaultTexture(DefaultTexPath);
            TerrainLayer layer = new TerrainLayer
            {
                diffuseTexture = tex,
                tileSize = new Vector2(20f, 20f),
                tileOffset = Vector2.zero
            };

            // ---- Crear/obtener terreno ----
            Terrain terrain = GameObject.Find(TerrainName)?.GetComponent<Terrain>();
            TerrainData tData;

            if (terrain == null)
            {
                tData = new TerrainData { heightmapResolution = HeightmapRes, size = TerrainSize };
                AssetDatabase.CreateAsset(tData, AssetDatabase.GenerateUniqueAssetPath(TerrainDataAssetPath));
                AssetDatabase.SaveAssets();

                GameObject tGO = Terrain.CreateTerrainGameObject(tData);
                tGO.name = TerrainName;
                terrain = tGO.GetComponent<Terrain>();
            }
            else
            {
                tData = terrain.terrainData ?? new TerrainData();
                tData.heightmapResolution = HeightmapRes;
                tData.size = TerrainSize;
                if (terrain.terrainData == null)
                {
                    AssetDatabase.CreateAsset(tData, AssetDatabase.GenerateUniqueAssetPath(TerrainDataAssetPath));
                    AssetDatabase.SaveAssets();
                    terrain.terrainData = tData;
                }
            }

            // Asegurar base del terreno en Y=0 (tu piso está en y=0)
            terrain.transform.position = new Vector3(0f, 0f, 0f);

            // Textura
            tData.terrainLayers = new TerrainLayer[] { layer };

            // ---- Alturas ----
            float[,] heights = GenerateMountainHeights(tData.heightmapResolution, tData.heightmapResolution);

            // Suavizado general (montañas)
            for (int i = 0; i < SmoothPasses; i++)
                SmoothHeightsBoxBlur(heights, SmoothStrength);

            // Caída progresiva hacia los bordes (rampa)
            ApplyEdgeFalloff(heights, EdgeFalloffPercent, EdgeHardness);

            // CLAMP DURO a 0 en el borde (para que toque exactamente el piso)
            ClampEdgeRingToZero(heights, EdgeClampPixels);

            // Aplicar
            tData.SetHeights(0, 0, heights);

            terrain.basemapDistance = 1500f;
            terrain.drawInstanced = true;

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorUtility.SetDirty(tData);
            AssetDatabase.SaveAssets();

            Debug.Log($"[{TerrainName}] listo: borde cae a y=0 (clamp {EdgeClampPixels}px) y rampa {EdgeFalloffPercent * 100f:F0}%.");
        }

        // ------------------------ Alturas ------------------------
        private static float[,] GenerateMountainHeights(int width, int height)
        {
            float[,] h = new float[height, width];

            System.Random rng = new System.Random(Seed);
            float offA = (float)rng.NextDouble() * 10000f;
            float offB = (float)rng.NextDouble() * 10000f;
            float ridgeOffA = (float)rng.NextDouble() * 10000f;
            float ridgeOffB = (float)rng.NextDouble() * 10000f;

            for (int y = 0; y < height; y++)
            {
                float v = (float)y / (height - 1);
                for (int x = 0; x < width; x++)
                {
                    float u = (float)x / (width - 1);

                    float fbm = FBM(u, v, BaseFrequency, Octaves, Lacunarity, Persistence, offA, offB);
                    float ridge = RidgedNoise(u, v, RidgeFrequency, ridgeOffA, ridgeOffB, RidgeSharpness);
                    float mountainMask = Mathf.InverseLerp(MountainThreshold, 1f, ridge);

                    h[y, x] = fbm * mountainMask;
                }
            }

            AddReferencePeak(h, ReferencePeak01, ReferencePeakRadius01, ReferencePeakBoost);
            NormalizeHeights(h, 0f, 1f);
            return h;
        }

        private static float FBM(float u, float v, float baseFreq, int octaves, float lacunarity, float persistence, float offA, float offB)
        {
            float amp = 1f, freq = baseFreq, sum = 0f, ampSum = 0f;
            for (int i = 0; i < octaves; i++)
            {
                float n = Mathf.PerlinNoise(u * freq * 1000f + offA, v * freq * 1000f + offB);
                sum += n * amp;
                ampSum += amp;
                amp *= persistence;
                freq *= lacunarity;
            }
            return sum / Mathf.Max(ampSum, 0.0001f);
        }

        private static float RidgedNoise(float u, float v, float freq, float offA, float offB, float sharpness)
        {
            float n = Mathf.PerlinNoise(u * freq * 1000f + offA, v * freq * 1000f + offB) * 2f - 1f;
            n = 1f - Mathf.Abs(n);
            n = Mathf.Pow(Mathf.Clamp01(n), sharpness);
            return n;
        }

        private static void AddReferencePeak(float[,] h, Vector2 center01, float radius01, float boost)
        {
            int H = h.GetLength(0), W = h.GetLength(1);
            int cx = Mathf.RoundToInt(center01.x * (W - 1));
            int cy = Mathf.RoundToInt(center01.y * (H - 1));
            float r = radius01 * Mathf.Min(W - 1, H - 1);
            float r2 = r * r;

            for (int y = Mathf.Max(0, cy - (int)r); y <= Mathf.Min(H - 1, cy + (int)r); y++)
            for (int x = Mathf.Max(0, cx - (int)r); x <= Mathf.Min(W - 1, cx + (int)r); x++)
            {
                float dx = x - cx, dy = y - cy;
                float d2 = dx * dx + dy * dy;
                if (d2 <= r2)
                {
                    float t = 1f - (d2 / r2);
                    float gauss = Mathf.SmoothStep(0f, 1f, t);
                    h[y, x] += gauss * boost;
                }
            }
        }

        private static void NormalizeHeights(float[,] h, float minTarget, float maxTarget)
        {
            int H = h.GetLength(0), W = h.GetLength(1);
            float min = float.MaxValue, max = float.MinValue;

            for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float v = h[y, x];
                if (v < min) min = v;
                if (v > max) max = v;
            }

            float range = Mathf.Max(0.0001f, max - min);
            for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float t = (h[y, x] - min) / range;
                h[y, x] = Mathf.Lerp(minTarget, maxTarget, t);
            }
        }

        // ------------------------ Borde: rampa + clamp ------------------------
        private static void ApplyEdgeFalloff(float[,] h, float percent, float hardness)
        {
            int H = h.GetLength(0), W = h.GetLength(1);
            percent = Mathf.Clamp(percent, 0.001f, 0.49f);
            hardness = Mathf.Max(0.5f, hardness);

            for (int y = 0; y < H; y++)
            {
                float v = (float)y / (H - 1);
                float dv = Mathf.Min(v, 1f - v);
                for (int x = 0; x < W; x++)
                {
                    float u = (float)x / (W - 1);
                    float du = Mathf.Min(u, 1f - u);
                    float dEdge = Mathf.Min(du, dv);

                    float t = Mathf.Clamp01(dEdge / percent);
                    float s = Mathf.SmoothStep(0f, 1f, t);
                    s = Mathf.Pow(s, hardness);

                    h[y, x] *= s; // rampa hacia 0 en el borde
                }
            }
        }

        private static void ClampEdgeRingToZero(float[,] h, int ringPx)
        {
            if (ringPx <= 0) return;
            int H = h.GetLength(0), W = h.GetLength(1);

            for (int y = 0; y < H; y++)
            {
                for (int x = 0; x < W; x++)
                {
                    int distLeft = x;
                    int distRight = (W - 1) - x;
                    int distBottom = y;
                    int distTop = (H - 1) - y;
                    int d = Mathf.Min(Mathf.Min(distLeft, distRight), Mathf.Min(distBottom, distTop));

                    if (d < ringPx) h[y, x] = 0f; // 0 EXACTO → y=0
                }
            }
        }

        // ------------------------ Suavizado ------------------------
        private static void SmoothHeightsBoxBlur(float[,] h, float strength)
        {
            int H = h.GetLength(0), W = h.GetLength(1);
            float[,] temp = new float[H, W];

            for (int y = 0; y < H; y++)
            {
                for (int x = 0; x < W; x++)
                {
                    float sum = 0f; int count = 0;
                    for (int oy = -1; oy <= 1; oy++)
                    {
                        int yy = y + oy; if (yy < 0 || yy >= H) continue;
                        for (int ox = -1; ox <= 1; ox++)
                        {
                            int xx = x + ox; if (xx < 0 || xx >= W) continue;
                            sum += h[yy, xx];
                            count++;
                        }
                    }
                    float avg = sum / Mathf.Max(1, count);
                    temp[y, x] = Mathf.Lerp(h[y, x], avg, strength);
                }
            }

            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                    h[y, x] = temp[y, x];
        }

        // ------------------------ Util ------------------------
        private static Texture2D EnsureDefaultTexture(string pngPath)
        {
            Texture2D existing = AssetDatabase.LoadAssetAtPath<Texture2D>(pngPath);
            if (existing != null) return existing;

            Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            Color a = new Color(0.38f, 0.55f, 0.33f, 1f);
            Color b = new Color(0.42f, 0.58f, 0.36f, 1f);
            tex.SetPixels(new Color[] { a, b, b, a });
            tex.Apply(false, false);

            byte[] bytes = tex.EncodeToPNG();
            EnsureFolder(Path.GetDirectoryName(pngPath) + "/");
            File.WriteAllBytes(pngPath, bytes);
            AssetDatabase.ImportAsset(pngPath, ImportAssetOptions.ForceUpdate);

            var importer = AssetImporter.GetAtPath(pngPath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Default;
                importer.mipmapEnabled = true;
                importer.isReadable = true;
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(pngPath);
        }

        private static void EnsureFolder(string pathWithTrailingSlash)
        {
            if (AssetDatabase.IsValidFolder(pathWithTrailingSlash)) return;
            string[] parts = pathWithTrailingSlash.TrimEnd('/').Split('/');
            string accum = parts[0]; // "Assets"
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{accum}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(accum, parts[i]);
                accum = next;
            }
        }
    }
}
#endif




