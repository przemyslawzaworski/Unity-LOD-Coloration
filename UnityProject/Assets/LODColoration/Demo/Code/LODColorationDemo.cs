using UnityEngine;

public class LODColorationDemo : MonoBehaviour
{
	public GameObject Source;

	void Start()
	{
		int r = 10;
		for (int i = 0; i < r * r * r; i++)
		{
			GameObject instance = Instantiate(Source);
			instance.transform.position = new Vector3(i % r, (i % (r * r)) / r, i / (r * r)) * 5.0f;
		}
	}

	void OnValidate()
	{
		#if UNITY_2019_1_OR_NEWER
			Renderer[] renderers = MonoBehaviour.FindObjectsOfType<Renderer>();
			for (int i = 0; i < renderers.Length; i++) 
			{
				if (UnityEngine.Rendering.GraphicsSettings.renderPipelineAsset != null)
					renderers[i].sharedMaterial = UnityEngine.Rendering.GraphicsSettings.renderPipelineAsset.defaultMaterial;
			}
		#endif
	}
}