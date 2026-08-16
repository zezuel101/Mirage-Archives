using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace Mirage.ShadowSystem
{
	/// <summary>
	/// Gives the deferred PQS terrain the sun's cascaded shadows — including craft/parts casting onto the
	/// ground — which it otherwise can't receive.
	///
	/// <para><b>Why this exists:</b> the terrain lights itself with a custom Hapke BRDF written to the
	/// G-buffer EMISSION target, so it bypasses Unity's deferred lighting and never gets the directional
	/// shadow. Unity's screen-space shadow (<c>_ShadowMapTexture</c>) <i>does</i> hold the correct per-pixel
	/// sun shadow for our terrain pixels, but only exists during the lighting stage (after the G-buffer
	/// fill where we light). So we composite it back on afterwards.</para>
	///
	/// <para><b>How</b> (the stencil-masked post-effect pattern blackrack's Deferred mod recommends):
	/// <list type="bullet">
	/// <item>a command buffer on the <b>sun light</b> at <see cref="F:UnityEngine.Rendering.LightEvent.AfterScreenspaceMask" />
	///   copies the just-built screen-space shadow into <c>_MirageSunShadowTex</c>;</item>
	/// <item>a command buffer on the <b>terrain camera</b> at <see cref="F:UnityEngine.Rendering.CameraEvent.AfterFinalPass" />
	///   blits a fullscreen pass that, for terrain pixels only (stencil 2, ReadMask 35), multiplies the
	///   lit colour by that shadow (<c>Mirage/TerrainShadowComposite</c>).</item>
	/// </list>
	/// Forward rendering already receives the cascade via <c>GET_SHADOW</c> in the terrain shader, so this
	/// only matters under deferred — it no-ops when no deferred terrain camera is present.</para>
	/// </summary>
	// Token: 0x0200005B RID: 91
	[KSPAddon(-3, false)]
	public class MirageTerrainShadowCompositor : MonoBehaviour
	{
		// Token: 0x0600028D RID: 653 RVA: 0x00016254 File Offset: 0x00014454
		private void Start()
		{
			Shader shader = Shader.Find("Mirage/TerrainShadowComposite");
			bool flag = shader == null;
			if (flag)
			{
				MirageDebug.LogWarning("MirageTerrainShadowCompositor: 'Mirage/TerrainShadowComposite' not found — deferred terrain won't receive sun/craft shadows. (AssetBundle out of date?)");
				base.enabled = false;
			}
			else
			{
				this.compositeMaterial = new Material(shader);
				this.captureBuffer = new CommandBuffer
				{
					name = "Mirage Capture Sun Shadow"
				};
				this.applyBuffer = new CommandBuffer
				{
					name = "Mirage Apply Terrain Shadow"
				};
				this.TrySetUp();
			}
		}

		// Token: 0x0600028E RID: 654 RVA: 0x000162D0 File Offset: 0x000144D0
		private void Update()
		{
			bool flag;
			if (this.active)
			{
				if (Screen.width == this.rtWidth && Screen.height == this.rtHeight && !(this.sunLight == null))
				{
					flag = this.terrainCameras.Any((Camera c) => c == null);
				}
				else
				{
					flag = true;
				}
			}
			else
			{
				flag = false;
			}
			bool flag2 = flag;
			if (flag2)
			{
				this.TearDown();
			}
			bool flag3 = !this.active;
			if (flag3)
			{
				this.TrySetUp();
			}
		}

		// Token: 0x0600028F RID: 655 RVA: 0x00016360 File Offset: 0x00014560
		private void TrySetUp()
		{
			this.terrainCameras = MirageTerrainShadowCompositor.FindDeferredTerrainCameras();
			bool flag = this.terrainCameras.Length == 0;
			if (!flag)
			{
				this.sunLight = MirageTerrainShadowCompositor.FindSunLight();
				bool flag2 = this.sunLight == null;
				if (!flag2)
				{
					this.rtWidth = Mathf.Max(Screen.width, 1);
					this.rtHeight = Mathf.Max(Screen.height, 1);
					this.sunShadowRT = new RenderTexture(this.rtWidth, this.rtHeight, 0, 15)
					{
						name = "MirageSunShadow",
						filterMode = 1,
						wrapMode = 1
					};
					this.sunShadowRT.Create();
					this.captureBuffer.Clear();
					this.captureBuffer.Blit(1, this.sunShadowRT);
					this.captureBuffer.SetGlobalTexture(MirageTerrainShadowCompositor.s_SunShadowTexID, this.sunShadowRT);
					this.sunLight.AddCommandBuffer(3, this.captureBuffer);
					this.applyBuffer.Clear();
					this.applyBuffer.Blit(null, 2, this.compositeMaterial);
					foreach (Camera cam in this.terrainCameras)
					{
						cam.AddCommandBuffer(9, this.applyBuffer);
					}
					this.active = true;
					MirageDebug.Log("MirageTerrainShadowCompositor: active (sun='" + this.sunLight.name + "', " + string.Format("cameras=[{0}], {1}x{2}).", string.Join(", ", from c in this.terrainCameras
					select c.name), this.rtWidth, this.rtHeight));
				}
			}
		}

		// Token: 0x06000290 RID: 656 RVA: 0x00016534 File Offset: 0x00014734
		private void TearDown()
		{
			bool flag = this.sunLight != null;
			if (flag)
			{
				this.sunLight.RemoveCommandBuffer(3, this.captureBuffer);
			}
			foreach (Camera cam in this.terrainCameras)
			{
				bool flag2 = cam != null;
				if (flag2)
				{
					cam.RemoveCommandBuffer(9, this.applyBuffer);
				}
			}
			bool flag3 = this.sunShadowRT != null;
			if (flag3)
			{
				this.sunShadowRT.Release();
				Object.Destroy(this.sunShadowRT);
				this.sunShadowRT = null;
			}
			this.terrainCameras = Array.Empty<Camera>();
			this.sunLight = null;
			this.active = false;
		}

		// Token: 0x06000291 RID: 657 RVA: 0x000165E8 File Offset: 0x000147E8
		private void OnDestroy()
		{
			this.TearDown();
			CommandBuffer commandBuffer = this.captureBuffer;
			if (commandBuffer != null)
			{
				commandBuffer.Dispose();
			}
			CommandBuffer commandBuffer2 = this.applyBuffer;
			if (commandBuffer2 != null)
			{
				commandBuffer2.Dispose();
			}
			bool flag = this.compositeMaterial != null;
			if (flag)
			{
				Object.Destroy(this.compositeMaterial);
			}
		}

		// Token: 0x06000292 RID: 658 RVA: 0x00016640 File Offset: 0x00014840
		private static Camera[] FindDeferredTerrainCameras()
		{
			return (from c in Camera.allCameras
			where c != null && c.actualRenderingPath == 3 && (c.name == "Camera 00" || c.name == "Camera 01")
			select c).ToArray<Camera>();
		}

		// Token: 0x06000293 RID: 659 RVA: 0x00016680 File Offset: 0x00014880
		private static Light FindSunLight()
		{
			Light[] lights = Object.FindObjectsOfType<Light>();
			Light result;
			if ((result = lights.FirstOrDefault((Light l) => l.name == "SunLight" && l.type == 1)) == null)
			{
				result = (from l in lights
				where l.type == 1
				orderby l.intensity descending
				select l).FirstOrDefault<Light>();
			}
			return result;
		}

		// Token: 0x04000270 RID: 624
		private const string ShaderName = "Mirage/TerrainShadowComposite";

		// Token: 0x04000271 RID: 625
		private static readonly int s_SunShadowTexID = Shader.PropertyToID("_MirageSunShadowTex");

		// Token: 0x04000272 RID: 626
		private const CameraEvent ApplyEvent = 9;

		// Token: 0x04000273 RID: 627
		private Material compositeMaterial;

		// Token: 0x04000274 RID: 628
		private Light sunLight;

		// Token: 0x04000275 RID: 629
		private Camera[] terrainCameras = Array.Empty<Camera>();

		// Token: 0x04000276 RID: 630
		private RenderTexture sunShadowRT;

		// Token: 0x04000277 RID: 631
		private CommandBuffer captureBuffer;

		// Token: 0x04000278 RID: 632
		private CommandBuffer applyBuffer;

		// Token: 0x04000279 RID: 633
		private int rtWidth;

		// Token: 0x0400027A RID: 634
		private int rtHeight;

		// Token: 0x0400027B RID: 635
		private bool active;
	}
}
