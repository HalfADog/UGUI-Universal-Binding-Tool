using System;

/// <summary>
/// 组件信息类
/// </summary>
public class ComponentInfo
{
    public string name;
    public Type type;
    public bool isDefaultComponent; // 是否为默认组件（GameObject、RectTransform）
    public bool canBind; // 是否可以绑定（视图开关状态）
    public AccessModifier accessModifier; // 访问修饰符
    public string customName; // 自定义名称

    public ComponentInfo(string name, Type type, bool isDefaultComponent)
    {
        this.name = name;
        this.type = type;
        this.isDefaultComponent = isDefaultComponent;
        this.canBind = false; // 默认不绑定
        this.accessModifier = AccessModifier.Private; // 默认私有
        this.customName = ""; // 初始为空，在需要时生成
    }

    /// <summary>
    /// 生成默认的自定义名称（组件名_对象名）
    /// </summary>
    public string GenerateDefaultName(string objectName)
    {
        return $"{name}_{objectName}";
    }
}