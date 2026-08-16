using System;
using Mirage.Configuration;
using Mirage.Subdivision;
using Mirage.VirtualTexture;
using Unity.Profiling;
using UnityEngine;

namespace Mirage.Runtime
{
	/// <summary>Per-frame driver for flight and KSC: shader globals, streaming, subdivision.</summary>
	// Token: 0x0200006D RID: 109
	[KSPAddon(-3, false)]
	public class MirageRuntime : MonoBehaviour
	{
		// Token: 0x06000340 RID: 832 RVA: 0x00018E75 File Offset: 0x00017075
		private void Awake()
		{
			MirageShaderGlobals.Install();
		}

		// Token: 0x06000341 RID: 833 RVA: 0x00018E80 File Offset: 0x00017080
		private void Update()
		{
			CelestialBody mainBody = FlightGlobals.currentMainBody;
			using (MirageRuntime.s_PushGlobalsMarker.Auto())
			{
				MirageShaderGlobals.PushPerFrame(mainBody);
			}
			using (MirageRuntime.s_EnsureBodyMarker.Auto())
			{
				MirageRuntime.EnsureActiveBody(mainBody);
			}
			using (MirageRuntime.s_SubdivisionMarker.Auto())
			{
				SubdivisionRuntime.Update();
			}
			using (MirageRuntime.s_StreamingMarker.Auto())
			{
				TileStreamingManager.Update(Time.frameCount);
			}
			using (MirageRuntime.s_AtmosphereMarker.Auto())
			{
				MirageAtmosphereBinder.Refresh(MirageRuntime.s_ActiveBody);
			}
		}

		// Token: 0x06000342 RID: 834 RVA: 0x00018F9C File Offset: 0x0001719C
		private void OnDestroy()
		{
			MirageShaderGlobals.Uninstall();
			MirageRuntime.DeactivateCurrent();
			SubdivisionRuntime.Clear();
		}

		// Token: 0x06000343 RID: 835 RVA: 0x00018FB4 File Offset: 0x000171B4
		private static void EnsureActiveBody(CelestialBody mainBody)
		{
			string targetName = (mainBody != null) ? mainBody.name : null;
			MirageBody mirageBody = MirageRuntime.s_ActiveBody;
			string activeName = (mirageBody != null) ? mirageBody.SphereName : null;
			bool flag = targetName == activeName;
			if (!flag)
			{
				MirageRuntime.DeactivateCurrent();
				bool flag2 = mainBody == null;
				if (!flag2)
				{
					VirtualTextureConfig cfg;
					bool flag3 = !MirageBodyRegistry.TryGetConfig(targetName, out cfg);
					if (!flag3)
					{
						bool flag4 = mainBody.pqsController == null;
						if (!flag4)
						{
							try
							{
								MirageRuntime.s_ActiveBody = new MirageBody(mainBody, cfg);
								TileStreamingManager.RegisterBody(targetName, MirageRuntime.s_ActiveBody);
								WebIngestActivator.TryEnable(targetName, cfg, mainBody);
								TerrainMaterialBinder.BindCaches(MirageRuntime.s_ActiveBody, mainBody.pqsController);
								TerrainMaterialBinder.ApplyTessellation(cfg, mainBody.pqsController);
								MirageDebug.Log("MirageRuntime: activated body '" + targetName + "'");
							}
							catch (Exception e)
							{
								MirageDebug.LogError("MirageRuntime: failed to activate '" + targetName + "': " + e.Message);
								TileStreamingManager.UnregisterBody(targetName);
								MirageBody mirageBody2 = MirageRuntime.s_ActiveBody;
								if (mirageBody2 != null)
								{
									mirageBody2.Dispose();
								}
								MirageRuntime.s_ActiveBody = null;
							}
						}
					}
				}
			}
		}

		// Token: 0x06000344 RID: 836 RVA: 0x000190E4 File Offset: 0x000172E4
		private static void DeactivateCurrent()
		{
			bool flag = MirageRuntime.s_ActiveBody == null;
			if (!flag)
			{
				string name = MirageRuntime.s_ActiveBody.SphereName;
				TileStreamingManager.UnregisterBody(name);
				MirageRuntime.s_ActiveBody.Dispose();
				MirageRuntime.s_ActiveBody = null;
				MirageAtmosphereBinder.Reset();
				MirageDebug.Log("MirageRuntime: deactivated body '" + name + "'");
			}
		}

		// Token: 0x04000318 RID: 792
		private static readonly ProfilerMarker s_PushGlobalsMarker = new ProfilerMarker("Mirage.PushPlanetGlobals");

		// Token: 0x04000319 RID: 793
		private static readonly ProfilerMarker s_EnsureBodyMarker = new ProfilerMarker("Mirage.EnsureActiveBody");

		// Token: 0x0400031A RID: 794
		private static readonly ProfilerMarker s_SubdivisionMarker = new ProfilerMarker("Mirage.SubdivisionRuntime.Update");

		// Token: 0x0400031B RID: 795
		private static readonly ProfilerMarker s_StreamingMarker = new ProfilerMarker("Mirage.TileStreamingManager.Update");

		// Token: 0x0400031C RID: 796
		private static readonly ProfilerMarker s_AtmosphereMarker = new ProfilerMarker("Mirage.Atmosphere");

		// Token: 0x0400031D RID: 797
		private static MirageBody s_ActiveBody;
	}
}
