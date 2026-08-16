using System;
using System.Reflection;

namespace Mirage.VirtualTexture
{
	/// <summary>
	/// Puts KSPTextureLoader on its seeking read backend, once, before the first archive read.
	/// </summary>
	// Token: 0x02000041 RID: 65
	internal static class SeekingReadBackend
	{
		// Token: 0x0600018B RID: 395 RVA: 0x0000C2D4 File Offset: 0x0000A4D4
		public static void Force()
		{
			bool flag = SeekingReadBackend.s_Forced;
			if (!flag)
			{
				SeekingReadBackend.s_Forced = true;
				try
				{
					object config;
					FieldInfo field;
					bool flag2 = !SeekingReadBackend.TryGetSetting(out config, out field);
					if (flag2)
					{
						MirageDebug.LogError("TileArchive: couldn't reach Config.UseAsyncReadManager — if archive loads are extremely slow, set UseAsyncReadManager = true in KSPTextureLoader.cfg manually.");
					}
					else
					{
						object value = field.GetValue(config);
						bool alreadyOn;
						bool flag3;
						if (value is bool)
						{
							alreadyOn = (bool)value;
							flag3 = true;
						}
						else
						{
							flag3 = false;
						}
						bool flag4 = flag3 && alreadyOn;
						if (!flag4)
						{
							field.SetValue(config, true);
							MirageDebug.Log("TileArchive: forced KSPTextureLoader UseAsyncReadManager = true (the managed read fallback scans the blob from byte 0 per tile — unusable for multi-GB archives).");
						}
					}
				}
				catch (Exception e)
				{
					MirageDebug.LogError("TileArchive: could not force the seeking read backend: " + e.Message);
				}
			}
		}

		// Token: 0x0600018C RID: 396 RVA: 0x0000C388 File Offset: 0x0000A588
		private static bool TryGetSetting(out object config, out FieldInfo field)
		{
			Type configType = Type.GetType("KSPTextureLoader.Config, KSPTextureLoader");
			object obj;
			if (configType == null)
			{
				obj = null;
			}
			else
			{
				PropertyInfo property = configType.GetProperty("Instance", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
				obj = ((property != null) ? property.GetValue(null) : null);
			}
			object obj2;
			if ((obj2 = obj) == null)
			{
				if (configType == null)
				{
					obj2 = null;
				}
				else
				{
					FieldInfo field2 = configType.GetField("Instance", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
					obj2 = ((field2 != null) ? field2.GetValue(null) : null);
				}
			}
			config = obj2;
			field = ((configType != null) ? configType.GetField("UseAsyncReadManager", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) : null);
			return config != null && field != null;
		}

		// Token: 0x0400014D RID: 333
		private const BindingFlags AnyStatic = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

		// Token: 0x0400014E RID: 334
		private const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

		// Token: 0x0400014F RID: 335
		private static bool s_Forced;
	}
}
