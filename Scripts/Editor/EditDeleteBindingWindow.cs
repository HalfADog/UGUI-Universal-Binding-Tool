using UnityEditor;
using UnityEngine;

//修改/删除绑定窗口
public class EditDeleteBindingWindow : EditorWindow
{
    private GameObject m_targetObject;
    private UIBindItem m_originalBinding;
    private GameObject m_rootPanel;
    private UIBindToolWindow m_parentWindow;

    // 界面字段
    private string m_variableName;
    private AccessModifier m_accessModifier;
    //private bool m_isEnabled;

    /// <summary>
    /// 显示修改/删除绑定窗口
    /// </summary>
    public static void ShowWindow(GameObject targetObject, UIBindItem binding, GameObject rootPanel,UIBindToolWindow parentWindow)
    {
        string objectName = targetObject != null ? targetObject.name : "无对象";
        string componentType = binding != null ? binding.componentTypeName : "未知组件";

        EditDeleteBindingWindow window = CreateInstance<EditDeleteBindingWindow>();
        window.m_targetObject = targetObject;
        window.m_originalBinding = binding;
        window.m_rootPanel = rootPanel;
        window.m_parentWindow = parentWindow;
        window.titleContent = new GUIContent($"编辑 {objectName} - {componentType}");
        window.InitializeFromBinding();
        window.ShowAuxWindow();
    }

    /// <summary>
    /// 从原始绑定初始化字段
    /// </summary>
    private void InitializeFromBinding()
    {
        if (m_originalBinding == null)
            return;

        m_variableName = m_originalBinding.variableName;
        m_accessModifier = m_originalBinding.accessModifier;
        //m_isEnabled = m_originalBinding.isEnabled;
    }

    void OnGUI()
    {
        if (m_targetObject == null || m_originalBinding == null || m_rootPanel == null)
        {
            EditorGUILayout.HelpBox("Invalid binding data", MessageType.Error);
            if (GUILayout.Button("Close"))
            {
                Close();
            }
            return;
        }

        EditorGUILayout.BeginVertical();

        // 第一层：访问修饰符选择（可修改）
        DrawAccessModifierSelection();
        EditorGUILayout.Space(10);

        // 第二层：组件类型显示（只读）
        DrawComponentTypeDisplay();
        EditorGUILayout.Space(10);

        // 第三层：变量名配置（可修改）
        DrawVariableNameConfiguration();
        EditorGUILayout.Space(10);

        // 第四层：操作按钮
        DrawActionButtons();

        EditorGUILayout.EndVertical();
    }

    // 第一层：访问修饰符选择（可修改）
    private void DrawAccessModifierSelection()
    {
        EditorGUILayout.LabelField("访问修饰符:", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();

        AccessModifier[] accessModifiers = { AccessModifier.Private, AccessModifier.Protected, AccessModifier.Public };

        foreach (var accessModifier in accessModifiers)
        {
            bool isSelected = m_accessModifier == accessModifier;

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
                    m_accessModifier = accessModifier;
                }
            }

            // 恢复原始颜色
            GUI.backgroundColor = Color.white;
        }

        EditorGUILayout.EndHorizontal();
    }

    // 第二层：组件类型显示（只读）
    private void DrawComponentTypeDisplay()
    {
        EditorGUILayout.LabelField("组件类型:", EditorStyles.boldLabel);

        if (m_originalBinding != null)
        {
            // 显示当前绑定的组件类型（只读，无法修改）
            EditorGUILayout.LabelField(m_originalBinding.componentTypeName, EditorStyles.helpBox);
        }
        else
        {
            EditorGUILayout.LabelField("未知组件类型", EditorStyles.helpBox);
        }
    }

    // 第三层：变量名配置（可修改）
    private void DrawVariableNameConfiguration()
    {
        EditorGUILayout.LabelField("变量名配置:", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();

        // 动态Label
        string labelText = "";
        if (m_originalBinding != null)
        {
            labelText = $"{m_accessModifier.ToString().ToLower()} {m_originalBinding.shortTypeName}";
        }

        // 计算Label所需宽度
        GUIStyle labelStyle = EditorStyles.label;
        Vector2 labelSize = labelStyle.CalcSize(new GUIContent(labelText));
        //float labelWidth = Mathf.Max(labelSize.x + 8f, 100f); // 至少100像素宽度，减少边距
        float labelWidth = labelSize.x;
        EditorGUILayout.LabelField(labelText, GUILayout.Width(labelWidth));

        // 输入框
        string newVariableName = EditorGUILayout.TextField(m_variableName);
        if (newVariableName != m_variableName)
        {
            m_variableName = newVariableName.Trim();
        }

        EditorGUILayout.EndHorizontal();

        // 启用状态
        //m_isEnabled = EditorGUILayout.Toggle("启用", m_isEnabled);
    }

    // 第四层：操作按钮
    private void DrawActionButtons()
    {
        EditorGUILayout.BeginHorizontal();

        // 修改按钮
        if (GUILayout.Button("修改"))
        {
            ModifyBinding();
        }

        // 删除按钮
        if (GUILayout.Button("删除"))
        {
            DeleteBinding();
        }

        // 取消按钮
        if (GUILayout.Button("取消"))
        {
            Close();
        }

        EditorGUILayout.EndHorizontal();
    }

    /// <summary>
    /// 修改绑定
    /// </summary>
    private void ModifyBinding()
    {
        if (string.IsNullOrEmpty(m_variableName))
        {
            EditorUtility.DisplayDialog("Error", "Variable name cannot be empty!", "OK");
            return;
        }

        // 获取绑定数据
        UIPanelBindings bindings = m_parentWindow.CurrentBindings;//UIBindDataManager.LoadBindingsForPanel(m_rootPanel);
        if (bindings == null)
        {
            EditorUtility.DisplayDialog("Error", "Cannot load binding data!", "OK");
            return;
        }

        // 验证原始绑定和目标对象
        if (m_originalBinding == null)
        {
            EditorUtility.DisplayDialog("Error", "Original binding is null!", "OK");
            return;
        }

        // 检查变量名是否重复，如果重复则生成唯一名称
        string finalVariableName = GenerateUniqueVariableName(m_variableName, m_targetObject);
        if (finalVariableName != m_variableName)
        {
            int acceptNewName = EditorUtility.DisplayDialogComplex(
                "变量名重复",
                $"变量名 '{m_variableName}' 已存在，是否使用 '{finalVariableName}'？",
                "使用新名称", "取消修改", "手动修改");

            if (acceptNewName == 0) // 使用新名称
            {
                m_variableName = finalVariableName;
            }
            else if (acceptNewName != 2) // 取消修改或手动修改（这里我们直接取消）
            {
                return;
            }
        }

        // 直接更新原始绑定信息
        m_originalBinding.variableName = m_variableName;
        m_originalBinding.accessModifier = m_accessModifier;
        //m_originalBinding.isEnabled = m_isEnabled;

        // 保存绑定数据
        UIBindDataManager.SaveBindings(bindings);

        //EditorUtility.DisplayDialog("Success", "Binding updated successfully!", "OK");
        Close();
    }

    /// <summary>
    /// 删除绑定
    /// </summary>
    private void DeleteBinding()
    {
        bool confirm = EditorUtility.DisplayDialog(
            "Confirm Delete",
            $"Are you sure you want to delete the binding '{m_originalBinding.componentTypeName}' from '{m_targetObject.name}'?",
            "Delete",
            "Cancel"
        );

        if (!confirm)
            return;

        // 获取绑定数据
        UIPanelBindings bindings = m_parentWindow.CurrentBindings;//UIBindDataManager.LoadBindingsForPanel(m_rootPanel);
        if (bindings == null)
        {
            EditorUtility.DisplayDialog("Error", "Cannot load binding data!", "OK");
            return;
        }

        // 删除绑定
        bool removed = bindings.RemoveBinding(m_originalBinding);
        if (removed)
        {
            // 保存绑定数据
            UIBindDataManager.SaveBindings(bindings);
        }
        else
        {
            EditorUtility.DisplayDialog("Error", "Failed to delete binding!", "OK");
        }

        Close();
    }

    /// <summary>
    /// 检查变量名是否已存在
    /// </summary>
    /// <param name="variableName">要检查的变量名</param>
    /// <param name="excludeObject">要排除的对象（用于修改时排除自己）</param>
    /// <returns>如果变量名已存在返回true</returns>
    private bool IsVariableNameExists(string variableName, GameObject excludeObject = null)
    {
        if (string.IsNullOrEmpty(variableName) || m_rootPanel == null)
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