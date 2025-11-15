using UnityEngine;
using System;
using System.Collections.Generic;
using UnityEditor;

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
}

[Serializable]
public class UIBindToolSettingsDataItem
{
    // 数据的名称
    public string settingsDataName = "Panel";
    [Header("数据存储设置")]
    public string bindDataFolder = "Assets/UIBindData/Panel";
    [Header("代码生成设置")]
      // 生成UI绑定脚本的文件夹路径
    public string generateUIBindScriptFolder = "Assets/Scripts/UIBind/Panel";
    // 生成UI逻辑脚本的文件夹路径
    public string generateUILogicScriptFolder = "Assets/Scripts/UILogic/Panel";
    // 模板文本文件路径
    public string templateTextFilePath = "";
    // 生成脚本时，基类或接口名称（逗号分隔）
    public string baseClassOrInterfaceNames = "";
    // 主脚本中引用的命名空间
    public List<string> mainUsingNamespaces;
    // 是否使用命名空间
    public bool useNamespace = false;
    // 脚本命名空间
    public string scriptNamespace = "";
    // 生成脚本后是否自动打开
    public bool autoOpenGeneratedScripts = false;

    public void UpdateInfoFormTemplateTextFile()
    {
        if(string.IsNullOrEmpty(templateTextFilePath))
        {
            Debug.Log("未配置模版文件");
            return;
        }
        TextAsset textAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(templateTextFilePath);
        if(textAsset)
        {
            //读取类模版的命名空间引用
            UpdateTemplateFileUsingNamespaces(textAsset.text);
            // 读取类模版的基类
            UpdateTemplateFileBaseClassName(textAsset.text);
        }
    }

    /// <summary>
    /// 获取模版文件中基类的名称
    /// </summary>
    /// <param name="templateFileString"></param>
    /// <returns></returns>
    private void UpdateTemplateFileBaseClassName(string templateFileString)
    {
        baseClassOrInterfaceNames = "";
        if(string.IsNullOrEmpty(templateFileString))
            return;
        string[] lines = templateFileString.Split('\n');
        foreach(string line in lines)
        {
            if(line.Contains("<ClassName>") && line.Contains(':'))
            {
                var baseClassName = line.Split(':')[1].Split(',')[0];
                baseClassOrInterfaceNames = baseClassName.Replace(" ","");
            }
        }
    }

    /// <summary>
    /// 获取模版文件中所有的命名空间
    /// </summary>
    /// <param name="templateFileString"></param>
    private void UpdateTemplateFileUsingNamespaces(string templateFileString)
    {
        if(mainUsingNamespaces==null)
            mainUsingNamespaces = new List<string>();
        mainUsingNamespaces.Clear();
        string[] lines = templateFileString.Split('\n');
        foreach(string line in lines)
        {
            if(line.Contains("using") && line.Contains(';'))
            {
                var item = line.Split(';')[0].Replace("using","").Replace(" ","");
                mainUsingNamespaces.Add(item);
            }
        }
    }
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