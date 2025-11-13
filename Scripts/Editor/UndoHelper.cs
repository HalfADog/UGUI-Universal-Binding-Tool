using UnityEditor;
using UnityEngine;

/// <summary>
/// 撤销/重做辅助类
/// 统一管理所有撤销相关的操作
/// </summary>
public static class UndoHelper
{
    /// <summary>
    /// 记录绑定数据的修改
    /// </summary>
    /// <param name="bindings">绑定数据对象</param>
    /// <param name="operationName">操作名称</param>
    public static void RecordBindings(UIPanelBindings bindings, string operationName)
    {
        if (bindings != null)
        {
            Undo.RecordObject(bindings, operationName);
        }
    }

    /// <summary>
    /// 记录多个对象的修改
    /// </summary>
    public static void RecordObjects(UnityEngine.Object[] objects, string operationName)
    {
        if (objects != null && objects.Length > 0)
        {
            Undo.RecordObjects(objects, operationName);
        }
    }

    /// <summary>
    /// 执行操作并注册撤销
    /// </summary>
    /// <param name="bindings">绑定数据</param>
    /// <param name="operationName">操作名称</param>
    /// <param name="action">要执行的操作</param>
    public static void PerformUndoableOperation(UIPanelBindings bindings, string operationName, System.Action action)
    {
        if (bindings == null || action == null)
        {
            Debug.LogWarning("无法执行撤销操作：绑定数据或操作为空");
            return;
        }

        // 记录状态
        RecordBindings(bindings, operationName);

        // 执行操作
        action.Invoke();

        // 保存修改
        bindings.lastModifiedTime = System.DateTime.Now;
        EditorUtility.SetDirty(bindings);

        // 通知场景已修改
        MarkSceneDirty();
    }

    /// <summary>
    /// 标记当前场景为已修改
    /// </summary>
    public static void MarkSceneDirty()
    {
        var activeScene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
        if (activeScene.isLoaded)
        {
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(activeScene);
            Debug.Log("[UndoHelper] 场景已标记为已修改");
        }
    }

    /// <summary>
    /// 清空撤销栈（谨慎使用）
    /// </summary>
    public static void ClearUndoStack(UnityEngine.Object obj)
    {
        if (obj != null)
        {
            Undo.ClearUndo(obj);
            Debug.Log($"[UndoHelper] 已清空 {obj.name} 的撤销栈");
        }
    }

    /// <summary>
    /// 获取撤销操作的友好名称
    /// </summary>
    public static string GetUndoName(string action, GameObject targetObject, System.Type componentType = null)
    {
        string objectName = targetObject != null ? targetObject.name : "未知对象";
        string componentName = componentType != null ? componentType.Name : "组件";
        return $"{action} {objectName}.{componentName}";
    }

    /// <summary>
    /// 获取删除多个绑定的撤销名称
    /// </summary>
    public static string GetDeleteAllUndoName(GameObject targetObject, int count)
    {
        string objectName = targetObject != null ? targetObject.name : "未知对象";
        return $"删除 {objectName} 的 {count} 个绑定";
    }
}
