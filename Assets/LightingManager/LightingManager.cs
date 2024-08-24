using UnityEngine;
using System.IO;
using System.Collections;


#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteInEditMode]
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

	private void Awake()
	{
		singleton = this;
	}

	#if UNITY_EDITOR
	private void Update()
	{
		singleton = this;
	}
	#endif

	private void Start()
	{
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

	public static void ResetLighting()
	{
		LightmapSettings.lightProbes = null;
		LightmapSettings.lightmaps = null;
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
		EditorApplication.update -= EditorUpdate;
	}

	private static IEnumerator Lightmapping_completed_coroutine;
	private void Lightmapping_completed()
	{
		Lightmapping_completed_coroutine = Lightmapping_completed_Delay();
		EditorApplication.update += EditorUpdate;
	}

	private static void EditorUpdate()
	{
		if (Lightmapping_completed_coroutine != null && !Lightmapping_completed_coroutine.MoveNext())
        {
			Lightmapping_completed_coroutine = null;
            EditorApplication.update -= EditorUpdate;
        }
	}

	private static IEnumerator Lightmapping_completed_Delay()
	{
		Debug.Log("Post process of light bake...");
		yield return new WaitForSecondsRealtime(3);
		Debug.Log("Post process of light bake... Working...");
		AssetDatabase.SaveAssets();
		singleton.EditorConfigure();

		if (!singleton.ValidateActiveGroup()) yield break;
		if (string.IsNullOrWhiteSpace(singleton.bakedFolder))
		{
			Debug.LogError("BakedFolder is empty");
			yield break;
		}

		AssetDatabase.Refresh();
		singleton.CopyLightmapFilesToGroup();
		singleton.AutoSetLightmapFiles();
		Debug.Log("Post process of light bake... Done");
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