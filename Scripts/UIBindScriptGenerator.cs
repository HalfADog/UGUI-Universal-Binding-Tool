using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 代码生成配置
/// </summary>
public class GenerationConfig
{
    public ScriptCombinedMethod combineMethod;
    public string namespaceStr;
    public string baseClassOrInterfaceNames;
    public bool useNamespace;
    public string uiBindScriptFolder;
}

/// <summary>
/// 代码生成结果
/// </summary>
public struct GenerationResult
{
    public bool success;
    public string bindingScriptPath;
    public string errorMessage;
}

/// <summary>
/// UI绑定脚本生成器
/// 根据配置文件生成UI绑定代码
/// </summary>
public static class UIBindScriptGenerator
{
    #region 主生成方法

    /// <summary>
    /// 生成UI绑定脚本
    /// </summary>
    /// <param name="bindings">绑定数据</param>
    /// <returns>生成结果</returns>
    public static GenerationResult GenerateScripts(UIPanelBindings bindings)
    {
        var result = new GenerationResult { success = false };

        try
        {
            // 1. 加载配置
            var config = LoadGenerationConfig();
            if (config == null)
            {
                result.errorMessage = "无法加载生成配置";
                return result;
            }

            // 2. 验证配置
            string validationError = ValidateConfig(config);
            if (!string.IsNullOrEmpty(validationError))
            {
                result.errorMessage = validationError;
                return result;
            }

            // 3. 生成代码文件
            EditorUtility.DisplayProgressBar("生成脚本", "生成代码文件...", 0.2f);
            string bindingPath = GenerateScriptFiles(bindings, config);
            result.bindingScriptPath = bindingPath;

            // 4. 刷新AssetDatabase
            AssetDatabase.Refresh();
            EditorUtility.ClearProgressBar();

            result.success = true;
        }
        catch (Exception e)
        {
            result.errorMessage = $"生成脚本时发生错误: {e.Message}";
            Debug.LogError($"生成UI绑定脚本失败: {e}");
            EditorUtility.ClearProgressBar();
        }

        return result;
    }

    /// <summary>
    /// 加载生成配置
    /// </summary>
    /// <returns>配置对象</returns>
    public static GenerationConfig LoadGenerationConfig()
    {
        var settings = UIBindDataManager.GetCurrentSettingsItem();
        if (settings == null)
        {
            Debug.LogError("当前没有选中的设置项");
            return null;
        }

        var config = new GenerationConfig
        {
            combineMethod = settings.scriptCombinedMethod,
            namespaceStr = settings.scriptNamespace,
            useNamespace = settings.useNamespace,
            baseClassOrInterfaceNames = settings.baseClassOrInterfaceNames,
            uiBindScriptFolder = settings.generateUIBindScriptFolder,
        };

        return config;
    }

    /// <summary>
    /// 验证配置
    /// </summary>
    /// <param name="config">配置对象</param>
    /// <returns>错误信息，空字符串表示验证通过</returns>
    private static string ValidateConfig(GenerationConfig config)
    {
        if (string.IsNullOrEmpty(config.uiBindScriptFolder))
        {
            config.uiBindScriptFolder = DEFAULT_SCRIPT_FOLDER;
        }

        if (!config.uiBindScriptFolder.StartsWith("Assets/"))
        {
            return "UI绑定脚本文件夹路径必须以 'Assets/' 开头";
        }

        if (config.useNamespace && string.IsNullOrEmpty(config.namespaceStr))
        {
            return "启用命名空间但未配置命名空间名称";
        }

        return string.Empty;
    }

    #endregion

    #region 脚本文件生成

    /// <summary>
    /// 生成脚本文件
    /// </summary>
    /// <param name="bindings">绑定数据</param>
    /// <param name="config">配置</param>
    /// <returns>生成的文件路径</returns>
    private static string GenerateScriptFiles(UIPanelBindings bindings, GenerationConfig config)
    {
        // 确保文件夹存在
        EnsureFolderExists(config.uiBindScriptFolder);

        // 生成绑定脚本
        string bindingCode = GenerateBindingCode(bindings, config);
        string bindingPath = Path.Combine(config.uiBindScriptFolder, GetBindingScriptName(bindings, config));
        File.WriteAllText(bindingPath, bindingCode);
        Debug.Log($"生成绑定脚本: {bindingPath}");

        return bindingPath;
    }

    /// <summary>
    /// 生成绑定代码
    /// </summary>
    /// <param name="bindings">绑定数据</param>
    /// <param name="config">配置</param>
    /// <returns>生成的代码</returns>
    public static string GenerateBindingCode(UIPanelBindings bindings, GenerationConfig config)
    {
        switch (config.combineMethod)
        {
            case ScriptCombinedMethod.BaseClassInherit:
                return GenerateBaseClassCode(bindings, config);
            case ScriptCombinedMethod.PartialClass:
                return GeneratePartialClassCode(bindings, config); // 绑定部分
            case ScriptCombinedMethod.SingleScript:
                return GenerateSingleScriptCode(bindings, config); // 单脚本，包含全部内容
            default:
                return GenerateBaseClassCode(bindings, config);
        }
    }

    #endregion

    #region 代码模板生成

    /// <summary>
    /// 生成基类代码
    /// </summary>
    private static string GenerateBaseClassCode(UIPanelBindings bindings, GenerationConfig config)
    {
        var code = new System.Text.StringBuilder();
        bool hasNamespace = !string.IsNullOrEmpty(GetNamespaceDeclaration(config));

        // Using声明（总是在namespace外面）
        code.AppendLine("using UnityEngine;");
        code.AppendLine("using UnityEngine.UI;");
        code.AppendLine("using TMPro;");
        code.AppendLine("using System;");
        code.AppendLine();

        // 命名空间声明
        string namespaceDecl = GetNamespaceDeclaration(config);
        if (hasNamespace)
        {
            code.AppendLine(namespaceDecl);
            code.AppendLine("{");
        }

        // 类声明
        string classIndent = hasNamespace ? GetIndent(1) : "";
        code.AppendLine($"{classIndent}/// <summary>");
        code.AppendLine($"{classIndent}/// {bindings.panelName} UI绑定基类（自动生成，请勿手动修改）");
        code.AppendLine($"{classIndent}/// </summary>");
        code.AppendLine($"{classIndent}public class {GetBindingClassName(bindings)}Base : MonoBehaviour");
        code.AppendLine($"{classIndent}{{");

        // 字段声明
        string memberIndent = hasNamespace ? GetIndent(2) : "    ";
        code.AppendLine($"{memberIndent}[Header(\"UI Bindings\")]");
        string bindingFields = GenerateBindingFields(bindings, "protected");
        code.AppendLine(AddIndentToMultiLine(bindingFields, hasNamespace ? 1 : 0));

        // 初始化方法
        code.AppendLine();
        code.AppendLine($"{memberIndent}protected virtual void Awake()");
        code.AppendLine($"{memberIndent}{{");
        code.AppendLine($"{memberIndent}    InitializeBindings();");
        code.AppendLine($"{memberIndent}}}");

        // 绑定初始化方法
        code.AppendLine();
        code.AppendLine($"{memberIndent}protected void InitializeBindings()");
        code.AppendLine($"{memberIndent}{{");
        string initCode = GenerateInitializationCode(bindings);
        code.AppendLine(AddIndentToMultiLine(initCode, hasNamespace ? 1 : 0));
        code.AppendLine($"{memberIndent}}}");

        code.AppendLine($"{classIndent}}}");

        // 结束命名空间
        if (hasNamespace)
        {
            code.AppendLine("}");
        }

        return code.ToString();
    }

    /// <summary>
    /// 生成部分类代码
    /// </summary>
    private static string GeneratePartialClassCode(UIPanelBindings bindings, GenerationConfig config)
    {
        var code = new System.Text.StringBuilder();
        bool hasNamespace = !string.IsNullOrEmpty(GetNamespaceDeclaration(config));

        // Using声明（总是在namespace外面）
        code.AppendLine("using UnityEngine;");
        code.AppendLine("using UnityEngine.UI;");
        code.AppendLine("using TMPro;");
        code.AppendLine("using System;");
        code.AppendLine();

        // 命名空间声明
        string namespaceDecl = GetNamespaceDeclaration(config);
        if (hasNamespace)
        {
            code.AppendLine(namespaceDecl);
            code.AppendLine("{");
        }

        // 类声明
        string classIndent = hasNamespace ? GetIndent(1) : "";
        string className = GetBindingClassName(bindings);

        code.AppendLine($"{classIndent}/// <summary>");
        code.AppendLine($"{classIndent}/// {bindings.panelName} UI绑定部分（自动生成）");
        code.AppendLine($"{classIndent}/// </summary>");

        // 构建类声明行
        string classDeclaration = $"{classIndent}public partial class {className}";
        string baseClassDecl = GetBaseClassDeclaration(config);
        if (!string.IsNullOrEmpty(baseClassDecl))
        {
            classDeclaration += $" : {baseClassDecl}";
        }
        code.AppendLine(classDeclaration);
        code.AppendLine($"{classIndent}{{");

        // 绑定部分 - 字段声明
        string memberIndent = hasNamespace ? GetIndent(2) : "    ";
        code.AppendLine($"{memberIndent}[Header(\"UI Bindings\")]");
        string bindingFields = GenerateBindingFields(bindings, "private");
        code.AppendLine(AddIndentToMultiLine(bindingFields, hasNamespace ? 1 : 0));
        code.AppendLine();

        // 初始化方法
        code.AppendLine($"{memberIndent}private void Awake()");
        code.AppendLine($"{memberIndent}{{");
        code.AppendLine($"{memberIndent}    InitializeBindings();");
        code.AppendLine($"{memberIndent}}}");
        code.AppendLine();

        // 绑定初始化方法
        code.AppendLine($"{memberIndent}private void InitializeBindings()");
        code.AppendLine($"{memberIndent}{{");
        string initCode = GenerateInitializationCode(bindings);
        code.AppendLine(AddIndentToMultiLine(initCode, hasNamespace ? 1 : 0));
        code.AppendLine($"{memberIndent}}}");

        code.AppendLine($"{classIndent}}}");

        // 结束命名空间
        if (hasNamespace)
        {
            code.AppendLine("}");
        }

        return code.ToString();
    }

    /// <summary>
    /// 生成单脚本代码（包含字段和事件处理的完整脚本）
    /// </summary>
    private static string GenerateSingleScriptCode(UIPanelBindings bindings, GenerationConfig config)
    {
        var code = new System.Text.StringBuilder();
        bool hasNamespace = !string.IsNullOrEmpty(GetNamespaceDeclaration(config));

        // Using声明（总是在namespace外面）
        code.AppendLine("using UnityEngine;");
        code.AppendLine("using UnityEngine.UI;");
        code.AppendLine("using TMPro;");
        code.AppendLine("using System;");
        code.AppendLine();

        // 命名空间声明
        string namespaceDecl = GetNamespaceDeclaration(config);
        if (hasNamespace)
        {
            code.AppendLine(namespaceDecl);
            code.AppendLine("{");
        }

        // 类声明
        string classIndent = hasNamespace ? GetIndent(1) : "";
        string baseClassDecl = GetBaseClassDeclaration(config);
        string classDecl = string.IsNullOrEmpty(baseClassDecl) ? "" : $" : {baseClassDecl}";

        code.AppendLine($"{classIndent}/// <summary>");
        code.AppendLine($"{classIndent}/// {bindings.panelName} UI单脚本类（自动生成，包含字段和事件处理）");
        code.AppendLine($"{classIndent}/// </summary>");
        code.AppendLine($"{classIndent}public class {GetBindingClassName(bindings)}{classDecl}");
        code.AppendLine($"{classIndent}{{");

        // 字段声明
        string memberIndent = hasNamespace ? GetIndent(2) : "    ";
        code.AppendLine($"{memberIndent}[Header(\"UI Bindings\")]");
        string bindingFields = GenerateBindingFields(bindings, "private");
        code.AppendLine(AddIndentToMultiLine(bindingFields, hasNamespace ? 1 : 0));
        code.AppendLine();

        // Start方法
        code.AppendLine($"{memberIndent}private void Start()");
        code.AppendLine($"{memberIndent}{{");
        code.AppendLine($"{memberIndent}    InitializeBindings();");
        code.AppendLine($"{memberIndent}    SetupEventListeners();");
        code.AppendLine($"{memberIndent}}}");
        code.AppendLine();

        // 绑定初始化方法
        code.AppendLine($"{memberIndent}private void InitializeBindings()");
        code.AppendLine($"{memberIndent}{{");
        string initCode = GenerateInitializationCode(bindings);
        code.AppendLine(AddIndentToMultiLine(initCode, hasNamespace ? 1 : 0));
        code.AppendLine($"{memberIndent}}}");
        code.AppendLine();

        // 事件绑定方法
        code.AppendLine($"{memberIndent}private void SetupEventListeners()");
        code.AppendLine($"{memberIndent}{{");
        string eventCode = GenerateEventBindingCode(bindings);
        code.AppendLine(AddIndentToMultiLine(eventCode, hasNamespace ? 1 : 0));
        code.AppendLine($"{memberIndent}}}");
        code.AppendLine();

        // 事件处理方法
        string handlerCode = GenerateEventHandlers(bindings);
        code.AppendLine(AddIndentToMultiLine(handlerCode, hasNamespace ? 1 : 0));

        code.AppendLine($"{classIndent}}}");

        // 结束命名空间
        if (hasNamespace)
        {
            code.AppendLine("}");
        }

        return code.ToString();
    }

    #endregion

    #region 代码片段生成

    /// <summary>
    /// 生成绑定字段代码
    /// </summary>
    /// <param name="bindings">绑定数据</param>
    /// <param name="defaultAccessModifier">默认访问修饰符</param>
    /// <returns>字段代码</returns>
    private static string GenerateBindingFields(UIPanelBindings bindings, string defaultAccessModifier)
    {
        var code = new System.Text.StringBuilder();

        foreach (var binding in bindings.GetEnabledBindings())
        {
            string accessModifier = binding.accessModifier.ToString().ToLower();
            if (string.IsNullOrEmpty(accessModifier))
                accessModifier = defaultAccessModifier.ToLower();

            code.AppendLine($"    [SerializeField]");
            code.AppendLine($"    {accessModifier} {binding.shortTypeName} {binding.variableName};");
        }

        return code.ToString();
    }

    /// <summary>
    /// 生成初始化代码
    /// </summary>
    /// <param name="bindings">绑定数据</param>
    /// <returns>初始化代码</returns>
    private static string GenerateInitializationCode(UIPanelBindings bindings)
    {
        var code = new System.Text.StringBuilder();

        foreach (var binding in bindings.GetEnabledBindings())
        {
            // 优先使用相对路径，如果没有相对路径则使用绝对路径并转换
            string path = "";
            if (!string.IsNullOrEmpty(binding.targetObjectRelativePath))
            {
                // 使用存储的相对路径
                path = binding.targetObjectRelativePath;

                // 如果相对路径是[ROOT]标识，说明目标对象就是面板根对象
                if (path == "[ROOT]")
                {
                    path = ""; // 设置为空，让后面的逻辑处理根对象
                }
            }
            else if (!string.IsNullOrEmpty(binding.targetObjectFullPathInScene))
            {
                // 使用绝对路径并转换为相对路径
                path = binding.targetObjectFullPathInScene;
                if (path.StartsWith("/"))
                {
                    // 移除开头的'/'，使其成为相对路径
                    path = path.TrimStart('/');
                }

                // 如果路径包含面板名称，需要移除面板名称部分
                if (!string.IsNullOrEmpty(bindings.panelName) && path.Contains(bindings.panelName))
                {
                    // 找到面板名称后的路径部分
                    int panelIndex = path.IndexOf(bindings.panelName);
                    if (panelIndex >= 0)
                    {
                        path = path.Substring(panelIndex + bindings.panelName.Length).TrimStart('/');
                    }
                }
            }

            if (!string.IsNullOrEmpty(path))
            {
                code.AppendLine($"        {binding.variableName} = transform.Find(\"{path}\").GetComponent<{binding.shortTypeName}>();");

                // 添加错误检查
                code.AppendLine($"        if ({binding.variableName} == null)");
                code.AppendLine($"        {{");
                code.AppendLine($"            Debug.LogError($\"Failed to find component {binding.shortTypeName} at path '{path}'\");");
                code.AppendLine($"        }}");
            }
            else
            {
                // 如果路径为空，说明该组件就是面板根对象，直接使用GetComponent
                code.AppendLine($"        {binding.variableName} = GetComponent<{binding.shortTypeName}>();");

                // 添加错误检查
                code.AppendLine($"        if ({binding.variableName} == null)");
                code.AppendLine($"        {{");
                code.AppendLine($"            Debug.LogError($\"Failed to find component {binding.shortTypeName} on the root GameObject\");");
                code.AppendLine($"        }}");
            }
        }
        return code.ToString();
    }

    /// <summary>
    /// 生成事件绑定代码
    /// </summary>
    /// <param name="bindings">绑定数据</param>
    /// <returns>事件绑定代码</returns>
    private static string GenerateEventBindingCode(UIPanelBindings bindings)
    {
        var code = new System.Text.StringBuilder();
        var generatedHandlers = new HashSet<string>();

        foreach (var binding in bindings.GetEnabledBindings())
        {
            string methodName = GenerateSafeEventMethodName(binding.variableName, binding.shortTypeName);
            if (generatedHandlers.Contains(methodName))
                continue;

            generatedHandlers.Add(methodName);

            switch (binding.shortTypeName)
            {
                case "Button":
                    code.AppendLine($"        if ({binding.variableName} != null)");
                    code.AppendLine($"        {{");
                    code.AppendLine($"            {binding.variableName}.onClick.AddListener({methodName});");
                    code.AppendLine($"        }}");
                    break;

                case "Toggle":
                    code.AppendLine($"        if ({binding.variableName} != null)");
                    code.AppendLine($"        {{");
                    code.AppendLine($"            {binding.variableName}.onValueChanged.AddListener({methodName});");
                    code.AppendLine($"        }}");
                    break;

                case "InputField":
                    code.AppendLine($"        if ({binding.variableName} != null)");
                    code.AppendLine($"        {{");
                    code.AppendLine($"            {binding.variableName}.onEndEdit.AddListener({methodName});");
                    code.AppendLine($"        }}");
                    break;

                case "Slider":
                case "Scrollbar":
                    code.AppendLine($"        if ({binding.variableName} != null)");
                    code.AppendLine($"        {{");
                    code.AppendLine($"            {binding.variableName}.onValueChanged.AddListener({methodName});");
                    code.AppendLine($"        }}");
                    break;

                case "Dropdown":
                    code.AppendLine($"        if ({binding.variableName} != null)");
                    code.AppendLine($"        {{");
                    code.AppendLine($"            {binding.variableName}.onValueChanged.AddListener({methodName});");
                    code.AppendLine($"        }}");
                    break;
            }
        }

        return code.ToString();
    }

    /// <summary>
    /// 生成事件处理方法
    /// </summary>
    /// <param name="bindings">绑定数据</param>
    /// <returns>事件处理方法代码</returns>
    private static string GenerateEventHandlers(UIPanelBindings bindings)
    {
        var code = new System.Text.StringBuilder();
        var generatedHandlers = new HashSet<string>();

        foreach (var binding in bindings.GetEnabledBindings())
        {
            string methodName = GenerateSafeEventMethodName(binding.variableName, binding.shortTypeName);

            // 避免重复生成相同的方法
            if (generatedHandlers.Contains(methodName))
                continue;

            generatedHandlers.Add(methodName);

            switch (binding.shortTypeName)
            {
                case "Button":
                    code.AppendLine($"    private void {methodName}()");
                    code.AppendLine("    {");
                    code.AppendLine($"        // TODO: 处理 {binding.variableName} 点击事件");
                    code.AppendLine($"        Debug.Log(\"{binding.variableName} clicked\");");
                    code.AppendLine("    }");
                    break;

                case "Toggle":
                    code.AppendLine($"    private void {methodName}(bool isOn)");
                    code.AppendLine("    {");
                    code.AppendLine($"        // TODO: 处理 {binding.variableName} 切换事件");
                    code.AppendLine($"        Debug.Log($\"{binding.variableName} toggled: {{isOn}}\");");
                    code.AppendLine("    }");
                    break;

                case "InputField":
                    code.AppendLine($"    private void {methodName}(string text)");
                    code.AppendLine("    {");
                    code.AppendLine($"        // TODO: 处理 {binding.variableName} 输入结束事件");
                    code.AppendLine($"        Debug.Log($\"{binding.variableName} input ended: {{text}}\");");
                    code.AppendLine("    }");
                    break;

                case "Slider":
                case "Scrollbar":
                    code.AppendLine($"    private void {methodName}(float value)");
                    code.AppendLine("    {");
                    code.AppendLine($"        // TODO: 处理 {binding.variableName} 数值变化事件");
                    code.AppendLine($"        Debug.Log($\"{binding.variableName} value changed: {{value}}\");");
                    code.AppendLine("    }");
                    break;

                case "Dropdown":
                    code.AppendLine($"    private void {methodName}(int index)");
                    code.AppendLine("    {");
                    code.AppendLine($"        // TODO: 处理 {binding.variableName} 选择变化事件");
                    code.AppendLine($"        Debug.Log($\"{binding.variableName} option changed: {{index}}\");");
                    code.AppendLine("    }");
                    break;
            }

            code.AppendLine();
        }

        return code.ToString();
    }

    #endregion

    #region 工具方法

    private const string DEFAULT_SCRIPT_FOLDER = "Assets/Scripts/UI";

    /// <summary>
    /// 获取命名空间声明
    /// </summary>
    private static string GetNamespaceDeclaration(GenerationConfig config)
    {
        return config.useNamespace && !string.IsNullOrEmpty(config.namespaceStr)
            ? $"namespace {config.namespaceStr}"
            : "";
    }

    /// <summary>
    /// 获取基类声明
    /// </summary>
    private static string GetBaseClassDeclaration(GenerationConfig config)
    {
        return !string.IsNullOrEmpty(config.baseClassOrInterfaceNames)
            ? config.baseClassOrInterfaceNames
            : "";
    }

    /// <summary>
    /// 获取绑定类名
    /// </summary>
    private static string GetBindingClassName(UIPanelBindings bindings)
    {
        string className = bindings.panelName;

        // 移除空格和特殊字符
        className = System.Text.RegularExpressions.Regex.Replace(className, @"[^a-zA-Z0-9_]", "");

        // 确保首字母大写
        if (className.Length > 0)
        {
            className = char.ToUpper(className[0]) + className[1..];
        }

        return className;
    }

    /// <summary>
    /// 获取绑定脚本文件名
    /// </summary>
    private static string GetBindingScriptName(UIPanelBindings bindings, GenerationConfig config)
    {
        string className = GetBindingClassName(bindings);

        switch (config.combineMethod)
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
    /// 生成安全的事件方法名
    /// 格式：On{对象名称}{事件类型}
    /// </summary>
    private static string GenerateSafeEventMethodName(string variableName, string componentType)
    {
        string methodName = "On" + variableName;

        // 根据组件类型添加事件类型后缀
        methodName += componentType switch
        {
            "Button" => "Click",
            "Toggle" => "ValueChanged",
            "InputField" => "EndEdit",
            "Slider" or "Scrollbar" => "ValueChanged",
            "Dropdown" => "ValueChanged",
            _ => "Event"
        };

        return methodName;
    }

    /// <summary>
    /// 获取缩进字符串
    /// </summary>
    private static string GetIndent(int level)
    {
        return new string(' ', level * 4);
    }

    /// <summary>
    /// 为多行文本添加缩进
    /// </summary>
    private static string AddIndentToMultiLine(string text, int indentLevel)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        string indent = GetIndent(indentLevel);
        string[] lines = text.Split('\n');

        for (int i = 0; i < lines.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(lines[i]))
            {
                lines[i] = indent + lines[i];
            }
        }

        return string.Join("\n", lines);
    }

    /// <summary>
    /// 确保文件夹存在
    /// </summary>
    private static void EnsureFolderExists(string folderPath)
    {
        if (!Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
            AssetDatabase.Refresh();
        }
    }

    #endregion
}