using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// UI面板绑定数据容器
/// </summary>
public class UIPanelBindings : ScriptableObject
{
    [Header("面板信息")]
    public string panelPrefabGUID; // 面板的GUID
    public int panelInstanceID; // 面板的实例ID
    public string panelPathInScene; // 面板的路径
    public string panelName; // 面板名称
    public DateTime createdTime; // 创建时间
    public DateTime lastModifiedTime; // 最后修改时间

    [Header("绑定配置")]
    public List<UIBindItem> bindings = new List<UIBindItem>();

    /// <summary>
    /// 添加新的绑定项
    /// </summary>
    public void AddBinding(UIBindItem binding)
    {
        if (binding != null && binding.GetTargetObject() != null)
        {
            bindings.Add(binding);
            lastModifiedTime = DateTime.Now;
        }
    }

    /// <summary>
    /// 移除绑定项
    /// </summary>
    public bool RemoveBinding(UIBindItem binding)
    {
        bool removed = bindings.Remove(binding);
        if (removed)
        {
            lastModifiedTime = DateTime.Now;
        }
        return removed;
    }

    /// <summary>
    /// 移除指定对象的绑定
    /// </summary>
    public int RemoveBindingsForObject(GameObject targetObject)
    {
        if (targetObject == null)
            return 0;

        int count = bindings.RemoveAll(b => b != null && b.GetTargetObject() == targetObject);
        if (count > 0)
        {
            lastModifiedTime = DateTime.Now;
        }
        return count;
    }

    /// <summary>
    /// 获取指定对象的绑定项
    /// </summary>
    public List<UIBindItem> GetBindingsForObject(GameObject targetObject)
    {
        if (targetObject == null)
            return new List<UIBindItem>();

        return bindings.FindAll(b => b != null && b.GetTargetObject() == targetObject);
    }

    /// <summary>
    /// 获取指定对象的指定组件类型的绑定项
    /// </summary>
    public UIBindItem GetBindingForObjectAndComponent(GameObject targetObject, Type componentType)
    {
        if (targetObject == null || componentType == null)
            return null;

        string componentTypeName = componentType.FullName;
        if (string.IsNullOrEmpty(componentTypeName))
            return null;

        return bindings.Find(b =>
            b != null &&
            b.GetTargetObject() == targetObject &&
            b.componentTypeName == componentTypeName);
    }

    /// <summary>
    /// 更新绑定项
    /// </summary>
    public void UpdateBinding(UIBindItem binding)
    {
        if (binding == null)
            return;

        int index = bindings.FindIndex(b =>
            b != null &&
            b.GetTargetObject() == binding.GetTargetObject() &&
            b.componentTypeName == binding.componentTypeName);
        if (index >= 0)
        {
            bindings[index] = binding;
            lastModifiedTime = DateTime.Now;
        }
    }

    /// <summary>
    /// 检查是否已存在相同的绑定
    /// </summary>
    public bool HasBinding(GameObject targetObject, Type componentType)
    {
        if (targetObject == null || componentType == null)
            return false;

        string componentTypeName = componentType.FullName;
        if (string.IsNullOrEmpty(componentTypeName))
            return false;

        return bindings.Exists(b =>
            b != null &&
            b.GetTargetObject() == targetObject &&
            b.componentTypeName == componentTypeName);
    }

    /// <summary>
    /// 获取所有启用的绑定项
    /// </summary>
    public List<UIBindItem> GetEnabledBindings()
    {
        return bindings.FindAll(b => b.isEnabled);
    }

    /// <summary>
    /// 获取绑定总数
    /// </summary>
    public int GetTotalBindings()
    {
        return bindings.Count;
    }

    /// <summary>
    /// 获取启用的绑定数量
    /// </summary>
    public int GetEnabledBindingsCount()
    {
        return bindings.FindAll(b => b.isEnabled).Count;
    }

    /// <summary>
    /// 初始化数据
    /// </summary>
    public void Initialize(GameObject panel)
    {
        if (panel != null)
        {
            //获取GUID
            var prefabAsset = PrefabUtility.GetCorrespondingObjectFromSource(panel);
            string prefabPath = AssetDatabase.GetAssetPath(prefabAsset);
            panelPrefabGUID = AssetDatabase.GUIDFromAssetPath(prefabPath).ToString();
            panelInstanceID = panel.GetInstanceID();
            panelPathInScene = GetGameObjectFullPath(panel);
            panelName = panel.name;
        }
        else
        {
            panelInstanceID = 0;
            panelPathInScene = "";
            panelName = "";
        }

        createdTime = DateTime.Now;
        lastModifiedTime = DateTime.Now;
        bindings.Clear();
    }

    /// <summary>
    /// 获取面板对象（运行时获取）
    /// </summary>
    public GameObject GetPanelObject()
    {
        if (panelInstanceID == 0)
            return null;

        // 首先尝试通过实例ID获取
        var obj = EditorUtility.InstanceIDToObject(panelInstanceID) as GameObject;
        if (obj != null)
        {
            return obj;
        }

        // 如果实例ID失败，尝试通过路径获取
        if (!string.IsNullOrEmpty(panelPathInScene))
        {
            return GameObject.Find(panelPathInScene);
        }

        return null;
    }

    /// <summary>
    /// 获取GameObject的完整路径
    /// </summary>
    public static string GetGameObjectFullPath(GameObject obj)
    {
        if (obj == null)
            return "";
        string path = obj.name;
        Transform parent = obj.transform.parent;

        while (parent != null)
        {
            path = parent.name + "/" + path;
            parent = parent.parent;
        }

        return path;
    }

    /// <summary>
    /// 获取GameObject相对于根对象的路径
    /// </summary>
    public static string GetGameObjectRelativePath(GameObject root, GameObject obj)
    {
        if (root == null || obj == null)
            return "";

        if (root == obj)
            return "";

        string path = obj.name;
        Transform parent = obj.transform.parent;

        while (parent != null && parent.gameObject != root)
        {
            path = parent.name + "/" + path;
            parent = parent.parent;
        }

        if (parent == null)
        {
            // obj不是root的子对象
            return "";
        }

        return path;
    }

    /// <summary>
    /// 验证面板是否仍然有效
    /// </summary>
    public bool IsValidPanel()
    {
        var panel = GetPanelObject();
        return panel != null && panel.name == panelName;
    }
}