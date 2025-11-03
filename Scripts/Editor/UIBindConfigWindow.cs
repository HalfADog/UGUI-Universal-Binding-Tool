using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// 绑定配置窗口
public class UIBindConfigWindow : EditorWindow
{
    private GameObject targetObject; // 被绑定的具体对象
    private GameObject targetObjectInPrefab;
    private GameObject rootPanel; // UI面板的根对象
    private UIBindToolWindow m_parentWindow;

    // 第一层数据：访问修饰符选择
    private AccessModifier selectedAccessModifier = AccessModifier.Private;
    private readonly AccessModifier[] accessModifiers = { AccessModifier.Private, AccessModifier.Protected, AccessModifier.Public };

    // 第二层数据：组件类型选择
    private Type selectedComponentType;
    private readonly List<Type> availableComponentTypes = new();

    // 第三层数据：变量名配置
    private string variableName = "";

    public static void ShowWindow(GameObject target,GameObject targetPrefab ,GameObject panel,UIBindToolWindow parentWindow)
    {
        string objectName = target != null ? target.name : "无对象";
        string panelName = panel != null ? panel.name : "无面板";
        // Debug.Log($"创建绑定配置窗口，对象名称: {objectName}, 面板名称: {panelName}");

        var window = CreateInstance<UIBindConfigWindow>();
        window.targetObject = target;
        window.targetObjectInPrefab = targetPrefab;
        window.rootPanel = panel;
        window.m_parentWindow = parentWindow;
        window.titleContent = new GUIContent($"{objectName} - {panelName}");
        window.ShowAuxWindow();

        // 初始化数据
        window.InitializeData();
    }

    void InitializeData()
    {
        if (targetObject == null) return;

        // 获取对象的所有组件
        var allComponents = targetObject.GetComponents<Component>();
        availableComponentTypes.Clear();

        foreach (var component in allComponents)
        {
            if (component != null)
            {
                availableComponentTypes.Add(component.GetType());
            }
        }

        // 默认选择第一个未绑定的组件
        if (availableComponentTypes.Count > 0)
        {
            // 获取已绑定组件类型
            List<Type> boundComponentTypes = GetBoundComponentTypes();
            // 选择第一个未绑定的组件
            foreach (var componentType in availableComponentTypes)
            {
                if (!boundComponentTypes.Contains(componentType))
                {
                    selectedComponentType = componentType;
                    break;
                }
            }

            // 如果所有组件都已绑定，选择第一个（虽然会被禁用）
            if (selectedComponentType == null)
            {
                selectedComponentType = availableComponentTypes[0];
            }
        }

        // 更新变量名
        UpdateVariableName();
    }

    void UpdateVariableName()
    {
        if (targetObject != null && selectedComponentType != null)
        {
            string componentTypeName = selectedComponentType.Name;
            string objectName = targetObject.name;

            // 生成安全的变量名
            string baseVariableName = GenerateSafeVariableName(componentTypeName, objectName);

            // 检查并生成唯一的变量名
            variableName = GenerateUniqueVariableName(baseVariableName, targetObject);
        }
    }

    /// <summary>
    /// 生成安全的变量名（移除空格和非法字符）
    /// </summary>
    /// <param name="componentTypeName">组件类型名</param>
    /// <param name="objectName">对象名</param>
    /// <returns>安全的变量名</returns>
    private string GenerateSafeVariableName(string componentTypeName, string objectName)
    {
        if (string.IsNullOrEmpty(componentTypeName))
            componentTypeName = "Component";
        if (string.IsNullOrEmpty(objectName))
            objectName = "Object";

        // 获取组件前缀配置
        string componentPrefix = GetComponentPrefix(componentTypeName);

        // 清理对象名称（移除空格和非法字符）
        string cleanObjectName = objectName.Replace(" ", "");
        var validObjectChars = new System.Text.StringBuilder();

        foreach (char c in cleanObjectName)
        {
            if (char.IsLetterOrDigit(c) || c == '_')
                validObjectChars.Append(c);
        }

        cleanObjectName = validObjectChars.ToString();

        // 确保对象名不为空
        if (string.IsNullOrEmpty(cleanObjectName))
            cleanObjectName = "object";

        // 确保对象名首字母大写（驼峰命名）
        if (cleanObjectName.Length > 0)
        {
            cleanObjectName = char.ToUpper(cleanObjectName[0]) + cleanObjectName[1..];
        }

        // 组合变量名：前缀 + 对象名
        string variableName;
        // if (componentPrefix.Equals(cleanObjectName, StringComparison.OrdinalIgnoreCase))
        // {
        //     // 如果前缀和对象名相同，只使用一个（例如：buttonButton -> button）
        //     variableName = componentPrefix;
        // }
        // else if (cleanObjectName.StartsWith(componentPrefix, StringComparison.OrdinalIgnoreCase))
        // {
        //     // 如果对象名以前缀开头，只使用对象名（例如：buttonTestButton -> testButton）
        //     variableName = cleanObjectName;
        // }
        // else
        // {
            // 否则组合前缀和对象名
        variableName = componentPrefix + cleanObjectName;
        //}

        return variableName;
    }

    /// <summary>
    /// 获取组件类型的前缀
    /// </summary>
    /// <param name="componentTypeName">组件类型名称</param>
    /// <returns>前缀字符串</returns>
    private string GetComponentPrefix(string componentTypeName)
    {
        // 获取全局设置数据
        var settingsData = UIBindToolWindow.SettingsData;
        if (settingsData != null)
        {
            // 使用配置的前缀
            return settingsData.GetComponentPrefix(componentTypeName);
        }

        // 如果设置数据不可用，回退到组件类型小写
        return componentTypeName.ToLower();
    }

    void OnGUI()
    {
        EditorGUILayout.BeginVertical();

        // 第一层：访问修饰符选择
        DrawAccessModifierSelection();
        EditorGUILayout.Space(10);

        // 第二层：组件类型选择
        DrawComponentTypeSelection();
        EditorGUILayout.Space(10);

        // 第三层：变量名配置
        DrawVariableNameConfiguration();
        EditorGUILayout.Space(10);

        // 第四层：操作按钮
        DrawActionButtons();

        EditorGUILayout.EndVertical();
    }

    // 第一层：访问修饰符选择
    private void DrawAccessModifierSelection()
    {
        EditorGUILayout.LabelField("访问修饰符:", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();

        foreach (var accessModifier in accessModifiers)
        {
            bool isSelected = selectedAccessModifier == accessModifier;

            // 根据选中状态设置按钮样式
            if (isSelected)
            {
                GUI.backgroundColor = Color.cyan; // 选中时的高亮颜色
            }

            // 绘制按钮
            if (GUILayout.Button(accessModifier.ToString().ToLower()))
            {
                if (!isSelected)
                {
                    selectedAccessModifier = accessModifier;
                    UpdateVariableName();
                }
            }

            // 恢复原始颜色
            GUI.backgroundColor = Color.white;
        }

        EditorGUILayout.EndHorizontal();
    }

    // 第二层：组件类型选择
    private void DrawComponentTypeSelection()
    {
        EditorGUILayout.LabelField("组件类型:", EditorStyles.boldLabel);

        if (availableComponentTypes.Count == 0)
        {
            EditorGUILayout.LabelField("没有可用的组件", EditorStyles.miniLabel);
            return;
        }

        // 获取当前对象的已绑定组件
        List<Type> boundComponentTypes = GetBoundComponentTypes();
        // 每行显示的按钮数量，根据窗口宽度自动调整
        int buttonsPerRow = CalculateButtonsPerRow();

        for (int i = 0; i < availableComponentTypes.Count; i++)
        {
            var componentType = availableComponentTypes[i];
            bool isSelected = selectedComponentType == componentType;
            bool isBound = boundComponentTypes.Contains(componentType);

            // 每行开始一个新的水平布局
            if (i % buttonsPerRow == 0)
            {
                EditorGUILayout.BeginHorizontal();
            }

            // 设置按钮样式
            if (isSelected)
            {
                GUI.backgroundColor = Color.cyan; // 选中时的高亮颜色
            }
            else if (isBound)
            {
                GUI.backgroundColor = Color.gray; // 已绑定的组件显示为灰色
            }

            // 绘制按钮
            GUI.enabled = !isBound; // 已绑定的组件禁用
            if (GUILayout.Button(componentType.Name))
            {
                if (!isSelected)
                {
                    selectedComponentType = componentType;
                    UpdateVariableName();
                }
            }
            GUI.enabled = true; // 恢复启用状态

            // 恢复原始颜色
            GUI.backgroundColor = Color.white;

            // 每行结束水平布局
            if (i % buttonsPerRow == buttonsPerRow - 1 || i == availableComponentTypes.Count - 1)
            {
                EditorGUILayout.EndHorizontal();
            }
        }
    }

    /// <summary>
    /// 获取当前对象已绑定的组件类型列表
    /// </summary>
    private List<Type> GetBoundComponentTypes()
    {
        List<Type> boundTypes = new();

        if (targetObject == null || rootPanel == null)
            return boundTypes;

        // 获取绑定数据
        UIPanelBindings bindings = m_parentWindow.CurrentBindings;//UIBindDataManager.LoadBindingsForPanel(rootPanel);

        // 获取当前对象的所有绑定（包括禁用的）
        List<UIBindItem> objectBindings = bindings?.GetBindingsForObject(targetObject) ?? null;
        if (objectBindings == null)
            return boundTypes;

        // 收集已绑定的组件类型（包括禁用的绑定）
        foreach (var binding in objectBindings)
        {
            Type componentType = binding.GetComponentType();
            if (componentType != null && !boundTypes.Contains(componentType))
            {
                boundTypes.Add(componentType);
            }
        }

        return boundTypes;
    }

    // 计算每行可以显示的按钮数量
    private int CalculateButtonsPerRow()
    {
        if (availableComponentTypes.Count == 0) return 1;

        // 估算每个按钮的宽度
        float buttonWidth = 80f; // 基础宽度
        float padding = 10f; // 按钮间距

        // 获取可用宽度
        float availableWidth = position.width - 40f; // 减去边距

        // 计算每行可以显示的按钮数量
        int buttonsPerRow = Mathf.Max(1, Mathf.FloorToInt(availableWidth / (buttonWidth + padding)));

        return buttonsPerRow;
    }

    // 第三层：变量名配置
    private void DrawVariableNameConfiguration()
    {
        EditorGUILayout.LabelField("变量名配置:", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();

        // 动态Label
        string labelText = "";
        if (selectedComponentType != null)
        {
            labelText = $"{selectedAccessModifier.ToString().ToLower()} {selectedComponentType.Name}";
        }

        // 计算Label所需宽度
        GUIStyle labelStyle = EditorStyles.label;
        Vector2 labelSize = labelStyle.CalcSize(new GUIContent(labelText));
        float labelWidth = labelSize.x;
        EditorGUILayout.LabelField(labelText, GUILayout.Width(labelWidth));

        // 输入框
        string newVariableName = EditorGUILayout.TextField(variableName);
        if (newVariableName != variableName)
        {
            variableName = newVariableName.Trim();
        }

        EditorGUILayout.EndHorizontal();
    }

    // 第四层：操作按钮
    private void DrawActionButtons()
    {
        EditorGUILayout.BeginHorizontal();

        // 添加按钮
        if (GUILayout.Button("添加"))
        {
            AddBinding();
        }

        // 取消按钮
        if (GUILayout.Button("取消"))
        {
            Close();
        }

        EditorGUILayout.EndHorizontal();
    }

    // 添加绑定数据
    private void AddBinding()
    {
        // 验证必要数据
        if (targetObject == null)
        {
            EditorUtility.DisplayDialog("错误", "目标对象为空，无法添加绑定", "确定");
            return;
        }

        if (rootPanel == null)
        {
            EditorUtility.DisplayDialog("错误", "UI面板为空，无法添加绑定", "确定");
            return;
        }

        if (selectedComponentType == null)
        {
            EditorUtility.DisplayDialog("错误", "请选择要绑定的组件类型", "确定");
            return;
        }

        if (string.IsNullOrEmpty(variableName))
        {
            EditorUtility.DisplayDialog("错误", "变量名不能为空", "确定");
            return;
        }

        // 检查变量名是否重复，如果重复则生成唯一名称
        string finalVariableName = GenerateUniqueVariableName(variableName, targetObject);
        if (finalVariableName != variableName)
        {
            Debug.Log($"变量名 '{variableName}' 已存在，自动调整为 '{finalVariableName}'");
            variableName = finalVariableName;
        }

        try
        {
            // 获取或创建绑定数据（使用UI面板的根对象）
            UIPanelBindings bindings = UIBindDataManager.GetOrCreateBindingsForPanel(rootPanel);
            m_parentWindow.CurrentBindings = bindings;
            if (bindings == null)
            {
                EditorUtility.DisplayDialog("错误", "无法创建绑定数据文件", "确定");
                return;
            }

            // 检查是否已存在相同的绑定
            if (bindings.HasBinding(targetObject, selectedComponentType))
            {
                EditorUtility.DisplayDialog("警告",
                    $"对象 {targetObject.name} 的 {selectedComponentType.Name} 组件已经存在绑定",
                    "确定");
                return;
            }

            // 创建新的绑定项
            UIBindItem newBinding = new UIBindItem(targetObject,targetObjectInPrefab,rootPanel,selectedComponentType, selectedAccessModifier, variableName);

            // 添加到绑定数据中
            bindings.AddBinding(newBinding);

            // 保存数据
            UIBindDataManager.SaveBindings(bindings);

            // 关闭窗口
            Close();
        }
        catch (Exception e)
        {
            EditorUtility.DisplayDialog("错误", $"添加绑定时发生错误: {e.Message}", "确定");
            // Debug.LogError($"添加绑定失败: {e}");
        }
    }

    /// <summary>
    /// 检查变量名是否已存在
    /// </summary>
    /// <param name="variableName">要检查的变量名</param>
    /// <param name="excludeObject">要排除的对象（用于修改时排除自己）</param>
    /// <returns>如果变量名已存在返回true</returns>
    private bool IsVariableNameExists(string variableName, GameObject excludeObject = null)
    {
        if (string.IsNullOrEmpty(variableName) || rootPanel == null)
            return false;

        // 获取绑定数据
        UIPanelBindings bindings = m_parentWindow?.CurrentBindings;
        if (bindings == null)
            return false;

        // 检查所有绑定项
        foreach (var binding in bindings.bindings)
        {
            if (binding == null)
                continue;

            // 如果指定了要排除的对象，跳过该对象的绑定
            if (excludeObject != null && binding.GetTargetObject() == excludeObject)
                continue;

            // 检查变量名是否相同（不区分大小写）
            if (string.Equals(binding.variableName, variableName, System.StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    /// <summary>
    /// 生成唯一的变量名
    /// </summary>
    /// <param name="baseVariableName">基础变量名</param>
    /// <param name="excludeObject">要排除的对象</param>
    /// <returns>唯一的变量名</returns>
    private string GenerateUniqueVariableName(string baseVariableName, GameObject excludeObject = null)
    {
        if (string.IsNullOrEmpty(baseVariableName))
            return baseVariableName;

        // 如果基础名称不重复，直接返回
        if (!IsVariableNameExists(baseVariableName, excludeObject))
            return baseVariableName;

        // 生成带数字后缀的唯一名称
        int suffix = 1;
        string uniqueName;

        do
        {
            uniqueName = $"{baseVariableName}{suffix}";
            suffix++;
        } while (IsVariableNameExists(uniqueName, excludeObject));

        return uniqueName;
    }
}