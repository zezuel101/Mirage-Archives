using System;
using System.Collections.Generic;
using Mirage.VirtualTexture;

namespace Mirage.Configuration
{
	/// <summary>
	/// Public registry of per-body VT configuration loaded from <c>MirageTerrain</c>
	/// blocks. Populated once at <see cref="F:KSPAddon.Startup.MainMenu" /> by
	/// <see cref="T:Mirage.Configuration.MirageConfigLoader" />; downstream consumers (a host-side runtime,
	/// Parallax's <c>EnsureVirtualTextureCache</c>, etc.) read from it on body
	/// load.
	/// </summary>
	// Token: 0x0200006E RID: 110
	public static class MirageBodyRegistry
	{
		/// <summary>Number of registered bodies.</summary>
		// Token: 0x17000086 RID: 134
		// (get) Token: 0x06000316 RID: 790 RVA: 0x000199DE File Offset: 0x00017BDE
		public static int Count
		{
			get
			{
				return MirageBodyRegistry.s_Configs.Count;
			}
		}

		/// <summary>
		/// Look up the VT config for <paramref name="bodyName" />. Returns null when
		/// no <c>MirageTerrain { Body { name = … } }</c> block matched.
		/// </summary>
		// Token: 0x06000317 RID: 791 RVA: 0x000199EC File Offset: 0x00017BEC
		public static VirtualTextureConfig GetConfig(string bodyName)
		{
			VirtualTextureConfig c;
			return MirageBodyRegistry.s_Configs.TryGetValue(bodyName, out c) ? c : null;
		}

		// Token: 0x06000318 RID: 792 RVA: 0x00019A0C File Offset: 0x00017C0C
		public static bool TryGetConfig(string bodyName, out VirtualTextureConfig config)
		{
			return MirageBodyRegistry.s_Configs.TryGetValue(bodyName, out config);
		}

		/// <summary>Enumerate every registered (body name, config) pair.</summary>
		// Token: 0x17000087 RID: 135
		// (get) Token: 0x06000319 RID: 793 RVA: 0x00019A1A File Offset: 0x00017C1A
		public static IEnumerable<KeyValuePair<string, VirtualTextureConfig>> All
		{
			get
			{
				return MirageBodyRegistry.s_Configs;
			}
		}

		// Token: 0x0600031A RID: 794 RVA: 0x00019A21 File Offset: 0x00017C21
		internal static void Register(string bodyName, VirtualTextureConfig config)
		{
			MirageBodyRegistry.s_Configs[bodyName] = config;
		}

		// Token: 0x0600031B RID: 795 RVA: 0x00019A30 File Offset: 0x00017C30
		internal static void Clear()
		{
			foreach (KeyValuePair<string, VirtualTextureConfig> kv in MirageBodyRegistry.s_Configs)
			{
				kv.Value.CloseWebArchives();
			}
			MirageBodyRegistry.s_Configs.Clear();
		}

		// Token: 0x040002C9 RID: 713
		private static readonly Dictionary<string, VirtualTextureConfig> s_Configs = new Dictionary<string, VirtualTextureConfig>();
	}
}
