using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System;
using System.Reflection;

public enum LODColorationMode
{
    Performance,
    Quality,
    SafeMode
}

public struct LODColorationSettings
{
    public LODColorationMode Mode;
    public Color[] Colors;
};

public class LODColorationManager : EditorWindow
{
    public Color[] Colors = new Color[] { Color.grey, Color.red, Color.green, Color.blue, Color.cyan, Color.magenta, Color.yellow, Color.black };
    public LODColorationMode Mode = LODColorationMode.Performance;
    public bool Update = false;

    [MenuItem("Tools/LOD Coloration Setup")]
    static void ShowWindow()
    {
        EditorWindow.GetWindow(typeof(LODColorationManager));
        LODColorationManager window = EditorWindow.GetWindow<LODColorationManager>();
        window.titleContent = new GUIContent("LOD Coloration Setup");
        window.minSize = new Vector2(300f, 250f);
    }

    void OnEnable()
    {
        LODColorationSettings settings = LODColoration.LoadData(Colors.Length);
        Colors = settings.Colors;
        Mode = settings.Mode;
    }

    void OnGUI()
    {
        for (int i = 0; i < Colors.Length; i++)
        {
            Colors[i] = EditorGUILayout.ColorField("Color LOD " + i.ToString(), Colors[i]);
        }
        LODColoration.Colors = Colors;
        Mode = (LODColorationMode)EditorGUILayout.EnumPopup("LOD Coloration Mode:", Mode);
        LODColoration.Mode = Mode;
        Update = GUILayout.Toggle(Update, "Update");
        LODColoration.Update = Update;
        if (GUILayout.Button("Apply")) LODColoration.Generate(Mode, Colors);
        if (GUILayout.Button("Reset"))
        {
            Colors = new Color[] { Color.grey, Color.red, Color.green, Color.blue, Color.cyan, Color.magenta, Color.yellow, Color.black };
            Mode = LODColorationMode.Performance;
            LODColoration.SaveData(Mode, Colors);
            string filePath = AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject(this));
            if (System.String.IsNullOrEmpty(filePath))
            {
                filePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                filePath = filePath.Substring(filePath.IndexOf("Assets"));
            }
            AssetDatabase.ImportAsset(filePath, ImportAssetOptions.Default);
        }
        if (GUILayout.Button("Info"))
        {
            EditorUtility.DisplayDialog("LOD Coloration version 1.03 (April 2022)",
                "Author:  \nPrzemyslaw Zaworski", "OK");
        }
    }
}

[InitializeOnLoad]
public class LODColoration
{
    public static Color[] Colors = new Color[] { Color.grey, Color.red, Color.green, Color.blue, Color.cyan, Color.magenta, Color.yellow, Color.black };
    public static LODColorationMode Mode = LODColorationMode.Performance;
    public static bool Update = false;
    static SceneView.CameraMode _CameraMode, _PreviousMode;
    static List<Entity> _Entities;
    static LODGroup[] _Groups;
    static Material _Material;
    static MaterialPropertyBlock _MaterialPropertyBlock;
    static Shader _Shader;

    struct Entity
    {
        public Matrix4x4 Matrix;
        public Mesh Mesh;
        public LODGroup Group;
        public LOD[] Lods;
        public int Level;
        public Transform Transform;
    }

    static SceneView.OnSceneFunc OnSceneGUIDelegate
    {
        get
        {
            FieldInfo fieldInfo = typeof(SceneView).GetField("onSceneGUIDelegate", BindingFlags.Public | BindingFlags.Static);
            return fieldInfo != null ? fieldInfo.GetValue(null) as SceneView.OnSceneFunc : null;
        }

        set
        {
            FieldInfo fieldInfo = typeof(SceneView).GetField("onSceneGUIDelegate", BindingFlags.Public | BindingFlags.Static);
            if (fieldInfo != null) fieldInfo.SetValue(null, value);
        }
    }

    static LODColoration()
    {
        _CameraMode = SceneView.AddCameraMode("LOD Coloration", "View Mode");
        LODColorationSettings settings = LoadData(Colors.Length);
        if (settings.Colors != null) Colors = settings.Colors;
        Mode = settings.Mode;
        Generate(Mode, Colors);
        EditorApplication.hierarchyChanged += OnHierarchyChanged;
        LODColorationCallbacks();
    }

    static void LODColorationCallbacks()
    {
        EventInfo eventInfo = typeof(UnityEditor.SceneView).GetEvent("duringSceneGui", BindingFlags.Public | BindingFlags.Static);
        if (eventInfo != null) // Unity 2019+
        {
            Type type = Type.GetType("LODColoration,LODColoration.dll");
            MethodInfo methodInfo = type.GetMethod("OnSceneGUI", BindingFlags.NonPublic | BindingFlags.Static);
            Delegate handler = Delegate.CreateDelegate(eventInfo.EventHandlerType, null, methodInfo);
            eventInfo.AddEventHandler(null, handler);
        }
        else
        {
            SceneView.OnSceneFunc onSceneFunc = OnSceneGUIDelegate;
            OnSceneGUIDelegate = new SceneView.OnSceneFunc(OnSceneGUI);
        }
    }

    public static void Generate(LODColorationMode mode, Color[] colors)
    {
        SaveData(mode, colors);
        _Shader = Shader.Find("Hidden/LODColoration");
        if (_Shader == null) return;
        _Entities = new List<Entity>();
        _Groups = MonoBehaviour.FindObjectsOfType<LODGroup>();
        if (_Material == null) _Material = new Material(_Shader);
        _MaterialPropertyBlock = new MaterialPropertyBlock();
        switch (mode)
        {
            case LODColorationMode.Performance:
                {
                    PerformanceModeStart();
                    break;
                }
            case LODColorationMode.SafeMode:
                {
                    SafeModeStart();
                    break;
                }
            default: break;
        }
    }

    public static LODColorationSettings LoadData(int length)
    {
        LODColorationSettings settings = new LODColorationSettings();
        Color[] colors = new Color[length];
        if (PlayerPrefs.HasKey("LODColorationMode")) settings.Mode = (LODColorationMode)PlayerPrefs.GetInt("LODColorationMode");
        bool exists = false;
        for (int i = 0; i < colors.Length; i++)
        {
            string key = "LODColorationColor" + i.ToString();
            if (PlayerPrefs.HasKey(key))
            {
                exists = true;
                colors[i] = LODColoration.StringToColor(PlayerPrefs.GetString(key));
            }
        }
        if (exists) settings.Colors = colors;
        return settings;
    }

    public static void SaveData(LODColorationMode mode, Color[] colors)
    {
        PlayerPrefs.SetInt("LODColorationMode", (int)mode);
        for (int i = 0; i < colors.Length; i++)
        {
            string key = "LODColorationColor" + i.ToString();
            PlayerPrefs.SetString(key, LODColoration.ColorToString(colors[i]));
        }
        PlayerPrefs.Save();
    }

    public static Color StringToColor(string hex)
    {
        return (ColorUtility.TryParseHtmlString("#" + hex, out Color result)) ? result : Color.clear;
    }

    public static string ColorToString(Color color)
    {
        return ColorUtility.ToHtmlStringRGBA(color);
    }

    static void PerformanceModeStart()
    {
        for (int i = 0; i < _Groups.Length; i++)
        {
            if (_Groups[i] == null) continue;
            LOD[] lods = _Groups[i].GetLODs();
            for (int k = 0; k < lods.Length; k++)
            {
                Renderer[] renderers = lods[k].renderers;
                for (int j = 0; j < renderers.Length; j++)
                {
                    if (renderers[j] == null) continue;
                    _MaterialPropertyBlock.SetColor("_LODColoration", Colors[k]);
                    renderers[j].SetPropertyBlock(_MaterialPropertyBlock);
                }
            }
        }
    }

    static void QualityModeUpdate(SceneView sceneView)
    {
        if (_Groups == null || _MaterialPropertyBlock == null) return;
        for (int i = 0; i < _Groups.Length; i++)
        {
            if (_Groups[i] == null) continue;
            LOD[] lods = _Groups[i].GetLODs();
            int index = GetCurrentLOD(sceneView.camera, _Groups[i], lods);
            Renderer[] renderers = lods[index].renderers;
            for (int j = 0; j < renderers.Length; j++)
            {
                if (index < 0 || index > (Colors.Length - 1) || renderers[j] == null) continue;
                _MaterialPropertyBlock.SetColor("_LODColoration", Colors[index]);
                renderers[j].SetPropertyBlock(_MaterialPropertyBlock);
            }
        }
    }

    static void SafeModeStart()
    {
        for (int i = 0; i < _Groups.Length; i++)
        {
            if (_Groups[i] == null) continue;
            LOD[] lods = _Groups[i].GetLODs();
            for (int k = 0; k < lods.Length; k++)
            {
                Renderer[] renderers = lods[k].renderers;
                for (int j = 0; j < renderers.Length; j++)
                {
                    if (renderers[j] == null) continue;
                    Renderer renderer = renderers[j];
                    MeshFilter filter = renderer.gameObject.GetComponent<MeshFilter>();
                    if (filter == null) continue;
                    Entity entity = new Entity();
                    Vector3 position = renderer.gameObject.transform.position;
                    Quaternion rotation = renderer.gameObject.transform.rotation;
                    Vector3 scale = renderer.gameObject.transform.lossyScale;
                    entity.Matrix = Matrix4x4.TRS(position, rotation, scale);
                    entity.Mesh = filter.sharedMesh;
                    entity.Group = _Groups[i];
                    entity.Lods = lods;
                    entity.Level = k;
                    entity.Transform = renderer.gameObject.transform;
                    _Entities.Add(entity);
                }
            }
        }
    }

    static void SafeModeUpdate(SceneView sceneView)
    {
        if (_Entities == null || _MaterialPropertyBlock == null) return;
        for (int i = 0; i < _Entities.Count; i++)
        {
            if (_Entities[i].Group == null) continue;
            if (_Entities[i].Transform.hasChanged)
            {
                Entity entity = _Entities[i];
                Transform transform = _Entities[i].Transform;
                entity.Matrix = Matrix4x4.TRS(transform.position, transform.rotation, transform.lossyScale);
                _Entities[i] = entity;
                _Entities[i].Transform.hasChanged = false;
            }
            int index = GetCurrentLOD(sceneView.camera, _Entities[i].Group, _Entities[i].Lods);
            if (_Entities[i].Level == index)
            {
                _MaterialPropertyBlock.SetColor("_LODColoration", Colors[index]);
                Graphics.DrawMesh(_Entities[i].Mesh, _Entities[i].Matrix, _Material, 0, sceneView.camera, 0, _MaterialPropertyBlock);
            }
        }
    }

    static int GetCurrentLOD(Camera camera, LODGroup lodGroup, LOD[] lods)
    {
        float distance = (lodGroup.transform.TransformPoint(lodGroup.localReferencePoint) - camera.transform.position).magnitude;
        Vector3 scale = lodGroup.transform.lossyScale;
        float size = Mathf.Max(Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y)), Mathf.Abs(scale.z)) * lodGroup.size;
        float halfAngle = Mathf.Tan(Mathf.Deg2Rad * camera.fieldOfView * 0.5f);
        float relativeHeight = size * 0.5f / ((distance / QualitySettings.lodBias) * halfAngle);
        if (camera.orthographic) relativeHeight = size * 0.5f / camera.orthographicSize;
        int level = lodGroup.lodCount - 1;
        for (int i = 0; i < lods.Length; i++)
        {
            if (relativeHeight >= lods[i].screenRelativeTransitionHeight)
            {
                level = i;
                break;
            }
        }
        return level;
    }

    static void OnHierarchyChanged()
    {
        if (EditorApplication.isPlaying == false && Update)
            Generate(Mode, Colors);
    }

    static void OnSceneGUI(SceneView sceneView)
    {
        if (_PreviousMode == null) _PreviousMode = sceneView.cameraMode;
        if (sceneView.cameraMode != _PreviousMode)
        {
            _PreviousMode = sceneView.cameraMode;
            if (sceneView.cameraMode == _CameraMode)
            {
                _Shader = Shader.Find("Hidden/LODColoration");
                if (_Shader == null) Debug.LogError("LOD Coloration shader not found !");
                sceneView.SetSceneViewShaderReplace(_Shader, null);
            }
            else
            {
                sceneView.SetSceneViewShaderReplace(null, null);
            }
        }
        if (sceneView.cameraMode == _CameraMode)
        {
            if (Mode == LODColorationMode.Quality) QualityModeUpdate(sceneView);
            if (Mode == LODColorationMode.SafeMode) SafeModeUpdate(sceneView);
            sceneView.Repaint();
        }
    }
}