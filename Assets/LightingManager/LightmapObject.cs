using System;
using UnityEngine;

[ExecuteAlways]
public class LightmapObject : MonoBehaviour
{
    public string id;

    public int hash
    {
        get
        {
            if (Guid.TryParse(id, out Guid guid))
            {
                byte[] bytes = guid.ToByteArray();
                int hash = 17;
                foreach (byte b in bytes) hash = hash * 31 + b;
                return hash;
            }
            else
            {
                Debug.LogError("Invalid Guid ID for: " + name);
                return 0;
            }
        }
    }

    private void Awake()
    {
        if (string.IsNullOrEmpty(id)) id = Guid.NewGuid().ToString();
    }
}
