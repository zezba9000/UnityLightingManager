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

#if UNITY_EDITOR
class StrCmpLogicalComparer : Comparer<string>
{
	[DllImport("Shlwapi.dll", CharSet = CharSet.Unicode)]
	private static extern int StrCmpLogicalW(string x, string y);

	public override int Compare(string x, string y)
	{
		return StrCmpLogicalW(x, y);
	}
}
#endif

[Serializable]
public class LightSetting
{
	public Light light;

	public bool setColor;
	public Color color;

	public bool setIntensity;
	public float intensity;

	public bool setRange;
	public float range;

	public bool setShadowType;
	public LightShadows shadowType;

	public void Apply()
	{
		if (setColor) light.color = color;
		if (setIntensity) light.intensity = intensity;
		if (setRange) light.range = range;
		if (setShadowType) light.shadows = shadowType;

		if (!light.enabled) light.enabled = true;
		if (!light.gameObject.activeSelf) light.gameObject.SetActive(true);
	}
}

[Serializable]
public class LightGroup
{
	public string sourceFolder;
	public LightSetting[] lightSettings;
	public GameObject[] sceneObjects;
	public List<Texture2D> directionTextures, lightTextures, shadowMaskTextures;

	internal LightmapData[] data;
	internal bool isInit;

	#if UNITY_EDITOR
	private Texture2D LoadAssetTexture(string file)
	{
		string assetPath = Path.Combine("Assets", sourceFolder);
		assetPath = Path.Combine(assetPath, Path.GetFileName(file));
		var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
		if (texture == null)
		{
			Debug.LogError("Unable to load texture: " + assetPath);
			return null;
		}

		return texture;
	}

	private bool GetSourcePaths(out string sourceFolderFormated, out string sourceFolderFull)
	{
		// format source path
		sourceFolderFormated = sourceFolder.Replace('/', '\\');
		sourceFolderFull = Path.Combine(Application.dataPath, sourceFolderFormated);
		#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
		sourceFolderFull = sourceFolderFull.Replace('/', '\\');
		#else
		sourceFolderFull = sourceFolderFull.Replace('\\', '/');
		#endif

		// validate source path
		if (!Directory.Exists(sourceFolderFull))
		{
			Debug.LogError("LightMapGroup source folder does not exist: " + sourceFolderFull);
			return false;
		}

		return true;
	}

	public void CopyLightmapFiles(string bakedFilePath)
	{
		string sourceFolderFormated, sourceFolderFull;
		if (!GetSourcePaths(out sourceFolderFormated, out sourceFolderFull)) return;

		// copy new files
		foreach (string srcFile in Directory.GetFiles(bakedFilePath))
		{
			if (Path.GetExtension(srcFile) == ".meta") continue;	
			string dstFile = Path.Combine(sourceFolderFull, Path.GetFileName(srcFile));
			if (File.Exists(dstFile))
			{
				var info = new FileInfo(dstFile);
				info.Attributes = FileAttributes.Normal;
			}

			File.Copy(srcFile, dstFile, true);
		}

		// save light probes
		const string lightProbDataFile = "LightProbes.data";
		if (LightmapSettings.lightProbes != null && LightmapSettings.lightProbes.bakedProbes != null)
		{
			using (var stream = new FileStream(Path.Combine(sourceFolderFull, lightProbDataFile), FileMode.Create, FileAccess.Write))
			using (var writer = new BinaryWriter(stream))
			{
				writer.Write(LightmapSettings.lightProbes.bakedProbes.Length);
				foreach (var probe in LightmapSettings.lightProbes.bakedProbes)
				{
					for (int x = 0; x != 3; ++x)
					{
						for (int y = 0; y != 9; ++y)
						{
							writer.Write(probe[x,y]);
						}
					}
				}
			}
		}

		// delete unused files
		foreach (string dstFile in Directory.GetFiles(sourceFolderFull))
		{
			string dstFileName = Path.GetFileName(dstFile);
			if (dstFileName == lightProbDataFile) continue;
			if (!Directory.GetFiles(bakedFilePath).Any(x => Path.GetFileName(x) == dstFileName))
			{
				var info = new FileInfo(dstFile);
				info.Attributes = FileAttributes.Normal;
				File.Delete(dstFile);
			}
		}
	}

	public void AutoSetLightmapFiles()
	{
		if (string.IsNullOrEmpty(sourceFolder))
		{
			isInit = true;
			return;
		}

		string sourceFolderFormated, sourceFolderFull;
		if (!GetSourcePaths(out sourceFolderFormated, out sourceFolderFull)) return;

		// gather textures
		directionTextures = new List<Texture2D>();
		lightTextures = new List<Texture2D>();
		shadowMaskTextures = new List<Texture2D>();
		var files = Directory.GetFiles(sourceFolderFull);
		Array.Sort(files, new StrCmpLogicalComparer());
		foreach (string file in files)
		{
			if (file.EndsWith("_dir.png"))
			{
				var texture = LoadAssetTexture(file);
				if (texture != null) directionTextures.Add(texture);
				else return;
			}
			else if (file.EndsWith("_light.exr"))
			{
				var texture = LoadAssetTexture(file);
				if (texture != null) lightTextures.Add(texture);
				else return;
			}
			else if (file.EndsWith("_shadowmask.png"))
			{
				var texture = LoadAssetTexture(file);
				if (texture != null) shadowMaskTextures.Add(texture);
				else return;
			}
		}

		// create data object
		CreateDataObjects();

		isInit = true;
	}
	#endif

	public void CreateDataObjects()
	{
		if (directionTextures.Count == 0) Debug.LogWarning("No lightmap textures set: " + sourceFolder);
		
		data = new LightmapData[directionTextures.Count];
		for (int i = 0; i != data.Length; ++i)
		{
			data[i] = new LightmapData();
			data[i].lightmapDir = directionTextures[i];
			if (i < lightTextures.Count) data[i].lightmapColor = lightTextures[i];
			if (i < shadowMaskTextures.Count) data[i].shadowMask = shadowMaskTextures[i];
		}
	}

	public void Disable()
	{
		try
		{
			// try-catch to handle bug in older unity versions
			LightmapSettings.lightProbes.bakedProbes = new SphericalHarmonicsL2[0];
		}
		catch { }

		if (lightSettings != null)
		{
			foreach (var lightSetting in lightSettings) lightSetting.light.gameObject.SetActive(false);
		}

		if (sceneObjects != null)
		{
			foreach (var obj in sceneObjects) obj.SetActive(false);
		}
	}

	public void Enable()
	{
		// set lightmap data object
		if (data == null) CreateDataObjects();
		LightmapSettings.lightmaps = data;

		// load and set light probes
		SphericalHarmonicsL2[] bakedProbes;
		string sourceFolderFormated, sourceFolderFull;
		if (LightmapSettings.lightProbes != null && GetSourcePaths(out sourceFolderFormated, out sourceFolderFull))
		{
			string probePath = Path.Combine(sourceFolderFull, "LightProbes.data");
			if (File.Exists(probePath))
			{
				using (var stream = new FileStream(probePath, FileMode.Open, FileAccess.Read))
				using (var reader = new BinaryReader(stream))
				{
					int probeCount = reader.ReadInt32();
					bakedProbes = new SphericalHarmonicsL2[probeCount];
					for (int i = 0; i != probeCount; ++i)
					{
						for (int x = 0; x != 3; ++x)
						{
							for (int y = 0; y != 9; ++y)
							{
								bakedProbes[i][x,y] = reader.ReadSingle();
							}
						}
					}
				}

				try
				{
					// try-catch to handle bug in older unity versions
					LightmapSettings.lightProbes.bakedProbes = bakedProbes;
				}
				catch { }
			}
			else
			{
				try
				{
					// try-catch to handle bug in older unity versions
					LightmapSettings.lightProbes.bakedProbes = new SphericalHarmonicsL2[0];
				}
				catch { }
			}
		}

		// enable objects and components
		if (lightSettings != null)
		{
			foreach (var lightSetting in lightSettings) lightSetting.Apply();
		}

		if (sceneObjects != null)
		{
			foreach (var obj in sceneObjects) obj.SetActive(true);
		}
	}
}

public class LightingManager : MonoBehaviour
{
	public static LightingManager singleton;

	public string bakedFileLocation;
	public int activeGroup;

	public LightGroup[] lightGroups;

	public LightingManager()
	{
		singleton = this;
	}

	private void Start()
	{
		if (singleton.lightGroups != null)
		{
			foreach (var group in singleton.lightGroups) group.CreateDataObjects();
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

	#if UNITY_EDITOR
	[InitializeOnLoadMethod]
	private static void PostBuild()
	{
		if (singleton == null || singleton.lightGroups == null) return;
		Lightmapping.completed += singleton.Lightmapping_completed;
	}

	private void OnDestroy()
	{
		Lightmapping.completed -= Lightmapping_completed;
	}

	private void Lightmapping_completed()
	{
		var group = lightGroups[activeGroup];
		if (EditorUtility.DisplayDialog("LightingManager", string.Format("Baking has finished.\nWould you like to copy lightmap files into selected group:\n'{0}'", group.sourceFolder), "Yes", "No"))
		{
			CopyLightmapFilesToGroup(singleton.activeGroup);
		}
	}

	public void CopyLightmapFilesToGroup(int groupIndex)
	{
		if (lightGroups == null || groupIndex > lightGroups.Length - 1 || groupIndex < 0 || string.IsNullOrEmpty(bakedFileLocation)) return;
		lightGroups[groupIndex].CopyLightmapFiles(Path.Combine(Application.dataPath, bakedFileLocation));
		AssetDatabase.Refresh();

		AutoSetLightmapFiles(false);
	}

	public void AutoSetLightmapFiles(bool editorDlg)
	{
		if (!ValidateActiveGroup()) return;

		foreach (var group in lightGroups) group.AutoSetLightmapFiles();
		SwitchToGroup(activeGroup, editorDlg);
	}
	#endif

	public void SwitchToGroup(int groupIndex, bool editorDlg = false)
	{
		#if UNITY_EDITOR
		if (editorDlg && !EditorApplication.isPlayingOrWillChangePlaymode && !EditorUtility.DisplayDialog("LightingManager Warning", "Are you sure you want to switch to selected group?\nNOTE: If you just baked light probe data it will be overwritten!", "Ok", "Cancel"))
		{
			return;
		}
		#endif

		activeGroup = groupIndex;
		if (!ValidateActiveGroup()) return;
		
		var nextGroup = lightGroups[activeGroup];
		foreach (var group in lightGroups)
		{
			if (group != nextGroup) group.Disable();
		}
		nextGroup.Enable();
	}
}