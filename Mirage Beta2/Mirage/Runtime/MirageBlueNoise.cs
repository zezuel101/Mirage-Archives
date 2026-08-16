using System;
using KSPTextureLoader;
using UnityEngine;

namespace Mirage.Runtime
{
	/// <summary>
	/// Loads the shared 64×64 blue-noise dither tile once and binds it — plus an animated frame counter —
	/// as shader globals (<c>_BlueNoise</c>, <c>_HasBlueNoise</c>, <c>_FrameCount</c>). The raymarched
	/// shadow pass uses it to dither the ray-march start so the discrete steps don't print a visible
	/// contour at shadow edges; blue noise distributes that error far better than a hash and animates
	/// cleanly for temporal averaging.
	///
	/// Best-effort: when the texture file is missing the shaders fall back to texture-free
	/// interleaved-gradient noise (<c>_HasBlueNoise</c> = 0), so this never hard-fails. The texture ships
	/// at <c>GameData/Mirage/Textures/PluginData/blueNoise.dds</c>.
	///
	/// Shared (not scaled-specific) because the PQS raymarched-shadow path will reuse the same globals.
	/// </summary>
	// Token: 0x0200005F RID: 95
	public static class MirageBlueNoise
	{
		/// <summary>
		/// Load + bind the blue-noise globals. Idempotent and safe to call every frame: it latches once it
		/// succeeds (or if the file is genuinely missing). A <i>transient</i> failure — most commonly the
		/// blocking <c>GetTexture()</c> not being permitted during a scene switch — is NOT latched, so a
		/// later scene retries; IGN dither covers the gap meanwhile.
		/// </summary>
		// Token: 0x060002BF RID: 703 RVA: 0x000172C8 File Offset: 0x000154C8
		public static void EnsureLoaded()
		{
			bool flag = MirageBlueNoise.s_Loaded || MirageBlueNoise.s_FileMissing;
			if (!flag)
			{
				bool flag2 = !TextureLoader.TextureExists("Mirage/Textures/PluginData/blueNoise.dds");
				if (flag2)
				{
					MirageDebug.LogWarning("MirageBlueNoise: 'Mirage/Textures/PluginData/blueNoise.dds' not found — raymarched shadows fall back to interleaved-gradient noise.");
					Shader.SetGlobalFloat(MirageBlueNoise.s_HasBlueNoiseID, 0f);
					MirageBlueNoise.s_FileMissing = true;
				}
				else
				{
					try
					{
						TextureLoadOptions textureLoadOptions = new TextureLoadOptions();
						textureLoadOptions.Linear = new bool?(true);
						textureLoadOptions.Unreadable = true;
						TextureLoadOptions options = textureLoadOptions;
						MirageBlueNoise.s_Handle = TextureLoader.LoadTexture<Texture2D>("Mirage/Textures/PluginData/blueNoise.dds", options);
						Texture2D tex = MirageBlueNoise.s_Handle.GetTexture();
						tex.filterMode = 0;
						tex.wrapMode = 0;
						Shader.SetGlobalTexture(MirageBlueNoise.s_BlueNoiseID, tex);
						Shader.SetGlobalFloat(MirageBlueNoise.s_HasBlueNoiseID, 1f);
						MirageBlueNoise.s_Loaded = true;
						MirageDebug.Log("MirageBlueNoise: loaded blue-noise dither texture.");
					}
					catch (Exception e)
					{
						bool flag3 = !MirageBlueNoise.s_WarnedTransient;
						if (flag3)
						{
							MirageDebug.LogWarning("MirageBlueNoise: deferred blue-noise load (" + e.Message + "); will retry. Using IGN dither until then.");
							MirageBlueNoise.s_WarnedTransient = true;
						}
						Shader.SetGlobalFloat(MirageBlueNoise.s_HasBlueNoiseID, 0f);
						MirageBlueNoise.s_Handle.Dispose();
					}
				}
			}
		}

		/// <summary>
		/// Push the per-frame counter that animates the dither. Masked to 16 bits so the float the shader
		/// multiplies by the golden ratio stays precise (a raw, ever-growing frame count would lose its low
		/// bits and freeze the animation after a while).
		/// </summary>
		// Token: 0x060002C0 RID: 704 RVA: 0x00017408 File Offset: 0x00015608
		public static void SetFrameGlobal()
		{
			Shader.SetGlobalInt(MirageBlueNoise.s_FrameCountID, Time.frameCount & 65535);
		}

		// Token: 0x04000296 RID: 662
		private const string TexturePath = "Mirage/Textures/PluginData/blueNoise.dds";

		// Token: 0x04000297 RID: 663
		private static readonly int s_BlueNoiseID = Shader.PropertyToID("_BlueNoise");

		// Token: 0x04000298 RID: 664
		private static readonly int s_HasBlueNoiseID = Shader.PropertyToID("_HasBlueNoise");

		// Token: 0x04000299 RID: 665
		private static readonly int s_FrameCountID = Shader.PropertyToID("_FrameCount");

		// Token: 0x0400029A RID: 666
		private static bool s_Loaded;

		// Token: 0x0400029B RID: 667
		private static bool s_FileMissing;

		// Token: 0x0400029C RID: 668
		private static bool s_WarnedTransient;

		// Token: 0x0400029D RID: 669
		private static TextureHandle<Texture2D> s_Handle;
	}
}
