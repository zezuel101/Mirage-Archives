using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Mirage.Configuration
{
	/// <summary>
	/// Optional bridge to Scatterer's precomputed Bruneton atmosphere tables, used to light Mirage terrain with
	/// physically-based ground irradiance (atmosphere-transmitted sun + sky irradiance) instead of a flat sun
	/// colour + ambient. Scatterer is an optional dependency, so everything is reached by cached reflection
	/// (no hard assembly reference) — mirrors the pattern Parallax-Continued uses for the eclipse material.
	///
	/// <para><see cref="M:Mirage.Configuration.MirageScattererBridge.TryBindAtmosphere(System.String,UnityEngine.Material)" /> finds the active body's <c>SkyNode</c> and calls its public
	/// <c>InitUniforms(Material)</c>, which pushes the whole atmosphere uniform set Mirage needs onto the
	/// material (the <c>AtmosphereAtlas</c> + irradiance/transmittance scale-offsets + Rg/Rt + sun colour). The
	/// caller toggles the shader's <c>MIRAGE_ATMOSPHERE</c> keyword on the result; on false the shader stays on
	/// the flat sun/ambient fallback (no Scatterer, no atmosphere on the body, or tables not precomputed yet).</para>
	/// </summary>
	// Token: 0x02000074 RID: 116
	public static class MirageScattererBridge
	{
		// Token: 0x060003CC RID: 972 RVA: 0x0001B018 File Offset: 0x00019218
		private static void Resolve()
		{
			MirageScattererBridge.s_resolved = true;
			try
			{
				Type scattererType = Type.GetType("Scatterer.Scatterer, scatterer");
				bool flag = scattererType == null;
				if (!flag)
				{
					MirageScattererBridge.s_instanceProp = scattererType.GetProperty("Instance", BindingFlags.Static | BindingFlags.Public);
					MirageScattererBridge.s_pcrField = scattererType.GetField("planetsConfigsReader", BindingFlags.Instance | BindingFlags.Public);
					MirageScattererBridge.s_available = (MirageScattererBridge.s_instanceProp != null && MirageScattererBridge.s_pcrField != null);
					bool flag2 = MirageScattererBridge.s_available;
					if (flag2)
					{
						MirageDebug.Log("MirageScattererBridge: Scatterer detected — atmospheric ground irradiance available.");
					}
				}
			}
			catch (Exception e)
			{
				MirageDebug.LogError("MirageScattererBridge: failed to resolve Scatterer: " + e.Message);
				MirageScattererBridge.s_available = false;
			}
		}

		/// <summary>
		/// Binds the Scatterer atmosphere uniforms for <paramref name="celestialBodyName" /> onto
		/// <paramref name="mat" /> (via <c>SkyNode.InitUniforms</c>). Returns true only when Scatterer is present,
		/// the body has a loaded atmosphere, and binding succeeded — the caller should enable
		/// <c>MIRAGE_ATMOSPHERE</c> on success and disable it otherwise.
		/// </summary>
		// Token: 0x060003CD RID: 973 RVA: 0x0001B0D4 File Offset: 0x000192D4
		public static bool TryBindAtmosphere(string celestialBodyName, Material mat)
		{
			bool flag = mat == null || string.IsNullOrEmpty(celestialBodyName);
			bool result;
			if (flag)
			{
				result = false;
			}
			else
			{
				bool flag2 = !MirageScattererBridge.s_resolved;
				if (flag2)
				{
					MirageScattererBridge.Resolve();
				}
				bool flag3 = !MirageScattererBridge.s_available;
				if (flag3)
				{
					result = false;
				}
				else
				{
					try
					{
						object instance = MirageScattererBridge.s_instanceProp.GetValue(null);
						bool flag4 = instance == null;
						if (flag4)
						{
							result = false;
						}
						else
						{
							object pcr = MirageScattererBridge.s_pcrField.GetValue(instance);
							bool flag5 = pcr == null;
							if (flag5)
							{
								result = false;
							}
							else
							{
								if (MirageScattererBridge.s_bodiesField == null)
								{
									MirageScattererBridge.s_bodiesField = pcr.GetType().GetField("scattererCelestialBodies");
								}
								FieldInfo fieldInfo = MirageScattererBridge.s_bodiesField;
								IEnumerable bodies = ((fieldInfo != null) ? fieldInfo.GetValue(pcr) : null) as IEnumerable;
								bool flag6 = bodies == null;
								if (flag6)
								{
									result = false;
								}
								else
								{
									foreach (object scb in bodies)
									{
										bool flag7 = scb == null;
										if (!flag7)
										{
											if (MirageScattererBridge.s_nameField == null)
											{
												MirageScattererBridge.s_nameField = scb.GetType().GetField("celestialBodyName");
											}
											FieldInfo fieldInfo2 = MirageScattererBridge.s_nameField;
											bool flag8 = ((fieldInfo2 != null) ? fieldInfo2.GetValue(scb) : null) as string != celestialBodyName;
											if (!flag8)
											{
												if (MirageScattererBridge.s_prolandField == null)
												{
													MirageScattererBridge.s_prolandField = scb.GetType().GetField("prolandManager");
												}
												FieldInfo fieldInfo3 = MirageScattererBridge.s_prolandField;
												object proland = (fieldInfo3 != null) ? fieldInfo3.GetValue(scb) : null;
												bool flag9 = proland == null;
												if (flag9)
												{
													return false;
												}
												if (MirageScattererBridge.s_skyNodeField == null)
												{
													MirageScattererBridge.s_skyNodeField = proland.GetType().GetField("skyNode");
												}
												FieldInfo fieldInfo4 = MirageScattererBridge.s_skyNodeField;
												object skyNode = (fieldInfo4 != null) ? fieldInfo4.GetValue(proland) : null;
												bool flag10 = skyNode == null;
												if (flag10)
												{
													return false;
												}
												if (MirageScattererBridge.s_loadedField == null)
												{
													MirageScattererBridge.s_loadedField = skyNode.GetType().GetField("precomputedAtmoLoaded", BindingFlags.Instance | BindingFlags.NonPublic);
												}
												FieldInfo fieldInfo5 = MirageScattererBridge.s_loadedField;
												object obj = (fieldInfo5 != null) ? fieldInfo5.GetValue(skyNode) : null;
												bool flag11;
												if (obj is bool)
												{
													bool loaded = (bool)obj;
													flag11 = !loaded;
												}
												else
												{
													flag11 = true;
												}
												bool flag12 = flag11;
												if (flag12)
												{
													return false;
												}
												if (MirageScattererBridge.s_initUniforms == null)
												{
													MirageScattererBridge.s_initUniforms = skyNode.GetType().GetMethod("InitUniforms", new Type[]
													{
														typeof(Material)
													});
												}
												bool flag13 = MirageScattererBridge.s_initUniforms == null;
												if (flag13)
												{
													return false;
												}
												MirageScattererBridge.s_initUniforms.Invoke(skyNode, new object[]
												{
													mat
												});
												MirageScattererBridge.SnapshotAndBindAtlas(celestialBodyName, mat);
												return true;
											}
										}
									}
									result = false;
								}
							}
						}
					}
					catch (Exception e)
					{
						MirageDebug.LogError("MirageScattererBridge: bind failed for " + celestialBodyName + ": " + e.Message);
						result = false;
					}
				}
			}
			return result;
		}

		// Token: 0x060003CE RID: 974 RVA: 0x0001B3EC File Offset: 0x000195EC
		private static void SnapshotAndBindAtlas(string body, Material mat)
		{
			RenderTexture snap;
			bool flag = !MirageScattererBridge.s_AtlasSnapshots.TryGetValue(body, out snap) || snap == null;
			if (flag)
			{
				Texture src = mat.GetTexture("AtmosphereAtlas");
				bool flag2 = src == null;
				if (flag2)
				{
					return;
				}
				snap = new RenderTexture(src.width, src.height, 0, 2, 1)
				{
					name = "MirageAtmosphereAtlas_" + body,
					filterMode = 1,
					wrapMode = 1
				};
				snap.Create();
				Graphics.Blit(src, snap);
				MirageScattererBridge.s_AtlasSnapshots[body] = snap;
				MirageDebug.Log("MirageScattererBridge: snapshotted static atmosphere atlas for '" + body + "' " + string.Format("({0}x{1}) — terrain decoupled from Scatterer's RenderTexture.", src.width, src.height));
			}
			mat.SetTexture("AtmosphereAtlas", snap);
		}

		// Token: 0x040002D5 RID: 725
		private static bool s_resolved;

		// Token: 0x040002D6 RID: 726
		private static bool s_available;

		// Token: 0x040002D7 RID: 727
		private static PropertyInfo s_instanceProp;

		// Token: 0x040002D8 RID: 728
		private static FieldInfo s_pcrField;

		// Token: 0x040002D9 RID: 729
		private static FieldInfo s_bodiesField;

		// Token: 0x040002DA RID: 730
		private static FieldInfo s_nameField;

		// Token: 0x040002DB RID: 731
		private static FieldInfo s_prolandField;

		// Token: 0x040002DC RID: 732
		private static FieldInfo s_skyNodeField;

		// Token: 0x040002DD RID: 733
		private static FieldInfo s_loadedField;

		// Token: 0x040002DE RID: 734
		private static MethodInfo s_initUniforms;

		// Token: 0x040002DF RID: 735
		private static readonly Dictionary<string, RenderTexture> s_AtlasSnapshots = new Dictionary<string, RenderTexture>();
	}
}
