using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>Authors the approved mockup assets through the live Editor. Safe to run again.</summary>
    public static class MockupArtInstaller
    {
        private const string Root = "Assets/SqadSort/Content/Art/MockupV1";
        private static readonly string[] Colors = { "red", "orange", "yellow", "green", "darkgreen", "cyan", "blue", "purple", "pink", "brown" };

        [MenuItem("SqadSort/Art/Apply Approved Mockup")]
        public static void Apply()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Apply in Edit Mode.");
            Directory.CreateDirectory(Root + "/Materials");
            Directory.CreateDirectory(Root + "/Prefabs");
            AssetDatabase.Refresh();
            foreach (string oldPath in Directory.GetFiles(Root + "/Prefabs", "*.prefab"))
            {
                string path = oldPath.Replace('\\', '/');
                if (Path.GetFileName(path).Contains("-"))
                {
                    string error = AssetDatabase.MoveAsset(path, Root + "/Prefabs/" + Path.GetFileName(path).Replace('-', '_'));
                    if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException(error);
                }
            }
            foreach (string path in Directory.GetFiles(Root, "*.fbx"))
            {
                var importer = (ModelImporter)AssetImporter.GetAtPath(path.Replace('\\', '/'));
                importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                importer.importCameras = false;
                importer.importLights = false;
                importer.importAnimation = false;
                importer.SaveAndReimport();
                BuildModel(Path.GetFileNameWithoutExtension(path));
            }
            BuildModel("character-green", "character-darkgreen");
            var look = new SerializedObject(AssetDatabase.LoadMainAssetAtPath("Assets/SqadSort/Content/UI/Look/LookLibrary.asset"));
            var models = look.FindProperty("_buddyModels");
            models.arraySize = Colors.Length;
            for (int i = 0; i < Colors.Length; i++) models.GetArrayElementAtIndex(i).objectReferenceValue = Model("character-" + Colors[i]);
            look.FindProperty("_hiddenBuddyModel").objectReferenceValue = Model("character-hidden");
            look.ApplyModifiedPropertiesWithoutUndo();
            ConfigureBoard();
            ConfigureHud();
            var registrar = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType("ClassicSpins.PrototypeFramework.Editor.Addressables.PrefabAddressableRegistrar"))
                .First(t => t != null);
            registrar.GetMethod("SyncAllIfPresent", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic).Invoke(null, null);
            AssetDatabase.SaveAssets();
            Debug.Log("Approved mockup installed: real 3D buddies, modular trays, courtyard and HUD.");
        }

        private static GameObject Model(string name) => AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/" + name.Replace('-', '_') + ".prefab");

        private static void BuildModel(string source, string name = null)
        {
            name = name ?? source;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/" + source + ".fbx"));
            try
            {
                foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
                {
                    var mats = r.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++)
                    {
                        string key = mats[i].name;
                        Color color = mats[i].color;
                        if (name == "character-darkgreen" && key == "M_green") { key = "M_darkgreen"; ColorUtility.TryParseHtmlString("#239448", out color); }
                        string path = Root + "/Materials/" + key + ".mat";
                        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                        if (mat == null)
                        {
                            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                            AssetDatabase.CreateAsset(mat, path);
                        }
                        mat.SetColor("_BaseColor", color);
                        mat.SetFloat("_Smoothness", 0.3f);
                        mat.SetFloat("_Metallic", 0f);
                        mats[i] = mat;
                    }
                    r.sharedMaterials = mats;
                }
                PrefabUtility.SaveAsPrefabAsset(go, Root + "/Prefabs/" + name.Replace('-', '_') + ".prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        private static void ConfigureBoard()
        {
            string path = "Assets/SqadSort/Views/Board/Prefabs/Board.prefab";
            var go = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var view = go.GetComponents<MonoBehaviour>().First(c => c.GetType().Name == "BoardView");
                var so = new SerializedObject(view);
                so.FindProperty("_queueModel").objectReferenceValue = Model("queue-four");
                so.FindProperty("_cellModel").objectReferenceValue = Model("waiting-cell");
                so.FindProperty("_edgeModel").objectReferenceValue = Model("waiting-edge");
                Set(so, "_tiltDegrees", 32f); Set(so, "_buddyUpright", 0.65f);
                Set(so, "_buddyLift", 26f); Set(so, "_characterScale", 105f);
                Set(so, "_modelCellSize", 1.15f); Set(so, "_rimWidth", 13f);
                Set(so, "_rimHeight", 23f); Set(so, "_trayWall", 11f);
                Set(so, "_marginTop", 315f); Set(so, "_marginBottom", 315f);
                Set(so, "_marginSide", 65f); Set(so, "_maxFitScale", 1.65f);
                var ground = (MeshRenderer)so.FindProperty("_ground").objectReferenceValue;
                string texPath = Root + "/courtyard-background.png";
                var importer = (TextureImporter)AssetImporter.GetAtPath(texPath);
                importer.textureType = TextureImporterType.Default; importer.mipmapEnabled = false; importer.npotScale = TextureImporterNPOTScale.None;
                importer.maxTextureSize = 2048; importer.wrapMode = TextureWrapMode.Clamp; importer.SaveAndReimport();
                string matPath = Root + "/Materials/Courtyard.mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Unlit")); AssetDatabase.CreateAsset(mat, matPath); }
                mat.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath); mat.color = Color.white;
                ground.sharedMaterial = mat; so.FindProperty("_groundColor").colorValue = Color.white;
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(go, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(go); }
        }
        private static void Set(SerializedObject so, string name, float value) => so.FindProperty(name).floatValue = value;

        private static void ConfigureHud()
        {
            string texPath = Root + "/ui-atlas.png";
            var importer = (TextureImporter)AssetImporter.GetAtPath(texPath);
            importer.textureType = TextureImporterType.Default; importer.mipmapEnabled = false; importer.npotScale = TextureImporterNPOTScale.None;
            importer.alphaIsTransparency = true; importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.Uncompressed; importer.SaveAndReimport();
            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            string[] names = { "undo", "shuffle", "add-slot", "restart", "settings", "level-plate", "count-badge" };
            Rect[] rects = { new Rect(53, 1334, 346, 334), new Rect(485, 1334, 347, 334), new Rect(52, 901, 350, 344), new Rect(486, 901, 338, 342), new Rect(57, 481, 340, 344), new Rect(35, 141, 531, 190), new Rect(618, 129, 210, 211) };
            Sprite[] sprites = new Sprite[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                string spritePath = Root + "/" + names[i] + ".asset";
                Sprite old = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
                Sprite fresh = Sprite.Create(tex, rects[i], new Vector2(0.5f, 0.5f), 1f);
                fresh.name = names[i];
                if (old != null)
                {
                    EditorUtility.CopySerialized(fresh, old);
                    UnityEngine.Object.DestroyImmediate(fresh);
                    EditorUtility.SetDirty(old);
                    sprites[i] = old;
                }
                else { sprites[i] = fresh; AssetDatabase.CreateAsset(fresh, spritePath); }
            }
            string path = "Assets/SqadSort/Content/UI/Hud/Prefabs/HudWidget.prefab";
            var go = PrefabUtility.LoadPrefabContents(path);
            try
            {
                ImageAt(go, "Top Bar/Restart", sprites[3], new Vector2(132,132));
                ImageAt(go, "Top Bar/Settings", sprites[4], new Vector2(132,132));
                ImageAt(go, "Top Bar/Level Plate", sprites[5], new Vector2(390,140));
                ((RectTransform)go.transform.Find("Top Bar")).anchoredPosition = new Vector2(0,-115);
                ((RectTransform)go.transform.Find("Booster Bar")).anchoredPosition = new Vector2(0,175);
                string[] buttons = { "Undo", "Shuffle", "Add Container" };
                for (int i=0; i<3; i++)
                {
                    string bp = "Booster Bar/" + buttons[i];
                    ImageAt(go,bp,sprites[i],new Vector2(225,225));
                    ImageAt(go,bp+"/Badge",sprites[6],new Vector2(66,66));
                    ((RectTransform)go.transform.Find(bp+"/Badge")).anchoredPosition = new Vector2(75,76);
                    var count=go.transform.Find(bp+"/Badge/Count") as RectTransform;
                    count.sizeDelta=new Vector2(66,66);
                    SetText(count, 38);
                }
                var level=go.transform.Find("Top Bar/Level Plate/Level") as RectTransform;
                level.sizeDelta=new Vector2(350,100);SetText(level,52);
                PrefabUtility.SaveAsPrefabAsset(go,path);
            }
            finally { PrefabUtility.UnloadPrefabContents(go); }
        }
        private static void SetText(Transform t, float size)
        {
            Component text=t.GetComponent("TMPro.TextMeshProUGUI");
            var so=new SerializedObject(text);so.FindProperty("m_fontSize").floatValue=size;
            so.FindProperty("m_fontSizeBase").floatValue=size;
            so.FindProperty("m_enableAutoSizing").boolValue=false;
            so.FindProperty("m_fontStyle").intValue=1;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        private static void ImageAt(GameObject root,string path,Sprite sprite,Vector2 size)
        {
            var t=(RectTransform)root.transform.Find(path);var image=t.GetComponent("UnityEngine.UI.Image");
            var so=new SerializedObject(image);so.FindProperty("m_Sprite").objectReferenceValue=sprite;
            so.FindProperty("m_Color").colorValue=Color.white;so.FindProperty("m_Type").intValue=0;
            so.FindProperty("m_PreserveAspect").boolValue=true;so.ApplyModifiedPropertiesWithoutUndo();t.sizeDelta=size;
        }
    }
}
