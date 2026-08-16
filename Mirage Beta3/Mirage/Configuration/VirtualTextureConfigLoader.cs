using System;
using Kopernicus.ConfigParser.Attributes;
using Kopernicus.ConfigParser.BuiltinTypeParsers;
using Mirage.VirtualTexture;

namespace Mirage.Configuration
{
	/// <summary>ConfigParser target for <c>VirtualTexture { }</c>, forwarding to <see cref="T:Mirage.VirtualTexture.VirtualTextureConfig" />.</summary>
	// Token: 0x02000087 RID: 135
	[RequireConfigType(1)]
	public class VirtualTextureConfigLoader
	{
		// Token: 0x17000097 RID: 151
		// (get) Token: 0x060003CF RID: 975 RVA: 0x0001C55A File Offset: 0x0001A75A
		public VirtualTextureConfig Config { get; } = new VirtualTextureConfig();

		// Token: 0x17000098 RID: 152
		// (get) Token: 0x060003D0 RID: 976 RVA: 0x0001C562 File Offset: 0x0001A762
		// (set) Token: 0x060003D1 RID: 977 RVA: 0x0001C56F File Offset: 0x0001A76F
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

		// Token: 0x17000099 RID: 153
		// (get) Token: 0x060003D2 RID: 978 RVA: 0x0001C57D File Offset: 0x0001A77D
		// (set) Token: 0x060003D3 RID: 979 RVA: 0x0001C58A File Offset: 0x0001A78A
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

		// Token: 0x1700009A RID: 154
		// (get) Token: 0x060003D4 RID: 980 RVA: 0x0001C598 File Offset: 0x0001A798
		// (set) Token: 0x060003D5 RID: 981 RVA: 0x0001C5AA File Offset: 0x0001A7AA
		[ParserTarget("emissiveLayer", Optional = true)]
		public NumericParser<bool> EmissiveLayer
		{
			get
			{
				return this.Config.emissiveLayer;
			}
			set
			{
				this.Config.emissiveLayer = value;
			}
		}

		// Token: 0x1700009B RID: 155
		// (get) Token: 0x060003D6 RID: 982 RVA: 0x0001C5BD File Offset: 0x0001A7BD
		// (set) Token: 0x060003D7 RID: 983 RVA: 0x0001C5CA File Offset: 0x0001A7CA
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

		// Token: 0x1700009C RID: 156
		// (get) Token: 0x060003D8 RID: 984 RVA: 0x0001C5D8 File Offset: 0x0001A7D8
		// (set) Token: 0x060003D9 RID: 985 RVA: 0x0001C5EA File Offset: 0x0001A7EA
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

		// Token: 0x1700009D RID: 157
		// (get) Token: 0x060003DA RID: 986 RVA: 0x0001C5FD File Offset: 0x0001A7FD
		// (set) Token: 0x060003DB RID: 987 RVA: 0x0001C60F File Offset: 0x0001A80F
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

		// Token: 0x1700009E RID: 158
		// (get) Token: 0x060003DC RID: 988 RVA: 0x0001C622 File Offset: 0x0001A822
		// (set) Token: 0x060003DD RID: 989 RVA: 0x0001C634 File Offset: 0x0001A834
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

		// Token: 0x1700009F RID: 159
		// (get) Token: 0x060003DE RID: 990 RVA: 0x0001C647 File Offset: 0x0001A847
		// (set) Token: 0x060003DF RID: 991 RVA: 0x0001C659 File Offset: 0x0001A859
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

		// Token: 0x170000A0 RID: 160
		// (get) Token: 0x060003E0 RID: 992 RVA: 0x0001C66C File Offset: 0x0001A86C
		// (set) Token: 0x060003E1 RID: 993 RVA: 0x0001C67E File Offset: 0x0001A87E
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

		// Token: 0x170000A1 RID: 161
		// (get) Token: 0x060003E2 RID: 994 RVA: 0x0001C691 File Offset: 0x0001A891
		// (set) Token: 0x060003E3 RID: 995 RVA: 0x0001C6A3 File Offset: 0x0001A8A3
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

		// Token: 0x170000A2 RID: 162
		// (get) Token: 0x060003E4 RID: 996 RVA: 0x0001C6B6 File Offset: 0x0001A8B6
		// (set) Token: 0x060003E5 RID: 997 RVA: 0x0001C6C8 File Offset: 0x0001A8C8
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

		// Token: 0x170000A3 RID: 163
		// (get) Token: 0x060003E6 RID: 998 RVA: 0x0001C6DB File Offset: 0x0001A8DB
		// (set) Token: 0x060003E7 RID: 999 RVA: 0x0001C6ED File Offset: 0x0001A8ED
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

		// Token: 0x170000A4 RID: 164
		// (get) Token: 0x060003E8 RID: 1000 RVA: 0x0001C700 File Offset: 0x0001A900
		// (set) Token: 0x060003E9 RID: 1001 RVA: 0x0001C712 File Offset: 0x0001A912
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

		// Token: 0x170000A5 RID: 165
		// (get) Token: 0x060003EA RID: 1002 RVA: 0x0001C725 File Offset: 0x0001A925
		// (set) Token: 0x060003EB RID: 1003 RVA: 0x0001C737 File Offset: 0x0001A937
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

		// Token: 0x170000A6 RID: 166
		// (get) Token: 0x060003EC RID: 1004 RVA: 0x0001C74A File Offset: 0x0001A94A
		// (set) Token: 0x060003ED RID: 1005 RVA: 0x0001C75C File Offset: 0x0001A95C
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

		// Token: 0x170000A7 RID: 167
		// (get) Token: 0x060003EE RID: 1006 RVA: 0x0001C76F File Offset: 0x0001A96F
		// (set) Token: 0x060003EF RID: 1007 RVA: 0x0001C781 File Offset: 0x0001A981
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

		// Token: 0x170000A8 RID: 168
		// (get) Token: 0x060003F0 RID: 1008 RVA: 0x0001C794 File Offset: 0x0001A994
		// (set) Token: 0x060003F1 RID: 1009 RVA: 0x0001C7A6 File Offset: 0x0001A9A6
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

		// Token: 0x170000A9 RID: 169
		// (get) Token: 0x060003F2 RID: 1010 RVA: 0x0001C7B9 File Offset: 0x0001A9B9
		// (set) Token: 0x060003F3 RID: 1011 RVA: 0x0001C7CB File Offset: 0x0001A9CB
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

		// Token: 0x170000AA RID: 170
		// (get) Token: 0x060003F4 RID: 1012 RVA: 0x0001C7DE File Offset: 0x0001A9DE
		// (set) Token: 0x060003F5 RID: 1013 RVA: 0x0001C7F0 File Offset: 0x0001A9F0
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

		// Token: 0x170000AB RID: 171
		// (get) Token: 0x060003F6 RID: 1014 RVA: 0x0001C803 File Offset: 0x0001AA03
		// (set) Token: 0x060003F7 RID: 1015 RVA: 0x0001C815 File Offset: 0x0001AA15
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

		// Token: 0x170000AC RID: 172
		// (get) Token: 0x060003F8 RID: 1016 RVA: 0x0001C828 File Offset: 0x0001AA28
		// (set) Token: 0x060003F9 RID: 1017 RVA: 0x0001C83A File Offset: 0x0001AA3A
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

		// Token: 0x170000AD RID: 173
		// (get) Token: 0x060003FA RID: 1018 RVA: 0x0001C84D File Offset: 0x0001AA4D
		// (set) Token: 0x060003FB RID: 1019 RVA: 0x0001C85F File Offset: 0x0001AA5F
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

		// Token: 0x170000AE RID: 174
		// (get) Token: 0x060003FC RID: 1020 RVA: 0x0001C872 File Offset: 0x0001AA72
		// (set) Token: 0x060003FD RID: 1021 RVA: 0x0001C884 File Offset: 0x0001AA84
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
