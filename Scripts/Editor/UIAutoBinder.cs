using UnityEditor;
using UnityEngine;
using System;
using System.IO;
using System.Reflection;
using Unity.VisualScripting;

/// <summary>
/// UI自动绑定器
/// 用于在代码编译完成后自动将生成的UI主脚本挂载到对应的游戏对象上
/// </summary>
public static class UIAutoBinder
{
    private static BindingTask bindingTask;
    private static bool isInitialized = false;
    // 使用EditorPrefs来持久化绑定任务，防止静态数据在编译时丢失
    private const string BINDING_TASKS_KEY = "Unity_UIAutoBinder_BindingTask";
    private const string BINDING_TASKS = "Unity_UIAutoBinder_BindingTask_State";

    /// <summary>
    /// 绑定任务
    /// </summary>
    [Serializable]
    public class BindingTask
    {
        public int targetPanelInstanceId;
        public string mainScriptClassName;
        public string bindingsDataPath;
    }

    /// <summary>
    /// 初始化自动绑定器
    /// </summary>
    [InitializeOnLoadMethod]
    private static void Initialize()
    {
        if (!isInitialized)
        {
            isInitialized = true;
            // 从EditorPrefs加载之前保存的绑定任务
            LoadBindingTasks();
        }
    }

    /// <summary>
    /// 脚本重载完成后的回调
    /// </summary>
    [UnityEditor.Callbacks.DidReloadScripts]
    private static void OnScriptsReloaded()
    {
        string bind = EditorPrefs.GetString(BINDING_TASKS, "False");
        if (bind == "True")
        {
            // 从EditorPrefs重新加载绑定任务
            LoadBindingTasks();
            // 延迟执行绑定，确保所有组件都已加载完成
            EditorApplication.delayCall += ProcessBindingTask;
        }
    }

    /// <summary>
    /// 注册绑定任务
    /// </summary>
    /// <param name="targetPanel">目标面板对象</param>
    /// <param name="mainScriptClassName">主脚本类名</param>
    /// <param name="mainScriptPath">主脚本文件路径</param>
    /// <param name="bindingsDataPath">绑定数据路径</param>
    public static void RegisterBindingTask(GameObject targetPanel, string mainScriptClassName, string mainScriptPath, string bindingsDataPath)
    {
        if (targetPanel == null || string.IsNullOrEmpty(mainScriptClassName))
        {
            Debug.LogWarning("UIAutoBinder: 无效的绑定任务参数");
            return;
        }

        var task = new BindingTask
        {
            targetPanelInstanceId = targetPanel.GetInstanceID(),
            mainScriptClassName = mainScriptClassName,
            bindingsDataPath = bindingsDataPath
        };

        bindingTask = task;
        // 立即保存到EditorPrefs
        SaveBindingTasks();

        Debug.Log($"UIAutoBinder: 注册绑定任务 - {mainScriptClassName} -> {targetPanel.name} (ID: {task.targetPanelInstanceId})");
        EditorPrefs.SetString(BINDING_TASKS, "True");
    }

    /// <summary>
    /// 保存绑定任务到EditorPrefs
    /// </summary>
    private static void SaveBindingTasks()
    {
        try
        {
            string json = JsonUtility.ToJson(bindingTask);
            EditorPrefs.SetString(BINDING_TASKS_KEY, json);
        }
        catch (Exception e)
        {
            Debug.LogError($"UIAutoBinder: 保存绑定任务失败: {e.Message}");
        }
    }

    /// <summary>
    /// 从EditorPrefs加载绑定任务
    /// </summary>
    private static void LoadBindingTasks()
    {
        try
        {
            if (!EditorPrefs.HasKey(BINDING_TASKS_KEY))
            {
                bindingTask = new BindingTask();
                return;
            }

            string json = EditorPrefs.GetString(BINDING_TASKS_KEY, "");
            if (string.IsNullOrEmpty(json))
            {
                bindingTask = new BindingTask();
                return;
            }

            var task = JsonUtility.FromJson<BindingTask>(json);
            if (task != null)
            {
                bindingTask = task;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"UIAutoBinder: 加载绑定任务失败: {e.Message}");
            bindingTask = new BindingTask();
        }
    }

    /// <summary>
    /// 处理绑定任务
    /// </summary>
    private static void ProcessBindingTask()
    {
        if (bindingTask == null)
        {
            Debug.LogError("UIAutoBinder: 绑定任务为空");
            return;
        }
        try
        {
            // 通过实例ID查找目标面板
            GameObject targetPanel = FindTargetPanel(bindingTask.targetPanelInstanceId);

            // 检查目标面板是否仍然存在
            if (targetPanel == null)
            {
                Debug.LogWarning($"UIAutoBinder: 目标面板已被销毁或无法找到 - {bindingTask.mainScriptClassName}");
                return; // 任务已完成（虽然失败）
            }

            Type componentType = GetScriptType(bindingTask.mainScriptClassName);
            Component component = null;
            if (!targetPanel.TryGetComponent(componentType,out component))
            {
                // 尝试获取脚本类型
                if (componentType == null)
                {
                    Debug.LogError($"UIAutoBinder: 无法找到脚本类型 - {bindingTask.mainScriptClassName}");
                    return; // 稍后重试
                }
                // 添加组件到游戏对象
                component = targetPanel.AddComponent(componentType);
                if (component == null)
                {
                    Debug.LogError($"UIAutoBinder: 添加组件失败 - {bindingTask.mainScriptClassName}");
                    return;
                }
            }
            // 选中该游戏对象
            Selection.activeGameObject = targetPanel;
            // 延迟执行字段绑定，确保序列化系统完成初始加载
            EditorApplication.delayCall += () => ProcessFieldBindings(component, bindingTask.bindingsDataPath);

        }
        catch (Exception e)
        {
            Debug.LogError($"UIAutoBinder: 处理绑定任务时发生错误 - {bindingTask.mainScriptClassName}: {e.Message}");
            return;
        }
        finally
        {
            EditorPrefs.SetString(BINDING_TASKS, "False");
        }
    }

    /// <summary>
    /// 处理字段绑定
    /// </summary>
    /// <param name="targetPanel">目标面板对象</param>
    /// <param name="component">组件引用</param>
    /// <param name="bindingsDataPath">绑定数据路径</param>
    private static void ProcessFieldBindings(Component component, string bindingsDataPath)
    {
        if (string.IsNullOrEmpty(bindingsDataPath))
        {
            Debug.LogWarning("UIAutoBinder: 绑定数据路径为空，跳过字段绑定");
            return;
        }
        GameObject targetPanel = Selection.activeGameObject;
        // 加载绑定数据
        UIPanelBindings bindings = UIBindDataManager.LoadBindings(bindingsDataPath);
        if (bindings == null)
        {
            Debug.LogError($"UIAutoBinder: 无法加载绑定数据: {bindingsDataPath}");
            return;
        }

        // 使用SerializedObject系统进行字段赋值
        SerializedObject serializedObject = new(component);
        // 遍历所有绑定项，查找对应的字段
        foreach (var binding in bindings.bindings)
        {
            try
            {
                if (binding == null || binding.isEnabled == false)
                    continue;

                string fieldName = binding.variableName;
                if (string.IsNullOrEmpty(fieldName))
                    continue;

                // 查找对应的SerializedProperty
                SerializedProperty property = serializedObject.FindProperty(fieldName);
                if (property == null)
                {
                    Debug.LogWarning($"UIAutoBinder: 未找到字段 {fieldName}");
                    continue;
                }
                //绑定字段
                Type type = Type.GetType(binding.assemblyQualifiedName);
                if (binding.targetObjectRelativePath == "[ROOT]")
                {
                    property.objectReferenceValue = targetPanel.GetComponent(type);
                }
                else
                {
                    property.objectReferenceValue = targetPanel.transform.Find(binding.targetObjectRelativePath)?.GetComponent(type);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"UIAutoBinder: 字段{binding.variableName}绑定失败: {e.Message}");
            }
        }
        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(component);
        EditorUtility.SetDirty(targetPanel);
        AssetDatabase.SaveAssets();
    }

    /// <summary>
    /// 通过实例ID和路径查找目标面板
    /// </summary>
    /// <param name="instanceId">实例ID</param>
    /// <param name="assetPath">资源路径</param>
    /// <returns>找到的游戏对象</returns>
    private static GameObject FindTargetPanel(int instanceId)
    {
        var obj = EditorUtility.InstanceIDToObject(instanceId) as GameObject;
        if (obj != null)
            return obj;
        return null;
    }

    /// <summary>
    /// 获取脚本类型
    /// </summary>
    /// <param name="className">类名</param>
    /// <returns>脚本类型，如果找不到返回null</returns>
    private static Type GetScriptType(string className)
    {
        // 搜索所有程序集中的类型
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                var types = assembly.GetTypes();
                foreach (var type in types)
                {
                    if (type.Name == className && typeof(MonoBehaviour).IsAssignableFrom(type))
                        return type;
                }
            }
            catch (Exception)
            {
                // 忽略无法访问的程序集
                continue;
            }
        }

        return null;
    }
}