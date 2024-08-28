using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
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
		manager.EditorConfigure();

		// validate state
		if (manager.lightGroups == null || manager.lightGroups.Length == 0 || string.IsNullOrWhiteSpace(manager.bakedFolder)) return;

		// bake features
		if (GUILayout.Button("Bake Group"))
		{
			if (manager.lightGroups == null || manager.lightGroups.Length == 0)
			{
				EditorUtility.DisplayDialog("Alert", "No selected groups", "Ok");
			}
			else
			{
				manager.AutoSetLightmapFiles();
				var activeScene = EditorSceneManager.GetActiveScene();
				if (EditorSceneManager.SaveScene(activeScene))
				{
					Lightmapping.lightingDataAsset = null;
					Lightmapping.BakeAsync();
				}
				else
				{
					EditorUtility.DisplayDialog("Alert", "Failed to save scene", "Ok");
				}
			}
		}

		/*if (GUILayout.Button("Test"))
		{
			foreach (var o in UnityEngine.Object.FindObjectsOfType<GameObject>(false))
			{
				if (!o.isStatic) continue;
				Debug.Log($"Name:'{o.name}' ID:{o.GetInstanceID()}");
			}
		}*/

		/*// manual copy button
		if (GUILayout.Button("Copy main lightmap files to Selected-Group folder"))
		{
			if (EditorUtility.DisplayDialog("Alert", "Do you want to copy baked lighting files to selected group?", "Ok", "Cancel"))
			{
				manager.activeGroup = selectedGroup;
				manager.CopyLightmapFilesToGroup();
			}
		}

		if (GUILayout.Button("Detect and set lightmap textures to Selected-Group")) manager.AutoSetLightmapFiles();

		// manual apply butten
		if (manager.lightGroups == null || manager.lightGroups.Length == 0) return;
		if (GUILayout.Button("Apply Selected Group to scene")) manager.SwitchToGroup(selectedGroup);*/

		// apply group on selection change
		var values = new string[manager.lightGroups.Length];
		for (int i = 0; i != values.Length; ++i) values[i] = Path.GetFileName(manager.lightGroups[i].GetSourcePath());
		if (manager.activeGroup != selectedGroup)
		{
			selectedGroup = manager.activeGroup;
			manager.SwitchToGroup(selectedGroup);
			selectedGroup = manager.activeGroup;
		}

		selectedGroup = EditorGUILayout.Popup(selectedGroup, values);
		if (manager.activeGroup != selectedGroup)
		{
			manager.SwitchToGroup(selectedGroup);
			selectedGroup = manager.activeGroup;
		}
	}
}