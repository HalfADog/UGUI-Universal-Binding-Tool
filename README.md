# UGUI 通用绑定工具（UGUI Universal Binding Tool）

一个 Unity Editor 扩展工具，通过可视化界面自动化 UI 组件绑定代码生成，显著提升 UGUI 开发效率。

## 概述

UGUI 通用绑定工具是为 Unity 开发者设计的 UI 组件绑定自动化解决方案。它通过直观的可视化界面，帮助开发者快速将 UI 控件绑定到 C# 脚本，自动生成标准化的字段声明、访问修饰符和初始化代码，避免手动编写重复性绑定代码。

## 核心特性

- **可视化绑定管理**: 树形结构展示 UI 层级，一键选择和配置组件
- **自动化代码生成**: 根据模板一键生成 `.Bind.cs` 部分类文件
- **预制体友好**: 基于 Unity GUID 系统，绑定数据与预制体资产关联，跨场景保持一致
- **双路径存储**: 同时保存绝对路径（用于调试）和相对路径（用于代码生成）
- **智能命名**: 根据组件类型自动设置变量前缀（如 Button → btn，Text → txt）
- **访问修饰符控制**: 支持 Private、Protected、Public 三种访问级别
- **实时预览**: 在生成前预览代码内容，支持复制到剪贴板
- **撤销/重做支持**: 所有操作集成 Unity Undo 系统，支持 Ctrl+Z / Ctrl+Y
- **变量名重命名**: 修改变量名时自动追踪，生成代码后自动更新主脚本引用
- **多配置支持**: 支持多组设置配置（如 Panel 模式、Item 模式）
- **自动挂载与绑定**: 代码编译完成后自动挂载脚本并绑定字段，无需手动拖拽

## 安装说明

### 通过 Unity Package

1. 将本插件文件夹复制到项目的 `Assets/Plugins/` 目录下
2. 插件将自动集成到 Unity Editor

### 目录结构

```
UGUI Universal Binding Tool/
├── Scripts/
│   └── Editor/           # 编辑器脚本（仅在编辑器下编译）
│       ├── AccessModifier.cs              # 访问修饰符枚举
│       ├── UIBindItem.cs                  # 单个绑定项数据
│       ├── UIPanelBindings.cs             # 绑定数据容器（ScriptableObject）
│       ├── UIBindDataManager.cs           # 数据管理器
│       ├── UIBindToolWindow.cs            # 主工具窗口（1,499行）
│       ├── UIBindToolTreeView.cs          # TreeView实现（UI层级展示）
│       ├── UIBindToolTreeViewItem.cs      # TreeView项
│       ├── UIBindConfigWindow.cs          # 绑定配置窗口
│       ├── EditDeleteBindingWindow.cs     # 编辑/删除绑定窗口
│       ├── UIBindScriptGenerator.cs       # 代码生成器
│       ├── UIBindToolSettingsData.cs      # 设置数据（ScriptableObject）
│       ├── UndoHelper.cs                  # 撤销/重做辅助
│       └── UIAutoBinder.cs                # 自动绑定器（编译后自动挂载）
├── Example/              # 示例工程
│   ├── Scenes/
│   ├── Prefabs/          # 示例预制体
│   ├── Scripts/
│   │   ├── IUIObject.cs                 # UI对象接口
│   │   ├── UIItemBase.cs                # UI项基类
│   │   └── UIPanelBase.cs               # UI面板基类
│   └── ScriptsTemplate/
│       └── UIPanelTemplate.txt          # 代码生成模板
├── LICENSE
└── README.md
```

## 快速开始

### 1. 打开工具

在 Hierarchy 窗口中选择 UI 预制体的实例（不是场景中的独立对象），然后执行以下任一操作：

- **菜单方式**: `GameObject > UI Binding`（快捷键 `Alt + E`）
- **右键菜单**: 右键点击选中的 UI 对象 → `UI Binding`

### 2. 创建绑定

1. 在打开的窗口中，确认已选择正确的 UI 面板预制体
2. 在左侧树形视图中选择要绑定的 UI 组件
3. 在右侧配置区域设置：
   - **变量名**: 自动根据组件类型和对象名生成（可手动修改）
   - **访问修饰符**: Private / Protected / Public（推荐 Private）
4. 点击 **"Add"** 按钮添加绑定

### 3. 生成代码

点击窗口顶部的 **"Generate"** 按钮，工具将：

1. 在配置的输出目录生成 `{PanelName}.Bind.cs` 文件（部分类，包含所有绑定的字段声明）
2. 如果主脚本（`{PanelName}.cs`）不存在，会自动生成主脚本文件（可从模板创建）
3. 注册自动绑定任务

### 4. 使用生成的代码

Unity 编译完成后，工具会自动：

1. **自动**将主脚本挂载到预制体上（如果尚未挂载）
2. **自动**将所有 UI 组件拖拽到对应的序列化字段
3. 你可以在主脚本中直接访问已绑定的 UI 组件：

```csharp
public partial class MainMenuPanel : UIPanelBase
{
    void Start()
    {
        // 直接访问已绑定的组件
        btnStartButton.onClick.AddListener(OnStartClicked);
        btnSettingsButton.onClick.AddListener(OnSettingsClicked);
    }
}
```

## 使用示例

### 场景：绑定主菜单 UI

以下是一个完整的主菜单面板绑定示例。

**UI 结构**：
```
MainMenuPanel (Canvas)
├── Background (Image)
├── TitleText (Text)
├── StartButton (Button)
├── SettingsButton (Button)
└── ExitButton (Button)
```

**操作步骤**：

1. 在 Hierarchy 中选择 `MainMenuPanel` 预制体实例
2. 打开工具：`GameObject > UI Binding`
3. 在树形视图中选择需要绑定的组件（如 StartButton、SettingsButton 等）
4. 工具自动建议变量名（如 `btnStartButton`）
5. 设置访问修饰符为 `Private`
6. 点击 **Generate** 生成代码

**生成的代码**（`MainMenuPanel.Bind.cs`）：
```csharp
using UnityEngine;
using UnityEngine.UI;

public partial class MainMenuPanel
{
    [Header("UI Bindings")]
    [SerializeField] private Text txtTitleText;
    [SerializeField] private Button btnStartButton;
    [SerializeField] private Button btnSettingsButton;
    [SerializeField] private Button btnExitButton;
}
```

**业务逻辑代码**（`MainMenuPanel.cs`）：
```csharp
public partial class MainMenuPanel : UIPanelBase
{
    private void Awake()
    {
        btnStartButton.onClick.AddListener(OnStartGame);
        btnSettingsButton.onClick.AddListener(OnOpenSettings);
        btnExitButton.onClick.AddListener(OnExitGame);
    }

    private void OnStartGame()
    {
        Debug.Log("Start game clicked!");
        // 切换到游戏场景
    }

    private void OnOpenSettings()
    {
        Debug.Log("Open settings!");
        // 打开设置面板
    }

    private void OnExitGame()
    {
        Debug.Log("Exit game!");
        Application.Quit();
    }
}
```

## 高级特性

### 撤销重做支持

所有绑定操作都集成 Unity 撤销系统：
- **添加绑定**：支持撤销/重做
- **删除绑定**：支持撤销/重做
- **修改变量名**：支持撤销/重做
- **修改访问修饰符**：支持撤销/重做

使用快捷键 `Ctrl+Z` / `Ctrl+Y` 或在 Unity 编辑器菜单中操作。

### 变量名重命名

支持修改变量名并自动更新引用：
1. 在 TreeView 中点击已绑定的组件标签
2. 修改变量名并保存
3. 重新生成代码后，工具会自动扫描主脚本
4. 使用正则表达式精确匹配并替换所有旧变量名引用

### 自动挂载与字段绑定

代码生成后无需手动操作：
- 自动将主脚本挂载到预制体（如果尚未挂载）
- 自动将所有 UI 组件拖拽到对应的序列化字段
- 无需在 Inspector 中手动赋值

### 自定义组件前缀

工具内置的组件前缀映射：
- Button → `btn`
- Text → `txt`
- TextMeshProUGUI → `tmp`
- Image → `img`
- RawImage → `rawImg`
- Toggle → `toggle`
- Slider → `slider`
- Dropdown → `dropdown`
- InputField → `input`
- ScrollRect → `scrollRect`
- Scrollbar → `scrollbar`
- Canvas → `canvas`
- RectTransform → `rect`
- Panel → `panel`

可以在设置模式中自定义前缀映射。

### 多配置支持

工具支持多组设置配置：
- **Panel 模式**：适用于 UI 面板
- **Item 模式**：适用于列表项等重复元素
- 可以创建自定义配置模式

每种模式可以配置：
- 独立的数据存储文件夹
- 独立的代码输出路径
- 独立的命名空间和基类
- 独立的组件前缀映射

在工具窗口顶部的下拉框中切换配置模式。

### 代码生成模式

目前支持部分类模式（PartialClass）：
- 生成 `{PanelName}.Bind.cs` 文件
- 包含所有 UI 字段的序列化声明
- 与主脚本构成部分类

架构上预留了扩展接口，未来可支持：
- **BaseClassInherit**: 生成基类供继承
- **SingleScript**: 生成包含事件处理的完整脚本

### 配置自定义

在工具窗口的设置模式（齿轮图标）中，可以配置：
- **基础设置**：设置名称、生成后自动打开脚本
- **数据存储**：绑定数据文件夹路径
- **代码生成**：UI绑定脚本文件夹、UI主脚本文件夹、模板文件路径、基类/接口名称
- **命名空间**：启用/禁用命名空间、脚本命名空间
- **组件前缀**：自定义每种组件类型的变量名前缀

## 技术架构

### 核心组件

- **UIPanelBindings**: ScriptableObject，存储所有绑定数据
- **UIBindItem**: 单个组件绑定信息，包含绝对路径和相对路径
- **UIBindDataManager**: 管理绑定数据的持久化和预制体同步
- **UIBindToolWindow**: 主工具窗口，提供完整的 UI 交互界面
- **UIBindToolTreeView**: Unity TreeView 实现，展示 UI 层级
- **UIBindScriptGenerator**: 基于模板的代码生成引擎
- **UIBindToolSettingsData**: 设置数据，支持多配置管理
- **UndoHelper**: 统一管理撤销/重做操作
- **UIAutoBinder**: 代码编译完成后自动挂载组件并绑定字段

### 数据流

```
UI 预制体
    ↓
UIBindToolWindow.ValidatePanelForBinding()  // 验证 Prefab 实例
    ↓
UIBindToolTreeView（解析并展示 UI 层级）
    ↓
UIBindConfigWindow（用户配置绑定）
    ↓
UIBindItem（创建绑定项）
    ↓
UIPanelBindings（存储所有绑定，ScriptableObject）
    ↓
UIBindDataManager.SaveBindings()  // 持久化到 .asset 文件
    ↓
用户点击"生成"按钮
    ↓
UIBindScriptGenerator.GenerateScripts()  // 生成代码
    ↓
    ├── 生成 {PanelName}.Bind.cs 文件（字段声明）
    └── 生成 {PanelName}.cs 文件（从模板）
    ↓
UIAutoBinder.RegisterBindingTask()  // 注册自动绑定任务
    ↓
Unity 编译完成后 [DidReloadScripts]
    ↓
UIAutoBinder.ProcessBindingTask()  // 自动挂载脚本
    ↓
UIAutoBinder.ProcessFieldBindings()  // 自动绑定字段引用
    ↓
可直接在代码中访问绑定的 UI 组件
```

## 注意事项

### Prefab 要求
- **必须使用预制体实例**: 工具需要预制体实例，不支持场景独有对象
- **确保实例与资源同步**: 绑定前请确保 Hierarchy 中的实例与 Prefab 资源完全一致（如有修改请先应用）
- **选择根对象**: 必须选择预制体实例的根对象（面板），不能选择子对象

### 数据持久化
- **GUID 依赖**: 绑定数据使用 Unity GUID，避免在 Unity 外重命名/移动预制体
- **备份绑定数据**: 绑定数据存储在 `Assets/UIBindData/` 或自定义文件夹的 `.asset` 文件中，建议纳入版本控制
- **跨场景一致**: 基于 GUID 机制，同一预制体在不同场景共享绑定数据

### 代码生成
- **文件命名**: 生成文件遵循严格命名规范（`{ClassName}.Bind.cs`）
- **命名空间支持**: 可通过设置配置，支持多级命名空间（如 `Game.UI.Panel`）
- **字段声明**: 所有字段使用 `[SerializeField]` 属性，支持 Private/Protected/Public
- **变量名唯一性**: 自动生成时会确保变量名唯一，重复时添加数字后缀

### 撤销系统
- **全面支持**: 所有操作支持 Unity 撤销系统（Ctrl+Z / Ctrl+Y）
- **场景标记**: 执行绑定操作后场景会被标记为已修改，需要保存场景
- **撤销范围**: 支持撤销添加、删除、修改绑定操作

### 自动绑定
- **编译后触发**: 代码生成后需要等待 Unity 编译完成，自动绑定才会执行
- **字段绑定机制**: 使用 `SerializedObject` 系统绕过访问修饰符限制
-  **Prefab 应用**  : 自动绑定后需要应用 Prefab 修改才能持久化

### 版本控制
- **绑定数据**: 建议将 `Assets/UIBindData/` 纳入版本控制
- **生成代码**: 生成的脚本可根据团队规范决定是否纳入版本控制
- **设置数据**: `Assets/Settings/UIBindToolSettingsData.asset` 建议纳入版本控制

### 性能考虑
- **绑定数据体积**: 每个绑定包含完整的路径和类型信息，大量绑定可能增加数据文件大小
- **TreeView 性能**: 层级很深的 UI 结构可能影响 TreeView 渲染性能
- **代码生成**: 生成操作在编辑器线程执行，绑定数量巨大时可能有短暂卡顿

## 贡献与扩展

### 添加新组件前缀

在设置模式中添加新的组件前缀映射：
1. 点击工具栏齿轮图标进入设置模式
2. 滚动到"组件前缀配置"区域
3. 在"添加新映射"部分输入：
   - 组件类型：如 `YourComponent`
   - 前缀：如 `yc`
4. 点击"添加"按钮

或者直接在 `Assets/Settings/UIBindToolSettingsData.asset` 中编辑配置。

### 自定义模板

编辑 `Example/ScriptsTemplate/UIPanelTemplate.txt` 修改代码生成模板：
- 使用 `<ClassName>` 作为类名占位符
- 模板中的命名空间引用会自动解析并用于生成主脚本
- 模板中的基类会自动识别并应用于生成的类

### 扩展现有功能

#### 添加新的代码生成模式
编辑 `UIBindScriptGenerator.cs`：
```csharp
// 在 GenerateBindingCode 方法中添加新的生成模式
private static string GenerateBaseClassCode(UIPanelBindings bindings, GenerationConfig config)
{
    // TODO: 实现基类继承模式
    return string.Empty;
}

private static string GenerateSingleScriptCode(UIPanelBindings bindings, GenerationConfig config)
{
    // TODO: 实现单文件模式
    return string.Empty;
}
```

#### 扩展设置界面
编辑 `UIBindToolWindow.cs` 的 `DrawSettingsArea()` 方法，添加新的设置项。

#### 添加新的事件处理器
编辑 `EditDeleteBindingWindow.cs`，扩展绑定编辑功能。

## 许可证

本项目基于 MIT 许可证开源，详见 [LICENSE](LICENSE) 文件。

## 支持与反馈

如有问题或建议，欢迎提交 Issue 或 Pull Request。

---

**版本**: v0.2.0
**Unity 版本**: 2019.4+
**最后更新**: 2025-11-23

### 版本历史

**v0.2.0** (2025-11-23)
- 添加撤销/重做系统支持
- 添加变量名重命名追踪和自动更新
- 添加自动挂载组件和字段绑定功能
- 添加多配置支持（Panel/Item 模式）
- 添加设置管理界面
- 优化 TreeView 视觉显示（访问修饰符颜色区分）
- 增强模板系统，自动解析命名空间和基类
- 添加实时代码预览和剪贴板复制功能
- 添加完整的验证和错误处理

**v0.1.0** (2025-11-22)
- 初始版本发布
- 支持可视化 UI 绑定管理
- 支持自动生成 .Bind.cs 部分类文件
- 支持双路径存储系统
- 支持智能变量命名
