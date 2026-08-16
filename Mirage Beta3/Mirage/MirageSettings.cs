using System;
using Mirage.Configuration;
using Mirage.VirtualTexture;
using UnityEngine;

namespace Mirage
{
	/// <summary>Mod-wide settings from the top-level <c>Mirage { }</c> config node.</summary>
	// Token: 0x02000005 RID: 5
	public static class MirageSettings
	{
		// Token: 0x17000001 RID: 1
		// (get) Token: 0x0600000A RID: 10 RVA: 0x00002177 File Offset: 0x00000377
		// (set) Token: 0x0600000B RID: 11 RVA: 0x0000217E File Offset: 0x0000037E
		public static bool WebIngest { get; private set; }

		// Token: 0x17000002 RID: 2
		// (get) Token: 0x0600000C RID: 12 RVA: 0x00002186 File Offset: 0x00000386
		// (set) Token: 0x0600000D RID: 13 RVA: 0x0000218D File Offset: 0x0000038D
		public static bool WebStreaming { get; private set; }

		// Token: 0x17000003 RID: 3
		// (get) Token: 0x0600000E RID: 14 RVA: 0x00002195 File Offset: 0x00000395
		// (set) Token: 0x0600000F RID: 15 RVA: 0x0000219C File Offset: 0x0000039C
		public static bool ScaledWebStreaming { get; private set; }

		// Token: 0x17000004 RID: 4
		// (get) Token: 0x06000010 RID: 16 RVA: 0x000021A4 File Offset: 0x000003A4
		// (set) Token: 0x06000011 RID: 17 RVA: 0x000021AB File Offset: 0x000003AB
		public static int WebDiskCapMB { get; private set; } = 4096;

		// Token: 0x17000005 RID: 5
		// (get) Token: 0x06000012 RID: 18 RVA: 0x000021B3 File Offset: 0x000003B3
		// (set) Token: 0x06000013 RID: 19 RVA: 0x000021BA File Offset: 0x000003BA
		public static int WebIngestConcurrency { get; private set; } = 4;

		// Token: 0x17000006 RID: 6
		// (get) Token: 0x06000014 RID: 20 RVA: 0x000021C2 File Offset: 0x000003C2
		// (set) Token: 0x06000015 RID: 21 RVA: 0x000021C9 File Offset: 0x000003C9
		public static bool WorldCoverWater { get; private set; }

		// Token: 0x17000007 RID: 7
		// (get) Token: 0x06000016 RID: 22 RVA: 0x000021D1 File Offset: 0x000003D1
		// (set) Token: 0x06000017 RID: 23 RVA: 0x000021D8 File Offset: 0x000003D8
		public static string WorldCoverUrl { get; private set; } = "https://esa-worldcover.s3.amazonaws.com/v200/2021/map";

		// Token: 0x17000008 RID: 8
		// (get) Token: 0x06000018 RID: 24 RVA: 0x000021E0 File Offset: 0x000003E0
		// (set) Token: 0x06000019 RID: 25 RVA: 0x000021E7 File Offset: 0x000003E7
		public static string WorldCoverPrefix { get; private set; } = "ESA_WorldCover_10m_2021_v200";

		// Token: 0x17000009 RID: 9
		// (get) Token: 0x0600001A RID: 26 RVA: 0x000021EF File Offset: 0x000003EF
		// (set) Token: 0x0600001B RID: 27 RVA: 0x000021F6 File Offset: 0x000003F6
		public static float WorldCoverSeaSinkM { get; private set; } = 2f;

		// Token: 0x1700000A RID: 10
		// (get) Token: 0x0600001C RID: 28 RVA: 0x000021FE File Offset: 0x000003FE
		// (set) Token: 0x0600001D RID: 29 RVA: 0x00002205 File Offset: 0x00000405
		public static float WorldCoverSeaSinkMaxM { get; private set; } = 10f;

		// Token: 0x1700000B RID: 11
		// (get) Token: 0x0600001E RID: 30 RVA: 0x0000220D File Offset: 0x0000040D
		// (set) Token: 0x0600001F RID: 31 RVA: 0x00002214 File Offset: 0x00000414
		public static float SeaFlattenMin { get; private set; } = -8f;

		// Token: 0x1700000C RID: 12
		// (get) Token: 0x06000020 RID: 32 RVA: 0x0000221C File Offset: 0x0000041C
		// (set) Token: 0x06000021 RID: 33 RVA: 0x00002223 File Offset: 0x00000423
		public static float SeaFlattenMax { get; private set; } = 8f;

		// Token: 0x1700000D RID: 13
		// (get) Token: 0x06000022 RID: 34 RVA: 0x0000222B File Offset: 0x0000042B
		// (set) Token: 0x06000023 RID: 35 RVA: 0x00002232 File Offset: 0x00000432
		public static float SeaFlattenSlope { get; private set; } = 6f;

		// Token: 0x1700000E RID: 14
		// (get) Token: 0x06000024 RID: 36 RVA: 0x0000223A File Offset: 0x0000043A
		// (set) Token: 0x06000025 RID: 37 RVA: 0x00002241 File Offset: 0x00000441
		public static float WaterMaskBlurPx { get; private set; } = 1.5f;

		// Token: 0x1700000F RID: 15
		// (get) Token: 0x06000026 RID: 38 RVA: 0x00002249 File Offset: 0x00000449
		// (set) Token: 0x06000027 RID: 39 RVA: 0x00002250 File Offset: 0x00000450
		public static float Oversample { get; private set; } = 1f;

		// Token: 0x17000010 RID: 16
		// (get) Token: 0x06000028 RID: 40 RVA: 0x00002258 File Offset: 0x00000458
		// (set) Token: 0x06000029 RID: 41 RVA: 0x0000225F File Offset: 0x0000045F
		public static bool ValidateIndirection { get; private set; }

		/// <summary>Read and apply all <c>Mirage { }</c> nodes from the game database.</summary>
		// Token: 0x0600002A RID: 42 RVA: 0x00002268 File Offset: 0x00000468
		internal static void Load()
		{
			MirageSettings.ParsedSettings parsed = MirageSettings.ReadConfig();
			MirageSettings.Apply(parsed);
			MirageSettings.LogSummary();
		}

		/// <summary>Push shader-relevant settings as globals.</summary>
		// Token: 0x0600002B RID: 43 RVA: 0x00002289 File Offset: 0x00000489
		internal static void PushShaderGlobals()
		{
			Shader.SetGlobalFloat(MirageSettings.s_VTOversampleID, MirageSettings.Oversample);
		}

		/// <summary>Fold all <c>Mirage { }</c> nodes into one set of values (last wins).</summary>
		// Token: 0x0600002C RID: 44 RVA: 0x0000229C File Offset: 0x0000049C
		private static MirageSettings.ParsedSettings ReadConfig()
		{
			MirageSettings.ParsedSettings parsed = new MirageSettings.ParsedSettings();
			GameDatabase instance = GameDatabase.Instance;
			UrlDir.UrlConfig[] nodes = (instance != null) ? instance.GetConfigs("Mirage") : null;
			bool flag = nodes == null;
			MirageSettings.ParsedSettings result;
			if (flag)
			{
				result = parsed;
			}
			else
			{
				foreach (UrlDir.UrlConfig url in nodes)
				{
					parsed.ReadFrom(url.config);
				}
				result = parsed;
			}
			return result;
		}

		/// <summary>Apply parsed values, clamping where needed.</summary>
		// Token: 0x0600002D RID: 45 RVA: 0x00002304 File Offset: 0x00000504
		private static void Apply(MirageSettings.ParsedSettings parsed)
		{
			MirageSettings.WebIngest = parsed.WebIngest;
			MirageSettings.WebStreaming = parsed.WebStreaming;
			MirageSettings.ScaledWebStreaming = parsed.ScaledWebStreaming;
			MirageSettings.WebDiskCapMB = Math.Max(1, parsed.WebDiskCapMB);
			MirageSettings.WebIngestConcurrency = Math.Max(1, parsed.WebIngestConcurrency);
			MirageSettings.WorldCoverWater = parsed.WorldCoverWater;
			MirageSettings.WorldCoverUrl = parsed.WorldCoverUrl;
			MirageSettings.WorldCoverPrefix = parsed.WorldCoverPrefix;
			MirageSettings.WorldCoverSeaSinkM = Math.Max(0f, parsed.WorldCoverSeaSinkM);
			MirageSettings.WorldCoverSeaSinkMaxM = Math.Max(0f, parsed.WorldCoverSeaSinkMaxM);
			MirageSettings.SeaFlattenMin = parsed.SeaFlattenMin;
			MirageSettings.SeaFlattenMax = parsed.SeaFlattenMax;
			MirageSettings.SeaFlattenSlope = Math.Max(0f, parsed.SeaFlattenSlope);
			MirageSettings.WaterMaskBlurPx = Math.Max(0f, parsed.WaterMaskBlurPx);
			MirageSettings.Oversample = MirageSettings.ClampOversample(parsed.Oversample);
			MirageSettings.ValidateIndirection = parsed.ValidateIndirection;
			TileStreamingManager.ValidateEveryFrame = MirageSettings.ValidateIndirection;
			MirageSettings.PushShaderGlobals();
		}

		// Token: 0x0600002E RID: 46 RVA: 0x0000241C File Offset: 0x0000061C
		private static float ClampOversample(float requested)
		{
			float clamped = MirageSettings.s_OversampleRange.Clamp(requested);
			bool flag = Math.Abs(clamped - requested) > 0.0001f;
			if (flag)
			{
				MirageDebug.LogError(string.Format("MirageSettings: oversample = {0} is outside {1}; ", requested, MirageSettings.s_OversampleRange) + string.Format("clamped to {0}.", clamped));
			}
			return clamped;
		}

		/// <summary>Log the active configuration.</summary>
		// Token: 0x0600002F RID: 47 RVA: 0x00002484 File Offset: 0x00000684
		private static void LogSummary()
		{
			MirageDebug.Log(string.Concat(new string[]
			{
				string.Format("MirageSettings: webIngest = {0}, webStreaming = {1}, ", MirageSettings.WebIngest, MirageSettings.WebStreaming),
				string.Format("scaledWebStreaming = {0}, webDiskCapMB = {1}, ", MirageSettings.ScaledWebStreaming, MirageSettings.WebDiskCapMB),
				string.Format("webIngestConcurrency = {0}, ", MirageSettings.WebIngestConcurrency),
				string.Format("worldCoverWater = {0}, oversample = {1}, ", MirageSettings.WorldCoverWater, MirageSettings.Oversample),
				string.Format("validateIndirection = {0}", MirageSettings.ValidateIndirection)
			}));
			bool validateIndirection = MirageSettings.ValidateIndirection;
			if (validateIndirection)
			{
				MirageDebug.Log("MirageSettings: VT indirection validation is armed — the checker runs every frame and logs the first violation as '[VT Validate]'. No such line means no CPU-side violation was seen.");
			}
		}

		// Token: 0x04000005 RID: 5
		private const string NodeName = "Mirage";

		// Token: 0x04000006 RID: 6
		private static readonly int s_VTOversampleID = Shader.PropertyToID("_VTOversample");

		// Token: 0x04000007 RID: 7
		private static readonly FloatRange s_OversampleRange = new FloatRange(0.25f, 8f);

		/// <summary>Raw config values before clamping.</summary>
		// Token: 0x0200008D RID: 141
		private sealed class ParsedSettings
		{
			/// <summary>Merge one node in; absent keys keep their current value.</summary>
			// Token: 0x06000407 RID: 1031 RVA: 0x0001C91C File Offset: 0x0001AB1C
			public void ReadFrom(ConfigNode node)
			{
				this.WebIngest = node.ParseBool("webIngest", this.WebIngest);
				this.WebStreaming = node.ParseBool("webStreaming", this.WebStreaming);
				this.ScaledWebStreaming = node.ParseBool("scaledWebStreaming", this.ScaledWebStreaming);
				this.WebDiskCapMB = node.ParseInt("webDiskCapMB", this.WebDiskCapMB);
				this.WebIngestConcurrency = node.ParseInt("webIngestConcurrency", this.WebIngestConcurrency);
				this.WorldCoverWater = node.ParseBool("worldCoverWater", this.WorldCoverWater);
				this.WorldCoverUrl = node.ParseString("worldCoverUrl", this.WorldCoverUrl);
				this.WorldCoverPrefix = node.ParseString("worldCoverPrefix", this.WorldCoverPrefix);
				this.WorldCoverSeaSinkM = node.ParseFloat("worldCoverSeaSinkM", this.WorldCoverSeaSinkM);
				this.WorldCoverSeaSinkMaxM = node.ParseFloat("worldCoverSeaSinkMaxM", this.WorldCoverSeaSinkMaxM);
				this.SeaFlattenMin = node.ParseFloat("seaFlattenMin", this.SeaFlattenMin);
				this.SeaFlattenMax = node.ParseFloat("seaFlattenMax", this.SeaFlattenMax);
				this.SeaFlattenSlope = node.ParseFloat("seaFlattenSlope", this.SeaFlattenSlope);
				this.WaterMaskBlurPx = node.ParseFloat("waterMaskBlurPx", this.WaterMaskBlurPx);
				this.Oversample = node.ParseFloat("oversample", this.Oversample);
				this.ValidateIndirection = node.ParseBool("validateIndirection", this.ValidateIndirection);
			}

			// Token: 0x04000378 RID: 888
			public bool WebIngest;

			// Token: 0x04000379 RID: 889
			public bool WebStreaming;

			// Token: 0x0400037A RID: 890
			public bool ScaledWebStreaming;

			// Token: 0x0400037B RID: 891
			public int WebDiskCapMB = 4096;

			// Token: 0x0400037C RID: 892
			public int WebIngestConcurrency = 4;

			// Token: 0x0400037D RID: 893
			public bool WorldCoverWater;

			// Token: 0x0400037E RID: 894
			public string WorldCoverUrl = "https://esa-worldcover.s3.amazonaws.com/v200/2021/map";

			// Token: 0x0400037F RID: 895
			public string WorldCoverPrefix = "ESA_WorldCover_10m_2021_v200";

			// Token: 0x04000380 RID: 896
			public float WorldCoverSeaSinkM = 2f;

			// Token: 0x04000381 RID: 897
			public float WorldCoverSeaSinkMaxM = 10f;

			// Token: 0x04000382 RID: 898
			public float SeaFlattenMin = -8f;

			// Token: 0x04000383 RID: 899
			public float SeaFlattenMax = 8f;

			// Token: 0x04000384 RID: 900
			public float SeaFlattenSlope = 6f;

			// Token: 0x04000385 RID: 901
			public float WaterMaskBlurPx = 1.5f;

			// Token: 0x04000386 RID: 902
			public float Oversample = 1f;

			// Token: 0x04000387 RID: 903
			public bool ValidateIndirection;
		}

		/// <summary>Fallback values when a key is absent from all nodes.</summary>
		// Token: 0x0200008E RID: 142
		private static class Defaults
		{
			// Token: 0x04000388 RID: 904
			public const int WebDiskCapMB = 4096;

			// Token: 0x04000389 RID: 905
			public const int WebIngestConcurrency = 4;

			// Token: 0x0400038A RID: 906
			public const float WorldCoverSeaSinkM = 2f;

			// Token: 0x0400038B RID: 907
			public const float WorldCoverSeaSinkMaxM = 10f;

			// Token: 0x0400038C RID: 908
			public const float SeaFlattenMin = -8f;

			// Token: 0x0400038D RID: 909
			public const float SeaFlattenMax = 8f;

			// Token: 0x0400038E RID: 910
			public const float SeaFlattenSlope = 6f;

			// Token: 0x0400038F RID: 911
			public const float WaterMaskBlurPx = 1.5f;

			// Token: 0x04000390 RID: 912
			public const float Oversample = 1f;

			// Token: 0x04000391 RID: 913
			public const string WorldCoverUrl = "https://esa-worldcover.s3.amazonaws.com/v200/2021/map";

			// Token: 0x04000392 RID: 914
			public const string WorldCoverPrefix = "ESA_WorldCover_10m_2021_v200";
		}
	}
}
