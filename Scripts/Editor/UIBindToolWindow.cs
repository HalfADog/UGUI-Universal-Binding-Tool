using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

public class UIBindToolWindow : EditorWindow
{
    private const string MENU_PATH = "GameObject/UI Binding &e";
    private GameObject bindTarget;
    private GameObject bindTargetPrefab;
    private List<KeyValuePair<int, (GameObject obj,GameObject objPrefab)>> bindableObjects = new List<KeyValuePair<int, (GameObject,GameObject)>>();

    public GameObject BindTargetPrefab => bindTargetPrefab;

    [SerializeField]
    private TreeViewState m_TreeViewState;
    [SerializeField]
    private MultiColumnHeaderState m_HeaderState;
    private UIBindToolTreeView m_UIBindToolTreeView;
    private GUID m_PrefabGuid; // 绑定的Prefab的GUID
    private UIPanelBindings m_CurrentBindings = null; // 当前面板的绑定数据

    // 预览模式相关
    private bool m_IsPreviewMode = false; // 是否处于预览模式
    private Vector2 m_CodeScrollPosition = Vector2.zero; // 代码滚动位置
    private string m_PreviewFileName = "UIBinding.cs"; // 预览文件名
    private float m_CodeFontSize = 12f; // 代码字体大小

    // 设置模式相关
    private bool m_IsSettingsMode = false; // 是否处于设置模式
    private int m_SelectedSettingsIndex = 0; // 当前选中的设置索引
    private string[] m_SettingsNames = new string[0]; // 设置名称数组
    private Vector2 m_SettingsScrollPosition = Vector2.zero; // 设置界面滚动位置
    private static UIBindToolSettingsData s_SettingsData; // 设置数据单例

    public GUID PrefabGuid => m_PrefabGuid;
    public static UIBindToolSettingsData SettingsData => s_SettingsData;
    public UIPanelBindings CurrentBindings
    {
        get => m_CurrentBindings;
        set
        {
            if (m_CurrentBindings != value)
            {
                m_CurrentBindings = value;

                // 如果在预览模式，更新预览文件名
                if (m_IsPreviewMode)
                {
                    m_PreviewFileName = GetPreviewFileName();
                }

                Repaint();
            }
        }
    }

    [MenuItem(MENU_PATH, false, 1000)]
    public static void ExecuteUIBind()
    {
        UIBindToolWindow win = GetWindow<UIBindToolWindow>("UI Bind Tool");
        win.Show();
    }

    [MenuItem(MENU_PATH, true)]
    public static bool ValidateUIBind()
    {
        return Selection.activeGameObject != null
               && Selection.activeGameObject.GetComponent<RectTransform>() != null;
    }

    /// <summary>
    /// 初始化数据
    /// </summary>
    private void InitData(GameObject target)
    {
        if (target == null) return;

        // 验证Panel是否为Prefab实例
        var validation = ValidatePanelForBinding(target);
        if (!validation.IsValid)
        {
            EditorUtility.DisplayDialog("绑定错误", validation.Message, "确定");
            bindTarget = null;
            return;
        }

        bindTarget = target;
        bindableObjects.Clear();
        bindTargetPrefab = PrefabUtility.GetCorrespondingObjectFromSource(bindTarget);
        GetBindableObjectsWithStructure(bindTarget, bindTargetPrefab);
        //获取Prefab的GUID
        m_PrefabGuid = UIBindDataManager.GetPrefabGUID(bindTarget);
        if (m_PrefabGuid != default) Debug.Log($"Prefab GUID: {m_PrefabGuid}");

        //尝试加载绑定数据
        UpdateCurrentBindingsData();
    }

    private void UpdateCurrentBindingsData()
    {
        m_CurrentBindings = null;
        var allBindingDatas = UIBindDataManager.GetAllBindings();
        foreach (var bindingData in allBindingDatas)
        {
            if (bindingData.panelPrefabGUID == m_PrefabGuid.ToString())
            {
                m_CurrentBindings = bindingData;
                break;
            }
        }
    }

    /// <summary>
    /// 验证Panel是否可以进行绑定
    /// </summary>
    private PanelValidationResult ValidatePanelForBinding(GameObject panel)
    {
        if (panel == null)
            return new PanelValidationResult(false, "Panel对象为空");

        // 检查是否为Prefab实例
        if (!IsPrefabInstance(panel))
        {
            return new PanelValidationResult(false,
                "选中的对象不是Prefab实例。\n\n请先将UI Panel保存为Prefab，然后再进行绑定。\n\n操作步骤：\n1. 在Hierarchy中选中Panel\n2. 将其拖拽到Project窗口创建Prefab\n3. 使用场景中的Prefab实例进行绑定");
        }

        return new PanelValidationResult(true, "Panel验证通过");
    }

    /// <summary>
    /// 检查对象是否为Prefab实例
    /// </summary>
    private bool IsPrefabInstance(GameObject obj)
    {
        return PrefabUtility.GetPrefabInstanceStatus(obj) == PrefabInstanceStatus.Connected;
    }

    /// <summary>
    /// 获取或创建设置数据
    /// </summary>
    private static void GetOrCreateSettingsData()
    {
        if (s_SettingsData != null)
            return;

        const string SETTINGS_FOLDER = "Assets/Settings";
        const string SETTINGS_ASSET_PATH = SETTINGS_FOLDER + "/UIBindToolSettingsData.asset";

        // 确保Settings文件夹存在
        if (!Directory.Exists(SETTINGS_FOLDER))
        {
            Directory.CreateDirectory(SETTINGS_FOLDER);
            AssetDatabase.Refresh();
        }

        // 尝试加载现有设置
        s_SettingsData = AssetDatabase.LoadAssetAtPath<UIBindToolSettingsData>(SETTINGS_ASSET_PATH);

        // 如果不存在，创建新的设置数据
        if (s_SettingsData == null)
        {
            s_SettingsData = ScriptableObject.CreateInstance<UIBindToolSettingsData>();
            AssetDatabase.CreateAsset(s_SettingsData, SETTINGS_ASSET_PATH);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }

    
    void OnEnable()
    {
        // 加载设置数据
        GetOrCreateSettingsData();

        // 设置数据管理器的设置数据
        if (s_SettingsData != null)
        {
            //初始化设置选项
            UpdateSettingsOptions();
            //默认选择第一个设置数据项
            var lastSelectedItem = s_SettingsData.GetLastSelectedSettingsDataItem();
            m_SelectedSettingsIndex = System.Array.IndexOf(m_SettingsNames, lastSelectedItem.settingsDataName);
            if (m_SelectedSettingsIndex < 0) m_SelectedSettingsIndex = 0;
            UIBindDataManager.SetSettingsDataContainer(s_SettingsData);
        }

        InitData(Selection.activeGameObject);

        // 同步绑定数据与UI面板状态
        if (bindTarget != null)
        {
            UIBindDataManager.UpdateBindingInstanceData(CurrentBindings, bindTarget);
        }

        // 确保 TreeViewState 存在
        if (m_TreeViewState == null)
            m_TreeViewState = new TreeViewState();
        // --- 创建 Header 和 HeaderState ---

        var headerState = CreateHeaderState();

        if (MultiColumnHeaderState.CanOverwriteSerializedFields(m_HeaderState, headerState))
        {
            MultiColumnHeaderState.OverwriteSerializedFields(m_HeaderState, headerState);
        }
        m_HeaderState = headerState;

        var multiColumnHeader = new MultiColumnHeader(headerState);

        // 在 TreeView 构造时传入 Header 和组件名称列表
        m_UIBindToolTreeView = new(m_TreeViewState, multiColumnHeader, this, bindableObjects, bindTarget);

        //默认展开
        ExpandAll();
    }

    void OnDisable()
    {
        // 保存数据
        UIBindDataManager.SaveBindings(CurrentBindings);
    }
    
    void OnGUI()
    {
        // 绘制ToolBar
        DrawToolbar();

        // 根据模式绘制不同内容
        if (m_IsPreviewMode)
        {
            // 绘制代码预览区域，留出ToolBar的空间
            Rect rect = new Rect(0, 30, position.width, position.height - 30);
            DrawCodePreviewArea(rect);
        }
        else if (m_IsSettingsMode)
        {
            // 绘制设置界面，留出ToolBar的空间
            Rect rect = new Rect(0, 30, position.width, position.height - 30);
            DrawSettingsArea(rect);
        }
        else
        {
            // 更新列宽以适应窗口大小
            UpdateColumnWidths();

            // 绘制TreeView，留出ToolBar的空间
            Rect rect = new Rect(0, 30, position.width, position.height - 30);
            m_UIBindToolTreeView.OnGUI(rect);
        }
    }

    /// <summary>
    /// 更新列宽以适应窗口大小
    /// </summary>
    private void UpdateColumnWidths()
    {
        if (m_HeaderState == null || m_HeaderState.columns.Length < 3)
            return;

        // Name列宽度固定
        const float nameColumnWidth = 200f;

        // Bind列宽度固定
        const float bindColumnWidth = 60f;

        // Bind Components列宽度 = 窗口宽度 - 其他列宽度 - 边距
        float availableWidth = position.width;
        float bindComponentsWidth = availableWidth - nameColumnWidth - bindColumnWidth -16f; // 60f为边距和滚动条空间

        // 限制最小和最大宽度
        bindComponentsWidth = Mathf.Clamp(bindComponentsWidth, 200f, 1000f);

        // 只有在宽度变化时才更新，避免不必要的重绘
        if (Math.Abs(m_HeaderState.columns[2].width - bindComponentsWidth) > 1f)
        {
            m_HeaderState.columns[0].width = nameColumnWidth; // Name列
            m_HeaderState.columns[1].width = bindColumnWidth; // Bind列
            m_HeaderState.columns[2].width = bindComponentsWidth; // Bind Components列

            // 标记需要重绘
            Repaint();
        }
    }

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        if (m_IsPreviewMode)
        {
            DrawPreviewToolbarContent();
        }
        else if (m_IsSettingsMode)
        {
            DrawSettingsToolbarContent();
        }
        else
        {
            DrawMainToolbarContent();
        }

        EditorGUILayout.EndHorizontal();
    }

    /// <summary>
    /// 绘制主ToolBar内容
    /// </summary>
    private void DrawMainToolbarContent()
    {
        // 左侧显示UIPanel名称
        string panelName = GetDisplayPanelName();
        string currentSettingsName = m_SelectedSettingsIndex >= 0 && m_SelectedSettingsIndex < m_SettingsNames.Length
            ? m_SettingsNames[m_SelectedSettingsIndex]
            : "Default";
        EditorGUILayout.LabelField($"{currentSettingsName} Mode : {panelName}", EditorStyles.toolbarButton, GUILayout.ExpandWidth(false));

        // 右侧弹性空间
        GUILayout.FlexibleSpace();
        // 设置下拉选择框
        if (m_SettingsNames.Length > 0)
        {
            EditorGUILayout.LabelField("Mode:", GUILayout.Width(40));
            int newIndex = EditorGUILayout.Popup(m_SelectedSettingsIndex, m_SettingsNames, GUILayout.Width(100));
            if (newIndex != m_SelectedSettingsIndex)
            {
                m_SelectedSettingsIndex = newIndex;
                OnSettingsSelectionChanged();
                UpdateCurrentBindingsData();
                Repaint();
            }
        }

        // 分隔符
        GUILayout.Space(5);

        // 折叠全部按钮
        if (GUILayout.Button("折叠", EditorStyles.toolbarButton))
        {
            CollapseAll();
        }

        // 展开全部按钮
        if (GUILayout.Button("展开", EditorStyles.toolbarButton))
        {
            ExpandAll();
        }

        // 分隔符
        GUILayout.Space(5);

        // 预览按钮
        if (GUILayout.Button("预览", EditorStyles.toolbarButton, GUILayout.Width(50)))
        {
            TogglePreviewMode();
        }

        // 设置按钮（齿轮图标）
        if (GUILayout.Button(EditorGUIUtility.IconContent("Settings"), EditorStyles.toolbarButton, GUILayout.Width(30)))
        {
            ToggleSettingsMode();
        }
    }

    /// <summary>
    /// 绘制预览模式ToolBar内容
    /// </summary>
    private void DrawPreviewToolbarContent()
    {
        // 左侧显示文件名
        EditorGUILayout.LabelField(m_PreviewFileName, EditorStyles.toolbarButton, GUILayout.ExpandWidth(false));

        // 弹性空间
        GUILayout.FlexibleSpace();

        // 字体大小调整输入框
        EditorGUILayout.LabelField("字体大小:", GUILayout.Width(60));
        string fontSizeText = EditorGUILayout.TextField(m_CodeFontSize.ToString("F0"), GUILayout.Width(32));

        // 尝试解析新的字体大小
        if (float.TryParse(fontSizeText, out float newFontSize) &&
            newFontSize >= 8f && newFontSize <= 48f &&
            Mathf.Abs(newFontSize - m_CodeFontSize) > 0.1f)
        {
            m_CodeFontSize = newFontSize;
            Repaint();
        }

        // 分隔符
        GUILayout.Space(5);

        // 生成按钮
        if (GUILayout.Button("生成", EditorStyles.toolbarButton, GUILayout.Width(50)))
        {
            GenerateScripts();
        }

        // 分隔符
        GUILayout.Space(5);

        // 复制按钮
        if (GUILayout.Button(EditorGUIUtility.IconContent("Clipboard"), EditorStyles.toolbarButton, GUILayout.Width(30)))
        {
            CopyCodeToClipboard();
        }

        // 关闭按钮
        if (GUILayout.Button("关闭", EditorStyles.toolbarButton, GUILayout.Width(50)))
        {
            TogglePreviewMode();
        }
    }

    /// <summary>
    /// 绘制设置模式ToolBar内容
    /// </summary>
    private void DrawSettingsToolbarContent()
    {
        string currentSettingsName = m_SelectedSettingsIndex >= 0 && m_SelectedSettingsIndex < m_SettingsNames.Length
            ? m_SettingsNames[m_SelectedSettingsIndex]
            : "Default";
        // 左侧显示"设置"
        EditorGUILayout.LabelField($"{currentSettingsName} Mode:设置", EditorStyles.toolbarButton, GUILayout.ExpandWidth(false));

        // 弹性空间
        GUILayout.FlexibleSpace();

        // 保存按钮
        if (GUILayout.Button("保存", EditorStyles.toolbarButton, GUILayout.Width(50)))
        {
            SaveCurrentSettings();
        }

        // 关闭按钮
        if (GUILayout.Button("关闭", EditorStyles.toolbarButton, GUILayout.Width(50)))
        {
            ToggleSettingsMode();
        }
    }

    private void CollapseAll()
    {
        // 清空展开ID列表，实现全部折叠
        m_UIBindToolTreeView.state.expandedIDs.Clear();
        m_UIBindToolTreeView.Reload();
    }

    private void ExpandAll()
    {
        // 收集所有数据中的有子项的节点ID
        var allIds = new List<int>();

        // 遍历所有数据，而不是只看当前可见的行
        for (int i = 0; i < bindableObjects.Count; i++)
        {
            int depth = bindableObjects[i].Key;
            // 检查是否有更深层级的子对象
            bool hasChildren = false;
            for (int j = i + 1; j < bindableObjects.Count; j++)
            {
                if (bindableObjects[j].Key > depth)
                {
                    hasChildren = true;
                    break;
                }
                else if (bindableObjects[j].Key <= depth)
                {
                    break;
                }
            }

            if (hasChildren)
            {
                allIds.Add(i + 1); // TreeViewItem的ID是index+1
            }
        }

        m_UIBindToolTreeView.state.expandedIDs = allIds;
        m_UIBindToolTreeView.Reload();
    }

    void GetBindableObjectsWithStructure(GameObject root,GameObject rootPrefab, int depth = 1)
    {
        if (depth == 1)
        {
            bindableObjects.Add(new KeyValuePair<int, (GameObject,GameObject)>(depth - 1, (root,rootPrefab)));
        }
        for (int i = 0; i < root.transform.childCount; i++)
        {
            GameObject child = root.transform.GetChild(i).gameObject;
            GameObject childPrefab = rootPrefab.transform.GetChild(i).gameObject;
            bindableObjects.Add(new KeyValuePair<int, (GameObject,GameObject)>(depth, (child,childPrefab)));
            if (child.transform.childCount > 0) GetBindableObjectsWithStructure(child, childPrefab ,depth + 1);
        }
    }

    private MultiColumnHeaderState CreateHeaderState()
    {
        var columns = new List<MultiColumnHeaderState.Column>();

        // 创建Name列（冻结）
        columns.Add(new MultiColumnHeaderState.Column
        {
            headerContent = new GUIContent("Name"),
            headerTextAlignment = TextAlignment.Center,
            canSort = false,
            width = 200,
            minWidth = 200,
            maxWidth = 600,
            autoResize = true, // 允许自动调整大小
            allowToggleVisibility = false, // Name列不允许隐藏
        });

        // 创建Bind列（冻结）
        columns.Add(new MultiColumnHeaderState.Column
        {
            headerContent = new GUIContent("Bind"),
            headerTextAlignment = TextAlignment.Center,
            canSort = false,
            width = 60, // 固定宽度
            minWidth = 60,
            maxWidth = 60, // 最小和最大宽度相同，固定宽度
            autoResize = false, // 不允许自动调整大小
            allowToggleVisibility = false, // Bind列不允许隐藏
        });

        // 创建Bind Components列
        columns.Add(new MultiColumnHeaderState.Column
        {
            headerContent = new GUIContent("Bind Components"),
            headerTextAlignment = TextAlignment.Center,
            canSort = false,
            width = 300, // 初始宽度，会在UpdateColumnWidths中动态调整
            minWidth = 200,
            maxWidth = 1000, // 增加最大宽度限制
            autoResize = false, // 禁用自动调整，使用我们的手动调整
            allowToggleVisibility = true, // 允许隐藏
        });

        var headerState = new MultiColumnHeaderState(columns.ToArray());
        return headerState;
    }

    /// <summary>
    /// 切换预览模式
    /// </summary>
    private void TogglePreviewMode()
    {
        m_IsPreviewMode = !m_IsPreviewMode;

        if (m_IsPreviewMode)
        {
            // 进入预览模式时，确保退出其他模式
            m_IsSettingsMode = false;

            // 更新文件名
            m_PreviewFileName = GetPreviewFileName();
        }

        Repaint();
    }

    /// <summary>
    /// 切换设置模式
    /// </summary>
    private void ToggleSettingsMode()
    {
        m_IsSettingsMode = !m_IsSettingsMode;

        if (m_IsSettingsMode)
        {
            // 进入设置模式时，确保退出其他模式
            m_IsPreviewMode = false;
        }

        Repaint();
    }

    /// <summary>
    /// 绘制代码预览区域
    /// </summary>
    private void DrawCodePreviewArea(Rect previewArea)
    {
        // 获取示例代码
        string sampleCode = GetSampleCode();

        // 创建代码文本样式，使用动态字体大小
        GUIStyle codeStyle = new(EditorStyles.textArea)
        {
            font = EditorStyles.label.font,
            fontSize = (int)m_CodeFontSize,
            wordWrap = false,
            richText = false,
            alignment = TextAnchor.UpperLeft,
            padding = new(10, 10, 10, 10),
            normal = { textColor = new(0.8f, 0.8f, 0.8f, 1f) }
        };

        // 计算文本区域大小 - 确保宽度与窗口宽度匹配
        Vector2 textSize = codeStyle.CalcSize(new GUIContent(sampleCode));
        float contentWidth = Mathf.Max(textSize.x, previewArea.width - 30); // 确保最小宽度，留出滚动条空间
        float contentHeight = textSize.y;

        // 开始滚动视图，覆盖整个预览区域
        m_CodeScrollPosition = GUI.BeginScrollView(previewArea, m_CodeScrollPosition,
            new(0, 0, contentWidth + 20, contentHeight + 20));

        // 绘制代码文本，宽度匹配内容区域
        Rect textRect = new(10, 10, contentWidth, contentHeight);

        // 使用TextField替代Label，设置为只读但可选择复制
        string tempCode = sampleCode; // 临时变量用于TextField
        GUI.SetNextControlName("CodePreviewTextField");
        GUI.TextField(textRect, tempCode, codeStyle);

        // 结束滚动视图
        GUI.EndScrollView();

        // 如果代码预览文本框获得焦点且用户按Ctrl+C，则复制到剪贴板
        if (GUI.GetNameOfFocusedControl() == "CodePreviewTextField")
        {
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.C && Event.current.control)
            {
                // 复制代码到剪贴板
                EditorGUIUtility.systemCopyBuffer = sampleCode;
                Event.current.Use(); // 阻止默认行为

                // 显示复制成功的提示
                ShowNotification("代码已复制到剪贴板");
            }
        }
    }

    /// <summary>
    /// 显示通知
    /// </summary>
    private void ShowNotification(string message)
    {
        // 使用Unity编辑器的通知系统
        this.ShowNotification(new GUIContent(message), 3f);
    }

    /// <summary>
    /// 绘制设置界面
    /// </summary>
    private void DrawSettingsArea(Rect settingsArea)
    {
        // 获取当前设置项
        var currentSettings = UIBindDataManager.GetCurrentSettingsItem();
        if (currentSettings == null)
        {
            // 显示无设置提示
            GUIStyle labelStyle = new(EditorStyles.centeredGreyMiniLabel)
            {
                fontSize = 14,
                normal = { textColor = Color.white },
                alignment = TextAnchor.MiddleCenter
            };
            EditorGUI.LabelField(settingsArea, "无可用设置项\n请先在设置下拉框中选择配置", labelStyle);
            return;
        }

        // 开始滚动区域
        Rect rect = new Rect(settingsArea.x + 10, settingsArea.y, settingsArea.width - 20, settingsArea.height - 10);
        EditorGUI.DrawRect(rect, new Color(0.2f, 0.2f, 0.2f, 1f));

        GUILayout.BeginArea(rect);
        m_SettingsScrollPosition = EditorGUILayout.BeginScrollView(m_SettingsScrollPosition);

        EditorGUILayout.Space(10);
        // 绘制各个设置字段
        DrawSettingsName(currentSettings);
        EditorGUILayout.Space(10);

        DrawBindDataFolder(currentSettings);
        EditorGUILayout.Space(10);

        DrawGenerateUIBindScriptFolder(currentSettings);
        EditorGUILayout.Space(10);

        DrawScriptCombinedMethod(currentSettings);
        EditorGUILayout.Space(10);

        DrawBaseClassOrInterfaceNames(currentSettings);
        EditorGUILayout.Space(10);

        DrawNamespaceSettings(currentSettings);
        EditorGUILayout.Space(10);

        DrawAutoOpenGeneratedScripts(currentSettings);

        EditorGUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    /// <summary>
    /// 生成UI绑定脚本
    /// </summary>
    private void GenerateScripts()
    {
        if (CurrentBindings == null)
        {
            EditorUtility.DisplayDialog("错误", "未选择面板或没有绑定数据", "确定");
            return;
        }

        if (CurrentBindings.GetEnabledBindingsCount() == 0)
        {
            EditorUtility.DisplayDialog("错误", "没有启用的绑定项，请先添加UI组件绑定", "确定");
            return;
        }

        var result = UIBindScriptGenerator.GenerateScripts(CurrentBindings);

        if (!result.success)
        {
            EditorUtility.DisplayDialog("生成失败", result.errorMessage, "确定");
        }
        else
        {
            // 更新TreeView显示
            m_UIBindToolTreeView?.Reload();

            // 检查是否需要自动打开生成的脚本
            var currentSettings = UIBindDataManager.GetCurrentSettingsItem();
            if (currentSettings != null && currentSettings.autoOpenGeneratedScripts)
            {
                string filePath = result.bindingScriptPath;
                if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
                {
                    // 打开生成的脚本文件
                    UnityEditor.EditorUtility.OpenWithDefaultApp(filePath);
                    Debug.Log($"已自动打开生成的脚本: {filePath}");
                }
            }
        }
    }

    /// <summary>
    /// 获取预览代码
    /// </summary>
    private string GetSampleCode()
    {
        if (CurrentBindings == null || CurrentBindings.GetEnabledBindingsCount() == 0)
        {
            return "// 未选择面板或没有绑定数据\n// 请先在主界面中添加UI组件绑定";
        }

        try
        {
            var config = UIBindScriptGenerator.LoadGenerationConfig();

            if (config == null)
            {
                return "// 无法加载生成配置\n// 请检查设置配置";
            }

            // 生成绑定代码预览
            string fullCode = UIBindScriptGenerator.GenerateBindingCode(CurrentBindings, config);

            return fullCode;
        }
        catch (Exception e)
        {
            return $"// 生成预览代码时发生错误: {e.Message}\n// 请检查配置和绑定数据";
        }
    }

    /// <summary>
    /// 获取显示的面板名称
    /// </summary>
    private string GetDisplayPanelName()
    {
        if (CurrentBindings != null && !string.IsNullOrEmpty(CurrentBindings.panelName))
        {
            return CurrentBindings.panelName;
        }
        else if (bindTarget != null && !string.IsNullOrEmpty(bindTarget.name))
        {
            return bindTarget.name;
        }
        else
        {
            return "未选择面板";
        }
    }

    /// <summary>
    /// 更新设置选项列表
    /// </summary>
    private void UpdateSettingsOptions()
    {
        if (s_SettingsData != null)
        {
            m_SettingsNames = s_SettingsData.GetAllSettingsDataNames();
        }
        else
        {
            m_SettingsNames = new string[0];
        }
    }

    /// <summary>
    /// 设置选择改变时的处理
    /// </summary>
    private void OnSettingsSelectionChanged()
    {
        if (s_SettingsData != null && m_SelectedSettingsIndex >= 0 && m_SelectedSettingsIndex < m_SettingsNames.Length)
        {
            string selectedName = m_SettingsNames[m_SelectedSettingsIndex];
            var selectedItem = s_SettingsData.GetSettingsDataItemByName(selectedName);
            if (selectedItem != null)
            {
                UIBindDataManager.SetCurrentSettingsItem(selectedItem);
                Debug.Log($"切换到设置: {selectedName}");
            }
        }
    }

    #region 设置界面绘制方法

    /// <summary>
    /// 绘制设置名称
    /// </summary>
    private void DrawSettingsName(UIBindToolSettingsDataItem settings)
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"设置名称 : {settings.settingsDataName}", GUILayout.Width(200));
        EditorGUILayout.EndHorizontal();
    }

    /// <summary>
    /// 绘制绑定数据文件夹
    /// </summary>
    private void DrawBindDataFolder(UIBindToolSettingsDataItem settings)
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("绑定数据文件夹:", GUILayout.Width(100));

        // 只读输入框（使用EditorStyles.label样式实现只读效果）
        EditorGUI.BeginDisabledGroup(true);
        EditorGUILayout.LabelField(settings.bindDataFolder, EditorStyles.textField);
        EditorGUI.EndDisabledGroup();

        // 文件夹选择按钮
        if (GUILayout.Button(EditorGUIUtility.IconContent("Folder On Icon"), GUILayout.Width(25), GUILayout.Height(EditorGUIUtility.singleLineHeight)))
        {
            string selectedPath = EditorUtility.OpenFolderPanel("选择绑定数据文件夹", "Assets", "");
            if (!string.IsNullOrEmpty(selectedPath) && selectedPath.Contains("Assets"))
            {
                // 转换为相对路径
                int assetsIndex = selectedPath.IndexOf("Assets");
                if (assetsIndex >= 0)
                {
                    string relativePath = selectedPath.Substring(assetsIndex);
                    relativePath = relativePath.Replace('\\', '/'); // 确保使用正斜杠
                    settings.bindDataFolder = relativePath;
                }
            }
        }

        EditorGUILayout.EndHorizontal();
    }

    /// <summary>
    /// 绘制脚本结合方式
    /// </summary>
    private void DrawScriptCombinedMethod(UIBindToolSettingsDataItem settings)
    {
        EditorGUILayout.LabelField("脚本结合方式:", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();

        // 获取所有枚举值
        var enumValues = System.Enum.GetValues(typeof(ScriptCombinedMethod));
        int selectedIndex = 0;

        // 找到当前选中的索引
        for (int i = 0; i < enumValues.Length; i++)
        {
            if ((ScriptCombinedMethod)enumValues.GetValue(i) == settings.scriptCombinedMethod)
            {
                selectedIndex = i;
                break;
            }
        }

        // 绘制选项按钮
        for (int i = 0; i < enumValues.Length; i++)
        {
            ScriptCombinedMethod method = (ScriptCombinedMethod)enumValues.GetValue(i);
            bool isSelected = (i == selectedIndex);

            // 设置按钮样式
            if (isSelected)
            {
                GUI.backgroundColor = Color.cyan;
            }

            // 获取友好的显示名称
            string displayName = GetScriptCombinedMethodDisplayName(method);

            if (GUILayout.Button(displayName))
            {
                settings.scriptCombinedMethod = method;

                // 如果在预览模式，更新预览文件名
                if (m_IsPreviewMode)
                {
                    m_PreviewFileName = GetPreviewFileName();
                }
            }

            // 恢复颜色
            GUI.backgroundColor = Color.white;
        }

        EditorGUILayout.EndHorizontal();
    }

    /// <summary>
    /// 绘制UI绑定脚本文件夹
    /// </summary>
    private void DrawGenerateUIBindScriptFolder(UIBindToolSettingsDataItem settings)
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("UI绑定脚本文件夹:", GUILayout.Width(100));

        // 只读输入框
        EditorGUI.BeginDisabledGroup(true);
        EditorGUILayout.LabelField(settings.generateUIBindScriptFolder, EditorStyles.textField);
        EditorGUI.EndDisabledGroup();

        if (GUILayout.Button(EditorGUIUtility.IconContent("Folder On Icon"), GUILayout.Width(25), GUILayout.Height(EditorGUIUtility.singleLineHeight)))
        {
            string selectedPath = EditorUtility.OpenFolderPanel("选择UI绑定脚本文件夹", "Assets", "");
            if (!string.IsNullOrEmpty(selectedPath) && selectedPath.Contains("Assets"))
            {
                int assetsIndex = selectedPath.IndexOf("Assets");
                if (assetsIndex >= 0)
                {
                    string relativePath = selectedPath.Substring(assetsIndex);
                    relativePath = relativePath.Replace('\\', '/');
                    settings.generateUIBindScriptFolder = relativePath;
                }
            }
        }

        EditorGUILayout.EndHorizontal();
    }

    /// <summary>
    /// 绘制UI逻辑脚本文件夹
    /// </summary>
    private void DrawGenerateUILogicScriptFolder(UIBindToolSettingsDataItem settings)
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("UI逻辑脚本文件夹:", GUILayout.Width(100));

        // 只读输入框
        EditorGUI.BeginDisabledGroup(true);
        EditorGUILayout.LabelField(settings.generateUILogicScriptFolder, EditorStyles.textField);
        EditorGUI.EndDisabledGroup();

        if (GUILayout.Button(EditorGUIUtility.IconContent("Folder On Icon"), GUILayout.Width(25), GUILayout.Height(EditorGUIUtility.singleLineHeight)))
        {
            string selectedPath = EditorUtility.OpenFolderPanel("选择UI逻辑脚本文件夹", "Assets", "");
            if (!string.IsNullOrEmpty(selectedPath) && selectedPath.Contains("Assets"))
            {
                int assetsIndex = selectedPath.IndexOf("Assets");
                if (assetsIndex >= 0)
                {
                    string relativePath = selectedPath.Substring(assetsIndex);
                    relativePath = relativePath.Replace('\\', '/');
                    settings.generateUILogicScriptFolder = relativePath;
                }
            }
        }

        EditorGUILayout.EndHorizontal();
    }

    /// <summary>
    /// 绘制基类或接口名称
    /// </summary>
    private void DrawBaseClassOrInterfaceNames(UIBindToolSettingsDataItem settings)
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("基类或接口名称:", GUILayout.Width(120));
        string newValue = EditorGUILayout.TextField(settings.baseClassOrInterfaceNames);
        if (newValue != settings.baseClassOrInterfaceNames)
        {
            settings.baseClassOrInterfaceNames = newValue;
        }
        EditorGUILayout.EndHorizontal();
    }

    /// <summary>
    /// 绘制命名空间设置
    /// </summary>
    private void DrawNamespaceSettings(UIBindToolSettingsDataItem settings)
    {
        // Use Namespace Toggle
        EditorGUILayout.BeginHorizontal();
        bool newUseNamespace = EditorGUILayout.Toggle("使用命名空间", settings.useNamespace);
        if (newUseNamespace != settings.useNamespace)
        {
            settings.useNamespace = newUseNamespace;
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(10);
        // Script Namespace TextField
        EditorGUILayout.BeginHorizontal();
        EditorGUI.BeginDisabledGroup(!settings.useNamespace);
        EditorGUILayout.LabelField("脚本命名空间:", GUILayout.Width(100));
        // 使用BeginDisabledGroup来显示失活状态
        string newNamespace = EditorGUILayout.TextField(settings.scriptNamespace);
        if (newNamespace != settings.scriptNamespace && settings.useNamespace)
        {
            settings.scriptNamespace = newNamespace;
        }
        EditorGUI.EndDisabledGroup();

        EditorGUILayout.EndHorizontal();
    }

    /// <summary>
    /// 绘制自动打开生成脚本设置
    /// </summary>
    private void DrawAutoOpenGeneratedScripts(UIBindToolSettingsDataItem settings)
    {
        bool newValue = EditorGUILayout.Toggle("生成脚本后自动打开", settings.autoOpenGeneratedScripts);
        if (newValue != settings.autoOpenGeneratedScripts)
        {
            settings.autoOpenGeneratedScripts = newValue;
        }
    }

    /// <summary>
    /// 保存当前设置
    /// </summary>
    private void SaveCurrentSettings()
    {
        var currentSettings = UIBindDataManager.GetCurrentSettingsItem();
        if (currentSettings == null)
        {
            EditorUtility.DisplayDialog("错误", "没有可保存的设置项", "确定");
            return;
        }

        // 验证设置数据
        if (!ValidateSettingsData(currentSettings))
        {
            return;
        }

        try
        {
            // 更新设置数据容器中的对应项
            if (s_SettingsData != null)
            {
                // 查找对应的设置项并更新
                for (int i = 0; i < s_SettingsData.settingsDataItems.Count; i++)
                {
                    if (s_SettingsData.settingsDataItems[i].settingsDataName == currentSettings.settingsDataName)
                    {
                        s_SettingsData.settingsDataItems[i] = currentSettings;
                        break;
                    }
                }

                // 保存ScriptableObject
                EditorUtility.SetDirty(s_SettingsData);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                // 更新UIBindDataManager中的设置
                UIBindDataManager.SetCurrentSettingsItem(currentSettings);

                EditorUtility.DisplayDialog("保存成功", "设置已成功保存", "确定");
                Debug.Log($"设置 '{currentSettings.settingsDataName}' 已保存");
            }
        }
        catch (System.Exception e)
        {
            EditorUtility.DisplayDialog("保存失败", $"保存设置时发生错误: {e.Message}", "确定");
            Debug.LogError($"保存设置失败: {e}");
        }
    }

    /// <summary>
    /// 验证设置数据
    /// </summary>
    private bool ValidateSettingsData(UIBindToolSettingsDataItem settings)
    {
        // 验证文件夹路径
        if (string.IsNullOrEmpty(settings.bindDataFolder))
        {
            EditorUtility.DisplayDialog("验证错误", "绑定数据文件夹不能为空", "确定");
            return false;
        }

        if (!settings.bindDataFolder.StartsWith("Assets/"))
        {
            EditorUtility.DisplayDialog("验证错误", "绑定数据文件夹必须以 'Assets/' 开头", "确定");
            return false;
        }

        if (string.IsNullOrEmpty(settings.generateUIBindScriptFolder))
        {
            EditorUtility.DisplayDialog("验证错误", "UI绑定脚本文件夹不能为空", "确定");
            return false;
        }

        if (!settings.generateUIBindScriptFolder.StartsWith("Assets/"))
        {
            EditorUtility.DisplayDialog("验证错误", "UI绑定脚本文件夹必须以 'Assets/' 开头", "确定");
            return false;
        }

        if (string.IsNullOrEmpty(settings.generateUILogicScriptFolder))
        {
            EditorUtility.DisplayDialog("验证错误", "UI逻辑脚本文件夹不能为空", "确定");
            return false;
        }

        if (!settings.generateUILogicScriptFolder.StartsWith("Assets/"))
        {
            EditorUtility.DisplayDialog("验证错误", "UI逻辑脚本文件夹必须以 'Assets/' 开头", "确定");
            return false;
        }

        // 验证命名空间
        if (settings.useNamespace && string.IsNullOrEmpty(settings.scriptNamespace))
        {
            EditorUtility.DisplayDialog("验证错误", "启用命名空间时，脚本命名空间不能为空", "确定");
            return false;
        }

        // 验证命名空间格式
        if (settings.useNamespace && !IsValidNamespace(settings.scriptNamespace))
        {
            EditorUtility.DisplayDialog("验证错误", "脚本命名空间格式无效", "确定");
            return false;
        }

        return true;
    }

    /// <summary>
    /// 验证命名空间格式
    /// </summary>
    private bool IsValidNamespace(string namespaceStr)
    {
        if (string.IsNullOrEmpty(namespaceStr))
            return false;

        // 简单的命名空间验证：只允许字母、数字、点和下划线
        foreach (char c in namespaceStr)
        {
            if (!char.IsLetterOrDigit(c) && c != '.' && c != '_')
                return false;
        }

        // 不能以点开头或结尾
        if (namespaceStr.StartsWith('.') || namespaceStr.EndsWith('.'))
            return false;

        // 不能有连续的点
        if (namespaceStr.Contains(".."))
            return false;

        return true;
    }

    /// <summary>
    /// 复制代码到剪贴板
    /// </summary>
    private void CopyCodeToClipboard()
    {
        try
        {
            string sampleCode = GetSampleCode();

            if (!string.IsNullOrEmpty(sampleCode))
            {
                // 将代码复制到系统剪贴板
                EditorGUIUtility.systemCopyBuffer = sampleCode;
                ShowNotification("代码已复制到剪贴板");
            }
            else
            {
                ShowNotification("没有可复制的代码");
            }
        }
        catch (System.Exception e)
        {
            ShowNotification($"复制失败: {e.Message}");
            Debug.LogError($"复制代码到剪贴板失败: {e}");
        }
    }

    /// <summary>
    /// 获取预览文件名
    /// </summary>
    private string GetPreviewFileName()
    {
        if (CurrentBindings == null || string.IsNullOrEmpty(CurrentBindings.panelName))
        {
            return "UIBinding.cs";
        }

        // 获取当前设置
        var currentSettings = UIBindDataManager.GetCurrentSettingsItem();
        if (currentSettings == null)
        {
            return $"{CurrentBindings.panelName}.cs";
        }

        // 清理Panel名称，移除空格和特殊字符
        string className = CurrentBindings.panelName;
        className = System.Text.RegularExpressions.Regex.Replace(className, @"[^a-zA-Z0-9_]", "");

        // 确保首字母大写
        if (className.Length > 0)
        {
            className = char.ToUpper(className[0]) + className[1..];
        }

        // 根据模式返回不同的文件名
        switch (currentSettings.scriptCombinedMethod)
        {
            case ScriptCombinedMethod.BaseClassInherit:
                return $"{className}Base.cs";
            case ScriptCombinedMethod.PartialClass:
                return $"{className}.Bind.cs";
            case ScriptCombinedMethod.SingleScript:
                return $"{className}.cs";
            default:
                return $"{className}.cs";
        }
    }

    /// <summary>
    /// 获取脚本结合方式的显示名称
    /// </summary>
    private string GetScriptCombinedMethodDisplayName(ScriptCombinedMethod method)
    {
        return method switch
        {
            ScriptCombinedMethod.BaseClassInherit => "基类继承",
            ScriptCombinedMethod.PartialClass => "部分类",
            ScriptCombinedMethod.SingleScript => "单脚本",
            _ => method.ToString()
        };
    }

    #endregion
}

/// <summary>
/// Panel验证结果
/// </summary>
public struct PanelValidationResult
{
    public bool IsValid;
    public string Message;

    public PanelValidationResult(bool isValid, string message)
    {
        IsValid = isValid;
        Message = message;
    }
}