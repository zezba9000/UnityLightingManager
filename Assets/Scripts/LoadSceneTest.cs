using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LoadSceneTest : MonoBehaviour
{
	public GameObject[] disableObjects;

	private void OnGUI()
	{
		if (GUI.Button(new Rect(10, 10, 128, 32), "Load scene additive"))
		{
			if (disableObjects != null)
			{
				foreach (var o in disableObjects) o.SetActive(false);
			}
			LightingManager.ResetLighting();// call this
			SceneManager.LoadSceneAsync(0, LoadSceneMode.Additive);
		}

		if (GUI.Button(new Rect(10, 10 + 34, 128, 32), "Reset"))
		{
			LightingManager.ResetLighting();
			SceneManager.LoadSceneAsync(1);
		}
	}
}
