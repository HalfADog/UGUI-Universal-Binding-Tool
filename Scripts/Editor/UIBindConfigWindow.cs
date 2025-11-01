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
            if (component != null && component.GetType() != typeof(Transform))
            {
                // 如果启用了组件过滤，只添加允许的组件类型
                // if (UIBindToolWindow.allowedComponentTypes.Contains(component.GetType()))
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
            variableName = GenerateSafeVariableName(componentTypeName, objectName);
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

        // 直接删除空格
        string cleanComponentName = componentTypeName.Replace(" ", "");
        string cleanObjectName = objectName.Replace(" ", "");

        // 移除其他非法字符，只保留字母、数字和下划线
        var validComponentChars = new System.Text.StringBuilder();
        var validObjectChars = new System.Text.StringBuilder();

        foreach (char c in cleanComponentName)
        {
            if (char.IsLetterOrDigit(c) || c == '_')
                validComponentChars.Append(c);
        }

        foreach (char c in cleanObjectName)
        {
            if (char.IsLetterOrDigit(c) || c == '_')
                validObjectChars.Append(c);
        }

        cleanComponentName = validComponentChars.ToString();
        cleanObjectName = validObjectChars.ToString();

        // 如果结果为空或以数字开头，添加前缀
        if (string.IsNullOrEmpty(cleanComponentName) || (cleanComponentName.Length > 0 && char.IsDigit(cleanComponentName[0])))
        {
            cleanComponentName = "comp_" + cleanComponentName;
        }

        if (string.IsNullOrEmpty(cleanObjectName) || (cleanObjectName.Length > 0 && char.IsDigit(cleanObjectName[0])))
        {
            cleanObjectName = "obj_" + cleanObjectName;
        }

        // 确保不为空
        if (string.IsNullOrEmpty(cleanComponentName))
            cleanComponentName = "component";
        if (string.IsNullOrEmpty(cleanObjectName))
            cleanObjectName = "object";

        // 将组件名转为小写开头
        if (cleanComponentName.Length > 0)
        {
            cleanComponentName = char.ToLower(cleanComponentName[0]) + cleanComponentName[1..];
        }

        // 确保对象名首字母大写（驼峰命名）
        if (cleanObjectName.Length > 0)
        {
            cleanObjectName = char.ToUpper(cleanObjectName[0]) + cleanObjectName[1..];
        }

        // 组合变量名，避免重复
        string variableName;
        if (cleanComponentName.Equals(cleanObjectName, StringComparison.OrdinalIgnoreCase))
        {
            // 如果组件名和对象名相同，只使用一个（例如：buttonButton -> button）
            variableName = cleanComponentName;
        }
        else if (cleanObjectName.StartsWith(cleanComponentName, StringComparison.OrdinalIgnoreCase))
        {
            // 如果对象名以组件名开头，只使用对象名（例如：buttonTestButton -> testButton）
            variableName = cleanObjectName;
        }
        else
        {
            // 否则组合两个名称
            variableName = cleanComponentName + cleanObjectName;
        }
        return variableName;
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
        if (bindings == null)
            return boundTypes;

        // 获取当前对象的所有绑定（包括禁用的）
        List<UIBindItem> objectBindings = bindings.GetBindingsForObject(targetObject);
        if (objectBindings == null)
            return boundTypes;

        // 收集已绑定的组件类型（包括禁用的绑定）
        foreach (var binding in objectBindings)
        {
            Type componentType = binding.GetComponentType();
            if (componentType != null)
            {
                // 检查目标对象是否仍然有效
                var boundObject = binding.GetTargetObject();
                if (boundObject == targetObject)
                {
                    // 使用FullName进行匹配，而不是Type对象比较
                    Type matchingType = null;
                    foreach (var availableType in availableComponentTypes)
                    {
                        if (availableType.FullName == componentType.FullName)
                        {
                            matchingType = availableType;
                            break;
                        }
                    }

                    if (matchingType != null && !boundTypes.Contains(matchingType))
                    {
                        boundTypes.Add(matchingType);
                    }
                }
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
        //float labelWidth = Mathf.Max(labelSize.x + 5f, 100f); // 至少100像素宽度，减少边距
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

            // Debug.Log($"成功添加绑定: {selectedAccessModifier} {selectedComponentType.Name} {variableName} 对象: {targetObject.name} 面板: {rootPanel.name}");

            // 关闭窗口
            Close();
        }
        catch (Exception e)
        {
            EditorUtility.DisplayDialog("错误", $"添加绑定时发生错误: {e.Message}", "确定");
            // Debug.LogError($"添加绑定失败: {e}");
        }
    }
}