using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

//TreeViewItem
public class UIBindToolTreeViewItem : TreeViewItem
{
    public GameObject boundObject;
    public GameObject boundObjectInPrefab;
    public UIBindToolTreeViewItem(int id, int depth, string displayName, GameObject obj,GameObject objPrefab) : base(id, depth, displayName)
    {
        boundObject = obj;
        boundObjectInPrefab = objPrefab;
    }
}