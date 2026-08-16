using System;
using System.Globalization;
using Mirage.VirtualTexture;
using UnityEngine;

namespace Mirage
{
	/// <summary>
	/// Global (non-per-body) Mirage settings, read from the top-level <c>Mirage { }</c> node. Populated once at
	/// main menu by <see cref="T:Mirage.Configuration.MirageConfigLoader" />. Distinct from
	/// <see cref="T:Mirage.Configuration.MirageBodyRegistry" />, which holds the per-body
	/// <c>MirageTerrain { Body { … } }</c> config — these are the settings that apply mod-wide.
	/// </summary>
	// Token: 0x02000006 RID: 6
	public static class MirageSettings
	{
		/// <summary>
		/// Master switch for bake-as-you-fly across ALL bodies. Off by default: ingest pulls third-party imagery
		/// over the network and writes to the user's disk, so the whole feature is opt-in with one toggle rather
		/// than per body. A body still needs its own <c>webPath</c> + archive to actually ingest (see
		/// <see cref="P:Mirage.VirtualTexture.VirtualTextureConfig.UseWebIngest" />); this just gates it globally.
		/// </summary>
		// Token: 0x17000001 RID: 1
		// (get) Token: 0x0600000C RID: 12 RVA: 0x0000219D File Offset: 0x0000039D
		// (set) Token: 0x0600000D RID: 13 RVA: 0x000021A4 File Offset: 0x000003A4
		public static bool WebIngest { get; private set; }

		/// <summary>
		/// Read the web tier at all — stream levels finer than the canonical archive (L8+). Off by default. When
		/// off, the runtime caps streaming at each body's <c>canonicalMaxLevel</c> and never consults the web
		/// index, which silences "tile load failed" spam for un-baked fine tiles and skips their per-frame work.
		/// <see cref="P:Mirage.MirageSettings.WebIngest" /> implies this — you must stream what you bake — so ingest still works with this
		/// off. Turn it on (without ingest) to view an already-baked web tier without baking more.
		/// </summary>
		// Token: 0x17000002 RID: 2
		// (get) Token: 0x0600000E RID: 14 RVA: 0x000021AC File Offset: 0x000003AC
		// (set) Token: 0x0600000F RID: 15 RVA: 0x000021B3 File Offset: 0x000003B3
		public static bool WebStreaming { get; private set; }

		/// <summary>
		/// Let SCALED space read the web tier too. Off by default: scaled stops at each body's
		/// <c>canonicalMaxLevel</c> even when <see cref="P:Mirage.MirageSettings.WebStreaming" /> / <see cref="P:Mirage.MirageSettings.WebIngest" /> are on, so the
		/// fine tier is a surface-only feature.
		///
		/// Off is not merely a cap on detail — the scaled cache is then BUILT to canonical depth, so a scaled
		/// body allocates no fine-block atlas, never pages a block in, and never requests an L8+ tile that only
		/// web ingest (which runs for the surface body, not the scaled one) would have baked. Turn it on once
		/// the scaled path is meant to carry web-tier detail.
		///
		/// Read once at main menu like the rest of this node, and the scaled caches are built from it at body
		/// setup — so changing it needs a scene reload, not just a config reload.
		/// </summary>
		// Token: 0x17000003 RID: 3
		// (get) Token: 0x06000010 RID: 16 RVA: 0x000021BB File Offset: 0x000003BB
		// (set) Token: 0x06000011 RID: 17 RVA: 0x000021C2 File Offset: 0x000003C2
		public static bool ScaledWebStreaming { get; private set; }

		/// <summary>Disk cap for each body's baked web tier across all layers, in MB. A flown-over cube pyramid
		/// grows without limit; the oldest tiles are evicted past this (see <c>WebDiskBudget</c>). Mod-wide — the
		/// same cap applies to every body's tier. Default 4096.</summary>
		// Token: 0x17000004 RID: 4
		// (get) Token: 0x06000012 RID: 18 RVA: 0x000021CA File Offset: 0x000003CA
		// (set) Token: 0x06000013 RID: 19 RVA: 0x000021D1 File Offset: 0x000003D1
		public static int WebDiskCapMB { get; private set; } = 4096;

		/// <summary>Concurrent bakes per body. Deliberately modest — one bake is dozens of HTTPS fetches, three
		/// decodes, a reproject and a BC7 encode. Mod-wide. Default 4.</summary>
		// Token: 0x17000005 RID: 5
		// (get) Token: 0x06000014 RID: 20 RVA: 0x000021D9 File Offset: 0x000003D9
		// (set) Token: 0x06000015 RID: 21 RVA: 0x000021E0 File Offset: 0x000003E0
		public static int WebIngestConcurrency { get; private set; } = 4;

		/// <summary>Derive the ingested water mask from ESA WorldCover (10 m land cover, class 80 = permanent
		/// water) instead of the crude "height ≤ sea level" proxy. Off by default: like <see cref="P:Mirage.MirageSettings.WebIngest" /> it
		/// fetches third-party data over the network. A body must also have its own <c>waterMask</c> on to use it;
		/// where WorldCover has no data (deep open ocean) the mask falls back to the sea-level term. WorldCover is
		/// CC BY 4.0 — attribution required (logged at ingest start).</summary>
		// Token: 0x17000006 RID: 6
		// (get) Token: 0x06000016 RID: 22 RVA: 0x000021E8 File Offset: 0x000003E8
		// (set) Token: 0x06000017 RID: 23 RVA: 0x000021EF File Offset: 0x000003EF
		public static bool WorldCoverWater { get; private set; }

		/// <summary>Base URL of the WorldCover COG tiles (the folder holding the per-3° <c>…_Map.tif</c> files).
		/// Overridable so a mirror or a newer product year can be pointed at without a rebuild.</summary>
		// Token: 0x17000007 RID: 7
		// (get) Token: 0x06000018 RID: 24 RVA: 0x000021F7 File Offset: 0x000003F7
		// (set) Token: 0x06000019 RID: 25 RVA: 0x000021FE File Offset: 0x000003FE
		public static string WorldCoverUrl { get; private set; } = "https://esa-worldcover.s3.amazonaws.com/v200/2021/map";

		/// <summary>Filename prefix of the WorldCover COGs, before the <c>_{tile}_Map.tif</c> suffix. Paired with
		/// <see cref="P:Mirage.MirageSettings.WorldCoverUrl" /> so both can move together when the product version changes.</summary>
		// Token: 0x17000008 RID: 8
		// (get) Token: 0x0600001A RID: 26 RVA: 0x00002206 File Offset: 0x00000406
		// (set) Token: 0x0600001B RID: 27 RVA: 0x0000220D File Offset: 0x0000040D
		public static string WorldCoverPrefix { get; private set; } = "ESA_WorldCover_10m_2021_v200";

		/// <summary>Sink depth (body/game metres) for WorldCover ocean. WorldCover water (class 80) within the
		/// flatten band is set to this far below sea level BEFORE the flatten, so the flatten curve then sends it
		/// DOWN — a positive bad ocean fill (3DEP/SRTM write 0..~10 m into the sea) would otherwise be pushed UP
		/// into a spike. Elevated water (a mountain lake, above <see cref="P:Mirage.MirageSettings.SeaFlattenMax" />) is excluded; water
		/// already deeper than this is left for bathymetry. 0 disables the sink. Default 2 m.</summary>
		// Token: 0x17000009 RID: 9
		// (get) Token: 0x0600001C RID: 28 RVA: 0x00002215 File Offset: 0x00000415
		// (set) Token: 0x0600001D RID: 29 RVA: 0x0000221C File Offset: 0x0000041C
		public static float WorldCoverSeaSinkM { get; private set; } = 2f;

		/// <summary>Ceiling (body/game metres) above which WorldCover water is treated as an ELEVATED lake, not
		/// mislabelled ocean, and is never sunk. This is the lake safety, independent of the flatten band: a
		/// mountain lake, the Great Lakes (~46 game m), Titicaca sit far above it and are left at their true
		/// altitude. Keep it just above the worst DEM ocean fill (3DEP/SRTM reach ~10 m). Default 10 m.</summary>
		// Token: 0x1700000A RID: 10
		// (get) Token: 0x0600001E RID: 30 RVA: 0x00002224 File Offset: 0x00000424
		// (set) Token: 0x0600001F RID: 31 RVA: 0x0000222B File Offset: 0x0000042B
		public static float WorldCoverSeaSinkMaxM { get; private set; } = 10f;

		/// <summary>Lower edge (game metres) of the sea-level flatten band. Terrain below this is real DEM, kept.
		/// See <see cref="T:Mirage.WebIngest.SeaLevelFlatten" />. For sea level to be the fixed point, keep it
		/// symmetric: <c>SeaFlattenMin = -SeaFlattenMax</c>. Default −8 m.</summary>
		// Token: 0x1700000B RID: 11
		// (get) Token: 0x06000020 RID: 32 RVA: 0x00002233 File Offset: 0x00000433
		// (set) Token: 0x06000021 RID: 33 RVA: 0x0000223A File Offset: 0x0000043A
		public static float SeaFlattenMin { get; private set; } = -8f;

		/// <summary>Upper edge (game metres) of the sea-level flatten band. Terrain above this is real DEM, kept.
		/// Default +8 m.</summary>
		// Token: 0x1700000C RID: 12
		// (get) Token: 0x06000022 RID: 34 RVA: 0x00002242 File Offset: 0x00000442
		// (set) Token: 0x06000023 RID: 35 RVA: 0x00002249 File Offset: 0x00000449
		public static float SeaFlattenMax { get; private set; } = 8f;

		/// <summary>Steepening of the flatten curve about sea level — higher = thinner band residue = less
		/// z-fighting but a sharper coastal transition. 0 (or an empty band) disables the flatten. Default 6.</summary>
		// Token: 0x1700000D RID: 13
		// (get) Token: 0x06000024 RID: 36 RVA: 0x00002251 File Offset: 0x00000451
		// (set) Token: 0x06000025 RID: 37 RVA: 0x00002258 File Offset: 0x00000458
		public static float SeaFlattenSlope { get; private set; } = 6f;

		/// <summary>Gaussian sigma (in output texels) that softens the hard 0/255 water mask into a ramp before it
		/// is stored in colour alpha and BC7-compressed. Stops the razor mask edge from becoming blocky colour
		/// artefacts on the coast (BC7 shares a block between the smooth RGB and the mask). ~1.5 is a gentle
		/// coast; 0 keeps the hard edge. Default 1.5.</summary>
		// Token: 0x1700000E RID: 14
		// (get) Token: 0x06000026 RID: 38 RVA: 0x00002260 File Offset: 0x00000460
		// (set) Token: 0x06000027 RID: 39 RVA: 0x00002267 File Offset: 0x00000467
		public static float WaterMaskBlurPx { get; private set; } = 1.5f;

		/// <summary>
		/// Texel-density multiplier for VT level selection. The default LOD target is one tile texel per screen
		/// pixel — anything finer cannot be seen, so it is neither streamed nor sampled. This scales that target:
		/// 2 asks for 2x the texels per pixel (one whole level finer), 4 for two levels, and so on. It is a LINEAR
		/// density factor, not a level offset, so non-power-of-two values are meaningful (1.5 ≈ +0.58 of a level).
		///
		/// Wanted for supersampled screenshots (a 2x downsampled grab really does resolve 2x the texels) and for
		/// sub-pixel high-frequency detail that survives downsampling as an average rather than vanishing — river
		/// glint, surf lines, road cuts — which the strict 1:1 target throws away at distance.
		///
		/// Cost is quadratic: 2 quadruples the tiles the working set needs, at the same atlas size, so a value
		/// past ~2 will thrash the cache and (with web ingest on) multiply the bake and disk load. Values below 1
		/// undersample deliberately — a cheap way to cut VRAM/bandwidth at the cost of a blurrier surface.
		/// Clamped to [<see cref="F:Mirage.MirageSettings.MinOversample" />, <see cref="F:Mirage.MirageSettings.MaxOversample" />]. Default 1 (exactly 1:1).
		///
		/// Applied in THREE places that must agree, since a level the shader samples but the streamer never
		/// fetched just resolves back to a coarser resident tile (all cost, no detail):
		/// <see cref="T:Mirage.VirtualTexture.TileStreamingManager" />'s screen-space descent, the scaled path's
		/// <c>LevelForDistance</c>, and the shader's <c>VTLevelFromMetres</c> via <c>_VTOversample</c>.
		/// </summary>
		// Token: 0x1700000F RID: 15
		// (get) Token: 0x06000028 RID: 40 RVA: 0x0000226F File Offset: 0x0000046F
		// (set) Token: 0x06000029 RID: 41 RVA: 0x00002276 File Offset: 0x00000476
		public static float Oversample { get; private set; } = 1f;

		/// <summary>
		/// Run the VT indirection self-check every frame and log the first breakage in full (see
		/// <see cref="M:Mirage.VirtualTexture.TileCache.ValidateIndirection(System.Collections.Generic.List{System.String},System.Int32,System.Boolean)" />). Off by default — it is a
		/// debugging aid for the "terrain shows tiles from somewhere else" corruption, not a runtime guard.
		/// The per-frame part is O(slots + blocks); only the first failure pays for the deep walk.
		/// </summary>
		// Token: 0x17000010 RID: 16
		// (get) Token: 0x0600002A RID: 42 RVA: 0x0000227E File Offset: 0x0000047E
		// (set) Token: 0x0600002B RID: 43 RVA: 0x00002285 File Offset: 0x00000485
		public static bool ValidateIndirection { get; private set; }

		/// <summary>Read the global <c>Mirage { }</c> node(s) from the game database. Last value wins if several
		/// exist (e.g. a Module Manager override). Called at main menu; safe to call again on a database reload.</summary>
		// Token: 0x0600002C RID: 44 RVA: 0x00002290 File Offset: 0x00000490
		internal static void Load()
		{
			bool webIngest = false;
			bool webStreaming = false;
			bool scaledWebStreaming = false;
			int webDiskCapMB = 4096;
			int webIngestConcurrency = 4;
			bool worldCoverWater = false;
			string worldCoverUrl = "https://esa-worldcover.s3.amazonaws.com/v200/2021/map";
			string worldCoverPrefix = "ESA_WorldCover_10m_2021_v200";
			float worldCoverSeaSinkM = 2f;
			float worldCoverSeaSinkMaxM = 10f;
			float seaFlattenMin = -8f;
			float seaFlattenMax = 8f;
			float seaFlattenSlope = 6f;
			float waterMaskBlurPx = 1.5f;
			float oversample = 1f;
			bool validateIndirection = false;
			GameDatabase instance = GameDatabase.Instance;
			UrlDir.UrlConfig[] nodes = (instance != null) ? instance.GetConfigs("Mirage") : null;
			bool flag = nodes != null;
			if (flag)
			{
				foreach (UrlDir.UrlConfig url in nodes)
				{
					ConfigNode i = url.config;
					string v = i.GetValue("webIngest");
					bool flag2 = v != null;
					if (flag2)
					{
						webIngest = MirageSettings.ParseBool(v, webIngest);
					}
					v = i.GetValue("webStreaming");
					bool flag3 = v != null;
					if (flag3)
					{
						webStreaming = MirageSettings.ParseBool(v, webStreaming);
					}
					v = i.GetValue("scaledWebStreaming");
					bool flag4 = v != null;
					if (flag4)
					{
						scaledWebStreaming = MirageSettings.ParseBool(v, scaledWebStreaming);
					}
					webDiskCapMB = MirageSettings.ParseInt(i.GetValue("webDiskCapMB"), webDiskCapMB);
					webIngestConcurrency = MirageSettings.ParseInt(i.GetValue("webIngestConcurrency"), webIngestConcurrency);
					v = i.GetValue("worldCoverWater");
					bool flag5 = v != null;
					if (flag5)
					{
						worldCoverWater = MirageSettings.ParseBool(v, worldCoverWater);
					}
					v = i.GetValue("worldCoverUrl");
					bool flag6 = !string.IsNullOrEmpty(v);
					if (flag6)
					{
						worldCoverUrl = v.Trim();
					}
					v = i.GetValue("worldCoverPrefix");
					bool flag7 = !string.IsNullOrEmpty(v);
					if (flag7)
					{
						worldCoverPrefix = v.Trim();
					}
					worldCoverSeaSinkM = MirageSettings.ParseFloat(i.GetValue("worldCoverSeaSinkM"), worldCoverSeaSinkM);
					worldCoverSeaSinkMaxM = MirageSettings.ParseFloat(i.GetValue("worldCoverSeaSinkMaxM"), worldCoverSeaSinkMaxM);
					seaFlattenMin = MirageSettings.ParseFloat(i.GetValue("seaFlattenMin"), seaFlattenMin);
					seaFlattenMax = MirageSettings.ParseFloat(i.GetValue("seaFlattenMax"), seaFlattenMax);
					seaFlattenSlope = MirageSettings.ParseFloat(i.GetValue("seaFlattenSlope"), seaFlattenSlope);
					waterMaskBlurPx = MirageSettings.ParseFloat(i.GetValue("waterMaskBlurPx"), waterMaskBlurPx);
					oversample = MirageSettings.ParseFloat(i.GetValue("oversample"), oversample);
					v = i.GetValue("validateIndirection");
					bool flag8 = v != null;
					if (flag8)
					{
						validateIndirection = MirageSettings.ParseBool(v, validateIndirection);
					}
				}
			}
			MirageSettings.WebIngest = webIngest;
			MirageSettings.WebStreaming = webStreaming;
			MirageSettings.ScaledWebStreaming = scaledWebStreaming;
			MirageSettings.WebDiskCapMB = Math.Max(1, webDiskCapMB);
			MirageSettings.WebIngestConcurrency = Math.Max(1, webIngestConcurrency);
			MirageSettings.WorldCoverWater = worldCoverWater;
			MirageSettings.WorldCoverUrl = worldCoverUrl;
			MirageSettings.WorldCoverPrefix = worldCoverPrefix;
			MirageSettings.WorldCoverSeaSinkM = Math.Max(0f, worldCoverSeaSinkM);
			MirageSettings.WorldCoverSeaSinkMaxM = worldCoverSeaSinkMaxM;
			MirageSettings.SeaFlattenMin = seaFlattenMin;
			MirageSettings.SeaFlattenMax = seaFlattenMax;
			MirageSettings.SeaFlattenSlope = Math.Max(0f, seaFlattenSlope);
			MirageSettings.WaterMaskBlurPx = Math.Max(0f, waterMaskBlurPx);
			MirageSettings.Oversample = Math.Min(Math.Max(oversample, 0.25f), 8f);
			bool flag9 = Math.Abs(MirageSettings.Oversample - oversample) > 0.0001f;
			if (flag9)
			{
				MirageDebug.LogError(string.Format("MirageSettings: oversample = {0} is outside [{1}, {2}]; ", oversample, 0.25f, 8f) + string.Format("clamped to {0}.", MirageSettings.Oversample));
			}
			MirageSettings.ValidateIndirection = validateIndirection;
			TileStreamingManager.ValidateEveryFrame = validateIndirection;
			MirageSettings.PushShaderGlobals();
			MirageDebug.Log(string.Concat(new string[]
			{
				string.Format("MirageSettings: webIngest = {0}, webStreaming = {1}, ", MirageSettings.WebIngest, MirageSettings.WebStreaming),
				string.Format("scaledWebStreaming = {0}, ", MirageSettings.ScaledWebStreaming),
				string.Format("webDiskCapMB = {0}, webIngestConcurrency = {1}, ", MirageSettings.WebDiskCapMB, MirageSettings.WebIngestConcurrency),
				string.Format("worldCoverWater = {0}, oversample = {1}, ", MirageSettings.WorldCoverWater, MirageSettings.Oversample),
				string.Format("validateIndirection = {0}", MirageSettings.ValidateIndirection)
			}));
			bool validateIndirection2 = MirageSettings.ValidateIndirection;
			if (validateIndirection2)
			{
				MirageDebug.Log("MirageSettings: VT indirection validation is ARMED — the checker runs every frame and logs the first violation as '[VT Validate]'. No such line means no CPU-side violation was seen.");
			}
		}

		/// <summary>Push the settings that the shader needs as globals. Called on <see cref="M:Mirage.MirageSettings.Load" /> and again by
		/// each scene's driver (<c>MirageRuntime</c> / <c>MirageScaledManager</c>) — settings are read once at main
		/// menu, so this only has to survive scene changes, not track a value that moves.</summary>
		// Token: 0x0600002D RID: 45 RVA: 0x000026DE File Offset: 0x000008DE
		internal static void PushShaderGlobals()
		{
			Shader.SetGlobalFloat(MirageSettings.s_VTOversampleID, MirageSettings.Oversample);
		}

		// Token: 0x0600002E RID: 46 RVA: 0x000026F4 File Offset: 0x000008F4
		private static int ParseInt(string s, int fallback)
		{
			bool flag = string.IsNullOrEmpty(s);
			int result;
			if (flag)
			{
				result = fallback;
			}
			else
			{
				int v;
				result = (int.TryParse(s.Trim(), out v) ? v : fallback);
			}
			return result;
		}

		// Token: 0x0600002F RID: 47 RVA: 0x00002728 File Offset: 0x00000928
		private static float ParseFloat(string s, float fallback)
		{
			bool flag = string.IsNullOrEmpty(s);
			float result;
			if (flag)
			{
				result = fallback;
			}
			else
			{
				float v;
				result = (float.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out v) ? v : fallback);
			}
			return result;
		}

		// Token: 0x06000030 RID: 48 RVA: 0x00002768 File Offset: 0x00000968
		private static bool ParseBool(string s, bool fallback)
		{
			bool flag = string.IsNullOrEmpty(s);
			bool result;
			if (flag)
			{
				result = fallback;
			}
			else
			{
				s = s.Trim();
				bool b;
				bool flag2 = bool.TryParse(s, out b);
				if (flag2)
				{
					result = b;
				}
				else
				{
					bool flag3 = s == "1";
					if (flag3)
					{
						result = true;
					}
					else
					{
						bool flag4 = s == "0";
						result = (!flag4 && fallback);
					}
				}
			}
			return result;
		}

		// Token: 0x04000005 RID: 5
		private const string NodeName = "Mirage";

		/// <summary>Floor on <see cref="P:Mirage.MirageSettings.Oversample" />. Below this the surface is a blur and the coarse pinned
		/// levels are all that is left.</summary>
		// Token: 0x04000016 RID: 22
		public const float MinOversample = 0.25f;

		/// <summary>Ceiling on <see cref="P:Mirage.MirageSettings.Oversample" /> — 8 is +3 levels, i.e. 64x the tiles. Well past useful;
		/// it exists so a typo'd value can't ask for a working set the atlas could never hold.</summary>
		// Token: 0x04000017 RID: 23
		public const float MaxOversample = 8f;

		// Token: 0x04000018 RID: 24
		private const int DefaultWebDiskCapMB = 4096;

		// Token: 0x04000019 RID: 25
		private const int DefaultWebIngestConcurrency = 4;

		// Token: 0x0400001A RID: 26
		private const float DefaultWorldCoverSeaSinkM = 2f;

		// Token: 0x0400001B RID: 27
		private const float DefaultWorldCoverSeaSinkMaxM = 10f;

		// Token: 0x0400001C RID: 28
		private const float DefaultSeaFlattenMin = -8f;

		// Token: 0x0400001D RID: 29
		private const float DefaultSeaFlattenMax = 8f;

		// Token: 0x0400001E RID: 30
		private const float DefaultSeaFlattenSlope = 6f;

		// Token: 0x0400001F RID: 31
		private const float DefaultWaterMaskBlurPx = 1.5f;

		// Token: 0x04000020 RID: 32
		private const float DefaultOversample = 1f;

		// Token: 0x04000021 RID: 33
		private const string DefaultWorldCoverUrl = "https://esa-worldcover.s3.amazonaws.com/v200/2021/map";

		// Token: 0x04000022 RID: 34
		private const string DefaultWorldCoverPrefix = "ESA_WorldCover_10m_2021_v200";

		// Token: 0x04000023 RID: 35
		private static readonly int s_VTOversampleID = Shader.PropertyToID("_VTOversample");
	}
}
