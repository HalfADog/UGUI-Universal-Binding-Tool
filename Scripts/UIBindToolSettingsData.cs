using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// UI绑定工具设置数据
/// 存储工具相关的配置信息
/// </summary>
public class UIBindToolSettingsData : ScriptableObject
{
    //上一次选择的设置数据名称
    [HideInInspector]
    public string lastSelectedSettingsDataName = "Panel";
    public List<UIBindToolSettingsDataItem> settingsDataItems = new List<UIBindToolSettingsDataItem>();

    [Header("全局组件前缀配置")]
    public List<ComponentPrefixMapping> componentPrefixMappings = new List<ComponentPrefixMapping>();

    public UIBindToolSettingsData()
    {
        // 添加默认设置数据项
        settingsDataItems.Add(new UIBindToolSettingsDataItem());

        // 初始化默认的组件前缀映射
        InitializeDefaultPrefixMappings();
    }

    /// <summary>
    /// 初始化默认的组件前缀映射
    /// </summary>
    public void InitializeDefaultPrefixMappings()
    {
        componentPrefixMappings.Clear();

        // 添加常用的UI组件前缀映射
        componentPrefixMappings.Add(new ComponentPrefixMapping("Button", "btn"));
        componentPrefixMappings.Add(new ComponentPrefixMapping("Image", "img"));
        componentPrefixMappings.Add(new ComponentPrefixMapping("Text", "txt"));
        componentPrefixMappings.Add(new ComponentPrefixMapping("TextMeshProUGUI", "tmp"));
        componentPrefixMappings.Add(new ComponentPrefixMapping("InputField", "input"));
        componentPrefixMappings.Add(new ComponentPrefixMapping("Toggle", "toggle"));
        componentPrefixMappings.Add(new ComponentPrefixMapping("Slider", "slider"));
        componentPrefixMappings.Add(new ComponentPrefixMapping("Scrollbar", "scrollbar"));
        componentPrefixMappings.Add(new ComponentPrefixMapping("Dropdown", "dropdown"));
        componentPrefixMappings.Add(new ComponentPrefixMapping("Canvas", "canvas"));
        componentPrefixMappings.Add(new ComponentPrefixMapping("RectTransform", "rect"));
        componentPrefixMappings.Add(new ComponentPrefixMapping("ScrollRect", "scrollRect"));
        componentPrefixMappings.Add(new ComponentPrefixMapping("RawImage", "rawImg"));
        componentPrefixMappings.Add(new ComponentPrefixMapping("Panel", "panel"));
    }

    /// <summary>
    /// 根据组件类型获取前缀
    /// </summary>
    /// <param name="componentTypeName">组件类型名称</param>
    /// <returns>前缀字符串，如果没有找到则返回组件类型的小写形式</returns>
    public string GetComponentPrefix(string componentTypeName)
    {
        if (string.IsNullOrEmpty(componentTypeName))
            return "";

        var mapping = componentPrefixMappings.Find(m => m.componentType == componentTypeName);
        if (mapping != null)
        {
            return mapping.prefix;
        }

        // 如果没有找到映射，返回组件类型的小写形式
        return componentTypeName.ToLower();
    }

    /// <summary>
    /// 添加或更新组件前缀映射
    /// </summary>
    /// <param name="componentTypeName">组件类型名称</param>
    /// <param name="prefix">前缀字符串</param>
    public void SetComponentPrefix(string componentTypeName, string prefix)
    {
        if (string.IsNullOrEmpty(componentTypeName))
            return;

        var existingMapping = componentPrefixMappings.Find(m => m.componentType == componentTypeName);
        if (existingMapping != null)
        {
            existingMapping.prefix = prefix;
        }
        else
        {
            componentPrefixMappings.Add(new ComponentPrefixMapping(componentTypeName, prefix));
        }
    }

    /// <summary>
    /// 删除组件前缀映射
    /// </summary>
    /// <param name="componentTypeName">组件类型名称</param>
    /// <returns>是否成功删除</returns>
    public bool RemoveComponentPrefix(string componentTypeName)
    {
        if (string.IsNullOrEmpty(componentTypeName))
            return false;

        return componentPrefixMappings.RemoveAll(m => m.componentType == componentTypeName) > 0;
    }

    /// <summary>
    /// 获取所有组件前缀映射的副本
    /// </summary>
    /// <returns>组件前缀映射列表的副本</returns>
    public List<ComponentPrefixMapping> GetComponentPrefixMappingsCopy()
    {
        return new List<ComponentPrefixMapping>(componentPrefixMappings);
    }

    /// <summary>
    /// 批量更新组件前缀映射
    /// </summary>
    /// <param name="newMappings">新的映射列表</param>
    public void UpdateComponentPrefixMappings(List<ComponentPrefixMapping> newMappings)
    {
        if (newMappings == null)
            return;

        componentPrefixMappings.Clear();
        componentPrefixMappings.AddRange(newMappings);
    }

    /// <summary>
    /// 检查是否存在指定的组件类型映射
    /// </summary>
    /// <param name="componentTypeName">组件类型名称</param>
    /// <returns>是否存在</returns>
    public bool HasComponentPrefixMapping(string componentTypeName)
    {
        if (string.IsNullOrEmpty(componentTypeName))
            return false;

        return componentPrefixMappings.Exists(m => m.componentType == componentTypeName);
    }

    public UIBindToolSettingsDataItem GetLastSelectedSettingsDataItem()
    {
        return GetSettingsDataItemByName(lastSelectedSettingsDataName);
    }

    public UIBindToolSettingsDataItem GetSettingsDataItemByName(string name)
    {
        return settingsDataItems.Find(item => item.settingsDataName == name);
    }

    public string[] GetAllSettingsDataNames()
    {
        List<string> names = new List<string>();
        foreach (var item in settingsDataItems)
        {
            names.Add(item.settingsDataName);
        }
        return names.ToArray();
    }

    /// <summary>
    /// 获取全局设置数据单例实例
    /// </summary>
    /// <returns>设置数据实例，如果不存在则返回null</returns>
    public static UIBindToolSettingsData GetInstance()
    {
        // 这个方法将由UIBindToolWindow在OnEnable时调用设置
        // 这里返回null，实际使用时通过UIBindToolWindow.SettingsData获取
        return null;
    }
}

[Serializable]
public class UIBindToolSettingsDataItem
{
    // 数据的名称
    public string settingsDataName = "Panel";
    [Header("数据存储设置")]
    public string bindDataFolder = "Assets/UIBindData/Panel";
    [Header("代码生成设置")]
    // Bind与Logic的结合方式
    public ScriptCombinedMethod scriptCombinedMethod = ScriptCombinedMethod.PartialClass;
    // 生成UI绑定脚本的文件夹路径
    public string generateUIBindScriptFolder = "Assets/Scripts/UIBind/Panel";
    // 生成UI逻辑脚本的文件夹路径
    public string generateUILogicScriptFolder = "Assets/Scripts/UILogic/Panel";
    // 生成脚本时，基类或接口名称（逗号分隔）
    public string baseClassOrInterfaceNames = "";
    // 是否使用命名空间
    public bool useNamespace = false;
    // 脚本命名空间
    public string scriptNamespace = "";
    // 生成脚本后是否自动打开
    public bool autoOpenGeneratedScripts = false;
}

/// <summary>
/// 组件前缀映射
/// </summary>
[Serializable]
public class ComponentPrefixMapping
{
    public string componentType;
    public string prefix;

    public ComponentPrefixMapping()
    {
        componentType = "";
        prefix = "";
    }

    public ComponentPrefixMapping(string componentType, string prefix)
    {
        this.componentType = componentType;
        this.prefix = prefix;
    }
}

/// <summary>
/// Bind与Logic的结合方式
/// </summary>
public enum ScriptCombinedMethod
{
    BaseClassInherit,//基类继承
    PartialClass,//部分类
    SingleScript //单脚本
}