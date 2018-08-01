﻿using System.IO;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(LightingManager))]
public class LightingManagerEditor : Editor
{
	private LightingManager manager;
	private int selectedGroup;

	private void OnEnable()
	{
		manager = target as LightingManager;
		if (manager == null) return;
		selectedGroup = manager.activeGroup;
	}

	public override void OnInspectorGUI()
	{
		base.OnInspectorGUI();
		if (manager == null) return;

		// common features
		if (GUILayout.Button("Copy lightmap files to Selected Group"))
		{
			if (EditorUtility.DisplayDialog("Alert", "Do you want to copy baked lighting files to selected group?", "Ok", "Cancel"))
			{
				manager.CopyLightmapFilesToGroup(selectedGroup);
			}
		}

		if (GUILayout.Button("Auto set lightmap files to groups")) manager.AutoSetLightmapFiles();
		if (GUILayout.Button("Apply Selected Group changes")) manager.SwitchToGroup(selectedGroup);

		// apply group on selection change
		var values = new string[manager.lightGroups.Length];
		for (int i = 0; i != values.Length; ++i) values[i] = Path.GetFileName(manager.lightGroups[i].sourceFolder);
		if (manager.activeGroup != selectedGroup)
		{
			selectedGroup = manager.activeGroup;
			manager.SwitchToGroup(selectedGroup);
		}

		selectedGroup = EditorGUILayout.Popup(selectedGroup, values);
		if (manager.activeGroup != selectedGroup) manager.SwitchToGroup(selectedGroup);
	}
}