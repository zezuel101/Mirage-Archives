using System;
using System.Collections.Generic;
using Mirage.VirtualTexture;

namespace Mirage.Configuration
{
	/// <summary>Per-body VT configs, filled at main menu by <see cref="T:Mirage.Configuration.MirageConfigLoader" />.</summary>
	// Token: 0x02000081 RID: 129
	public static class MirageBodyRegistry
	{
		// Token: 0x1700008D RID: 141
		// (get) Token: 0x060003A0 RID: 928 RVA: 0x0001B72E File Offset: 0x0001992E
		public static int Count
		{
			get
			{
				return MirageBodyRegistry.s_Configs.Count;
			}
		}

		// Token: 0x1700008E RID: 142
		// (get) Token: 0x060003A1 RID: 929 RVA: 0x0001B73A File Offset: 0x0001993A
		public static IEnumerable<KeyValuePair<string, VirtualTextureConfig>> All
		{
			get
			{
				return MirageBodyRegistry.s_Configs;
			}
		}

		// Token: 0x060003A2 RID: 930 RVA: 0x0001B744 File Offset: 0x00019944
		public static VirtualTextureConfig GetConfig(string bodyName)
		{
			VirtualTextureConfig config;
			return MirageBodyRegistry.s_Configs.TryGetValue(bodyName, out config) ? config : null;
		}

		// Token: 0x060003A3 RID: 931 RVA: 0x0001B764 File Offset: 0x00019964
		public static bool TryGetConfig(string bodyName, out VirtualTextureConfig config)
		{
			return MirageBodyRegistry.s_Configs.TryGetValue(bodyName, out config);
		}

		// Token: 0x060003A4 RID: 932 RVA: 0x0001B772 File Offset: 0x00019972
		internal static void Register(string bodyName, VirtualTextureConfig config)
		{
			MirageBodyRegistry.s_Configs[bodyName] = config;
		}

		// Token: 0x060003A5 RID: 933 RVA: 0x0001B784 File Offset: 0x00019984
		internal static void Clear()
		{
			foreach (KeyValuePair<string, VirtualTextureConfig> entry in MirageBodyRegistry.s_Configs)
			{
				entry.Value.CloseWebArchives();
			}
			MirageBodyRegistry.s_Configs.Clear();
		}

		// Token: 0x04000351 RID: 849
		private static readonly Dictionary<string, VirtualTextureConfig> s_Configs = new Dictionary<string, VirtualTextureConfig>();
	}
}
