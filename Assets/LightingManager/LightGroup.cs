using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.ComTypes;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

[Serializable]
public class LightGroupTexture
{
	public Texture2D color, direction, shadowMask;
}

[Serializable]
public class LightGroup
{
	internal LightingManager lightingManager;

	public string name;
	public GameObject[] sceneObjects;
	public List<LightGroupTexture> lightmapTextures;
	internal LightmapData[] data;

	public string GetBakingSourcePath()
	{
		string result = lightingManager.bakedFolder;
		result = Path.Combine(Application.dataPath, result);

		#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
		result = result.Replace('/', '\\');
		#else
		result = result.Replace('\\', '/');
		#endif

		return result;
	}

	public string GetSourcePath()
	{
		int index = Array.IndexOf(lightingManager.lightGroups, this) + 1;
		return GetBakingSourcePath() + " " + index.ToString();
	}

	public void CreateDataObjects()
	{
		if (lightmapTextures == null || lightmapTextures.Count == 0)
		{
			Debug.LogWarning("No lightmap textures to set");
			data = null;
			return;
		}

		data = new LightmapData[lightmapTextures.Count];
		for (int i = 0; i != data.Length; ++i)
		{
			var map = lightmapTextures[i];
			data[i] = new LightmapData();
			data[i].lightmapColor = map.color;
			data[i].lightmapDir = map.direction;
			data[i].shadowMask = map.shadowMask;
		}
	}

	public void Enable()
	{
		// enable objects and components
		if (sceneObjects != null)
		{
			foreach (var obj in sceneObjects) obj.SetActive(true);
		}

		// set editor data or editor can get confused
		#if UNITY_EDITOR
		if (!Application.isPlaying)
		{
			string lightingDataPath = GetSourcePath();
			Lightmapping.lightingDataAsset = LoadAsset<LightingDataAsset>(Path.Combine(lightingDataPath, "LightingData.asset"));
			return;// don't need to run anything else in editor
		}
		#endif

		// load and set light probes
		if (LightmapSettings.lightProbes != null)
		{
			string sourcePath = GetSourcePath();
			string probePath = Path.Combine(sourcePath, "LightProbes.data");
			if (File.Exists(probePath))
			{
				SphericalHarmonicsL2[] bakedProbes;
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

				LightmapSettings.lightProbes.bakedProbes = bakedProbes;
			}
			else
			{
				LightmapSettings.lightProbes.bakedProbes = new SphericalHarmonicsL2[0];
			}
		}

		// set lightmap data object
		if (data == null) CreateDataObjects();
		LightmapSettings.lightmaps = data;

		// make sure GI is up to date
		DynamicGI.UpdateEnvironment();
	}

	public void Disable()
	{
		if (sceneObjects != null)
		{
			foreach (var obj in sceneObjects) obj.SetActive(false);
		}
	}

	#if UNITY_EDITOR
	public void CopyLightmapFiles(string srcPath)
	{
		string dstPath = GetSourcePath();
		if (!Directory.Exists(dstPath)) Directory.CreateDirectory(dstPath);
		CopyLightmapFiles(srcPath, dstPath, true);
	}

	public void CopyLightmapFiles(string srcPath, string dstPath, bool saveLightProbeData)
	{
		const string lightProbDataFile = "LightProbes.data";

		// delete unused files
		var bakedFolderFiles = Directory.GetFiles(srcPath);
		foreach (string dstFile in Directory.GetFiles(dstPath))
		{
			string dstFileName = Path.GetFileName(dstFile);
			if (dstFileName == lightProbDataFile) continue;
			if (!bakedFolderFiles.Any(x => Path.GetFileName(x) == dstFileName))
			{
				var info = new FileInfo(dstFile);
				info.Attributes = FileAttributes.Normal;
				File.Delete(dstFile);
			}
		}

		// copy new files
		foreach (string srcFile in Directory.GetFiles(srcPath))
		{
			// make sure file isn't locked
			string dstFile = Path.Combine(dstPath, Path.GetFileName(srcFile));
			if (File.Exists(dstFile))
			{
				var info = new FileInfo(dstFile);
				info.Attributes = FileAttributes.Normal;
			}

			// ignore meta files
			if (Path.GetExtension(srcFile) == ".meta") continue;

			// delete existing files
			if (File.Exists(dstFile)) File.Delete(dstFile);
			if (File.Exists(dstFile + ".meta")) File.Delete(dstFile + ".meta");

			// move asset files
			string srcFileAsset = "Assets" + srcFile.Substring(Application.dataPath.Length);
			string dstFileAsset = "Assets" + dstFile.Substring(Application.dataPath.Length);
			string error = AssetDatabase.MoveAsset(srcFileAsset, dstFileAsset);
			if (!string.IsNullOrEmpty(error)) Debug.LogError(error);
		}

		// save light probes
		if (saveLightProbeData)
		{
			if (LightmapSettings.lightProbes != null && LightmapSettings.lightProbes.bakedProbes != null)
			{
				using (var stream = new FileStream(Path.Combine(dstPath, lightProbDataFile), FileMode.Create, FileAccess.Write))
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
		}
	}

	private T LoadAsset<T>(string file) where T : UnityEngine.Object
	{
		file = file.Substring(Application.dataPath.Length);
		file = "Assets" + file;
		var asset = AssetDatabase.LoadAssetAtPath<T>(file);
		if (asset == null)
		{
			Debug.LogError("Unable to load asset: " + file);
			return null;
		}
		return asset;
	}

	public void AutoSetLightmapFiles()
	{
		string sourcePath = GetSourcePath();
		if (!Directory.Exists(sourcePath)) return;

		// gather textures
		lightmapTextures = new List<LightGroupTexture>();
		foreach (var lightmap in LightmapSettings.lightmaps)
		{
			var texture = new LightGroupTexture()
			{
				color = lightmap.lightmapColor,
				direction = lightmap.lightmapDir,
				shadowMask = lightmap.shadowMask
			};
			lightmapTextures.Add(texture);
		}
	}
	#endif
}