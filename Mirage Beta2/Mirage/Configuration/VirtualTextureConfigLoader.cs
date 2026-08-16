using System;
using Kopernicus.ConfigParser.Attributes;
using Kopernicus.ConfigParser.BuiltinTypeParsers;
using Mirage.VirtualTexture;

namespace Mirage.Configuration
{
	/// <summary>
	/// Kopernicus ConfigParser target for a <c>VirtualTexture { … }</c> subnode.
	/// Wraps an underlying <see cref="T:Mirage.VirtualTexture.VirtualTextureConfig" /> DTO with parser-bound
	/// properties so Kopernicus's reflection-based loader can populate it
	/// declaratively while the DTO stays clean.
	/// </summary>
	// Token: 0x0200006F RID: 111
	[RequireConfigType(1)]
	public class VirtualTextureConfigLoader
	{
		/// <summary>The populated DTO. Read this after parsing completes.</summary>
		// Token: 0x17000088 RID: 136
		// (get) Token: 0x0600031D RID: 797 RVA: 0x00019AA4 File Offset: 0x00017CA4
		public VirtualTextureConfig Config { get; } = new VirtualTextureConfig();

		// Token: 0x17000089 RID: 137
		// (get) Token: 0x0600031E RID: 798 RVA: 0x00019AAC File Offset: 0x00017CAC
		// (set) Token: 0x0600031F RID: 799 RVA: 0x00019AB9 File Offset: 0x00017CB9
		[ParserTarget("colormapTilePath", Optional = true)]
		public string ColormapTilePath
		{
			get
			{
				return this.Config.colormapTilePath;
			}
			set
			{
				this.Config.colormapTilePath = value;
			}
		}

		// Token: 0x1700008A RID: 138
		// (get) Token: 0x06000320 RID: 800 RVA: 0x00019AC7 File Offset: 0x00017CC7
		// (set) Token: 0x06000321 RID: 801 RVA: 0x00019AD4 File Offset: 0x00017CD4
		[ParserTarget("heightmapTilePath", Optional = true)]
		public string HeightmapTilePath
		{
			get
			{
				return this.Config.heightmapTilePath;
			}
			set
			{
				this.Config.heightmapTilePath = value;
			}
		}

		// Token: 0x1700008B RID: 139
		// (get) Token: 0x06000322 RID: 802 RVA: 0x00019AE2 File Offset: 0x00017CE2
		// (set) Token: 0x06000323 RID: 803 RVA: 0x00019AEF File Offset: 0x00017CEF
		[ParserTarget("normalmapTilePath", Optional = true)]
		public string NormalmapTilePath
		{
			get
			{
				return this.Config.normalmapTilePath;
			}
			set
			{
				this.Config.normalmapTilePath = value;
			}
		}

		// Token: 0x1700008C RID: 140
		// (get) Token: 0x06000324 RID: 804 RVA: 0x00019AFD File Offset: 0x00017CFD
		// (set) Token: 0x06000325 RID: 805 RVA: 0x00019B0A File Offset: 0x00017D0A
		[ParserTarget("archivePath", Optional = true)]
		public string ArchivePath
		{
			get
			{
				return this.Config.archivePath;
			}
			set
			{
				this.Config.archivePath = value;
			}
		}

		// Token: 0x1700008D RID: 141
		// (get) Token: 0x06000326 RID: 806 RVA: 0x00019B18 File Offset: 0x00017D18
		// (set) Token: 0x06000327 RID: 807 RVA: 0x00019B25 File Offset: 0x00017D25
		[ParserTarget("webPath", Optional = true)]
		public string WebPath
		{
			get
			{
				return this.Config.webPath;
			}
			set
			{
				this.Config.webPath = value;
			}
		}

		/// <summary>Deprecated alias for <c>webPath</c>, kept so configs written when the web tier was
		/// color-only keep working. Same directory; it now also holds web.height.* / web.normal.*.</summary>
		// Token: 0x1700008E RID: 142
		// (get) Token: 0x06000328 RID: 808 RVA: 0x00019B33 File Offset: 0x00017D33
		// (set) Token: 0x06000329 RID: 809 RVA: 0x00019B40 File Offset: 0x00017D40
		[ParserTarget("webColorPath", Optional = true)]
		public string WebColorPath
		{
			get
			{
				return this.Config.webPath;
			}
			set
			{
				this.Config.webPath = value;
			}
		}

		// Token: 0x1700008F RID: 143
		// (get) Token: 0x0600032A RID: 810 RVA: 0x00019B4E File Offset: 0x00017D4E
		// (set) Token: 0x0600032B RID: 811 RVA: 0x00019B5B File Offset: 0x00017D5B
		[ParserTarget("imageryProvider", Optional = true)]
		public string ImageryProvider
		{
			get
			{
				return this.Config.imageryProvider;
			}
			set
			{
				this.Config.imageryProvider = value;
			}
		}

		/// <summary>Fill the ocean floor with GMRT bathymetry on coastal tiles (needs webIngest). Off by default:
		/// it adds a GridServer fetch per coastal tile and pulls a third source over the network.</summary>
		// Token: 0x17000090 RID: 144
		// (get) Token: 0x0600032C RID: 812 RVA: 0x00019B69 File Offset: 0x00017D69
		// (set) Token: 0x0600032D RID: 813 RVA: 0x00019B7B File Offset: 0x00017D7B
		[ParserTarget("bathymetry", Optional = true)]
		public NumericParser<bool> Bathymetry
		{
			get
			{
				return this.Config.bathymetry;
			}
			set
			{
				this.Config.bathymetry = value;
			}
		}

		/// <summary>Strip corrupt hot pixels from Terrarium source tiles (on by default; see HeightDespike).</summary>
		// Token: 0x17000091 RID: 145
		// (get) Token: 0x0600032E RID: 814 RVA: 0x00019B8E File Offset: 0x00017D8E
		// (set) Token: 0x0600032F RID: 815 RVA: 0x00019BA0 File Offset: 0x00017DA0
		[ParserTarget("heightDespike", Optional = true)]
		public NumericParser<bool> HeightDespike
		{
			get
			{
				return this.Config.heightDespike;
			}
			set
			{
				this.Config.heightDespike = value;
			}
		}

		/// <summary>Apply the colour grade to web imagery at bake time (on by default). Off bakes the raw imagery.</summary>
		// Token: 0x17000092 RID: 146
		// (get) Token: 0x06000330 RID: 816 RVA: 0x00019BB3 File Offset: 0x00017DB3
		// (set) Token: 0x06000331 RID: 817 RVA: 0x00019BC5 File Offset: 0x00017DC5
		[ParserTarget("colorGrade", Optional = true)]
		public NumericParser<bool> ColorGradeEnabled
		{
			get
			{
				return this.Config.colorGrade;
			}
			set
			{
				this.Config.colorGrade = value;
			}
		}

		// Token: 0x17000093 RID: 147
		// (get) Token: 0x06000332 RID: 818 RVA: 0x00019BD8 File Offset: 0x00017DD8
		// (set) Token: 0x06000333 RID: 819 RVA: 0x00019BEA File Offset: 0x00017DEA
		[ParserTarget("colorExposure", Optional = true)]
		public NumericParser<float> ColorExposure
		{
			get
			{
				return this.Config.colorExposure;
			}
			set
			{
				this.Config.colorExposure = value;
			}
		}

		// Token: 0x17000094 RID: 148
		// (get) Token: 0x06000334 RID: 820 RVA: 0x00019BFD File Offset: 0x00017DFD
		// (set) Token: 0x06000335 RID: 821 RVA: 0x00019C0F File Offset: 0x00017E0F
		[ParserTarget("colorBrightness", Optional = true)]
		public NumericParser<float> ColorBrightness
		{
			get
			{
				return this.Config.colorBrightness;
			}
			set
			{
				this.Config.colorBrightness = value;
			}
		}

		// Token: 0x17000095 RID: 149
		// (get) Token: 0x06000336 RID: 822 RVA: 0x00019C22 File Offset: 0x00017E22
		// (set) Token: 0x06000337 RID: 823 RVA: 0x00019C34 File Offset: 0x00017E34
		[ParserTarget("colorContrast", Optional = true)]
		public NumericParser<float> ColorContrast
		{
			get
			{
				return this.Config.colorContrast;
			}
			set
			{
				this.Config.colorContrast = value;
			}
		}

		// Token: 0x17000096 RID: 150
		// (get) Token: 0x06000338 RID: 824 RVA: 0x00019C47 File Offset: 0x00017E47
		// (set) Token: 0x06000339 RID: 825 RVA: 0x00019C59 File Offset: 0x00017E59
		[ParserTarget("colorSaturation", Optional = true)]
		public NumericParser<float> ColorSaturation
		{
			get
			{
				return this.Config.colorSaturation;
			}
			set
			{
				this.Config.colorSaturation = value;
			}
		}

		// Token: 0x17000097 RID: 151
		// (get) Token: 0x0600033A RID: 826 RVA: 0x00019C6C File Offset: 0x00017E6C
		// (set) Token: 0x0600033B RID: 827 RVA: 0x00019C7E File Offset: 0x00017E7E
		[ParserTarget("colorGamma", Optional = true)]
		public NumericParser<float> ColorGamma
		{
			get
			{
				return this.Config.colorGamma;
			}
			set
			{
				this.Config.colorGamma = value;
			}
		}

		// Token: 0x17000098 RID: 152
		// (get) Token: 0x0600033C RID: 828 RVA: 0x00019C91 File Offset: 0x00017E91
		// (set) Token: 0x0600033D RID: 829 RVA: 0x00019CA3 File Offset: 0x00017EA3
		[ParserTarget("colorTemperature", Optional = true)]
		public NumericParser<float> ColorTemperature
		{
			get
			{
				return this.Config.colorTemperature;
			}
			set
			{
				this.Config.colorTemperature = value;
			}
		}

		// Token: 0x17000099 RID: 153
		// (get) Token: 0x0600033E RID: 830 RVA: 0x00019CB6 File Offset: 0x00017EB6
		// (set) Token: 0x0600033F RID: 831 RVA: 0x00019CC8 File Offset: 0x00017EC8
		[ParserTarget("colorTint", Optional = true)]
		public NumericParser<float> ColorTint
		{
			get
			{
				return this.Config.colorTint;
			}
			set
			{
				this.Config.colorTint = value;
			}
		}

		/// <summary>Bake the colour tile's alpha as a water mask from the height layer (white = water; on by default).</summary>
		// Token: 0x1700009A RID: 154
		// (get) Token: 0x06000340 RID: 832 RVA: 0x00019CDB File Offset: 0x00017EDB
		// (set) Token: 0x06000341 RID: 833 RVA: 0x00019CED File Offset: 0x00017EED
		[ParserTarget("waterMask", Optional = true)]
		public NumericParser<bool> WaterMask
		{
			get
			{
				return this.Config.waterMask;
			}
			set
			{
				this.Config.waterMask = value;
			}
		}

		// Token: 0x1700009B RID: 155
		// (get) Token: 0x06000342 RID: 834 RVA: 0x00019D00 File Offset: 0x00017F00
		// (set) Token: 0x06000343 RID: 835 RVA: 0x00019D12 File Offset: 0x00017F12
		[ParserTarget("atlasSize", Optional = true)]
		public NumericParser<int> AtlasSize
		{
			get
			{
				return this.Config.atlasSize;
			}
			set
			{
				this.Config.atlasSize = value;
			}
		}

		// Token: 0x1700009C RID: 156
		// (get) Token: 0x06000344 RID: 836 RVA: 0x00019D25 File Offset: 0x00017F25
		// (set) Token: 0x06000345 RID: 837 RVA: 0x00019D37 File Offset: 0x00017F37
		[ParserTarget("tileSize", Optional = true)]
		public NumericParser<int> TileSize
		{
			get
			{
				return this.Config.tileSize;
			}
			set
			{
				this.Config.tileSize = value;
			}
		}

		// Token: 0x1700009D RID: 157
		// (get) Token: 0x06000346 RID: 838 RVA: 0x00019D4A File Offset: 0x00017F4A
		// (set) Token: 0x06000347 RID: 839 RVA: 0x00019D5C File Offset: 0x00017F5C
		[ParserTarget("borderPx", Optional = true)]
		public NumericParser<int> BorderPx
		{
			get
			{
				return this.Config.borderPx;
			}
			set
			{
				this.Config.borderPx = value;
			}
		}

		/// <summary>Deepest level web ingest streams to (the overall pyramid depth). Normally set. Omit and it
		/// defaults to <c>canonicalMaxLevel</c> — a canonical-only body with no fine (web) tier.</summary>
		// Token: 0x1700009E RID: 158
		// (get) Token: 0x06000348 RID: 840 RVA: 0x00019D6F File Offset: 0x00017F6F
		// (set) Token: 0x06000349 RID: 841 RVA: 0x00019D81 File Offset: 0x00017F81
		[ParserTarget("webMaxLevel", Optional = true)]
		public NumericParser<int> WebMaxLevel
		{
			get
			{
				return this.Config.webMaxLevel;
			}
			set
			{
				this.Config.webMaxLevel = value;
			}
		}

		/// <summary>Depth of the installed canonical data (the flat-directory tier). Auto-detected from the
		/// archive / loose pyramid when omitted; set it only to override the probe.</summary>
		// Token: 0x1700009F RID: 159
		// (get) Token: 0x0600034A RID: 842 RVA: 0x00019D94 File Offset: 0x00017F94
		// (set) Token: 0x0600034B RID: 843 RVA: 0x00019DA6 File Offset: 0x00017FA6
		[ParserTarget("canonicalMaxLevel", Optional = true)]
		public NumericParser<int> CanonicalMaxLevel
		{
			get
			{
				return this.Config.canonicalMaxLevel;
			}
			set
			{
				this.Config.canonicalMaxLevel = value;
			}
		}

		// Token: 0x170000A0 RID: 160
		// (get) Token: 0x0600034C RID: 844 RVA: 0x00019DB9 File Offset: 0x00017FB9
		// (set) Token: 0x0600034D RID: 845 RVA: 0x00019DCB File Offset: 0x00017FCB
		[ParserTarget("tessellationEdgeLength", Optional = true)]
		public NumericParser<float> TessellationEdgeLength
		{
			get
			{
				return this.Config.tessellationEdgeLength;
			}
			set
			{
				this.Config.tessellationEdgeLength = value;
			}
		}

		// Token: 0x170000A1 RID: 161
		// (get) Token: 0x0600034E RID: 846 RVA: 0x00019DDE File Offset: 0x00017FDE
		// (set) Token: 0x0600034F RID: 847 RVA: 0x00019DF0 File Offset: 0x00017FF0
		[ParserTarget("maxTessellation", Optional = true)]
		public NumericParser<int> MaxTessellation
		{
			get
			{
				return this.Config.maxTessellation;
			}
			set
			{
				this.Config.maxTessellation = value;
			}
		}

		// Token: 0x170000A2 RID: 162
		// (get) Token: 0x06000350 RID: 848 RVA: 0x00019E03 File Offset: 0x00018003
		// (set) Token: 0x06000351 RID: 849 RVA: 0x00019E15 File Offset: 0x00018015
		[ParserTarget("maxTessellationRange", Optional = true)]
		public NumericParser<float> MaxTessellationRange
		{
			get
			{
				return this.Config.maxTessellationRange;
			}
			set
			{
				this.Config.maxTessellationRange = value;
			}
		}
	}
}
