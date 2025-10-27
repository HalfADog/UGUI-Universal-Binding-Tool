using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using UnityEngine.UI;

public class UIBindToolWindow : EditorWindow
{
    private const string MENU_PATH = "GameObject/UI Binding";
    private GameObject bindTarget;
    private GameObject bindTargetPrefab;
    private List<KeyValuePair<int, (GameObject obj,GameObject objPrefab)>> bindableObjects = new List<KeyValuePair<int, (GameObject,GameObject)>>();

    public GameObject BindTargetPrefab => bindTargetPrefab;

    // 可配置的组件类型列表，只有在这个列表中的组件才会被收集
    public static List<Type> allowedComponentTypes = new List<Type>
    {
        typeof(Button),typeof(Image),typeof(Text),typeof(Toggle),
        typeof(Scrollbar),typeof(Slider),typeof(InputField),typeof(Canvas),
        typeof(CanvasGroup),typeof(ScrollRect),typeof(TMP_Text)
    };

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
    private string m_currentSelectedSettingsDataName = "";
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
        GetBindableObjectsWithStructure(bindTarget,bindTargetPrefab);
        //获取Prefab的GUID
        m_PrefabGuid = UIBindDataManager.GetPrefabGUID(bindTarget);
        if(m_PrefabGuid != default)Debug.Log($"Prefab GUID: {m_PrefabGuid}");

        //尝试加载绑定数据
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

    /// <summary>
    /// 保存设置数据
    /// </summary>
    private static void SaveSettingsData()
    {
        if (s_SettingsData != null)
        {
            EditorUtility.SetDirty(s_SettingsData);
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
            //默认选择第一个设置数据项
            var lastSelectedItem = s_SettingsData.GetLastSelectedSettingsDataItem();
            m_currentSelectedSettingsDataName = lastSelectedItem.settingsDataName;
            UIBindDataManager.SetSettingsData(lastSelectedItem);
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
        EditorGUILayout.LabelField($"{m_currentSelectedSettingsDataName} Mode : {panelName}", EditorStyles.toolbarButton, GUILayout.ExpandWidth(false));

        // 分隔符
        //GUILayout.Space(10);
        // 右侧弹性空间
        GUILayout.FlexibleSpace();

        // 折叠全部按钮
        if (GUILayout.Button("折叠全部", EditorStyles.toolbarButton))
        {
            CollapseAll();
        }

        // 展开全部按钮
        if (GUILayout.Button("展开全部", EditorStyles.toolbarButton))
        {
            ExpandAll();
        }

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
        string fontSizeText = EditorGUILayout.TextField(m_CodeFontSize.ToString("F0"), GUILayout.Width(40));

        // 尝试解析新的字体大小
        if (float.TryParse(fontSizeText, out float newFontSize) &&
            newFontSize >= 8f && newFontSize <= 48f &&
            Mathf.Abs(newFontSize - m_CodeFontSize) > 0.1f)
        {
            m_CodeFontSize = newFontSize;
            Repaint();
        }

        // 弹性空间
        GUILayout.FlexibleSpace();

        // 生成按钮
        if (GUILayout.Button("生成", EditorStyles.toolbarButton, GUILayout.Width(50)))
        {
            // TODO: 实现代码生成功能
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
        // 左侧显示"设置"
        EditorGUILayout.LabelField("设置", EditorStyles.toolbarButton, GUILayout.ExpandWidth(false));

        // 弹性空间
        GUILayout.FlexibleSpace();

        // 保存按钮
        if (GUILayout.Button("保存", EditorStyles.toolbarButton, GUILayout.Width(50)))
        {
            // TODO: 实现保存设置功能
            SaveSettingsData();
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
            if (CurrentBindings != null && !string.IsNullOrEmpty(CurrentBindings.panelName))
            {
                m_PreviewFileName = $"{CurrentBindings.panelName}Binding.cs";
            }
            else
            {
                m_PreviewFileName = "UIBinding.cs";
            }
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
        float contentWidth = Mathf.Max(textSize.x, previewArea.width - 40); // 确保最小宽度，留出滚动条空间
        float contentHeight = textSize.y;

        // 开始滚动视图，覆盖整个预览区域
        m_CodeScrollPosition = GUI.BeginScrollView(previewArea, m_CodeScrollPosition,
            new(0, 0, contentWidth + 20, contentHeight + 20));

        // 绘制代码文本，宽度匹配内容区域
        Rect textRect = new(10, 10, contentWidth, contentHeight);
        GUI.Label(textRect, sampleCode, codeStyle);

        // 结束滚动视图
        GUI.EndScrollView();
    }

    /// <summary>
    /// 绘制设置界面
    /// </summary>
    private void DrawSettingsArea(Rect settingsArea)
    {
        // 绘制背景
        EditorGUI.DrawRect(settingsArea, new Color(0.2f, 0.2f, 0.2f, 1f));

        // 居中显示设置占位文本
        GUIStyle labelStyle = new(EditorStyles.centeredGreyMiniLabel)
        {
            fontSize = 14,
            normal = { textColor = Color.white },
            alignment = TextAnchor.MiddleCenter
        };

        EditorGUI.LabelField(settingsArea, "设置界面占位\n(具体功能待实现)", labelStyle);
    }

    /// <summary>
    /// 获取示例代码
    /// </summary>
    private string GetSampleCode()
    {
        return "using UnityEngine;\n" +
               "using UnityEngine.UI;\n" +
               "using TMPro;\n" +
               "\n" +
               "namespace UI\n" +
               "{\n" +
               "    public partial class LoginPanelBinding : MonoBehaviour\n" +
               "    {\n" +
               "        [Header(\"UI Bindings\")]\n" +
               "        private Button loginButton;\n" +
               "        private InputField usernameInput;\n" +
               "        private TextMeshProUGUI errorText;\n" +
               "        private Toggle rememberMeToggle;\n" +
               "        private Text welcomeText;\n" +
               "\n" +
               "        public void InitializeBindings()\n" +
               "        {\n" +
               "            loginButton = transform.Find(\"LoginPanel/LoginButton\").GetComponent<Button>();\n" +
               "            usernameInput = transform.Find(\"LoginPanel/UsernameInput\").GetComponent<InputField>();\n" +
               "            errorText = transform.Find(\"LoginPanel/ErrorText\").GetComponent<TextMeshProUGUI>();\n" +
               "            rememberMeToggle = transform.Find(\"LoginPanel/RememberMeToggle\").GetComponent<Toggle>();\n" +
               "            welcomeText = transform.Find(\"LoginPanel/WelcomeText\").GetComponent<Text>();\n" +
               "        }\n" +
               "\n" +
               "        // 自动生成的事件绑定方法\n" +
               "        private void Start()\n" +
               "        {\n" +
               "            InitializeBindings();\n" +
               "            SetupEventListeners();\n" +
               "        }\n" +
               "\n" +
               "        private void SetupEventListeners()\n" +
               "        {\n" +
               "            if (loginButton != null)\n" +
               "            {\n" +
               "                loginButton.onClick.AddListener(OnLoginButtonClicked);\n" +
               "            }\n" +
               "\n" +
               "            if (rememberMeToggle != null)\n" +
               "            {\n" +
               "                rememberMeToggle.onValueChanged.AddListener(OnRememberMeToggled);\n" +
               "            }\n" +
               "        }\n" +
               "\n" +
               "        // 事件处理方法（需要手动实现具体逻辑）\n" +
               "        private void OnLoginButtonClicked()\n" +
               "        {\n" +
               "            // TODO: 实现登录逻辑\n" +
               "            Debug.Log(\"Login button clicked\");\n" +
               "        }\n" +
               "\n" +
               "        private void OnRememberMeToggled(bool isOn)\n" +
               "        {\n" +
               "            // TODO: 实现记住密码逻辑\n" +
               "            Debug.Log($\"Remember me toggled: {isOn}\");\n" +
               "        }\n" +
               "\n" +
               "        // UI更新方法\n" +
               "        public void SetErrorText(string error)\n" +
               "        {\n" +
               "            if (errorText != null)\n" +
               "            {\n" +
               "                errorText.text = error;\n" +
               "            }\n" +
               "        }\n" +
               "\n" +
               "        public void SetWelcomeText(string welcome)\n" +
               "        {\n" +
               "            if (welcomeText != null)\n" +
               "            {\n" +
               "                welcomeText.text = welcome;\n" +
               "            }\n" +
               "        }\n" +
               "\n" +
               "        public string GetUsername()\n" +
               "        {\n" +
               "            return usernameInput != null ? usernameInput.text : \"\";\n" +
               "        }\n" +
               "\n" +
               "        public bool IsRememberMeChecked()\n" +
               "        {\n" +
               "            return rememberMeToggle != null && rememberMeToggle.isOn;\n" +
               "        }\n" +
               "    }\n" +
               "}";
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