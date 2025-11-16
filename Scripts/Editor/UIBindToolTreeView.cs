using System.Collections.Generic;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

public class UIBindToolTreeView : TreeView
{
    private List<KeyValuePair<int, (GameObject obj,GameObject objPrefab)>> m_data = new List<KeyValuePair<int, (GameObject,GameObject)>>();
    private GameObject m_rootPanel; // UI面板的根对象
    private UIBindToolWindow m_parentWindow;
    private GUIStyle m_labelStyle;

    public UIBindToolTreeView(TreeViewState state, MultiColumnHeader header,UIBindToolWindow parentWindow,List<KeyValuePair<int, (GameObject,GameObject)>> data, GameObject rootPanel) : base(state, header)
    {
        m_data = data;
        m_rootPanel = rootPanel;
        m_parentWindow = parentWindow;
        rowHeight = 25;
        showAlternatingRowBackgrounds = true;
        showBorder = true;
        m_labelStyle = new(EditorStyles.boldLabel)
        {
            normal = { textColor = new(0.5f, 0.5f, 1f, 1f) }
        };
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
                GameObject tempPrefab = UIBindDataManager.GetPrefabSourceRoot(item.boundObject);
                if(tempPrefab.name == item.boundObject.name)
                    EditorGUI.LabelField(cellRect, args.label,m_labelStyle);
                else
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
        UIPanelBindings bindings = m_parentWindow.CurrentBindings;
        if (bindings == null)
            return;

        List<UIBindItem> objectBindings = bindings.GetBindingsForObject(item.boundObject);
        if (objectBindings == null || objectBindings.Count == 0)
            return;

        // 过滤出有效的绑定
        var validBindings = objectBindings.FindAll(b => b.IsValidTarget());
        if (validBindings.Count == 0)
            return;

        // 绘制绑定组件
        float currentX = cellRect.x;
        float componentHeight = 22f; // 组件标签的高度（增大）
        float spacing = 6f; // 组件间距（增大）
        float padding = 4f; // 内边距（增大）

        // 垂直居中
        float startY = cellRect.y + (cellRect.height - componentHeight) * 0.5f;

        for (int i = 0; i < validBindings.Count; i++)
        {
            UIBindItem binding = validBindings[i];

            // 创建样式 - 组件名称（较大字体）
            GUIStyle componentNameStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 11, // 组件名称使用11号字体
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white },
                hover = { textColor = Color.white },
                clipping = TextClipping.Clip
            };

            // 创建样式 - 变量名（较小字体）
            GUIStyle variableNameStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 9, // 变量名使用9号字体（更小）
                fontStyle = FontStyle.Normal, // 变量名不使用粗体
                normal = { textColor = new Color(1f, 1f, 1f, 0.9f) }, // 稍淡的颜色
                hover = { textColor = new Color(1f, 1f, 1f, 0.9f) },
                clipping = TextClipping.Clip
            };

            // 测量文本尺寸
            Vector2 componentNameSize = componentNameStyle.CalcSize(new GUIContent(binding.shortTypeName));
            Vector2 variableNameSize = variableNameStyle.CalcSize(new GUIContent(binding.variableName));

            // 计算总宽度和高度
            float spacingBetween = 2f; // 组件名称和变量名之间的间距
            float totalTextWidth = componentNameSize.x + spacingBetween + variableNameSize.x + padding * 2;
            float componentWidth = Mathf.Max(totalTextWidth, 60f); // 最小宽度60

            // 检查是否超出单元格边界
            // if (currentX + componentWidth > cellRect.x + cellRect.width - spacing)
            //     break;

            Rect componentRect = new(currentX + padding, startY, componentWidth - padding, componentHeight);

            // 根据访问修饰符设置颜色
            Color backgroundColor = GetColorForAccessModifier(binding.accessModifier);

            // 绘制普通矩形背景
            EditorGUI.DrawRect(componentRect, backgroundColor);

            // 计算文本位置 - 垂直居中
            float textStartX = componentRect.x + padding / 2;
            float textY = componentRect.y + (componentHeight - componentNameSize.y) * 0.5f;

            // 绘制组件名称（较大字体）
            Rect componentNameRect = new(textStartX, textY, componentNameSize.x, componentNameSize.y);
            GUI.Label(componentNameRect, binding.shortTypeName, componentNameStyle);

            // 绘制变量名（较小字体）
            textStartX += componentNameSize.x + spacingBetween;
            float variableNameY = componentRect.y + (componentHeight - variableNameSize.y) * 0.5f;
            Rect variableNameRect = new(textStartX, variableNameY, variableNameSize.x, variableNameSize.y);
            GUI.Label(variableNameRect, binding.variableName, variableNameStyle);

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