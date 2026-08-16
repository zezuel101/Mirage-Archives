using System;
using System.Runtime.CompilerServices;
using Kopernicus.ConfigParser;
using Mirage.VirtualTexture;
using UnityEngine;

namespace Mirage.Configuration
{
	/// <summary>
	/// Scans the KSP game database for <c>MirageTerrain { Body { … } }</c> config
	/// blocks, parses each one via Kopernicus's ConfigParser, and populates
	/// <see cref="T:Mirage.Configuration.MirageBodyRegistry" />. Runs once per session at the main menu.
	///
	/// Expected config shape:
	/// <code>
	/// MirageTerrain
	/// {
	///     Body
	///     {
	///         name = Earth
	///         VirtualTexture
	///         {
	///             colormapTilePath  = SomeMod/PluginData/Earth/Color
	///             heightmapTilePath = SomeMod/PluginData/Earth/Height
	///             normalmapTilePath = SomeMod/PluginData/Earth/Normal
	///             atlasSize = 8192
	///             tileSize  = 256
	///             borderPx  = 4
	///             webMaxLevel = 6   // canonicalMaxLevel auto-detects from the installed tiles
	///         }
	///     }
	/// }
	/// </code>
	/// Multiple top-level <c>MirageTerrain</c> blocks across separate cfg files
	/// are merged; duplicate body names keep the last one parsed.
	/// </summary>
	// Token: 0x02000071 RID: 113
	[KSPAddon(2, true)]
	public class MirageConfigLoader : MonoBehaviour
	{
		// Token: 0x06000358 RID: 856 RVA: 0x00019E68 File Offset: 0x00018068
		private void Start()
		{
			string text = "Mirage";
			ParserOptions.Data data = new ParserOptions.Data();
			data.LogCallback = delegate(object o)
			{
				MirageDebug.Log((o != null) ? o.ToString() : null);
			};
			data.ErrorCallback = delegate(Exception e)
			{
				MirageDebug.LogError(e.ToString());
			};
			ParserOptions.Register(text, data);
			MirageBodyRegistry.Clear();
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
				string msg = string.Format("MirageConfigLoader: registered {0} body config(s)", registered);
				bool flag3 = failed > 0;
				if (flag3)
				{
					msg += string.Format(", {0} failed", failed);
				}
				MirageDebug.Log(msg);
			}
		}

		// Token: 0x06000359 RID: 857 RVA: 0x00019FBC File Offset: 0x000181BC
		private static bool TryRegisterBody(ConfigNode bodyNode, string sourceUrl)
		{
			MirageBodyConfigLoader loader;
			try
			{
				loader = new MirageBodyConfigLoader();
				Parser.LoadObjectFromConfigurationNode(loader, bodyNode, "Mirage", true);
			}
			catch (Exception e)
			{
				MirageDebug.LogError("MirageConfigLoader: parse failed for Body at '" + sourceUrl + "': " + e.Message);
				return false;
			}
			bool flag = string.IsNullOrEmpty(loader.Name);
			bool result;
			if (flag)
			{
				MirageDebug.LogError("MirageConfigLoader: Body block at '" + sourceUrl + "' is missing 'name'.");
				result = false;
			}
			else
			{
				VirtualTextureConfigLoader virtualTexture = loader.VirtualTexture;
				bool flag2 = ((virtualTexture != null) ? virtualTexture.Config : null) == null;
				if (flag2)
				{
					MirageDebug.LogError(string.Concat(new string[]
					{
						"MirageConfigLoader: Body '",
						loader.Name,
						"' at '",
						sourceUrl,
						"' has no VirtualTexture subnode."
					}));
					result = false;
				}
				else
				{
					bool flag3 = !loader.VirtualTexture.Config.IsValid;
					if (flag3)
					{
						MirageDebug.LogError(string.Concat(new string[]
						{
							"MirageConfigLoader: Body '",
							loader.Name,
							"' at '",
							sourceUrl,
							"' VirtualTexture block has no archivePath (with a manifest) nor any colormapTilePath / heightmapTilePath / normalmapTilePath set."
						}));
						result = false;
					}
					else
					{
						MirageConfigLoader.ResolveLevels(loader.Name, loader.VirtualTexture.Config);
						MirageBodyRegistry.Register(loader.Name, loader.VirtualTexture.Config);
						MirageDebug.Log(string.Concat(new string[]
						{
							"  Registered VT config for '",
							loader.Name,
							"': src=",
							loader.VirtualTexture.Config.UseArchive ? "archive" : "loose",
							" color=",
							loader.VirtualTexture.Config.HasLayer(VTLayer.Color) ? "y" : "n",
							" height=",
							loader.VirtualTexture.Config.HasLayer(VTLayer.Height) ? "y" : "n",
							" normal=",
							loader.VirtualTexture.Config.HasLayer(VTLayer.Normal) ? "y" : "n",
							" ",
							string.Format("canonicalMaxLevel={0} ", loader.VirtualTexture.Config.canonicalMaxLevel),
							string.Format("webMaxLevel={0} ", loader.VirtualTexture.Config.webMaxLevel),
							string.Format("atlas={0} ", loader.VirtualTexture.Config.atlasSize),
							string.Format("tile={0}+{1}", loader.VirtualTexture.Config.tileSize, loader.VirtualTexture.Config.borderPx)
						}));
						result = true;
					}
				}
			}
			return result;
		}

		/// <summary>
		/// Resolve the two level knobs after parsing:
		///  • <see cref="F:Mirage.VirtualTexture.VirtualTextureConfig.canonicalMaxLevel" /> — the depth of the installed canonical data (the
		///    flat page-table directory tier). Auto-detected from the archive / loose pyramid unless the config
		///    pinned it, since canonical tiles ARE exactly the ones the flat directory holds.
		///  • <see cref="F:Mirage.VirtualTexture.VirtualTextureConfig.webMaxLevel" /> — the deepest level overall (how far web ingest streams
		///    past canonical). Normally set in config; defaults to canonical (no fine tier) and is never below it.
		/// </summary>
		// Token: 0x0600035A RID: 858 RVA: 0x0001A29C File Offset: 0x0001849C
		private static void ResolveLevels(string bodyName, VirtualTextureConfig cfg)
		{
			bool flag = cfg.canonicalMaxLevel < 0;
			if (flag)
			{
				cfg.canonicalMaxLevel = MirageConfigLoader.DetectCanonicalMaxLevel(bodyName, cfg);
				MirageDebug.Log(string.Format("MirageConfigLoader: '{0}' canonicalMaxLevel={1} (auto-detected).", bodyName, cfg.canonicalMaxLevel));
			}
			bool flag2 = cfg.webMaxLevel < 0;
			if (flag2)
			{
				cfg.webMaxLevel = cfg.canonicalMaxLevel;
			}
			else
			{
				bool flag3 = cfg.webMaxLevel < cfg.canonicalMaxLevel;
				if (flag3)
				{
					MirageDebug.LogError(string.Format("MirageConfigLoader: '{0}' webMaxLevel={1} is below ", bodyName, cfg.webMaxLevel) + string.Format("canonicalMaxLevel={0}; raising it to canonical (the deepest level ", cfg.canonicalMaxLevel) + "cannot be shallower than the canonical data).");
					cfg.webMaxLevel = cfg.canonicalMaxLevel;
				}
			}
		}

		/// <summary>Detected depth of the installed canonical data: probe each present map's tile pyramid and take
		/// the shallowest, so every level exists in every configured map (color/height/normal share one level
		/// count). Falls back to <see cref="F:Mirage.VirtualTexture.VirtualTextureConfig.DefaultMaxLevel" /> if no tiles are found.</summary>
		// Token: 0x0600035B RID: 859 RVA: 0x0001A358 File Offset: 0x00018558
		private static int DetectCanonicalMaxLevel(string bodyName, VirtualTextureConfig cfg)
		{
			bool useArchive = cfg.UseArchive;
			int result;
			if (useArchive)
			{
				int min = int.MaxValue;
				foreach (VTLayer layer in new VTLayer[]
				{
					VTLayer.Color,
					VTLayer.Height,
					VTLayer.Normal
				})
				{
					int i = cfg.ArchiveLayerMaxLevel(layer);
					bool flag = i >= 0;
					if (flag)
					{
						min = Mathf.Min(min, i);
					}
				}
				result = ((min == int.MaxValue) ? 3 : min);
			}
			else
			{
				int color = cfg.HasColormap ? MirageTileMath.DetectMaxLevel(cfg.colormapTilePath) : int.MaxValue;
				int height = cfg.HasHeightmap ? MirageTileMath.DetectMaxLevel(cfg.heightmapTilePath) : int.MaxValue;
				int normal = cfg.HasNormalmap ? MirageTileMath.DetectMaxLevel(cfg.normalmapTilePath) : int.MaxValue;
				int detected = Mathf.Min(MirageConfigLoader.<DetectCanonicalMaxLevel>g__Sanitize|6_0(color), Mathf.Min(MirageConfigLoader.<DetectCanonicalMaxLevel>g__Sanitize|6_0(height), MirageConfigLoader.<DetectCanonicalMaxLevel>g__Sanitize|6_0(normal)));
				bool flag2 = detected == int.MaxValue;
				if (flag2)
				{
					MirageDebug.LogError(string.Concat(new string[]
					{
						"MirageConfigLoader: '",
						bodyName,
						"' canonicalMaxLevel auto-detect found no tiles on disk (color=",
						MirageConfigLoader.<DetectCanonicalMaxLevel>g__Fmt|6_2(color),
						" height=",
						MirageConfigLoader.<DetectCanonicalMaxLevel>g__Fmt|6_2(height),
						" normal=",
						MirageConfigLoader.<DetectCanonicalMaxLevel>g__Fmt|6_2(normal),
						"); ",
						string.Format("defaulting to {0}. Check the tile paths.", 3)
					}));
					result = 3;
				}
				else
				{
					bool flag3 = MirageConfigLoader.<DetectCanonicalMaxLevel>g__Differs|6_1(color, detected) || MirageConfigLoader.<DetectCanonicalMaxLevel>g__Differs|6_1(height, detected) || MirageConfigLoader.<DetectCanonicalMaxLevel>g__Differs|6_1(normal, detected);
					if (flag3)
					{
						MirageDebug.LogError(string.Concat(new string[]
						{
							"MirageConfigLoader: '",
							bodyName,
							"' map pyramids have differing depths (color=",
							MirageConfigLoader.<DetectCanonicalMaxLevel>g__Fmt|6_2(color),
							" height=",
							MirageConfigLoader.<DetectCanonicalMaxLevel>g__Fmt|6_2(height),
							" normal=",
							MirageConfigLoader.<DetectCanonicalMaxLevel>g__Fmt|6_2(normal),
							"); ",
							string.Format("using the shallowest ({0}). Deeper tiles in other maps will be unused.", detected)
						}));
					}
					result = detected;
				}
			}
			return result;
		}

		// Token: 0x0600035D RID: 861 RVA: 0x0001A570 File Offset: 0x00018770
		[CompilerGenerated]
		internal static int <DetectCanonicalMaxLevel>g__Sanitize|6_0(int v)
		{
			return (v < 0) ? int.MaxValue : v;
		}

		// Token: 0x0600035E RID: 862 RVA: 0x0001A57E File Offset: 0x0001877E
		[CompilerGenerated]
		internal static bool <DetectCanonicalMaxLevel>g__Differs|6_1(int v, int chosen)
		{
			return v != int.MaxValue && v >= 0 && v != chosen;
		}

		// Token: 0x0600035F RID: 863 RVA: 0x0001A596 File Offset: 0x00018796
		[CompilerGenerated]
		internal static string <DetectCanonicalMaxLevel>g__Fmt|6_2(int v)
		{
			return (v == int.MaxValue) ? "-" : v.ToString();
		}

		// Token: 0x040002CD RID: 717
		private const string TopLevelNodeName = "MirageTerrain";

		// Token: 0x040002CE RID: 718
		private const string BodyNodeName = "Body";

		// Token: 0x040002CF RID: 719
		private const string ConfigContextName = "Mirage";
	}
}
