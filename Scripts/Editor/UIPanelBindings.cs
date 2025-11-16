using System;
using System.Collections.Generic;
using System.Linq;
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
            // 记录撤销操作
            UndoHelper.RecordBindings(this, UndoHelper.GetUndoName("Add Binding", binding.GetTargetObject(), binding.GetComponentType()));

            bindings.Add(binding);
            lastModifiedTime = DateTime.Now;

            // Debug.Log($"[UIPanelBindings] 添加绑定: {binding.variableName}");
        }
    }

    /// <summary>
    /// 移除绑定项
    /// </summary>
    public bool RemoveBinding(UIBindItem binding)
    {
        if (binding == null)
            return false;

        // 记录撤销操作
        UndoHelper.RecordBindings(this, UndoHelper.GetUndoName("Remove Binding", binding.GetTargetObject(), binding.GetComponentType()));

        bool removed = bindings.Remove(binding);
        if (removed)
        {
            lastModifiedTime = DateTime.Now;
            // Debug.Log($"[UIPanelBindings] 移除绑定: {binding.variableName}");
        }

        return removed;
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
    /// 更新绑定项
    /// <param name="binding">新的绑定数据</param>
    public void UpdateBinding(UIBindItem binding)
    {
        if (binding == null || binding.GetTargetObject() == null)
            return;

        // 查找索引
        int index = bindings.FindIndex(b =>
            b != null &&
            b.GetTargetObject() == binding.GetTargetObject() &&
            b.componentTypeName == binding.componentTypeName);

        if (index >= 0)
        {
            var oldBinding = bindings[index];

            // 构建描述性操作名称
            string changedProperties = string.Empty;
            if (oldBinding.variableName != binding.variableName)
                changedProperties += $"变量名: {oldBinding.variableName} → {binding.variableName}";
            if (oldBinding.accessModifier != binding.accessModifier)
            {
                if (!string.IsNullOrEmpty(changedProperties))
                    changedProperties += "，";
                changedProperties += $"访问修饰符: {oldBinding.accessModifier} → {binding.accessModifier}";
            }

            string operationName = string.IsNullOrEmpty(changedProperties)
                ? UndoHelper.GetUndoName("Modify Binding", binding.GetTargetObject(), binding.GetComponentType())
                : $"Modify Binding: {changedProperties}";

            // 记录撤销操作
            UndoHelper.RecordBindings(this, operationName);

            // 执行更新
            bindings[index] = binding;
            lastModifiedTime = DateTime.Now;

            // Debug.Log($"[UIPanelBindings] 更新绑定: {binding.variableName}");
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
    /// 初始化数据
    /// </summary>
    public void Initialize(GameObject panel)
    {
        if (panel != null)
        {
            //获取GUID
            var prefabAsset = UIBindDataManager.GetPrefabSourceRoot(panel);
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
}