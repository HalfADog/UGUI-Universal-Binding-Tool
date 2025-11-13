using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 代码生成配置
/// </summary>
public class GenerationConfig
{
    public string namespaceStr;
    public string baseClassOrInterfaceNames;
    public bool useNamespace;
    public string uiBindScriptFolder;
    public string uiMainScriptFolder;
    public string templateTextFilePath;
}

/// <summary>
/// 代码生成结果
/// </summary>
public struct GenerationResult
{
    public bool success;
    public string bindingScriptPath;
    public string mainScriptPath;
    public GameObject targetPanel;
    public string mainScriptClassName;
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
            GenerationResult generationResult = GenerateScriptFiles(bindings, config);
            result.bindingScriptPath = generationResult.bindingScriptPath;
            result.mainScriptPath = generationResult.mainScriptPath;
            result.mainScriptClassName = generationResult.mainScriptClassName;

            // 获取目标面板对象
            GameObject targetPanel = GetRootPanelObject(bindings);
            result.targetPanel = targetPanel;

            result.success = generationResult.success;
            result.errorMessage = generationResult.errorMessage;
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
            namespaceStr = settings.scriptNamespace,
            useNamespace = settings.useNamespace,
            baseClassOrInterfaceNames = settings.baseClassOrInterfaceNames,
            uiBindScriptFolder = settings.generateUIBindScriptFolder,
            uiMainScriptFolder = settings.generateUILogicScriptFolder,
            templateTextFilePath = settings.templateTextFilePath,
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
    /// <returns>生成结果，包含所有生成文件的路径</returns>
    private static GenerationResult GenerateScriptFiles(UIPanelBindings bindings, GenerationConfig config)
    {
        var result = new GenerationResult { success = false };

        try
        {
            // 确保文件夹存在
            EnsureFolderExists(config.uiBindScriptFolder);
            if (!string.IsNullOrEmpty(config.uiMainScriptFolder))
            {
                EnsureFolderExists(config.uiMainScriptFolder);
            }

            // 生成绑定脚本
            string bindingCode = GenerateBindingCode(bindings, config);
            string bindingPath = Path.Combine(config.uiBindScriptFolder, GetBindingScriptName(bindings, config));
            File.WriteAllText(bindingPath, bindingCode);
            result.bindingScriptPath = bindingPath;
            Debug.Log($"生成绑定脚本: {bindingPath}");

            // 生成主脚本（如果指定了模板文件且主脚本不存在）
            string mainScriptPath = null;
            if (!string.IsNullOrEmpty(config.templateTextFilePath) &&
                !string.IsNullOrEmpty(config.uiMainScriptFolder))
            {
                mainScriptPath = Path.Combine(config.uiMainScriptFolder, GetMainScriptName(bindings));

                // 始终设置类名（即使主脚本已存在）
                result.mainScriptClassName = GetBindingClassName(bindings);

                // 检查主脚本是否已存在
                if (File.Exists(mainScriptPath))
                {
                    Debug.Log($"主脚本已存在，跳过生成: {mainScriptPath}");
                }
                else
                {
                    string mainScriptCode = GenerateMainScriptFromTemplate(bindings, config);
                    if (!string.IsNullOrEmpty(mainScriptCode))
                    {
                        File.WriteAllText(mainScriptPath, mainScriptCode);
                        result.mainScriptPath = mainScriptPath;
                        Debug.Log($"生成主脚本: {mainScriptPath}");
                    }
                }
            }

            // 处理变量名重命名（如果主脚本已存在且有变量名变更）
            if (!string.IsNullOrEmpty(mainScriptPath) && File.Exists(mainScriptPath))
            {
                // 收集所有有重命名历史的绑定项
                var renamedBindings = bindings.bindings?.Where(b => b != null && b.HasVariableNameChanged()).ToList();

                if (renamedBindings != null && renamedBindings.Count > 0)
                {
                    try
                    {
                        Debug.Log($"[UIBindScriptGenerator] 检测到 {renamedBindings.Count} 个变量名需要更新");

                        // 读取主脚本内容
                        string mainScriptContent = File.ReadAllText(mainScriptPath);
                        int totalReplacements = 0;

                        // 执行变量名替换
                        foreach (var binding in renamedBindings)
                        {
                            string oldName = binding.previousVariableName;
                            string newName = binding.variableName;

                            // 使用正则表达式精确匹配变量名（使用单词边界）
                            string pattern = $@"\b{Regex.Escape(oldName)}\b";

                            // 执行替换
                            int replacements = 0;
                            mainScriptContent = Regex.Replace(mainScriptContent, pattern, m =>
                            {
                                replacements++;
                                return newName;
                            });

                            if (replacements > 0)
                            {
                                Debug.Log($"[UIBindScriptGenerator] 替换变量名: {oldName} → {newName} ({replacements} 处)");
                                totalReplacements += replacements;
                            }

                            // 清除重命名历史（避免重复替换）
                            binding.ClearRenameHistory();
                        }

                        // 如果有替换，保存修改后的主脚本
                        if (totalReplacements > 0)
                        {
                            File.WriteAllText(mainScriptPath, mainScriptContent);
                            Debug.Log($"[UIBindScriptGenerator] 更新主脚本完成，共替换 {totalReplacements} 处");
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"[UIBindScriptGenerator] 更新主脚本变量名时出错: {e.Message}");
                    }
                }
            }

            result.success = true;
        }
        catch (Exception e)
        {
            result.errorMessage = $"生成脚本时发生错误: {e.Message}";
            Debug.LogError($"生成脚本失败: {e}");
        }

        return result;
    }

    /// <summary>
    /// 生成绑定代码
    /// </summary>
    /// <param name="bindings">绑定数据</param>
    /// <param name="config">配置</param>
    /// <returns>生成的代码</returns>
    public static string GenerateBindingCode(UIPanelBindings bindings, GenerationConfig config)
    {
        return GeneratePartialClassCode(bindings, config);
    }

    #endregion

    #region 命名空间处理

    /// <summary>
    /// 收集所有绑定项的命名空间
    /// </summary>
    /// <param name="bindings">绑定数据</param>
    /// <returns>去重后的命名空间列表</returns>
    private static HashSet<string> CollectNamespaces(UIPanelBindings bindings)
    {
        var namespaces = new HashSet<string>();
        if (bindings == null || bindings.bindings == null)
            return namespaces;
        namespaces.Add("UnityEngine");
        // 使用确保变量名唯一的绑定列表
        var uniqueBindings = EnsureUniqueVariableNames(bindings.bindings);

        foreach (var binding in uniqueBindings)
        {
            if (binding == null || string.IsNullOrEmpty(binding.componentNamespace))
                continue;
            namespaces.Add(binding.componentNamespace);
        }

        return namespaces;
    }

    /// <summary>
    /// 生成动态using语句
    /// </summary>
    /// <param name="bindings">绑定数据</param>
    /// <returns>using语句代码</returns>
    private static string GenerateUsingStatements(UIPanelBindings bindings)
    {
        var code = new System.Text.StringBuilder();
        // 收集并添加动态命名空间
        var namespaces = CollectNamespaces(bindings);
        if (namespaces.Count > 0)
        {
            // 排序命名空间以确保输出的一致性
            var sortedNamespaces = namespaces.OrderBy(n => n).ToList();
            foreach (var ns in sortedNamespaces)
            {
                code.AppendLine($"using {ns};");
            }
        }
        code.AppendLine();
        return code.ToString();
    }

    #endregion

    #region 代码模板生成

    
    /// <summary>
    /// 生成部分类代码
    /// </summary>
    private static string GeneratePartialClassCode(UIPanelBindings bindings, GenerationConfig config)
    {
        var code = new System.Text.StringBuilder();
        bool hasNamespace = !string.IsNullOrEmpty(GetNamespaceDeclaration(config));

        // 动态生成using语句
        string usingStatements = GenerateUsingStatements(bindings);
        code.Append(usingStatements);

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
        code.AppendLine($"{classIndent}/// {bindings.panelName} UI字段定义（自动生成）");
        code.AppendLine($"{classIndent}/// </summary>");

        // 构建类声明行
        string classDeclaration = $"{classIndent}public partial class {className}";
        if (!string.IsNullOrEmpty(config.baseClassOrInterfaceNames))
        {
            classDeclaration += $" : {config.baseClassOrInterfaceNames}";
        }
        code.AppendLine(classDeclaration);
        code.AppendLine($"{classIndent}{{");

        // 绑定部分 - 字段声明
        string memberIndent = hasNamespace ? GetIndent(2) : "    ";
        code.AppendLine($"{memberIndent}[Header(\"UI Bindings\")]");
        string bindingFields = GenerateBindingFields(bindings, "private");
        code.AppendLine(AddIndentToMultiLine(bindingFields, hasNamespace ? 1 : 0));

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

        // 在生成代码前进行最终的重复检查和修正
        var uniqueBindings = EnsureUniqueVariableNames(bindings.bindings);

        foreach (var binding in uniqueBindings)
        {
            string accessModifier = binding.accessModifier.ToString().ToLower();
            if (string.IsNullOrEmpty(accessModifier))
                accessModifier = defaultAccessModifier.ToLower();

            code.AppendLine($"    [SerializeField]");
            code.AppendLine($"    {accessModifier} {binding.shortTypeName} {binding.variableName};");
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
        return $"{className}.Bind.cs";
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

    /// <summary>
    /// 确保所有绑定项的变量名都是唯一的
    /// 这是代码生成前的最后一道保障
    /// </summary>
    /// <param name="bindings">绑定项列表</param>
    /// <returns>变量名唯一的绑定项列表</returns>
    private static List<UIBindItem> EnsureUniqueVariableNames(List<UIBindItem> bindings)
    {
        if (bindings == null || bindings.Count == 0)
            return new List<UIBindItem>();

        var uniqueBindings = new List<UIBindItem>();
        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var binding in bindings)
        {
            if (binding == null)
                continue;

            string originalName = binding.variableName;
            string finalName = originalName;

            // 如果变量名已使用，生成唯一名称
            int suffix = 1;
            while (usedNames.Contains(finalName))
            {
                finalName = $"{originalName}{suffix}";
                suffix++;
            }

            // 如果名称被修改，创建新的绑定项（避免修改原始数据）
            if (finalName != originalName)
            {
                var newBinding = new UIBindItem();
                // 复制所有属性
                newBinding.targetInstanceID = binding.targetInstanceID;
                newBinding.targetObjectFileID = binding.targetObjectFileID;
                newBinding.targetObjectFullPathInScene = binding.targetObjectFullPathInScene;
                newBinding.targetObjectRelativePath = binding.targetObjectRelativePath;
                newBinding.targetObjectName = binding.targetObjectName;
                newBinding.shortTypeName = binding.shortTypeName;
                newBinding.componentTypeName = binding.componentTypeName;
                newBinding.componentNamespace = binding.componentNamespace;
                newBinding.SetComponentType(binding.GetComponentType());
                newBinding.variableName = finalName;
                newBinding.accessModifier = binding.accessModifier;
                newBinding.isEnabled = binding.isEnabled;

                uniqueBindings.Add(newBinding);
                Debug.LogWarning($"变量名重复：'{originalName}' 已调整为 '{finalName}' 以确保代码生成正确");
            }
            else
            {
                uniqueBindings.Add(binding);
            }

            usedNames.Add(finalName);
        }

        return uniqueBindings;
    }

    /// <summary>
    /// 根据模板生成主脚本代码
    /// </summary>
    /// <param name="bindings">绑定数据</param>
    /// <param name="config">配置</param>
    /// <returns>生成的代码</returns>
    private static string GenerateMainScriptFromTemplate(UIPanelBindings bindings, GenerationConfig config)
    {
        if (string.IsNullOrEmpty(config.templateTextFilePath))
        {
            Debug.LogWarning("未指定模板文件，无法生成主脚本");
            return null;
        }

        try
        {
            // 读取模板文件
            string templateContent = File.ReadAllText(config.templateTextFilePath);

            // 获取类名
            string className = GetBindingClassName(bindings);

            // 替换占位符
            string generatedCode = templateContent.Replace("<ClassName>", className);

            return generatedCode;
        }
        catch (Exception e)
        {
            Debug.LogError($"读取模板文件失败: {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// 获取主脚本文件名
    /// </summary>
    /// <param name="bindings">绑定数据</param>
    /// <returns>主脚本文件名</returns>
    private static string GetMainScriptName(UIPanelBindings bindings)
    {
        string className = GetBindingClassName(bindings);
        return $"{className}.cs";
    }

    /// <summary>
    /// 获取根面板游戏对象
    /// </summary>
    /// <param name="bindings">绑定数据</param>
    /// <returns>根面板游戏对象</returns>
    private static GameObject GetRootPanelObject(UIPanelBindings bindings)
    {
        if (string.IsNullOrEmpty(bindings.panelPrefabGUID))
            return null;

        // 通过GUID查找预制体
        string assetPath = AssetDatabase.GUIDToAssetPath(bindings.panelPrefabGUID);
        if (string.IsNullOrEmpty(assetPath))
            return null;

        // 加载预制体
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        return prefab;
    }

    #endregion
}