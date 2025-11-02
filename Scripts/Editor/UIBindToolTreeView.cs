using System.Collections.Generic;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

//TreeView
public class UIBindToolTreeView : TreeView
{
    private List<KeyValuePair<int, (GameObject obj,GameObject objPrefab)>> m_data = new List<KeyValuePair<int, (GameObject,GameObject)>>();
    private GameObject m_rootPanel; // UI面板的根对象
    private UIBindToolWindow m_parentWindow;

    public UIBindToolTreeView(TreeViewState state, MultiColumnHeader header,UIBindToolWindow parentWindow,List<KeyValuePair<int, (GameObject,GameObject)>> data, GameObject rootPanel) : base(state, header)
    {
        m_data = data;
        m_rootPanel = rootPanel;
        m_parentWindow = parentWindow;
        rowHeight = 25;
        showAlternatingRowBackgrounds = true;
        showBorder = true;
        Reload();
    }

    protected override TreeViewItem BuildRoot()
    {
        var root = new TreeViewItem { id = 0, depth = -1, displayName = "Root" };
        var allItems = new List<TreeViewItem>();
        for (int i = 0; i < m_data.Count; i++)
        {
            var item = new UIBindToolTreeViewItem(i + 1, m_data[i].Key, m_data[i].Value.obj.name, m_data[i].Value.obj,m_data[i].Value.objPrefab);
            allItems.Add(item);
        }
        SetupParentsAndChildrenFromDepths(root, allItems);
        return root;
    }

    protected override void RowGUI(RowGUIArgs args)
    {
        var item = args.item as UIBindToolTreeViewItem;
        if (item == null)
            return;


        for (int i = 0; i < args.GetNumVisibleColumns(); i++)
        {
            Rect cellRect = args.GetCellRect(i);
            int columnIndex = args.GetColumn(i);
            // 第一列显示Name
            if (columnIndex == 0)
            {
                float indent = GetContentIndent(item);
                cellRect.x += indent;
                cellRect.width -= indent;
                EditorGUI.LabelField(cellRect, args.label);
            }
            // 第二列显示Bind按钮
            else if (columnIndex == 1)
            {
                DrawBindButton(cellRect, item, m_rootPanel);
            }
            // 第三列显示Bind Components
            else if (columnIndex == 2)
            {
                DrawBindComponents(cellRect, item);
            }
            // 如果需要其他基础列，可以在这里添加处理逻辑
            // 例如：显示对象类型、标签等信息
        }
    }

    /// <summary>
    /// 绘制绑定按钮（显示加号）
    /// </summary>
    private void DrawBindButton(Rect cellRect, UIBindToolTreeViewItem item, GameObject rootPanel)
    {
        // 创建按钮样式和布局
        float buttonSize = 20f;
        Rect buttonRect = new Rect(
            cellRect.x + (cellRect.width - buttonSize) * 0.5f,
            cellRect.y + (cellRect.height - buttonSize) * 0.5f,
            buttonSize,
            buttonSize
        );

        // 绘制加号按钮
        if (GUI.Button(buttonRect, "+"))
        {
            // 按钮点击事件处理 - 打开绑定配置窗口
            if (item.boundObject != null)
            {
                // 添加调试信息以确保传递正确的对象
                // Debug.Log($"打开绑定配置窗口，对象: {item.boundObject.name}, 面板: {rootPanel.name}");
                UIBindConfigWindow.ShowWindow(item.boundObject,item.boundObjectInPrefab ,rootPanel,m_parentWindow);
            }
        }
    }

    /// <summary>
    /// 绘制绑定组件显示
    /// </summary>
    private void DrawBindComponents(Rect cellRect, UIBindToolTreeViewItem item)
    {
        if (item.boundObject == null || m_rootPanel == null)
            return;

        // 获取当前对象的绑定数据
        UIPanelBindings bindings = m_parentWindow.CurrentBindings;//UIBindDataManager.LoadBindingsForPanel(m_rootPanel);
        if (bindings == null)
            return;

        List<UIBindItem> objectBindings = bindings.GetBindingsForObject(item.boundObject);
        if (objectBindings == null || objectBindings.Count == 0)
            return;

        // 过滤出有效的绑定
        var validBindings = objectBindings.FindAll(b => b.IsValidTarget());
        if (validBindings.Count == 0)
        {
            // 显示空状态提示
            EditorGUI.LabelField(cellRect, "No bindings", new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = new Color(0.7f, 0.7f, 0.7f, 0.5f) },
                alignment = TextAnchor.MiddleCenter
            });
            return;
        }

        // 绘制绑定组件
        float currentX = cellRect.x;
        float componentHeight = 22f; // 组件标签的高度（增大）
        float spacing = 6f; // 组件间距（增大）
        float padding = 4f; // 内边距（增大）

        // 垂直居中
        float startY = cellRect.y + (cellRect.height - componentHeight) * 0.5f;

        // 缓存样式以提高性能
        var labelStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 11, // 增大字体
            fontStyle = FontStyle.Bold, // 加粗字体
            normal = { textColor = Color.white },
            clipping = TextClipping.Clip
        };

        for (int i = 0; i < validBindings.Count; i++)
        {
            UIBindItem binding = validBindings[i];

            // 动态计算组件宽度（基于文本长度）
            Vector2 textSize = labelStyle.CalcSize(new GUIContent(binding.shortTypeName));
            float componentWidth = Mathf.Max(textSize.x + padding * 2 + 8f, 60f); // 最小宽度60，额外8像素缓冲

            // 检查是否超出单元格边界
            if (currentX + componentWidth > cellRect.x + cellRect.width - spacing)
                break;

            Rect componentRect = new(currentX + padding, startY, componentWidth - padding * 2, componentHeight);

            // 根据访问修饰符设置颜色
            Color backgroundColor = GetColorForAccessModifier(binding.accessModifier);

            // 绘制普通矩形背景
            EditorGUI.DrawRect(componentRect, backgroundColor);

            // 绘制文本
            GUIStyle textStyle = new(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white },
                hover = { textColor = Color.white }
            };
            GUI.Label(componentRect, binding.shortTypeName, textStyle);

            // 设置光标为手型
            EditorGUIUtility.AddCursorRect(componentRect, MouseCursor.Link);

            // 处理点击事件
            if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && componentRect.Contains(Event.current.mousePosition))
            {
                EditDeleteBindingWindow.ShowWindow(item.boundObject, binding, m_rootPanel,m_parentWindow);
                Event.current.Use();
            }

            currentX += componentWidth + spacing;
        }
    }

    /// <summary>
    /// 根据访问修饰符获取颜色
    /// </summary>
    private Color GetColorForAccessModifier(AccessModifier accessModifier) => accessModifier switch
    {
        AccessModifier.Private => new Color(0.2f, 0.6f, 0.2f, 0.9f),   // 偏绿色，增加不透明度
        AccessModifier.Protected => new Color(0.2f, 0.4f, 0.8f, 0.9f), // 偏蓝色，增加不透明度
        AccessModifier.Public => new Color(0.8f, 0.7f, 0.2f, 0.9f),   // 偏黄色，增加不透明度
        _ => new Color(0.5f, 0.5f, 0.5f, 0.9f)                     // 默认灰色，增加不透明度
    };
}