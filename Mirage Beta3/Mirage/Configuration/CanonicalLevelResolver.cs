using System;
using Mirage.VirtualTexture;
using UnityEngine;

namespace Mirage.Configuration
{
	/// <summary>Resolves canonical and web pyramid depth after config parsing.</summary>
	// Token: 0x0200007E RID: 126
	internal static class CanonicalLevelResolver
	{
		// Token: 0x06000397 RID: 919 RVA: 0x0001B440 File Offset: 0x00019640
		public static void Resolve(string bodyName, VirtualTextureConfig cfg)
		{
			bool flag = cfg.canonicalMaxLevel < 0;
			if (flag)
			{
				cfg.canonicalMaxLevel = CanonicalLevelResolver.Detect(cfg);
				MirageDebug.Log(string.Format("CanonicalLevelResolver: '{0}' canonicalMaxLevel={1} ", bodyName, cfg.canonicalMaxLevel) + "(auto-detected).");
			}
			bool flag2 = cfg.canonicalMaxLevel > 7;
			if (flag2)
			{
				MirageDebug.LogError(string.Format("CanonicalLevelResolver: '{0}' canonicalMaxLevel={1} ", bodyName, cfg.canonicalMaxLevel) + string.Format("exceeds the page table's ceiling of {0}; clamped. ", 7) + "Levels past it belong to the web tier, via webMaxLevel.");
				cfg.canonicalMaxLevel = 7;
			}
			bool flag3 = cfg.webMaxLevel < 0;
			if (flag3)
			{
				cfg.webMaxLevel = cfg.canonicalMaxLevel;
			}
			else
			{
				bool flag4 = cfg.webMaxLevel < cfg.canonicalMaxLevel;
				if (flag4)
				{
					MirageDebug.LogError(string.Format("CanonicalLevelResolver: '{0}' webMaxLevel={1} is below ", bodyName, cfg.webMaxLevel) + string.Format("canonicalMaxLevel={0}; raising it to canonical, since ", cfg.canonicalMaxLevel) + "the deepest level cannot be shallower than the canonical data.");
					cfg.webMaxLevel = cfg.canonicalMaxLevel;
				}
			}
		}

		// Token: 0x06000398 RID: 920 RVA: 0x0001B554 File Offset: 0x00019754
		private static int Detect(VirtualTextureConfig cfg)
		{
			int min = int.MaxValue;
			foreach (VTLayer layer in new VTLayer[]
			{
				VTLayer.Color,
				VTLayer.Height,
				VTLayer.Normal
			})
			{
				int level = cfg.ArchiveLayerMaxLevel(layer);
				bool flag = level >= 0;
				if (flag)
				{
					min = Mathf.Min(min, level);
				}
			}
			return (min == int.MaxValue) ? 3 : min;
		}
	}
}
