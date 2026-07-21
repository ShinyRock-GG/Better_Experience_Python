using UnityEngine;

namespace Better_Cloth;

public class CascadeDestroyer : MonoBehaviour
{
	public GameObject linkedObject { get; set; }

	public void OnDestroy()
	{
		if (linkedObject != null)
		{
			Object.DestroyImmediate(linkedObject);
		}
	}
}
