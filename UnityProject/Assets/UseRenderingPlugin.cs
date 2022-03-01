using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;
using System.Runtime.InteropServices;


public class UseRenderingPlugin : MonoBehaviour
{
	// Native plugin rendering events are only called if a plugin is used
	// by some script. This means we have to DllImport at least
	// one function in some active script.
	// For this example, we'll call into plugin's SetTimeFromUnity
	// function and pass the current time so the plugin can animate.
	[DllImport ("RenderingPlugin")]
	private static extern void SetTimeFromUnity(float t);

	// We'll also pass native pointer to a texture in Unity.
	// The plugin will fill texture data from native code.
	[DllImport ("RenderingPlugin")]
	private static extern void SetTextureFromUnity(System.IntPtr texture, int w, int h);

	// We'll pass native pointer to the mesh vertex buffer.
	// Also passing source unmodified mesh data.
	// The plugin will fill vertex data from native code.
	[DllImport ("RenderingPlugin")]
	private static extern void SetMeshBuffersFromUnity (IntPtr vertexBuffer, int vertexCount, IntPtr sourceVertices, IntPtr sourceNormals, IntPtr sourceUVs);

	[DllImport("RenderingPlugin")]
	private static extern IntPtr GetRenderEventFunc();

	IEnumerator Start()
	{
		CreateTextureAndPassToPlugin();
		SendMeshBuffersToPlugin();
		yield return StartCoroutine("CallPluginAtEndOfFrames");
	}

	public RawImage OptixViewer;

	private void CreateTextureAndPassToPlugin()
	{
		Screen.SetResolution(1920, 1080, false);

		// Create a texture
		Texture2D tex = new Texture2D((int)OptixViewer.rectTransform.rect.width, (int)OptixViewer.rectTransform.rect.height, TextureFormat.RGBA32,false);
		// Set point filtering just so we can see the pixels clearly
		tex.filterMode = FilterMode.Point;
		// Call Apply() so it's actually uploaded to the GPU
		tex.Apply();

		// Set texture onto our material
		OptixViewer.texture = tex;

		// Pass texture pointer to the plugin
		SetTextureFromUnity (tex.GetNativeTexturePtr(), tex.width, tex.height);
	}

	private void SendMeshBuffersToPlugin ()
	{
		var filter = GetComponent<MeshFilter> ();
		var mesh = filter.mesh;
		// The plugin will want to modify the vertex buffer -- on many platforms
		// for that to work we have to mark mesh as "dynamic" (which makes the buffers CPU writable --
		// by default they are immutable and only GPU-readable).
		mesh.MarkDynamic ();

		// Make sure to have vertex colors so that the plugin can rely on a known
		// vertex layout (position+normal+color+UV). Since Unity 2019.3 it's easier
		// since there are APIs to query all that info.
		var colors = mesh.colors;
		mesh.colors = colors;

		// However, mesh being dynamic also means that the CPU on most platforms can not
		// read from the vertex buffer. Our plugin also wants original mesh data,
		// so let's pass it as pointers to regular C# arrays.
		// This bit shows how to pass array pointers to native plugins without doing an expensive
		// copy: you have to get a GCHandle, and get raw address of that.
		var vertices = mesh.vertices;
		var normals = mesh.normals;
		var uvs = mesh.uv;
		GCHandle gcVertices = GCHandle.Alloc (vertices, GCHandleType.Pinned);
		GCHandle gcNormals = GCHandle.Alloc (normals, GCHandleType.Pinned);
		GCHandle gcUV = GCHandle.Alloc (uvs, GCHandleType.Pinned);

		SetMeshBuffersFromUnity (mesh.GetNativeVertexBufferPtr (0), mesh.vertexCount, gcVertices.AddrOfPinnedObject (), gcNormals.AddrOfPinnedObject (), gcUV.AddrOfPinnedObject ());

		gcVertices.Free ();
		gcNormals.Free ();
		gcUV.Free ();
	}


	private IEnumerator CallPluginAtEndOfFrames()
	{
		while (true) {
			// Wait until all frame rendering is done
			yield return new WaitForEndOfFrame();

			// Set time for the plugin
			SetTimeFromUnity (Time.timeSinceLevelLoad);

			// Issue a plugin event with arbitrary integer identifier.
			// The plugin can distinguish between different
			// things it needs to do based on this ID.
			// For our simple plugin, it does not matter which ID we pass here.
			GL.IssuePluginEvent(GetRenderEventFunc(), 1);
		}
	}
}
