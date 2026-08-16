using System;
using KSPTextureLoader;
using UnityEngine;

namespace Mirage.Runtime
{
	/// <summary>Loads and binds the blue-noise dither tile used by raymarched shadows.</summary>
	// Token: 0x0200006B RID: 107
	public static class MirageBlueNoise
	{
		/// <summary>Call every frame; starts, polls, then no-ops once bound or failed.</summary>
		// Token: 0x0600032C RID: 812 RVA: 0x000189B0 File Offset: 0x00016BB0
		public static void EnsureLoaded()
		{
			MirageBlueNoise.LoadState loadState = MirageBlueNoise.s_State;
			MirageBlueNoise.LoadState loadState2 = loadState;
			if (loadState2 != MirageBlueNoise.LoadState.Idle)
			{
				if (loadState2 == MirageBlueNoise.LoadState.Loading)
				{
					MirageBlueNoise.PollLoad();
				}
			}
			else
			{
				MirageBlueNoise.BeginLoad();
			}
		}

		// Token: 0x0600032D RID: 813 RVA: 0x000189E1 File Offset: 0x00016BE1
		public static void SetFrameGlobal()
		{
			Shader.SetGlobalInt(MirageBlueNoise.s_FrameCountID, Time.frameCount & 65535);
		}

		// Token: 0x0600032E RID: 814 RVA: 0x000189FC File Offset: 0x00016BFC
		private static void BeginLoad()
		{
			Shader.SetGlobalFloat(MirageBlueNoise.s_HasBlueNoiseID, 0f);
			bool flag = !TextureLoader.TextureExists("Mirage/Textures/PluginData/blueNoise.dds");
			if (flag)
			{
				MirageBlueNoise.WarnFallingBack("was not found");
				MirageBlueNoise.s_State = MirageBlueNoise.LoadState.Unavailable;
			}
			else
			{
				TextureLoadOptions textureLoadOptions;
				textureLoadOptions..ctor();
				textureLoadOptions.Linear = new bool?(true);
				textureLoadOptions.Unreadable = true;
				TextureLoadOptions options = textureLoadOptions;
				MirageBlueNoise.s_Handle = TextureLoader.LoadTexture<Texture2D>("Mirage/Textures/PluginData/blueNoise.dds", options);
				MirageBlueNoise.s_State = MirageBlueNoise.LoadState.Loading;
			}
		}

		// Token: 0x0600032F RID: 815 RVA: 0x00018A78 File Offset: 0x00016C78
		private static void PollLoad()
		{
			bool isError = MirageBlueNoise.s_Handle.IsError;
			if (isError)
			{
				MirageBlueNoise.WarnFallingBack("could not be loaded");
				MirageBlueNoise.s_Handle.Dispose();
				MirageBlueNoise.s_State = MirageBlueNoise.LoadState.Unavailable;
			}
			else
			{
				bool flag = !MirageBlueNoise.s_Handle.IsComplete;
				if (!flag)
				{
					Texture2D texture = MirageBlueNoise.s_Handle.GetTexture();
					bool flag2 = texture == null;
					if (flag2)
					{
						MirageBlueNoise.WarnFallingBack("completed without a texture");
						MirageBlueNoise.s_Handle.Dispose();
						MirageBlueNoise.s_State = MirageBlueNoise.LoadState.Unavailable;
					}
					else
					{
						texture.filterMode = 0;
						texture.wrapMode = 0;
						Shader.SetGlobalTexture(MirageBlueNoise.s_BlueNoiseID, texture);
						Shader.SetGlobalFloat(MirageBlueNoise.s_HasBlueNoiseID, 1f);
						MirageBlueNoise.s_State = MirageBlueNoise.LoadState.Bound;
						MirageDebug.Log("MirageBlueNoise: loaded blue-noise dither texture.");
					}
				}
			}
		}

		// Token: 0x06000330 RID: 816 RVA: 0x00018B3B File Offset: 0x00016D3B
		private static void WarnFallingBack(string reason)
		{
			MirageDebug.LogWarning("MirageBlueNoise: 'Mirage/Textures/PluginData/blueNoise.dds' " + reason + " — raymarched shadows fall back to interleaved-gradient noise.");
		}

		// Token: 0x0400030B RID: 779
		private const string TexturePath = "Mirage/Textures/PluginData/blueNoise.dds";

		// Token: 0x0400030C RID: 780
		private static readonly int s_BlueNoiseID = Shader.PropertyToID("_BlueNoise");

		// Token: 0x0400030D RID: 781
		private static readonly int s_HasBlueNoiseID = Shader.PropertyToID("_HasBlueNoise");

		// Token: 0x0400030E RID: 782
		private static readonly int s_FrameCountID = Shader.PropertyToID("_FrameCount");

		// Token: 0x0400030F RID: 783
		private static MirageBlueNoise.LoadState s_State;

		// Token: 0x04000310 RID: 784
		private static TextureHandle<Texture2D> s_Handle;

		// Token: 0x020000DE RID: 222
		private enum LoadState
		{
			// Token: 0x040005BB RID: 1467
			Idle,
			// Token: 0x040005BC RID: 1468
			Loading,
			// Token: 0x040005BD RID: 1469
			Bound,
			// Token: 0x040005BE RID: 1470
			Unavailable
		}
	}
}
