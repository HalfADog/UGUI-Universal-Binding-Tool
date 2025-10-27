using System;
using System.Collections.Generic;
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

    public GUID PrefabGuid => m_PrefabGuid;
    public UIPanelBindings CurrentBindings
    {
        get => m_CurrentBindings;
        set
        {
            m_CurrentBindings = value;
            Repaint();
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

    void OnEnable()
    {
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

        // 更新列宽以适应窗口大小
        UpdateColumnWidths();

        // 绘制TreeView，留出ToolBar的空间
        Rect rect = new Rect(0, 30, position.width, position.height - 30);
        m_UIBindToolTreeView.OnGUI(rect);
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

        // 右侧弹性空间
        GUILayout.FlexibleSpace();

        // 预览按钮
        if (GUILayout.Button("预览", EditorStyles.toolbarButton, GUILayout.Width(50)))
        {
        }

        // 设置按钮（齿轮图标）
        if (GUILayout.Button(EditorGUIUtility.IconContent("Settings"), EditorStyles.toolbarButton, GUILayout.Width(30)))
        {
        }

        EditorGUILayout.EndHorizontal();
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