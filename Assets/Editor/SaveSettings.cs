using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditorInternal.VR;
using UnityEngine;
using UnityEngine.UIElements;

public class SaveSettings : SettingsProvider
{
    // 第一階層をProjectにします
    private const string SettingPath = "Project/Save Data Settings";
    private Editor _editor;
    [SettingsProvider]
    public static SettingsProvider CreateProvider()
    {
        // SettingsScopeをProjectにします
        return new SaveSettings(SettingPath, SettingsScope.Project, null);
    }

    public override void OnActivate(string searchContext, VisualElement rootElement)
    {
        Editor.CreateCachedEditor(SaveData.Instance, null, ref _editor);
    }

    public SaveSettings(string path, SettingsScope scopes, IEnumerable<string> keywords) : base(path, scopes, keywords)
    {
    }

    public override void OnGUI(string searchContext)
    {
        var instance = SaveData.Instance;
        if (instance == null)
        {
            if (GUILayout.Button("生成する"))
            {
                CreateSettings();
                Editor.CreateCachedEditor(SaveData.Instance, null, ref _editor);
            }

            return;
        }

        _editor.OnInspectorGUI();
    }
    
    private static void CreateSettings()
    {
        var config = ScriptableObject.CreateInstance<SaveData>();
        var parent = "Assets/Resources";
        if (AssetDatabase.IsValidFolder(parent) == false)
        {
            // Resourcesフォルダが無いことを考慮
            AssetDatabase.CreateFolder("Assets", "Resources");
        }

        var assetPath = Path.Combine(parent, Path.ChangeExtension(nameof(SaveData), ".asset"));
        AssetDatabase.CreateAsset(config, assetPath);
    }
}
