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

    public UIBindToolSettingsData()
    {
        // 添加默认设置数据项
        settingsDataItems.Add(new UIBindToolSettingsDataItem());
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
/// Bind与Logic的结合方式
/// </summary>
public enum ScriptCombinedMethod
{
    BaseClassInherit,//基类继承
    PartialClass,//部分类
    SingleScript //单脚本  
}