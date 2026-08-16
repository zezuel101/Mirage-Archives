using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Mirage.Configuration
{
	/// <summary>Reflection bridge to Scatterer's atmosphere tables for ground irradiance.</summary>
	// Token: 0x02000083 RID: 131
	public static class MirageScattererBridge
	{
		// Token: 0x060003AD RID: 941 RVA: 0x0001BBC6 File Offset: 0x00019DC6
		public static bool CanBindAtmosphere(Material mat)
		{
			return mat != null && mat.HasProperty(MirageScattererBridge.s_AtmosphereAtlasID);
		}

		// Token: 0x060003AE RID: 942 RVA: 0x0001BBE0 File Offset: 0x00019DE0
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
				bool flag2 = !MirageScattererBridge.CanBindAtmosphere(mat);
				if (flag2)
				{
					result = false;
				}
				else
				{
					bool flag3 = !MirageScattererBridge.s_Resolved;
					if (flag3)
					{
						MirageScattererBridge.Resolve();
					}
					bool flag4 = !MirageScattererBridge.s_Available;
					if (flag4)
					{
						result = false;
					}
					else
					{
						try
						{
							object skyNode;
							bool flag5 = !MirageScattererBridge.TryFindLoadedSkyNode(celestialBodyName, out skyNode);
							if (flag5)
							{
								result = false;
							}
							else
							{
								if (MirageScattererBridge.s_InitUniforms == null)
								{
									MirageScattererBridge.s_InitUniforms = skyNode.GetType().GetMethod("InitUniforms", MirageScattererBridge.s_MaterialArg);
								}
								bool flag6 = MirageScattererBridge.s_InitUniforms == null;
								if (flag6)
								{
									result = false;
								}
								else
								{
									MirageScattererBridge.s_InitUniforms.Invoke(skyNode, new object[]
									{
										mat
									});
									MirageScattererBridge.SnapshotAndBindAtlas(celestialBodyName, mat);
									result = true;
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
			}
			return result;
		}

		// Token: 0x060003AF RID: 943 RVA: 0x0001BCEC File Offset: 0x00019EEC
		private static void Resolve()
		{
			MirageScattererBridge.s_Resolved = true;
			try
			{
				Type scattererType = Type.GetType("Scatterer.Scatterer, scatterer");
				bool flag = scattererType == null;
				if (!flag)
				{
					MirageScattererBridge.s_InstanceProp = scattererType.GetProperty("Instance", BindingFlags.Static | BindingFlags.Public);
					MirageScattererBridge.s_ConfigReaderField = scattererType.GetField("planetsConfigsReader", BindingFlags.Instance | BindingFlags.Public);
					MirageScattererBridge.s_Available = (MirageScattererBridge.s_InstanceProp != null && MirageScattererBridge.s_ConfigReaderField != null);
					bool flag2 = MirageScattererBridge.s_Available;
					if (flag2)
					{
						MirageDebug.Log("MirageScattererBridge: Scatterer detected — atmospheric ground irradiance available.");
					}
				}
			}
			catch (Exception e)
			{
				MirageDebug.LogError("MirageScattererBridge: failed to resolve Scatterer: " + e.Message);
				MirageScattererBridge.s_Available = false;
			}
		}

		// Token: 0x060003B0 RID: 944 RVA: 0x0001BDA8 File Offset: 0x00019FA8
		private static bool TryFindLoadedSkyNode(string celestialBodyName, out object skyNode)
		{
			skyNode = null;
			object instance = MirageScattererBridge.s_InstanceProp.GetValue(null);
			bool flag = instance == null;
			bool result;
			if (flag)
			{
				result = false;
			}
			else
			{
				object configReader = MirageScattererBridge.s_ConfigReaderField.GetValue(instance);
				bool flag2 = configReader == null;
				if (flag2)
				{
					result = false;
				}
				else
				{
					if (MirageScattererBridge.s_BodiesField == null)
					{
						MirageScattererBridge.s_BodiesField = configReader.GetType().GetField("scattererCelestialBodies");
					}
					FieldInfo fieldInfo = MirageScattererBridge.s_BodiesField;
					IEnumerable bodies = ((fieldInfo != null) ? fieldInfo.GetValue(configReader) : null) as IEnumerable;
					bool flag3 = bodies == null;
					if (flag3)
					{
						result = false;
					}
					else
					{
						foreach (object body in bodies)
						{
							bool flag4 = body == null;
							if (!flag4)
							{
								if (MirageScattererBridge.s_NameField == null)
								{
									MirageScattererBridge.s_NameField = body.GetType().GetField("celestialBodyName");
								}
								FieldInfo fieldInfo2 = MirageScattererBridge.s_NameField;
								bool flag5 = ((fieldInfo2 != null) ? fieldInfo2.GetValue(body) : null) as string != celestialBodyName;
								if (!flag5)
								{
									if (MirageScattererBridge.s_ProlandField == null)
									{
										MirageScattererBridge.s_ProlandField = body.GetType().GetField("prolandManager");
									}
									FieldInfo fieldInfo3 = MirageScattererBridge.s_ProlandField;
									object proland = (fieldInfo3 != null) ? fieldInfo3.GetValue(body) : null;
									bool flag6 = proland == null;
									if (flag6)
									{
										return false;
									}
									if (MirageScattererBridge.s_SkyNodeField == null)
									{
										MirageScattererBridge.s_SkyNodeField = proland.GetType().GetField("skyNode");
									}
									FieldInfo fieldInfo4 = MirageScattererBridge.s_SkyNodeField;
									object node = (fieldInfo4 != null) ? fieldInfo4.GetValue(proland) : null;
									bool flag7 = node == null;
									if (flag7)
									{
										return false;
									}
									if (MirageScattererBridge.s_LoadedField == null)
									{
										MirageScattererBridge.s_LoadedField = node.GetType().GetField("precomputedAtmoLoaded", BindingFlags.Instance | BindingFlags.NonPublic);
									}
									FieldInfo fieldInfo5 = MirageScattererBridge.s_LoadedField;
									object obj = (fieldInfo5 != null) ? fieldInfo5.GetValue(node) : null;
									bool flag8;
									if (obj is bool)
									{
										bool loaded = (bool)obj;
										flag8 = !loaded;
									}
									else
									{
										flag8 = true;
									}
									bool flag9 = flag8;
									if (flag9)
									{
										return false;
									}
									skyNode = node;
									return true;
								}
							}
						}
						result = false;
					}
				}
			}
			return result;
		}

		// Token: 0x060003B1 RID: 945 RVA: 0x0001BFE0 File Offset: 0x0001A1E0
		private static void SnapshotAndBindAtlas(string body, Material mat)
		{
			RenderTexture snapshot;
			bool flag = !MirageScattererBridge.s_AtlasSnapshots.TryGetValue(body, out snapshot) || snapshot == null;
			if (flag)
			{
				Texture source = mat.GetTexture(MirageScattererBridge.s_AtmosphereAtlasID);
				bool flag2 = source == null;
				if (flag2)
				{
					return;
				}
				snapshot = new RenderTexture(source.width, source.height, 0, 2, 1)
				{
					name = "MirageAtmosphereAtlas_" + body,
					filterMode = 1,
					wrapMode = 1
				};
				snapshot.Create();
				Graphics.Blit(source, snapshot);
				MirageScattererBridge.s_AtlasSnapshots[body] = snapshot;
				MirageDebug.Log(string.Concat(new string[]
				{
					"MirageScattererBridge: snapshotted static atmosphere atlas for '",
					body,
					"' ",
					string.Format("({0}x{1}) — terrain decoupled from Scatterer's ", source.width, source.height),
					"RenderTexture."
				}));
			}
			mat.SetTexture(MirageScattererBridge.s_AtmosphereAtlasID, snapshot);
		}

		// Token: 0x04000355 RID: 853
		private static readonly int s_AtmosphereAtlasID = Shader.PropertyToID("AtmosphereAtlas");

		// Token: 0x04000356 RID: 854
		private static readonly Dictionary<string, RenderTexture> s_AtlasSnapshots = new Dictionary<string, RenderTexture>();

		// Token: 0x04000357 RID: 855
		private static bool s_Resolved;

		// Token: 0x04000358 RID: 856
		private static bool s_Available;

		// Token: 0x04000359 RID: 857
		private const BindingFlags PublicStatic = BindingFlags.Static | BindingFlags.Public;

		// Token: 0x0400035A RID: 858
		private const BindingFlags PublicInstance = BindingFlags.Instance | BindingFlags.Public;

		// Token: 0x0400035B RID: 859
		private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

		// Token: 0x0400035C RID: 860
		private static PropertyInfo s_InstanceProp;

		// Token: 0x0400035D RID: 861
		private static FieldInfo s_ConfigReaderField;

		// Token: 0x0400035E RID: 862
		private static FieldInfo s_BodiesField;

		// Token: 0x0400035F RID: 863
		private static FieldInfo s_NameField;

		// Token: 0x04000360 RID: 864
		private static FieldInfo s_ProlandField;

		// Token: 0x04000361 RID: 865
		private static FieldInfo s_SkyNodeField;

		// Token: 0x04000362 RID: 866
		private static FieldInfo s_LoadedField;

		// Token: 0x04000363 RID: 867
		private static MethodInfo s_InitUniforms;

		// Token: 0x04000364 RID: 868
		private static readonly Type[] s_MaterialArg = new Type[]
		{
			typeof(Material)
		};
	}
}
