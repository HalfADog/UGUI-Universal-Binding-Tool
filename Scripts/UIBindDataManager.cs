using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// UI绑定数据管理器
/// 负责创建、加载和管理UIPanelBindings ScriptableObject
/// </summary>
public class UIBindDataManager
{
    private static UIBindToolSettingsData s_SettingsDataContainer;
    private static UIBindToolSettingsDataItem s_CurrentSettingsItem;
    private static readonly string BIND_DATA_FOLDER = "Assets/UIBindData"; // 默认值，会被设置覆盖
    private static readonly string BIND_DATA_EXTENSION = ".asset";

    /// <summary>
    /// 设置设置数据容器
    /// </summary>
    public static void SetSettingsDataContainer(UIBindToolSettingsData settingsDataContainer)
    {
        s_SettingsDataContainer = settingsDataContainer;
        // 设置默认选中的设置项
        if (s_SettingsDataContainer != null)
        {
            SetCurrentSettingsItem(s_SettingsDataContainer.GetLastSelectedSettingsDataItem());
        }
    }

    /// <summary>
    /// 设置当前使用的设置项
    /// </summary>
    public static void SetCurrentSettingsItem(UIBindToolSettingsDataItem settingsItem)
    {
        s_CurrentSettingsItem = settingsItem;
        // 更新最后选择的设置项名称
        if (s_SettingsDataContainer != null && settingsItem != null)
        {
            s_SettingsDataContainer.lastSelectedSettingsDataName = settingsItem.settingsDataName;
        }
    }

    /// <summary>
    /// 获取当前设置项
    /// </summary>
    public static UIBindToolSettingsDataItem GetCurrentSettingsItem()
    {
        return s_CurrentSettingsItem;
    }

    /// <summary>
    /// 获取绑定数据文件夹路径
    /// </summary>
    private static string GetBindDataFolder()
    {
        return s_CurrentSettingsItem != null && !string.IsNullOrEmpty(s_CurrentSettingsItem.bindDataFolder)
            ? s_CurrentSettingsItem.bindDataFolder
            : BIND_DATA_FOLDER;
    }

    /// <summary>
    /// 获取或创建指定面板的绑定数据
    /// </summary>
    /// <param name="targetPanel">目标UI面板</param>
    /// <returns>绑定数据ScriptableObject</returns>
    public static UIPanelBindings GetOrCreateBindingsForPanel(GameObject targetPanel)
    {
        if (targetPanel == null)
        {
            Debug.LogWarning("目标面板为空，无法创建绑定数据");
            return null;
        }

        // 确保文件夹存在
        EnsureFolderExists();

        // 检查是否已存在绑定数据
        UIPanelBindings existingBindings = LoadBindingsForPanel(targetPanel);
        if (existingBindings != null)
        {
            return existingBindings;
        }

        // 创建新的绑定数据
        UIPanelBindings newBindings = ScriptableObject.CreateInstance<UIPanelBindings>();
        newBindings.Initialize(targetPanel);

        // 保存到Assets文件夹
        string assetPath = GetBindingsAssetPath(targetPanel);
        AssetDatabase.CreateAsset(newBindings, assetPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"已创建新的绑定数据: {assetPath}");
        return newBindings;
    }

    /// <summary>
    /// 加载指定面板的绑定数据
    /// </summary>
    /// <param name="targetPanel">目标UI面板</param>
    /// <returns>绑定数据ScriptableObject，如果不存在则返回null</returns>
    public static UIPanelBindings LoadBindingsForPanel(GameObject targetPanel)
    {
        if (targetPanel == null)
        {
            Debug.LogWarning("目标面板为空，无法加载绑定数据");
            return null;
        }

        string assetPath = GetBindingsAssetPath(targetPanel);
        if (File.Exists(assetPath))
        {
            return AssetDatabase.LoadAssetAtPath<UIPanelBindings>(assetPath);
        }

        return null;
    }

    /// <summary>
    /// 通过路径加载绑定数据
    /// </summary>
    /// <param name="assetPath">绑定数据文件路径</param>
    /// <returns>绑定数据ScriptableObject，如果不存在则返回null</returns>
    public static UIPanelBindings LoadBindings(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath))
        {
            Debug.LogWarning("绑定数据路径为空，无法加载");
            return null;
        }

        if (File.Exists(assetPath))
        {
            return AssetDatabase.LoadAssetAtPath<UIPanelBindings>(assetPath);
        }
        else
        {
            Debug.LogWarning($"绑定数据文件不存在: {assetPath}");
            return null;
        }
    }

    /// <summary>
    /// 保存绑定数据
    /// </summary>
    /// <param name="bindings">要保存的绑定数据</param>
    public static void SaveBindings(UIPanelBindings bindings)
    {
        if (bindings == null)
        {
            return;
        }

        bindings.lastModifiedTime = System.DateTime.Now;
        EditorUtility.SetDirty(bindings);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    /// <summary>
    /// 删除指定面板的绑定数据
    /// </summary>
    /// <param name="targetPanel">目标UI面板</param>
    /// <returns>是否成功删除</returns>
    public static bool DeleteBindingsForPanel(GameObject targetPanel)
    {
        if (targetPanel == null)
        {
            return false;
        }

        string assetPath = GetBindingsAssetPath(targetPanel);
        if (File.Exists(assetPath))
        {
            AssetDatabase.DeleteAsset(assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"已删除面板 {targetPanel.name} 的绑定数据");
            return true;
        }

        return false;
    }

    /// <summary>
    /// 获取所有面板的绑定数据
    /// </summary>
    /// <returns>所有UIPanelBindings资源</returns>
    public static UIPanelBindings[] GetAllBindings()
    {
        var allDataGUIDs = AssetDatabase.FindAssets("", new[] { GetBindDataFolder() });
        UIPanelBindings[] bindings = new UIPanelBindings[allDataGUIDs.Length];
        for (int i = 0; i < allDataGUIDs.Length; i++)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(allDataGUIDs[i]);
            bindings[i] = AssetDatabase.LoadAssetAtPath<UIPanelBindings>(assetPath);
            Debug.Log($"找到绑定数据: {assetPath}");
        }
        return bindings;
    }

    /// <summary>
    /// 确保绑定数据文件夹存在
    /// </summary>
    private static void EnsureFolderExists()
    {
        string folderPath = GetBindDataFolder();
        if (!Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
            AssetDatabase.Refresh();
        }
    }

    /// <summary>
    /// 获取绑定数据的Asset路径
    /// </summary>
    /// <param name="targetPanel">目标UI面板</param>
    /// <returns>Asset路径</returns>
    public static string GetBindingsAssetPath(GameObject targetPanel)
    {
        //获取GUID
        string prefabGuid = GetPrefabGUID(targetPanel).ToString();
        //获取所有绑定
        var allBindings = GetAllBindings();
        //查找对应GUID的绑定
        foreach (var binding in allBindings)
        {
            if (binding.panelPrefabGUID == prefabGuid)
            {
                return AssetDatabase.GetAssetPath(binding);
            }
        }
        // 使用面板名称作为文件名，确保唯一性
        string fileName = targetPanel.name;
        string safeFileName = string.Join("_", fileName.Split(Path.GetInvalidFileNameChars()));
        return $"{GetBindDataFolder()}/{safeFileName}{BIND_DATA_EXTENSION}";
    }

    /// <summary>
    /// 获取绑定数据的Asset路径
    /// </summary>
    /// <param name="bindings">绑定数据对象</param>
    /// <returns>Asset路径</returns>
    public static string GetBindingsAssetPath(UIPanelBindings bindings)
    {
        if (bindings == null)
            return "";

        // 如果已有GUID，尝试通过GUID查找
        if (!string.IsNullOrEmpty(bindings.panelPrefabGUID))
        {
            string path = AssetDatabase.GetAssetPath(bindings);
            if (!string.IsNullOrEmpty(path))
                return path;
        }

        // 使用面板名称作为文件名，确保唯一性
        string fileName = bindings.panelName;
        string safeFileName = string.Join("_", fileName.Split(Path.GetInvalidFileNameChars()));
        return $"{GetBindDataFolder()}/{safeFileName}{BIND_DATA_EXTENSION}";
    }

    /// <summary>
    /// 根据当前的实例更新绑定数据中记录的实例相关信息
    /// </summary>
    /// <param name="bindings"></param>
    /// <param name="panelInstance"></param>
    public static void UpdateBindingInstanceData(UIPanelBindings bindings, GameObject panelInstance)
    {
        if (bindings == null || panelInstance == null)
            return;
        //更新面板实例数据
        bindings.panelInstanceID = panelInstance.GetInstanceID();
        bindings.panelPathInScene = UIPanelBindings.GetGameObjectFullPath(panelInstance);
        bindings.panelName = panelInstance.name;
        //获取panel的Prefab
        var prefabAsset = PrefabUtility.GetCorrespondingObjectFromSource(panelInstance);
        //记录Prefab子对象的FileID与相对于Prefab的路径 以Dictionary形式存储
        Dictionary<long, string> prefabObjectData = new Dictionary<long, string>();
        long rootFileID = 0;
        if (prefabAsset != null)
        {
            foreach (Transform child in prefabAsset.GetComponentsInChildren<Transform>(true))
            {
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(child.gameObject, out string guid, out long fileID);
                string relativePath = UIPanelBindings.GetGameObjectRelativePath(prefabAsset, child.gameObject);
                prefabObjectData[fileID] = relativePath;
            }
            //包括Prefab根对象
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(prefabAsset, out string rootGuid, out rootFileID);
            prefabObjectData[rootFileID] = bindings.panelPathInScene;
        }
        //更新绑定数据
        foreach (var bindItem in bindings.bindings)
        {
            string path = prefabObjectData[bindItem.targetObjectFileID];
            bindItem.targetObjectFullPathInScene = bindings.panelPathInScene;
            if (bindItem.targetObjectFileID != rootFileID)
                bindItem.targetObjectFullPathInScene += (string.IsNullOrEmpty(path) ? "" : "/" + path);
            bindItem.targetObjectName = path.Substring(path.LastIndexOf('/') + 1);
            bindItem.targetInstanceID = GameObject.Find(bindItem.targetObjectFullPathInScene)?.GetInstanceID() ?? 0;
        }
        //保存修改
        SaveBindings(bindings);
    }

    public static GUID GetPrefabGUID(GameObject panel)
    {
        if (panel == null)
            return default;
        var prefabAsset = PrefabUtility.GetCorrespondingObjectFromSource(panel);
        string prefabPath = AssetDatabase.GetAssetPath(prefabAsset);
        return AssetDatabase.GUIDFromAssetPath(prefabPath);
    }
}