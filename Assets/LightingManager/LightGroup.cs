using Newtonsoft.Json.Linq;
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
	private const string lightProbDataFile = "LightProbes.data";
	//private const string renderersDataFile = "Renderers.data";

	internal LightingManager lightingManager;

	public bool changeSkybox;
	public Material skyboxMaterial;
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
		// change skybox
		if (changeSkybox) RenderSettings.skybox = skyboxMaterial;

		// enable objects and components
		if (sceneObjects != null)
		{
			foreach (var obj in sceneObjects) obj.SetActive(true);
		}

		// set editor data or editor can get confused
		#if UNITY_EDITOR
		if (!Application.isPlaying)
		{
			string lightingDataPath = Path.Combine(GetSourcePath(), "LightingData.asset");
			if (!File.Exists(lightingDataPath))
			{
				Debug.LogWarning("Can't find: " + lightingDataPath);
				return;
			}
			Lightmapping.lightingDataAsset = LoadAsset<LightingDataAsset>(lightingDataPath);
			return;// don't need to run anything else in editor
		}
		#endif

		// set lightmap data object
		if (data == null) CreateDataObjects();
		LightmapSettings.lightmaps = data;

		// source path
		string srcPath = GetSourcePath();

		// load and set light probes
		if (LightmapSettings.lightProbes != null)
		{
			string probePath = Path.Combine(srcPath, lightProbDataFile);
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
				Debug.LogError("Expected file: " + lightProbDataFile);
				LightmapSettings.lightProbes.bakedProbes = new SphericalHarmonicsL2[0];
			}
		}

		/*// load renderer states
		var objs = UnityEngine.Object.FindObjectsOfType<GameObject>(false);
		if (objs != null && objs.Length != 0)
		{
			string rendererPath = Path.Combine(srcPath, renderersDataFile);
			if (File.Exists(rendererPath))
			{
				using (var stream = new FileStream(Path.Combine(srcPath, renderersDataFile), FileMode.Open, FileAccess.Read))
				using (var reader = new BinaryReader(stream))
				{
					// read length
					int expectedLength = reader.ReadInt32();

					// read objects
					int length = 0;
					foreach (var o in objs)
					{
						if (!o.isStatic) continue;

						var r = o.GetComponent<MeshRenderer>();
						if (!r || r.receiveGI != ReceiveGI.Lightmaps) continue;

						var f = o.GetComponent<MeshFilter>();
						if (!f) continue;

						// validate instance
						int instanceID = reader.ReadInt32();
						var trueObj = objs.FirstOrDefault(x => x.GetInstanceID() == instanceID);
						if (!trueObj)
						{
							Debug.LogError("Failed to find object instance: " + o.name);
							break;
						}

						r = trueObj.GetComponent<MeshRenderer>();
						if (!r)
						{
							Debug.LogError("Renderer no longer exists on instance: " + trueObj.name);
							break;
						}
						
						// read lightmap offsets
						int lightmapIndex = reader.ReadInt32();
						var lightmapScaleOffset = reader.ReadVector4();
						r.lightmapIndex = lightmapIndex;
						r.lightmapScaleOffset = lightmapScaleOffset;
						r.realtimeLightmapIndex = lightmapIndex;
						r.realtimeLightmapScaleOffset = lightmapScaleOffset;

						// read lightmap uvs
						int uvCount = reader.ReadInt32();
						var uvs = new Vector2[uvCount];
						for (int i = 0; i < uvCount; ++i)
						{
							uvs[i] = reader.ReadVector2();
						}

						var mesh = new Mesh();
						mesh.SetUVs(1, uvs);
						r.enlightenVertexStream = mesh;

						// finish
						r.UpdateGIMaterials();
						length++;
					}

					if (length != expectedLength) Debug.LogError("Renderer length did not match expected length");
				}
			}
			else
			{
				Debug.LogError("Expected file: " + renderersDataFile);
			}
		}*/

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
		CopyLightmapFiles(srcPath, dstPath);
	}

	public void CopyLightmapFiles(string srcPath, string dstPath)
	{
		// delete unused files
		Debug.Log("Deleting unused lightmap files");
		var bakedFolderFiles = Directory.GetFiles(srcPath);
		foreach (string dstFile in Directory.GetFiles(dstPath))
		{
			string dstFileName = Path.GetFileName(dstFile);
			if (dstFileName == lightProbDataFile) continue;
			//if (dstFileName == renderersDataFile) continue;
			if (!bakedFolderFiles.Any(x => Path.GetFileName(x) == dstFileName))
			{
				var info = new FileInfo(dstFile);
				info.Attributes = FileAttributes.Normal;
				File.Delete(dstFile);
			}
		}

		// copy new files
		Debug.Log("Coping new baked files");
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
		if (LightmapSettings.lightProbes != null && LightmapSettings.lightProbes.bakedProbes != null)
		{
			Debug.Log("Saving lightprobe data");
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
		else
		{
			string path = Path.Combine(dstPath, lightProbDataFile);
			var info = new FileInfo(path);
			info.Attributes = FileAttributes.Normal;
			File.Delete(path);
			File.Delete(path + ".meta");
		}

		/*// save renderer states
		var objs = UnityEngine.Object.FindObjectsOfType<GameObject>(false);
		if (objs != null && objs.Length != 0)
		{
			Debug.Log("Saving renderer data");
			using (var stream = new FileStream(Path.Combine(dstPath, renderersDataFile), FileMode.Create, FileAccess.Write))
			using (var writer = new BinaryWriter(stream))
			{
				// find length
				int length = 0;
				foreach (var o in objs)
				{
					if (!o.isStatic) continue;

					var r = o.GetComponent<MeshRenderer>();
					if (!r || r.receiveGI != ReceiveGI.Lightmaps) continue;

					var f = o.GetComponent<MeshFilter>();
					if (!f) continue;

					length++;
				}
				writer.Write(length);// write length

				// write objects
				foreach (var o in objs)
				{
					if (!o.isStatic) continue;

					var r = o.GetComponent<MeshRenderer>();
					if (!r || r.receiveGI != ReceiveGI.Lightmaps) continue;

					var f = o.GetComponent<MeshFilter>();
					if (!f) continue;

					writer.Write(o.GetInstanceID());
					writer.Write(r.lightmapIndex);
					writer.Write(r.lightmapScaleOffset);

					var uvs = new List<Vector2>();
					f.mesh.GetUVs(1, uvs);
					writer.Write(uvs.Count);
					for (int i = 0; i != uvs.Count; ++i)
					{
						writer.Write(uvs[i]);
					}
				}
			}
		}
		else
		{
			string path = Path.Combine(dstPath, renderersDataFile);
			var info = new FileInfo(path);
			info.Attributes = FileAttributes.Normal;
			File.Delete(path);
			File.Delete(path + ".meta");
		}*/
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

static class StreamExt
{
	public static void Write(this BinaryWriter writer, Vector2 vector)
	{
		writer.Write(vector.x);
		writer.Write(vector.y);
	}

	public static void Write(this BinaryWriter writer, Vector4 vector)
	{
		writer.Write(vector.x);
		writer.Write(vector.y);
		writer.Write(vector.z);
		writer.Write(vector.w);
	}

	public static Vector2 ReadVector2(this BinaryReader reader)
	{
		Vector2 result;
		result.x = reader.ReadSingle();
		result.y = reader.ReadSingle();
		return result;
	}

	public static Vector4 ReadVector4(this BinaryReader reader)
	{
		Vector4 result;
		result.x = reader.ReadSingle();
		result.y = reader.ReadSingle();
		result.z = reader.ReadSingle();
		result.w = reader.ReadSingle();
		return result;
	}
}