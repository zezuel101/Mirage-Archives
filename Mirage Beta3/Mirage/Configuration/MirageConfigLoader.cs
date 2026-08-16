using System;
using System.Runtime.CompilerServices;
using Kopernicus.ConfigParser;
using Mirage.VirtualTexture;
using UnityEngine;

namespace Mirage.Configuration
{
	/// <summary>Parses <c>MirageTerrain { Body { } }</c> blocks and fills <see cref="T:Mirage.Configuration.MirageBodyRegistry" />.</summary>
	// Token: 0x02000082 RID: 130
	[KSPAddon(2, true)]
	public class MirageConfigLoader : MonoBehaviour
	{
		// Token: 0x060003A7 RID: 935 RVA: 0x0001B7F8 File Offset: 0x000199F8
		private void Start()
		{
			string text = "Mirage";
			ParserOptions.Data data = new ParserOptions.Data();
			data.LogCallback = delegate(object obj)
			{
				MirageDebug.Log((obj != null) ? obj.ToString() : null);
			};
			data.ErrorCallback = delegate(Exception err)
			{
				MirageDebug.LogError(err.ToString());
			};
			ParserOptions.Register(text, data);
			MirageSettings.Load();
			UrlDir.UrlConfig[] configs = GameDatabase.Instance.GetConfigs("MirageTerrain");
			bool flag = configs == null || configs.Length == 0;
			if (flag)
			{
				MirageDebug.Log("MirageConfigLoader: no MirageTerrain blocks found.");
			}
			else
			{
				int registered = 0;
				int failed = 0;
				foreach (UrlDir.UrlConfig url in configs)
				{
					foreach (ConfigNode bodyNode in url.config.GetNodes("Body"))
					{
						bool flag2 = MirageConfigLoader.TryRegisterBody(bodyNode, url.url);
						if (flag2)
						{
							registered++;
						}
						else
						{
							failed++;
						}
					}
				}
				string summary = string.Format("MirageConfigLoader: registered {0} body config(s)", registered);
				bool flag3 = failed > 0;
				if (flag3)
				{
					summary += string.Format(", {0} failed", failed);
				}
				MirageDebug.Log(summary);
			}
		}

		// Token: 0x060003A8 RID: 936 RVA: 0x0001B944 File Offset: 0x00019B44
		private static bool TryRegisterBody(ConfigNode bodyNode, string sourceUrl)
		{
			MirageBodyConfigLoader loader = new MirageBodyConfigLoader();
			try
			{
				Parser.LoadObjectFromConfigurationNode(loader, bodyNode, "Mirage", true);
			}
			catch (Exception e)
			{
				return MirageConfigLoader.Reject("parse failed for Body at '" + sourceUrl + "': " + e.Message);
			}
			bool flag = string.IsNullOrEmpty(loader.Name);
			bool result;
			if (flag)
			{
				result = MirageConfigLoader.Reject("Body block at '" + sourceUrl + "' is missing 'name'.");
			}
			else
			{
				VirtualTextureConfigLoader virtualTexture = loader.VirtualTexture;
				VirtualTextureConfig cfg = (virtualTexture != null) ? virtualTexture.Config : null;
				bool flag2 = cfg == null;
				if (flag2)
				{
					result = MirageConfigLoader.Reject(string.Concat(new string[]
					{
						"Body '",
						loader.Name,
						"' at '",
						sourceUrl,
						"' has no VirtualTexture subnode."
					}));
				}
				else
				{
					bool flag3 = !cfg.IsValid;
					if (flag3)
					{
						result = MirageConfigLoader.Reject(string.Concat(new string[]
						{
							"Body '",
							loader.Name,
							"' at '",
							sourceUrl,
							"' has no readable tile archive — archivePath must point at a directory holding at least one Level_<N> folder. Loose .dds tile pyramids are no longer supported; pack one with tools/ArchivePacker."
						}));
					}
					else
					{
						CanonicalLevelResolver.Resolve(loader.Name, cfg);
						MirageBodyRegistry.Register(loader.Name, cfg);
						MirageConfigLoader.LogRegistration(loader.Name, cfg);
						result = true;
					}
				}
			}
			return result;
		}

		// Token: 0x060003A9 RID: 937 RVA: 0x0001BA94 File Offset: 0x00019C94
		private static bool Reject(string reason)
		{
			MirageDebug.LogError("MirageConfigLoader: " + reason);
			return false;
		}

		// Token: 0x060003AA RID: 938 RVA: 0x0001BAB8 File Offset: 0x00019CB8
		private static void LogRegistration(string bodyName, VirtualTextureConfig cfg)
		{
			MirageConfigLoader.<>c__DisplayClass6_0 CS$<>8__locals1;
			CS$<>8__locals1.cfg = cfg;
			string layers = MirageConfigLoader.<LogRegistration>g__Layer|6_0(VTLayer.Color, ref CS$<>8__locals1) + MirageConfigLoader.<LogRegistration>g__Layer|6_0(VTLayer.Height, ref CS$<>8__locals1) + MirageConfigLoader.<LogRegistration>g__Layer|6_0(VTLayer.Normal, ref CS$<>8__locals1) + (CS$<>8__locals1.cfg.UseEmissiveLayer ? "y" : "n");
			MirageDebug.Log(string.Concat(new string[]
			{
				"  Registered VT config for '",
				bodyName,
				"': layers(chne)=",
				layers,
				" ",
				string.Format("canonical={0} web={1} ", CS$<>8__locals1.cfg.canonicalMaxLevel, CS$<>8__locals1.cfg.webMaxLevel),
				string.Format("atlas={0} tile={1}+{2}", CS$<>8__locals1.cfg.atlasSize, CS$<>8__locals1.cfg.tileSize, CS$<>8__locals1.cfg.borderPx)
			}));
		}

		// Token: 0x060003AC RID: 940 RVA: 0x0001BBAA File Offset: 0x00019DAA
		[CompilerGenerated]
		internal static string <LogRegistration>g__Layer|6_0(VTLayer layer, ref MirageConfigLoader.<>c__DisplayClass6_0 A_1)
		{
			return A_1.cfg.HasLayer(layer) ? "y" : "n";
		}

		// Token: 0x04000352 RID: 850
		private const string TopLevelNodeName = "MirageTerrain";

		// Token: 0x04000353 RID: 851
		private const string BodyNodeName = "Body";

		// Token: 0x04000354 RID: 852
		private const string ConfigContextName = "Mirage";
	}
}
