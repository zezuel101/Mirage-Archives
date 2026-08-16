using System;
using System.Collections.Generic;
using Mirage.Configuration;
using Mirage.Runtime;
using Mirage.VirtualTexture;
using UnityEngine;

namespace Mirage.ScaledSystem
{
	/// <summary>
	/// Sets up Mirage scaled-space rendering for every configured body: finds the <c>Mirage/Scaled</c>
	/// material Kopernicus created on the body's scaled mesh (see <see cref="T:Mirage.Configuration.MirageScaledShaderLoader" />)
	/// and attaches a <see cref="T:Mirage.ScaledSystem.ScaledOnDemandComponent" /> that loads/unloads the body's VT tile caches
	/// by on-screen size. Kopernicus owns material creation + the scattering/surge maps; this owns the
	/// VT residency.
	///
	/// Runs in flight/KSC/tracking station (the game scenes — NOT the main menu, which renders its own
	/// separate scaled bodies/materials; setting up there would bind the VT to the wrong instance).
	/// Set-up state is static (the scaled GameObjects + components are DontDestroyOnLoad), so each scene
	/// only sets up bodies it hasn't yet.
	///
	/// Uses <c>EveryScene</c> + an <see cref="M:Mirage.ScaledSystem.MirageScaledManager.IsRelevantScene" /> gate rather than a coarser Startup
	/// value: setup must reliably happen when a scene is entered cold (e.g. straight into the tracking
	/// station) — not only after passing through flight. Setup is also retried each frame (idempotent,
	/// guarded by <see cref="F:Mirage.ScaledSystem.MirageScaledManager.s_Bodies" />) so it catches up if the scaled materials/bodies aren't ready
	/// on the frame <c>Start()</c> runs.
	/// </summary>
	// Token: 0x0200005D RID: 93
	[KSPAddon(-1, false)]
	public class MirageScaledManager : MonoBehaviour
	{
		// Token: 0x060002B0 RID: 688 RVA: 0x00016D84 File Offset: 0x00014F84
		private void Start()
		{
			bool flag = !MirageScaledManager.IsRelevantScene();
			if (!flag)
			{
				Shader.SetGlobalFloat("_ScaledSpaceFactor", ScaledSpace.InverseScaleFactor);
				MirageSettings.PushShaderGlobals();
				MirageBlueNoise.EnsureLoaded();
				MirageScaledManager.TrySetUpBodies();
			}
		}

		// Token: 0x060002B1 RID: 689 RVA: 0x00016DC4 File Offset: 0x00014FC4
		private void Update()
		{
			bool flag = !MirageScaledManager.IsRelevantScene();
			if (!flag)
			{
				MirageBlueNoise.EnsureLoaded();
				MirageBlueNoise.SetFrameGlobal();
				MirageScaledManager.TrySetUpBodies();
				bool flag2 = HighLogic.LoadedScene == 8;
				if (flag2)
				{
					TileStreamingManager.Update(Time.frameCount);
				}
			}
		}

		// Token: 0x060002B2 RID: 690 RVA: 0x00016E0C File Offset: 0x0001500C
		private static void TrySetUpBodies()
		{
			int setUp = 0;
			foreach (KeyValuePair<string, VirtualTextureConfig> kvp in MirageBodyRegistry.All)
			{
				string name = kvp.Key;
				VirtualTextureConfig cfg = kvp.Value;
				bool flag = MirageScaledManager.s_Bodies.ContainsKey(name);
				if (!flag)
				{
					bool flag2 = !cfg.HasLayer(VTLayer.Color);
					if (!flag2)
					{
						CelestialBody body = FlightGlobals.GetBodyByName(name);
						bool flag3 = body == null || body.scaledBody == null;
						if (!flag3)
						{
							MeshRenderer mr = body.scaledBody.GetComponent<MeshRenderer>();
							bool flag4 = mr == null;
							if (!flag4)
							{
								Material mat = MirageScaledManager.FindScaledMaterial(mr.sharedMaterials);
								bool flag5 = mat == null;
								if (!flag5)
								{
									MirageScaledBody scaled = new MirageScaledBody(name, cfg, mat, body);
									MirageScaledManager.s_Bodies[name] = scaled;
									ScaledOnDemandComponent onDemand = body.scaledBody.AddComponent<ScaledOnDemandComponent>();
									onDemand.Init(scaled, body);
									setUp++;
								}
							}
						}
					}
				}
			}
			bool flag6 = setUp > 0;
			if (flag6)
			{
				MirageDebug.Log(string.Format("MirageScaledManager: set up {0} scaled body/bodies (on-demand).", setUp));
			}
		}

		// Token: 0x060002B3 RID: 691 RVA: 0x00016F5C File Offset: 0x0001515C
		private static bool IsRelevantScene()
		{
			return HighLogic.LoadedSceneIsFlight || HighLogic.LoadedScene == 8 || HighLogic.LoadedScene == 5;
		}

		// Token: 0x060002B4 RID: 692 RVA: 0x00016F78 File Offset: 0x00015178
		private static Material FindScaledMaterial(Material[] mats)
		{
			for (int i = 0; i < mats.Length; i++)
			{
				bool flag = MirageScaledShaderLoader.UsesSameShader(mats[i]);
				if (flag)
				{
					return mats[i];
				}
			}
			return null;
		}

		// Token: 0x0400028C RID: 652
		private static readonly Dictionary<string, MirageScaledBody> s_Bodies = new Dictionary<string, MirageScaledBody>();
	}
}
