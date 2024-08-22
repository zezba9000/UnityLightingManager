using UnityEngine;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine.Rendering;
using System.Runtime.InteropServices;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class LightingManager : MonoBehaviour
{
	public static LightingManager singleton;

	public string bakedFolder;
	public int activeGroup;

	public LightGroup[] lightGroups;

	public LightingManager()
	{
		singleton = this;
	}

	private void Start()
	{
		singleton = this;

		if (lightGroups != null)
		{
			foreach (var group in lightGroups)
			{
				group.lightingManager = this;
				group.CreateDataObjects();
			}
		}

		if (ValidateActiveGroup()) SwitchToGroup(activeGroup);
	}

	private bool ValidateActiveGroup()
	{
		if (lightGroups == null || lightGroups.Length == 0) return false;
		if (activeGroup < 0) activeGroup = 0;
		if (activeGroup > lightGroups.Length - 1) activeGroup = lightGroups.Length - 1;
		return true;
	}

	public void SwitchToGroup(int groupIndex)
	{
		activeGroup = groupIndex;
		if (!ValidateActiveGroup()) return;
		
		var nextGroup = lightGroups[activeGroup];
		foreach (var group in lightGroups)
		{
			if (group != nextGroup) group.Disable();
		}
		nextGroup.Enable();
	}

	#if UNITY_EDITOR
	public void EditorConfigure()
	{
		if (lightGroups != null)
		{
			foreach (var group in lightGroups)
			{
				group.lightingManager = this;
			}
		}
	}

	[InitializeOnLoadMethod]
	private static void PostBuild()
	{
		if (!singleton) return;
		Lightmapping.bakeCompleted -= singleton.Lightmapping_completed;
		Lightmapping.bakeCompleted += singleton.Lightmapping_completed;
	}

	private void OnDestroy()
	{
		Lightmapping.bakeCompleted -= Lightmapping_completed;
	}

	private void Lightmapping_completed()
	{
		if (!ValidateActiveGroup()) return;
		CopyLightmapFilesToGroup();
		AutoSetLightmapFiles();
	}

	public void CopyLightmapFilesToGroup()
	{
		if (!ValidateActiveGroup() || string.IsNullOrEmpty(bakedFolder)) return;
		lightGroups[activeGroup].CopyLightmapFiles(Path.Combine(Application.dataPath, bakedFolder));
		AssetDatabase.Refresh();
	}

	public void AutoSetLightmapFiles()
	{
		if (!ValidateActiveGroup()) return;
		lightGroups[activeGroup].AutoSetLightmapFiles();
		SwitchToGroup(activeGroup);
	}
	#endif
}