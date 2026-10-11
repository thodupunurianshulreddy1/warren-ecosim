// ============================================================================
//  EcoSimProjectBuilder.cs   (EDITOR ONLY - lives in an "Editor" folder,
//  so it is never included in a game build)
//
//  Menu:  EcoSim > Build Everything
//  Generates, from code only (no external assets):
//    Assets/Visual Assets/Textures  : procedural ground texture (PNG)
//    Assets/Visual Assets/Models    : procedural cone mesh (ears, snouts, grass)
//    Assets/Visual Assets/Materials : one material per species / body part
//    Assets/Prefabs                 : Grass, Rabbit, Deer, Fox, Wolf,
//                                     [H4] Burrow, PlayerMarker
//    Assets/Scenes                  : [H4] WarrenGame (the game, opened last),
//                                     EcoSimMain (Homework 3 sandbox),
//                                     Test_HerbivoresOnly (test scene)
//  Running it again updates everything in place (safe to re-run).
// ============================================================================

using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using ThodupunuriAnshul.EcoSim.Agents;
using ThodupunuriAnshul.EcoSim.Core;
using ThodupunuriAnshul.EcoSim.Game;
using ThodupunuriAnshul.EcoSim.Habitat;
using ThodupunuriAnshul.EcoSim.Player;
using ThodupunuriAnshul.EcoSim.UI;

namespace ThodupunuriAnshul.EcoSim.EditorTools
{
    public static class EcoSimProjectBuilder
    {
        // --------------------------------------------------------------------
        //  Paths & constants
        // --------------------------------------------------------------------
        private const string PrefabsFolder = "Assets/Prefabs";
        private const string VisualFolder = "Assets/Visual Assets";
        private const string MaterialsFolder = VisualFolder + "/Materials";
        private const string ModelsFolder = VisualFolder + "/Models";
        private const string TexturesFolder = VisualFolder + "/Textures";
        private const string ScenesFolder = "Assets/Scenes";

        private const string GameScenePath = ScenesFolder + "/WarrenGame.unity";      // [H4]
        private const string MainScenePath = ScenesFolder + "/EcoSimMain.unity";
        private const string TestScenePath = ScenesFolder + "/Test_HerbivoresOnly.unity";
        private const string GroundTexturePath = TexturesFolder + "/GroundTexture.png";
        private const string ConeMeshPath = ModelsFolder + "/ConeMesh.asset";

        private const float ArenaHalfSize = 25f;

        /// <summary>Every material used by the prefabs, grouped in one object.</summary>
        private class MaterialSet
        {
            public Material ground, wall, grass, rabbitFur, rabbitWhite, deerFur, antler,
                            foxFur, foxWhite, wolfFur, wolfDark, eye, statusLight,
                            burrowHole, burrowDirt, warrenTag, playerMarker;   // [H4]
        }

        /// <summary>The generated prefabs (component references).</summary>
        private class PrefabSet
        {
            public Plant grass;
            public Rabbit rabbit;
            public Deer deer;
            public Fox fox;
            public Wolf wolf;
            public Burrow burrow;              // [H4]
            public GameObject playerMarker;    // [H4]
        }

        /// <summary>[H4] Objects shared by every scene, returned by CreateBaseScene.</summary>
        private class BaseScene
        {
            public Light sun;
            public Camera camera;
            public SimulationManager manager;
        }

        // ====================================================================
        //  MENU ITEMS
        // ====================================================================
        [MenuItem("EcoSim/Build Everything (folders, assets, prefabs, scenes)", priority = 0)]
        public static void BuildEverything()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            CreateFolders();
            Texture2D groundTexture = CreateGroundTexture();
            Mesh coneMesh = CreateConeMesh();
            MaterialSet materials = CreateMaterials(groundTexture);
            PrefabSet prefabs = CreatePrefabs(materials, coneMesh);

            CreateSandboxScene(TestScenePath, materials, prefabs, herbivoresOnly: true);
            CreateSandboxScene(MainScenePath, materials, prefabs, herbivoresOnly: false);
            CreateGameScene(GameScenePath, materials, prefabs);                      // last = stays open
            RegisterScenesInBuildSettings();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[EcoSim] Build complete. The WarrenGame scene is open: press Play!");
        }

        [MenuItem("EcoSim/Open Game Scene (Warren)", priority = 20)]
        public static void OpenGameScene()
        {
            OpenScene(GameScenePath);
        }

        [MenuItem("EcoSim/Open Sandbox Scene (Homework 3)", priority = 21)]
        public static void OpenMainScene()
        {
            OpenScene(MainScenePath);
        }

        private static void OpenScene(string path)
        {
            if (!File.Exists(path))
            {
                Debug.LogWarning($"[EcoSim] {path} not found. Run 'EcoSim > Build Everything' first.");
                return;
            }
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(path);
        }

        // ====================================================================
        //  1. FOLDERS
        // ====================================================================
        private static void CreateFolders()
        {
            EnsureFolder("Assets", "Prefabs");
            EnsureFolder("Assets", "Visual Assets");
            EnsureFolder(VisualFolder, "Materials");
            EnsureFolder(VisualFolder, "Models");
            EnsureFolder(VisualFolder, "Textures");
            EnsureFolder("Assets", "Scenes");
        }

        private static void EnsureFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder($"{parent}/{name}"))
                AssetDatabase.CreateFolder(parent, name);
        }

        // ====================================================================
        //  2. TEXTURE  (procedural: Perlin noise grass + 1-unit grid lines)
        // ====================================================================
        private static Texture2D CreateGroundTexture()
        {
            const int size = 128;
            const int gridStep = 32;
            var texture = new Texture2D(size, size, TextureFormat.RGB24, false);
            Color dark = new Color(0.28f, 0.45f, 0.20f);
            Color light = new Color(0.40f, 0.58f, 0.27f);
            Color line = new Color(0.22f, 0.36f, 0.16f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool isGridLine = x % gridStep == 0 || y % gridStep == 0;
                    float noise = Mathf.PerlinNoise(x * 0.08f, y * 0.08f);
                    texture.SetPixel(x, y, isGridLine ? line : Color.Lerp(dark, light, noise));
                }
            }
            texture.Apply();

            File.WriteAllBytes(GroundTexturePath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(GroundTexturePath);

            var importer = (TextureImporter)AssetImporter.GetAtPath(GroundTexturePath);
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Bilinear;
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Texture2D>(GroundTexturePath);
        }

        // ====================================================================
        //  3. MODEL  (procedural cone: base radius 0.5 at y=0, apex at y=1)
        // ====================================================================
        private static Mesh CreateConeMesh()
        {
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(ConeMeshPath);
            bool isNew = mesh == null;
            if (isNew) mesh = new Mesh();
            mesh.Clear();
            mesh.name = "ConeMesh";

            const int segments = 16;
            const float radius = 0.5f;
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            Vector3 apex = Vector3.up;
            Vector3 baseCenter = Vector3.zero;

            for (int i = 0; i < segments; i++)
            {
                float a0 = i * Mathf.PI * 2f / segments;
                float a1 = (i + 1) * Mathf.PI * 2f / segments;
                Vector3 p0 = new Vector3(Mathf.Cos(a0) * radius, 0f, Mathf.Sin(a0) * radius);
                Vector3 p1 = new Vector3(Mathf.Cos(a1) * radius, 0f, Mathf.Sin(a1) * radius);

                // Side triangle (clockwise seen from outside = Unity front face)
                int start = vertices.Count;
                vertices.Add(p0); vertices.Add(apex); vertices.Add(p1);
                triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);

                // Base triangle (facing down)
                start = vertices.Count;
                vertices.Add(p0); vertices.Add(p1); vertices.Add(baseCenter);
                triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
            }

            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            if (isNew) AssetDatabase.CreateAsset(mesh, ConeMeshPath);
            else EditorUtility.SetDirty(mesh);
            return mesh;
        }

        // ====================================================================
        //  4. MATERIALS
        // ====================================================================
        private static MaterialSet CreateMaterials(Texture2D groundTexture)
        {
            var set = new MaterialSet
            {
                ground      = GetOrCreateMaterial("Ground", Color.white, false),
                wall        = GetOrCreateMaterial("ArenaWall", new Color(0.45f, 0.35f, 0.25f), false),
                grass       = GetOrCreateMaterial("Grass", new Color(0.25f, 0.75f, 0.20f), false),
                rabbitFur   = GetOrCreateMaterial("RabbitFur", new Color(0.85f, 0.82f, 0.78f), false),
                rabbitWhite = GetOrCreateMaterial("RabbitTail", Color.white, false),
                deerFur     = GetOrCreateMaterial("DeerFur", new Color(0.55f, 0.36f, 0.18f), false),
                antler      = GetOrCreateMaterial("DeerAntler", new Color(0.90f, 0.85f, 0.70f), false),
                foxFur      = GetOrCreateMaterial("FoxFur", new Color(0.95f, 0.45f, 0.10f), false),
                foxWhite    = GetOrCreateMaterial("FoxWhite", new Color(0.97f, 0.95f, 0.92f), false),
                wolfFur     = GetOrCreateMaterial("WolfFur", new Color(0.45f, 0.45f, 0.50f), false),
                wolfDark    = GetOrCreateMaterial("WolfDark", new Color(0.20f, 0.20f, 0.22f), false),
                eye         = GetOrCreateMaterial("Eye", Color.black, false),
                statusLight = GetOrCreateMaterial("StatusLight", Color.white, true),
                // [H4]
                burrowHole   = GetOrCreateMaterial("BurrowHole", new Color(0.08f, 0.05f, 0.03f), false),
                burrowDirt   = GetOrCreateMaterial("BurrowDirt", new Color(0.42f, 0.30f, 0.18f), false),
                warrenTag    = GetOrCreateMaterial("WarrenTag", new Color(1f, 0.8f, 0.2f), true),
                playerMarker = GetOrCreateMaterial("PlayerMarker", new Color(1f, 0.95f, 0.35f), true)
            };

            // Ground texture repeated 10 times across the arena.
            SetMaterialTexture(set.ground, groundTexture, new Vector2(10f, 10f));
            EditorUtility.SetDirty(set.ground);
            return set;
        }

        /// <summary>Loads the material if it exists (keeps references), otherwise creates it.</summary>
        private static Material GetOrCreateMaterial(string name, Color color, bool unlit)
        {
            string path = $"{MaterialsFolder}/{name}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = unlit ? FindUnlitShader() : FindLitShader();

            if (material == null)
            {
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                material.shader = shader;
            }

            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.1f);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", 0.1f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void SetMaterialTexture(Material material, Texture texture, Vector2 tiling)
        {
            foreach (string property in new[] { "_BaseMap", "_MainTex" })
            {
                if (!material.HasProperty(property)) continue;
                material.SetTexture(property, texture);
                material.SetTextureScale(property, tiling);
            }
        }

        /// <summary>Picks a lit shader that exists in the current render pipeline.</summary>
        private static Shader FindLitShader()
        {
            bool usesScriptablePipeline = GraphicsSettings.currentRenderPipeline != null;
            string[] candidates = usesScriptablePipeline
                ? new[] { "Universal Render Pipeline/Lit", "HDRP/Lit", "Standard" }
                : new[] { "Standard", "Universal Render Pipeline/Lit" };
            return FindFirstShader(candidates);
        }

        private static Shader FindUnlitShader()
        {
            bool usesScriptablePipeline = GraphicsSettings.currentRenderPipeline != null;
            string[] candidates = usesScriptablePipeline
                ? new[] { "Universal Render Pipeline/Unlit", "HDRP/Unlit", "Unlit/Color" }
                : new[] { "Unlit/Color", "Standard" };
            return FindFirstShader(candidates);
        }

        private static Shader FindFirstShader(string[] names)
        {
            foreach (string shaderName in names)
            {
                Shader shader = Shader.Find(shaderName);
                if (shader != null) return shader;
            }
            Debug.LogWarning("[EcoSim] No suitable shader found, using the default one.");
            return Shader.Find("Hidden/InternalErrorShader");
        }

        // ====================================================================
        //  5. PREFABS  (built from primitives + the cone mesh)
        // ====================================================================
        private static PrefabSet CreatePrefabs(MaterialSet m, Mesh cone)
        {
            return new PrefabSet
            {
                grass = SavePrefab(BuildGrass(m, cone), "Grass").GetComponent<Plant>(),
                rabbit = SavePrefab(BuildRabbit(m, cone), "Rabbit").GetComponent<Rabbit>(),
                deer = SavePrefab(BuildDeer(m, cone), "Deer").GetComponent<Deer>(),
                fox = SavePrefab(BuildCanine<Fox>("Fox", 1f, m.foxFur, m.foxWhite, m, cone), "Fox").GetComponent<Fox>(),
                wolf = SavePrefab(BuildCanine<Wolf>("Wolf", 1.4f, m.wolfFur, m.wolfDark, m, cone), "Wolf").GetComponent<Wolf>(),
                burrow = SavePrefab(BuildBurrow(m), "Burrow").GetComponent<Burrow>(),
                playerMarker = SavePrefab(BuildPlayerMarker(m, cone), "PlayerMarker")
            };
        }

        private static GameObject SavePrefab(GameObject root, string name)
        {
            string path = $"{PrefabsFolder}/{name}.prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        // ---------- Grass ----------
        private static GameObject BuildGrass(MaterialSet m, Mesh cone)
        {
            var root = new GameObject("Grass");
            root.AddComponent<Plant>();

            // A tuft of 5 blades with fixed offsets (the Plant randomizes its rotation at runtime).
            Vector3[] offsets =
            {
                new Vector3(0f, 0f, 0f), new Vector3(0.15f, 0f, 0.1f), new Vector3(-0.15f, 0f, 0.08f),
                new Vector3(0.05f, 0f, -0.15f), new Vector3(-0.08f, 0f, -0.1f)
            };
            for (int i = 0; i < offsets.Length; i++)
            {
                float height = 0.5f + 0.1f * (i % 3);
                MeshPart($"Blade_{i}", cone, root.transform, offsets[i], new Vector3(0.14f, height, 0.14f),
                         m.grass, new Vector3(0f, 0f, (i - 2) * 8f));
            }
            return root;
        }

        // ---------- Rabbit ----------
        private static GameObject BuildRabbit(MaterialSet m, Mesh cone)
        {
            var root = new GameObject("Rabbit");
            Transform visual = new GameObject("Visual").transform;
            visual.SetParent(root.transform, false);

            Part("Body", PrimitiveType.Sphere, visual, new Vector3(0f, 0.25f, 0f), new Vector3(0.45f, 0.42f, 0.6f), m.rabbitFur);
            Part("Head", PrimitiveType.Sphere, visual, new Vector3(0f, 0.5f, 0.28f), new Vector3(0.3f, 0.3f, 0.32f), m.rabbitFur);
            Part("Tail", PrimitiveType.Sphere, visual, new Vector3(0f, 0.3f, -0.32f), Vector3.one * 0.15f, m.rabbitWhite);
            MeshPart("Ear_L", cone, visual, new Vector3(-0.07f, 0.6f, 0.24f), new Vector3(0.09f, 0.35f, 0.06f), m.rabbitFur, new Vector3(-10f, 0f, 12f));
            MeshPart("Ear_R", cone, visual, new Vector3(0.07f, 0.6f, 0.24f), new Vector3(0.09f, 0.35f, 0.06f), m.rabbitFur, new Vector3(-10f, 0f, -12f));
            AddEyes(visual, new Vector3(0.09f, 0.55f, 0.41f), 0.06f, m.eye);

            // [H4] Gold tag on the back, hidden until the rabbit joins the player's warren.
            GameObject warrenTag = Part("WarrenTag", PrimitiveType.Sphere, visual, new Vector3(0f, 0.47f, -0.06f),
                                        new Vector3(0.3f, 0.08f, 0.36f), m.warrenTag);
            warrenTag.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            warrenTag.SetActive(false);

            FinishAnimal<Rabbit>(root, visual, 1.05f, 0.22f, m);
            var serialized = new SerializedObject(root.GetComponent<Rabbit>());
            serialized.FindProperty("warrenTag").objectReferenceValue = warrenTag;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }

        // ---------- Deer ----------
        private static GameObject BuildDeer(MaterialSet m, Mesh cone)
        {
            var root = new GameObject("Deer");
            Transform visual = new GameObject("Visual").transform;
            visual.SetParent(root.transform, false);

            Part("Body", PrimitiveType.Capsule, visual, new Vector3(0f, 0.95f, 0f), new Vector3(0.5f, 0.6f, 0.5f), m.deerFur, new Vector3(90f, 0f, 0f));
            AddLegs(visual, 0.18f, 0.4f, 0.35f, 0.1f, m.deerFur);
            Part("Neck", PrimitiveType.Cylinder, visual, new Vector3(0f, 1.3f, 0.5f), new Vector3(0.18f, 0.3f, 0.18f), m.deerFur, new Vector3(30f, 0f, 0f));
            Part("Head", PrimitiveType.Sphere, visual, new Vector3(0f, 1.6f, 0.68f), new Vector3(0.28f, 0.28f, 0.42f), m.deerFur);
            Part("Tail", PrimitiveType.Sphere, visual, new Vector3(0f, 1.05f, -0.62f), Vector3.one * 0.14f, m.antler);
            MeshPart("Antler_L", cone, visual, new Vector3(-0.1f, 1.7f, 0.62f), new Vector3(0.06f, 0.45f, 0.06f), m.antler, new Vector3(0f, 0f, 25f));
            MeshPart("Antler_R", cone, visual, new Vector3(0.1f, 1.7f, 0.62f), new Vector3(0.06f, 0.45f, 0.06f), m.antler, new Vector3(0f, 0f, -25f));
            AddEyes(visual, new Vector3(0.1f, 1.65f, 0.84f), 0.06f, m.eye);

            return FinishAnimal<Deer>(root, visual, 2.4f, 0.35f, m);
        }

        // ---------- Fox & Wolf (same body plan, different size & colours) ----------
        /// <summary>Generic method: builds a four-legged canine and adds component T.</summary>
        private static GameObject BuildCanine<T>(string name, float size, Material fur, Material accent,
                                                 MaterialSet m, Mesh cone) where T : Carnivore
        {
            var root = new GameObject(name);
            Transform visual = new GameObject("Visual").transform;
            visual.SetParent(root.transform, false);
            visual.localScale = Vector3.one * size;   // the whole body plan scales together

            Part("Body", PrimitiveType.Capsule, visual, new Vector3(0f, 0.45f, 0f), new Vector3(0.32f, 0.4f, 0.32f), fur, new Vector3(90f, 0f, 0f));
            AddLegs(visual, 0.1f, 0.25f, 0.15f, 0.07f, fur);
            Part("Head", PrimitiveType.Sphere, visual, new Vector3(0f, 0.62f, 0.45f), Vector3.one * 0.28f, fur);
            MeshPart("Snout", cone, visual, new Vector3(0f, 0.58f, 0.55f), new Vector3(0.14f, 0.22f, 0.12f), accent, new Vector3(90f, 0f, 0f));
            MeshPart("Ear_L", cone, visual, new Vector3(-0.08f, 0.72f, 0.42f), new Vector3(0.09f, 0.16f, 0.05f), fur, new Vector3(0f, 0f, 10f));
            MeshPart("Ear_R", cone, visual, new Vector3(0.08f, 0.72f, 0.42f), new Vector3(0.09f, 0.16f, 0.05f), fur, new Vector3(0f, 0f, -10f));
            Part("Tail", PrimitiveType.Capsule, visual, new Vector3(0f, 0.45f, -0.5f), new Vector3(0.13f, 0.22f, 0.13f), fur, new Vector3(-55f, 0f, 0f));
            Part("TailTip", PrimitiveType.Sphere, visual, new Vector3(0f, 0.32f, -0.68f), Vector3.one * 0.11f, accent);
            AddEyes(visual, new Vector3(0.07f, 0.67f, 0.57f), 0.05f, m.eye);

            return FinishAnimal<T>(root, visual, 1.15f * size, 0.27f * Mathf.Sqrt(size), m);
        }

        // ---------- [H4] Burrow: dark hole + ring of dirt mounds ----------
        private static GameObject BuildBurrow(MaterialSet m)
        {
            var root = new GameObject("Burrow");
            root.AddComponent<Burrow>();

            // A Unity cylinder has a diameter of 1 and a height of 2 (scale.y = half height).
            GameObject hole = Part("Hole", PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.01f, 0f),
                                   new Vector3(1.6f, 0.01f, 1.6f), m.burrowHole);
            hole.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;

            const int moundCount = 8;
            for (int i = 0; i < moundCount; i++)
            {
                float angle = i * Mathf.PI * 2f / moundCount;
                Vector3 position = new Vector3(Mathf.Cos(angle), 0.04f, Mathf.Sin(angle)) * 1.05f;
                Part($"Mound_{i}", PrimitiveType.Sphere, root.transform, position,
                     new Vector3(0.55f, 0.22f, 0.55f), m.burrowDirt);
            }
            return root;
        }

        // ---------- [H4] Player marker: a floating arrow pointing down ----------
        private static GameObject BuildPlayerMarker(MaterialSet m, Mesh cone)
        {
            var root = new GameObject("PlayerMarker");
            // The cone points up (apex at y = 1): rotated 180 degrees it points down to the rabbit.
            GameObject arrow = MeshPart("Arrow", cone, root.transform, new Vector3(0f, 0.35f, 0f),
                                        new Vector3(0.3f, 0.35f, 0.3f), m.playerMarker, new Vector3(180f, 0f, 0f));
            arrow.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            return root;
        }

        // ---------- Shared building helpers ----------

        /// <summary>Adds the status light + the behaviour component and links the references.</summary>
        private static GameObject FinishAnimal<T>(GameObject root, Transform visual, float indicatorHeight,
                                                  float indicatorSize, MaterialSet m) where T : Animal
        {
            GameObject indicator = Part("StatusIndicator", PrimitiveType.Sphere, root.transform,
                                        new Vector3(0f, indicatorHeight, 0f), Vector3.one * indicatorSize, m.statusLight);
            Renderer indicatorRenderer = indicator.GetComponent<Renderer>();
            indicatorRenderer.shadowCastingMode = ShadowCastingMode.Off;

            T animal = root.AddComponent<T>();
            animal.ApplySpeciesDefaults();        // abstract CreateDefaultStats() of each species

            // Assign the protected [SerializeField] references through the serialization API.
            var serialized = new SerializedObject(animal);
            serialized.FindProperty("visualRoot").objectReferenceValue = visual;
            serialized.FindProperty("statusIndicator").objectReferenceValue = indicatorRenderer;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }

        private static void AddLegs(Transform parent, float halfWidth, float halfLength, float legHalfHeight,
                                    float thickness, Material material)
        {
            int index = 0;
            foreach (float x in new[] { -halfWidth, halfWidth })
            {
                foreach (float z in new[] { -halfLength, halfLength })
                {
                    // A Unity cylinder is 2 units tall: scale.y = half height.
                    Part($"Leg_{index++}", PrimitiveType.Cylinder, parent, new Vector3(x, legHalfHeight, z),
                         new Vector3(thickness, legHalfHeight, thickness), material);
                }
            }
        }

        private static void AddEyes(Transform parent, Vector3 rightEyePosition, float size, Material material)
        {
            Part("Eye_R", PrimitiveType.Sphere, parent, rightEyePosition, Vector3.one * size, material);
            Vector3 left = rightEyePosition;
            left.x = -left.x;
            Part("Eye_L", PrimitiveType.Sphere, parent, left, Vector3.one * size, material);
        }

        /// <summary>Creates a primitive child WITHOUT collider (agents use distance checks, not physics).</summary>
        private static GameObject Part(string name, PrimitiveType type, Transform parent, Vector3 localPosition,
                                       Vector3 localScale, Material material, Vector3 localEuler = default)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localEulerAngles = localEuler;
            part.transform.localScale = localScale;
            Object.DestroyImmediate(part.GetComponent<Collider>());
            part.GetComponent<Renderer>().sharedMaterial = material;
            return part;
        }

        /// <summary>Creates a child using a custom mesh (our procedural cone).</summary>
        private static GameObject MeshPart(string name, Mesh mesh, Transform parent, Vector3 localPosition,
                                           Vector3 localScale, Material material, Vector3 localEuler = default)
        {
            var part = new GameObject(name);
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localEulerAngles = localEuler;
            part.transform.localScale = localScale;
            part.AddComponent<MeshFilter>().sharedMesh = mesh;
            part.AddComponent<MeshRenderer>().sharedMaterial = material;
            return part;
        }

        // ====================================================================
        //  6. SCENES
        //  [H4] Refactored: the part shared by every scene (light, arena,
        //  camera, simulation manager) is built once by CreateBaseScene, then
        //  each scene adds its own layer (sandbox observation OR the game).
        // ====================================================================

        /// <summary>Homework 3 observation scenes: orbit camera + population HUD.</summary>
        private static void CreateSandboxScene(string path, MaterialSet m, PrefabSet prefabs, bool herbivoresOnly)
        {
            BaseScene scene = CreateBaseScene(m, prefabs, serialized =>
            {
                if (!herbivoresOnly) return;
                // Test scene used during development: movement / eating / mating without predators.
                serialized.FindProperty("initialFoxes").intValue = 0;
                serialized.FindProperty("initialWolves").intValue = 0;
                serialized.FindProperty("useFixedSeed").boolValue = true;
            });

            scene.camera.gameObject.AddComponent<CameraController>();
            new GameObject("HUD").AddComponent<PopulationHUD>();

            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), path);
        }

        /// <summary>[H4] The game scene: player rig, day/night cycle, GameManager and game HUD.</summary>
        private static void CreateGameScene(string path, MaterialSet m, PrefabSet prefabs)
        {
            BaseScene scene = CreateBaseScene(m, prefabs, serialized =>
            {
                // A gentler start (onboarding): few predators on day 1, more arrive every night.
                serialized.FindProperty("initialRabbits").intValue = 14;
                serialized.FindProperty("initialDeer").intValue = 8;
                serialized.FindProperty("initialFoxes").intValue = 2;
                serialized.FindProperty("initialWolves").intValue = 1;
                serialized.FindProperty("logCensusToConsole").boolValue = false;
            });

            // --- Camera rig: third-person follow camera ---
            FollowCamera followCamera = scene.camera.gameObject.AddComponent<FollowCamera>();

            // --- Day / night cycle ---
            DayNightCycle dayNight = new GameObject("DayNightCycle").AddComponent<DayNightCycle>();
            var dayNightSerialized = new SerializedObject(dayNight);
            dayNightSerialized.FindProperty("sun").objectReferenceValue = scene.sun;
            dayNightSerialized.FindProperty("skyCamera").objectReferenceValue = scene.camera;
            dayNightSerialized.ApplyModifiedPropertiesWithoutUndo();

            // --- Player: controller + abilities + marker ---
            var playerObject = new GameObject("Player");
            PlayerAbilities abilities = playerObject.AddComponent<PlayerAbilities>();
            var abilitiesSerialized = new SerializedObject(abilities);
            abilitiesSerialized.FindProperty("burrowPrefab").objectReferenceValue = prefabs.burrow;
            abilitiesSerialized.ApplyModifiedPropertiesWithoutUndo();

            var marker = (GameObject)PrefabUtility.InstantiatePrefab(prefabs.playerMarker);
            marker.transform.SetParent(playerObject.transform, false);

            PlayerController controller = playerObject.AddComponent<PlayerController>();
            var controllerSerialized = new SerializedObject(controller);
            controllerSerialized.FindProperty("cameraTransform").objectReferenceValue = scene.camera.transform;
            controllerSerialized.FindProperty("abilities").objectReferenceValue = abilities;
            controllerSerialized.FindProperty("marker").objectReferenceValue = marker.transform;
            controllerSerialized.ApplyModifiedPropertiesWithoutUndo();

            // --- Game manager ---
            GameManager gameManager = new GameObject("GameManager").AddComponent<GameManager>();
            var gameSerialized = new SerializedObject(gameManager);
            gameSerialized.FindProperty("simulation").objectReferenceValue = scene.manager;
            gameSerialized.FindProperty("dayNightCycle").objectReferenceValue = dayNight;
            gameSerialized.FindProperty("playerController").objectReferenceValue = controller;
            gameSerialized.FindProperty("playerAbilities").objectReferenceValue = abilities;
            gameSerialized.FindProperty("followCamera").objectReferenceValue = followCamera;
            gameSerialized.FindProperty("burrowPrefab").objectReferenceValue = prefabs.burrow;
            gameSerialized.ApplyModifiedPropertiesWithoutUndo();

            // --- HUD ---
            new GameObject("GameHUD").AddComponent<GameHUD>();

            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), path);
        }

        /// <summary>
        /// Light, arena, camera and simulation manager. 'configureManager' lets each scene
        /// change the manager's serialized settings (lambda / delegate parameter).
        /// </summary>
        private static BaseScene CreateBaseScene(MaterialSet m, PrefabSet prefabs,
                                                 System.Action<SerializedObject> configureManager)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var result = new BaseScene();

            // --- Lighting ---
            var lightObject = new GameObject("Directional Light");
            result.sun = lightObject.AddComponent<Light>();
            result.sun.type = LightType.Directional;
            result.sun.intensity = 1.1f;
            result.sun.shadows = LightShadows.Soft;
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.55f, 0.58f, 0.62f);

            // --- Environment: ground + low walls showing the arena limits ---
            var environment = new GameObject("Environment").transform;
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);   // a Plane is 10x10 units
            ground.name = "Ground";
            ground.transform.SetParent(environment);
            ground.transform.localScale = new Vector3(ArenaHalfSize * 0.2f, 1f, ArenaHalfSize * 0.2f);
            ground.GetComponent<Renderer>().sharedMaterial = m.ground;

            CreateWall(environment, "Wall_North", new Vector3(0f, 0.25f, ArenaHalfSize + 0.25f), new Vector3(ArenaHalfSize * 2f + 1f, 0.5f, 0.5f), m.wall);
            CreateWall(environment, "Wall_South", new Vector3(0f, 0.25f, -ArenaHalfSize - 0.25f), new Vector3(ArenaHalfSize * 2f + 1f, 0.5f, 0.5f), m.wall);
            CreateWall(environment, "Wall_East", new Vector3(ArenaHalfSize + 0.25f, 0.25f, 0f), new Vector3(0.5f, 0.5f, ArenaHalfSize * 2f), m.wall);
            CreateWall(environment, "Wall_West", new Vector3(-ArenaHalfSize - 0.25f, 0.25f, 0f), new Vector3(0.5f, 0.5f, ArenaHalfSize * 2f), m.wall);

            // --- Camera ---
            var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            result.camera = cameraObject.AddComponent<Camera>();
            result.camera.farClipPlane = 300f;
            result.camera.clearFlags = CameraClearFlags.SolidColor;
            result.camera.backgroundColor = new Color(0.55f, 0.75f, 0.95f);
            cameraObject.AddComponent<AudioListener>();
            cameraObject.transform.SetPositionAndRotation(new Vector3(0f, 37f, -26f), Quaternion.Euler(55f, 0f, 0f));

            // --- Simulation manager (prefab references assigned through SerializedObject) ---
            var managerObject = new GameObject("SimulationManager");
            result.manager = managerObject.AddComponent<SimulationManager>();
            var serialized = new SerializedObject(result.manager);
            serialized.FindProperty("arenaHalfSize").floatValue = ArenaHalfSize;
            serialized.FindProperty("grassPrefab").objectReferenceValue = prefabs.grass;
            serialized.FindProperty("rabbitPrefab").objectReferenceValue = prefabs.rabbit;
            serialized.FindProperty("deerPrefab").objectReferenceValue = prefabs.deer;
            serialized.FindProperty("foxPrefab").objectReferenceValue = prefabs.fox;
            serialized.FindProperty("wolfPrefab").objectReferenceValue = prefabs.wolf;
            configureManager?.Invoke(serialized);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            // [H4] Fail loudly if a reference did not stick (instead of an empty arena at Play).
            serialized.Update();
            foreach (string field in new[] { "grassPrefab", "rabbitPrefab", "deerPrefab", "foxPrefab", "wolfPrefab" })
            {
                if (serialized.FindProperty(field).objectReferenceValue == null)
                    Debug.LogError($"[EcoSim] Builder: '{field}' is empty on the SimulationManager. Check that Assets/Prefabs was created.");
            }

            return result;
        }

        private static void CreateWall(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
        {
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = name;
            wall.transform.SetParent(parent);
            wall.transform.position = position;
            wall.transform.localScale = scale;
            wall.GetComponent<Renderer>().sharedMaterial = material;
        }

        /// <summary>Scenes must be in Build Settings for SceneManager.LoadScene (Restart buttons).</summary>
        private static void RegisterScenesInBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            scenes.RemoveAll(scene => scene.path == GameScenePath || scene.path == MainScenePath
                                      || scene.path == TestScenePath);
            scenes.Insert(0, new EditorBuildSettingsScene(GameScenePath, true));   // [H4] the game starts first
            scenes.Insert(1, new EditorBuildSettingsScene(MainScenePath, true));
            scenes.Add(new EditorBuildSettingsScene(TestScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
