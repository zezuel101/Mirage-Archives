using System;
using System.Collections.Generic;
using Mirage.Configuration;
using Mirage.Runtime;
using Mirage.VirtualTexture;
using UnityEngine;

namespace Mirage.ScaledSystem
{
	/// <summary>Attaches scaled VT to every configured body's Mirage/Scaled material.</summary>
	// Token: 0x02000068 RID: 104
	[KSPAddon(-1, false)]
	public class MirageScaledManager : MonoBehaviour
	{
		// Token: 0x06000317 RID: 791 RVA: 0x0001813C File Offset: 0x0001633C
		private void Start()
		{
			bool flag = !MirageScaledManager.IsRelevantScene();
			if (!flag)
			{
				Shader.SetGlobalFloat(MirageScaledManager.s_ScaledSpaceFactorId, ScaledSpace.InverseScaleFactor);
				MirageSettings.PushShaderGlobals();
				MirageBlueNoise.EnsureLoaded();
				MirageScaledManager.TrySetUpBodies();
			}
		}

		// Token: 0x06000318 RID: 792 RVA: 0x0001817C File Offset: 0x0001637C
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

		/// <summary>Set up any configured body not yet registered. Safe to call every frame.</summary>
		// Token: 0x06000319 RID: 793 RVA: 0x000181C4 File Offset: 0x000163C4
		private static void TrySetUpBodies()
		{
			int setUp = 0;
			foreach (KeyValuePair<string, VirtualTextureConfig> entry in MirageBodyRegistry.All)
			{
				bool flag = MirageScaledManager.TrySetUpBody(entry.Key, entry.Value);
				if (flag)
				{
					setUp++;
				}
			}
			bool flag2 = setUp > 0;
			if (flag2)
			{
				MirageDebug.Log(string.Format("MirageScaledManager: set up {0} scaled body/bodies (on-demand).", setUp));
			}
		}

		// Token: 0x0600031A RID: 794 RVA: 0x0001824C File Offset: 0x0001644C
		private static bool TrySetUpBody(string name, VirtualTextureConfig cfg)
		{
			bool flag = MirageScaledManager.s_Bodies.ContainsKey(name);
			bool result;
			if (flag)
			{
				result = false;
			}
			else
			{
				bool flag2 = !cfg.HasLayer(VTLayer.Color);
				if (flag2)
				{
					result = false;
				}
				else
				{
					CelestialBody body = FlightGlobals.GetBodyByName(name);
					bool flag3 = body == null || body.scaledBody == null;
					if (flag3)
					{
						result = false;
					}
					else
					{
						MeshRenderer mr = body.scaledBody.GetComponent<MeshRenderer>();
						bool flag4 = mr == null;
						if (flag4)
						{
							result = false;
						}
						else
						{
							Material mat = MirageScaledManager.FindScaledMaterial(mr.sharedMaterials);
							bool flag5 = mat == null;
							if (flag5)
							{
								result = false;
							}
							else
							{
								mat = MirageScaledManager.EnsureOwnMaterial(name, mr, mat);
								MirageScaledBody scaled = new MirageScaledBody(name, cfg, mat, body);
								MirageScaledManager.s_Bodies[name] = scaled;
								MirageDebug.Log(string.Concat(new string[]
								{
									"MirageScaledManager: '",
									name,
									"' -> scaled material '",
									mat.name,
									"' ",
									string.Format("#{0}", mat.GetInstanceID())
								}));
								ScaledOnDemandComponent onDemand = body.scaledBody.AddComponent<ScaledOnDemandComponent>();
								onDemand.Init(scaled, body);
								result = true;
							}
						}
					}
				}
			}
			return result;
		}

		/// <summary>Not the main menu — it has separate scaled body instances that would poison s_Bodies.</summary>
		// Token: 0x0600031B RID: 795 RVA: 0x00018386 File Offset: 0x00016586
		private static bool IsRelevantScene()
		{
			return HighLogic.LoadedSceneIsFlight || HighLogic.LoadedScene == 8 || HighLogic.LoadedScene == 5;
		}

		// Token: 0x0600031C RID: 796 RVA: 0x000183A4 File Offset: 0x000165A4
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

		/// <summary>
		/// Guarantee this body owns its scaled material, giving it a private copy if another body already
		/// claimed the same instance.
		///
		/// <b>One material per body is an invariant the whole scaled path rests on</b> and nothing else
		/// checks it. Each body binds its own atlases and page table through
		/// <c>TileCache.BindToMaterial</c>, so two bodies sharing one material means the second to load
		/// silently overwrites the first's bindings — and the first then renders the second's surface.
		/// Kopernicus normally hands out a material per body, but two bodies cloned from the same
		/// <c>Template</c> come from the same source object, which is where a shared instance can come
		/// from.
		/// </summary>
		// Token: 0x0600031D RID: 797 RVA: 0x000183DC File Offset: 0x000165DC
		private static Material EnsureOwnMaterial(string name, MeshRenderer mr, Material mat)
		{
			bool claimed = false;
			foreach (KeyValuePair<string, MirageScaledBody> entry in MirageScaledManager.s_Bodies)
			{
				bool flag = entry.Value.ScaledMaterial == mat;
				if (flag)
				{
					claimed = true;
					break;
				}
			}
			bool flag2 = !claimed;
			Material result;
			if (flag2)
			{
				result = mat;
			}
			else
			{
				Material own = new Material(mat)
				{
					name = mat.name + " (" + name + ")"
				};
				Material[] mats = mr.sharedMaterials;
				for (int i = 0; i < mats.Length; i++)
				{
					bool flag3 = mats[i] == mat;
					if (flag3)
					{
						mats[i] = own;
					}
				}
				mr.sharedMaterials = mats;
				result = own;
			}
			return result;
		}

		// Token: 0x040002FD RID: 765
		private static readonly Dictionary<string, MirageScaledBody> s_Bodies = new Dictionary<string, MirageScaledBody>();

		// Token: 0x040002FE RID: 766
		private static readonly int s_ScaledSpaceFactorId = Shader.PropertyToID("_ScaledSpaceFactor");
	}
}
