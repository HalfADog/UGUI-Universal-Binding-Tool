using System;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 单个绑定项的数据结构
/// </summary>
[Serializable]
public class UIBindItem
{
    [HideInInspector]
    public int targetInstanceID; // 目标对象的实例ID
    public long targetObjectFileID; // 目标对象的文件ID
    public string targetObjectFullPathInScene; // 目标对象的绝对路径
    public string targetObjectRelativePath; // 目标对象相对于面板的路径
    public string targetObjectName; // 目标对象的名称
    public string shortTypeName; // 简短类型名
    public string componentTypeName; // 组件类型名称
    public string variableName; // 变量名
    private string assemblyQualifiedName;// 完全限定名
    public AccessModifier accessModifier; // 访问修饰符
    public bool isEnabled; // 是否启用该绑定

    public UIBindItem()
    {
        targetInstanceID = 0;
        targetObjectFileID = 0;
        targetObjectFullPathInScene = "";
        targetObjectRelativePath = "";
        targetObjectName = "";
        componentTypeName = "";
        shortTypeName = "";
        assemblyQualifiedName = "";
        accessModifier = AccessModifier.Private;
        variableName = "";
        isEnabled = true;
    }

    /// <summary>
    /// 带面板参数的构造函数，用于存储相对路径
    /// </summary>
    public UIBindItem(GameObject targetInstance, GameObject targetPrefab, GameObject panelRoot, Type componentType, AccessModifier access, string varName)
    {
        if (targetInstance != null)
        {
            targetInstanceID = targetInstance.GetInstanceID();
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(targetPrefab, out string guid, out targetObjectFileID);
            //Debug.Log(targetObjectFileID);

            // 优先使用相对路径，如果无法计算则使用绝对路径
            if (panelRoot != null)
            {
                targetObjectRelativePath = UIPanelBindings.GetGameObjectRelativePath(panelRoot, targetInstance);
                // 如果相对路径为空，说明目标对象就是面板根对象，设置为特殊标识
                if (string.IsNullOrEmpty(targetObjectRelativePath))
                {
                    targetObjectRelativePath = "[ROOT]";
                }
                targetObjectFullPathInScene = UIPanelBindings.GetGameObjectFullPath(targetInstance);
            }
            else
            {
                targetObjectRelativePath = "";
                targetObjectFullPathInScene = UIPanelBindings.GetGameObjectFullPath(targetInstance);
            }

            targetObjectName = targetInstance.name;
        }
        else
        {
            targetInstanceID = 0;
            targetObjectFullPathInScene = "";
            targetObjectRelativePath = "";
            targetObjectName = "";
        }

        componentTypeName = componentType.FullName;
        shortTypeName = componentType.Name;
        assemblyQualifiedName = componentType.AssemblyQualifiedName;
        accessModifier = access;
        variableName = varName;
        isEnabled = true;
    }

    /// <summary>
    /// 获取目标对象（对象在场景中的实例）
    /// </summary>
    public GameObject GetTargetObject()
    {
        if (targetInstanceID == 0)
            return null;

        // 首先尝试通过实例ID获取
        var obj = EditorUtility.InstanceIDToObject(targetInstanceID) as GameObject;
        if (obj != null)
        {
            return obj;
        }

        // 如果实例ID失败，尝试通过路径获取
        if (!string.IsNullOrEmpty(targetObjectFullPathInScene))
        {
            return GameObject.Find(targetObjectFullPathInScene);
        }

        return null;
    }

    /// <summary>
    /// 获取组件类型
    /// </summary>
    public Type GetComponentType()
    {
        if (string.IsNullOrEmpty(assemblyQualifiedName))
            return null;

        // 直接使用FullName获取类型
        return Type.GetType(assemblyQualifiedName);
    }

    /// <summary>
    /// 设置组件类型
    /// </summary>
    public void SetComponentType(Type type)
    {
        componentTypeName = type.FullName;
        assemblyQualifiedName = type.AssemblyQualifiedName;
    }

    /// <summary>
    /// 验证目标对象是否仍然有效
    /// </summary>
    public bool IsValidTarget()
    {
        var target = GetTargetObject();
        return target != null && target.name == targetObjectName;
    }
}