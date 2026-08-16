using System;
using Kopernicus.ConfigParser.Attributes;
using Kopernicus.ConfigParser.BuiltinTypeParsers;
using Kopernicus.Configuration.Attributes;
using Kopernicus.Configuration.MaterialLoader;
using Kopernicus.Configuration.MaterialLoader.Parsing;
using Kopernicus.Configuration.Parsing;
using UnityEngine;

namespace Mirage.Configuration
{
	/// <summary>
	/// Kopernicus material loader for the Mirage <c>Mirage/Parallax</c> terrain shader.
	///
	/// <para>Use it from a Kopernicus <c>Material</c> block — Kopernicus picks the loader
	/// up automatically from the <c>shader</c> key:</para>
	///
	/// <code>
	/// PQS
	/// {
	///     Material
	///     {
	///         shader = Mirage/Parallax
	///
	///         mainTexLow  = MyMod/PluginData/Earth/LowTex.dds
	///         bumpMapLow  = MyMod/PluginData/Earth/LowBump.dds
	///         midHighBlendStart = 4
	///         specularPower = 50
	///         ...
	///     }
	/// }
	/// </code>
	///
	/// <para>Only inspector-visible shader properties are surfaced here. The VT atlas /
	/// page-table / tile-size uniforms (<c>_ColorTileAtlas</c>, <c>_HeightPageTable</c>, …)
	/// are auto-wired at runtime by <c>TileCache.BindToMaterial</c>, and the
	/// <c>_HasNormalVT</c> toggle is set by <c>TileStreamingManager</c> — both live
	/// outside the config surface.</para>
	///
	/// <para>Properties not listed here can still be set ad-hoc using their raw
	/// underscore-prefixed names — the base <see cref="M:Kopernicus.Configuration.MaterialLoader.MaterialLoader.PostApply(ConfigNode)" />
	/// reads any <c>_X = …</c> key in the Material block and routes it through the
	/// shader's reflected property table.</para>
	///
	/// <para><b>Asset-bundle ordering:</b> the bundle that ships the
	/// <c>Mirage/Parallax</c> Shader must be loaded by the time Kopernicus parses
	/// bodies (i.e. before the main menu). If <see cref="M:UnityEngine.Shader.Find(System.String)" />
	/// returns <c>null</c> at loader-construction time, Kopernicus falls back to
	/// <c>Hidden/InternalErrorShader</c> with a warning in the log.</para>
	/// </summary>
	// Token: 0x02000075 RID: 117
	[RequireConfigType(1)]
	[MaterialLoader("Mirage/Parallax")]
	public class ParallaxShaderLoader : PQSMaterialLoader
	{
		// Token: 0x170000D6 RID: 214
		// (get) Token: 0x060003D0 RID: 976 RVA: 0x0001B4DA File Offset: 0x000196DA
		private static Shader Shader
		{
			get
			{
				return (ParallaxShaderLoader.s_Shader != null) ? ParallaxShaderLoader.s_Shader : (ParallaxShaderLoader.s_Shader = Shader.Find("Mirage/Parallax"));
			}
		}

		// Token: 0x170000D7 RID: 215
		// (get) Token: 0x060003D1 RID: 977 RVA: 0x0001B500 File Offset: 0x00019700
		// (set) Token: 0x060003D2 RID: 978 RVA: 0x0001B508 File Offset: 0x00019708
		public override ShaderParser ShaderParser { get; set; } = ParallaxShaderLoader.Shader;

		// Token: 0x060003D3 RID: 979 RVA: 0x0001B511 File Offset: 0x00019711
		public static bool UsesSameShader(Material m)
		{
			return m != null && m.shader != null && m.shader.name == "Mirage/Parallax";
		}

		// Token: 0x170000D8 RID: 216
		// (get) Token: 0x060003D4 RID: 980 RVA: 0x0001B542 File Offset: 0x00019742
		// (set) Token: 0x060003D5 RID: 981 RVA: 0x0001B554 File Offset: 0x00019754
		[ParserTarget("maxTessellation")]
		public NumericParser<float> MaxTessellation
		{
			get
			{
				return base.GetFloat("_MaxTessellation");
			}
			set
			{
				base.SetFloat("_MaxTessellation", value);
			}
		}

		// Token: 0x170000D9 RID: 217
		// (get) Token: 0x060003D6 RID: 982 RVA: 0x0001B568 File Offset: 0x00019768
		// (set) Token: 0x060003D7 RID: 983 RVA: 0x0001B57A File Offset: 0x0001977A
		[ParserTarget("tessellationEdgeLength")]
		public NumericParser<float> TessellationEdgeLength
		{
			get
			{
				return base.GetFloat("_TessellationEdgeLength");
			}
			set
			{
				base.SetFloat("_TessellationEdgeLength", value);
			}
		}

		// Token: 0x170000DA RID: 218
		// (get) Token: 0x060003D8 RID: 984 RVA: 0x0001B58E File Offset: 0x0001978E
		// (set) Token: 0x060003D9 RID: 985 RVA: 0x0001B5A0 File Offset: 0x000197A0
		[ParserTarget("maxTessellationRange")]
		public NumericParser<float> MaxTessellationRange
		{
			get
			{
				return base.GetFloat("_MaxTessellationRange");
			}
			set
			{
				base.SetFloat("_MaxTessellationRange", value);
			}
		}

		// Token: 0x170000DB RID: 219
		// (get) Token: 0x060003DA RID: 986 RVA: 0x0001B5B4 File Offset: 0x000197B4
		// (set) Token: 0x060003DB RID: 987 RVA: 0x0001B5C6 File Offset: 0x000197C6
		[ParserTarget("tileDisplacementRange")]
		public NumericParser<float> TileDisplacementRange
		{
			get
			{
				return base.GetFloat("_TileDisplacementRange");
			}
			set
			{
				base.SetFloat("_TileDisplacementRange", value);
			}
		}

		// Token: 0x170000DC RID: 220
		// (get) Token: 0x060003DC RID: 988 RVA: 0x0001B5DA File Offset: 0x000197DA
		// (set) Token: 0x060003DD RID: 989 RVA: 0x0001B5EC File Offset: 0x000197EC
		[ParserTarget("mainTexLow")]
		public MaterialTextureParser MainTexLow
		{
			get
			{
				return base.GetTextureName("_MainTexLow");
			}
			set
			{
				base.SetTexture("_MainTexLow", value);
			}
		}

		// Token: 0x170000DD RID: 221
		// (get) Token: 0x060003DE RID: 990 RVA: 0x0001B5FB File Offset: 0x000197FB
		// (set) Token: 0x060003DF RID: 991 RVA: 0x0001B60D File Offset: 0x0001980D
		[ParserTarget("bumpMapLow")]
		public MaterialTextureParser BumpMapLow
		{
			get
			{
				return base.GetTextureName("_BumpMapLow");
			}
			set
			{
				base.SetTexture("_BumpMapLow", value);
			}
		}

		// Token: 0x170000DE RID: 222
		// (get) Token: 0x060003E0 RID: 992 RVA: 0x0001B61C File Offset: 0x0001981C
		// (set) Token: 0x060003E1 RID: 993 RVA: 0x0001B62E File Offset: 0x0001982E
		[ParserTarget("mainTexMid")]
		public MaterialTextureParser MainTexMid
		{
			get
			{
				return base.GetTextureName("_MainTexMid");
			}
			set
			{
				base.SetTexture("_MainTexMid", value);
			}
		}

		// Token: 0x170000DF RID: 223
		// (get) Token: 0x060003E2 RID: 994 RVA: 0x0001B63D File Offset: 0x0001983D
		// (set) Token: 0x060003E3 RID: 995 RVA: 0x0001B64F File Offset: 0x0001984F
		[ParserTarget("bumpMapMid")]
		public MaterialTextureParser BumpMapMid
		{
			get
			{
				return base.GetTextureName("_BumpMapMid");
			}
			set
			{
				base.SetTexture("_BumpMapMid", value);
			}
		}

		// Token: 0x170000E0 RID: 224
		// (get) Token: 0x060003E4 RID: 996 RVA: 0x0001B65E File Offset: 0x0001985E
		// (set) Token: 0x060003E5 RID: 997 RVA: 0x0001B670 File Offset: 0x00019870
		[ParserTarget("mainTexHigh")]
		public MaterialTextureParser MainTexHigh
		{
			get
			{
				return base.GetTextureName("_MainTexHigh");
			}
			set
			{
				base.SetTexture("_MainTexHigh", value);
			}
		}

		// Token: 0x170000E1 RID: 225
		// (get) Token: 0x060003E6 RID: 998 RVA: 0x0001B67F File Offset: 0x0001987F
		// (set) Token: 0x060003E7 RID: 999 RVA: 0x0001B691 File Offset: 0x00019891
		[ParserTarget("bumpMapHigh")]
		public MaterialTextureParser BumpMapHigh
		{
			get
			{
				return base.GetTextureName("_BumpMapHigh");
			}
			set
			{
				base.SetTexture("_BumpMapHigh", value);
			}
		}

		// Token: 0x170000E2 RID: 226
		// (get) Token: 0x060003E8 RID: 1000 RVA: 0x0001B6A0 File Offset: 0x000198A0
		// (set) Token: 0x060003E9 RID: 1001 RVA: 0x0001B6B2 File Offset: 0x000198B2
		[ParserTarget("mainTexSteep")]
		public MaterialTextureParser MainTexSteep
		{
			get
			{
				return base.GetTextureName("_MainTexSteep");
			}
			set
			{
				base.SetTexture("_MainTexSteep", value);
			}
		}

		// Token: 0x170000E3 RID: 227
		// (get) Token: 0x060003EA RID: 1002 RVA: 0x0001B6C1 File Offset: 0x000198C1
		// (set) Token: 0x060003EB RID: 1003 RVA: 0x0001B6D3 File Offset: 0x000198D3
		[ParserTarget("bumpMapSteep")]
		public MaterialTextureParser BumpMapSteep
		{
			get
			{
				return base.GetTextureName("_BumpMapSteep");
			}
			set
			{
				base.SetTexture("_BumpMapSteep", value);
			}
		}

		// Token: 0x170000E4 RID: 228
		// (get) Token: 0x060003EC RID: 1004 RVA: 0x0001B6E2 File Offset: 0x000198E2
		// (set) Token: 0x060003ED RID: 1005 RVA: 0x0001B6F4 File Offset: 0x000198F4
		[ParserTarget("influenceMap")]
		public MaterialTextureParser InfluenceMap
		{
			get
			{
				return base.GetTextureName("_InfluenceMap");
			}
			set
			{
				base.SetTexture("_InfluenceMap", value);
			}
		}

		// Token: 0x170000E5 RID: 229
		// (get) Token: 0x060003EE RID: 1006 RVA: 0x0001B703 File Offset: 0x00019903
		// (set) Token: 0x060003EF RID: 1007 RVA: 0x0001B715 File Offset: 0x00019915
		[ParserTarget("displacementMap")]
		public MaterialTextureParser DisplacementMap
		{
			get
			{
				return base.GetTextureName("_DisplacementMap");
			}
			set
			{
				base.SetTexture("_DisplacementMap", value);
			}
		}

		// Token: 0x170000E6 RID: 230
		// (get) Token: 0x060003F0 RID: 1008 RVA: 0x0001B724 File Offset: 0x00019924
		// (set) Token: 0x060003F1 RID: 1009 RVA: 0x0001B736 File Offset: 0x00019936
		[ParserTarget("occlusionMap")]
		public MaterialTextureParser OcclusionMap
		{
			get
			{
				return base.GetTextureName("_OcclusionMap");
			}
			set
			{
				base.SetTexture("_OcclusionMap", value);
			}
		}

		// Token: 0x170000E7 RID: 231
		// (get) Token: 0x060003F2 RID: 1010 RVA: 0x0001B745 File Offset: 0x00019945
		// (set) Token: 0x060003F3 RID: 1011 RVA: 0x0001B757 File Offset: 0x00019957
		[ParserTarget("heightScale")]
		public NumericParser<float> HeightScale
		{
			get
			{
				return base.GetFloat("_HeightScale");
			}
			set
			{
				base.SetFloat("_HeightScale", value);
			}
		}

		// Token: 0x170000E8 RID: 232
		// (get) Token: 0x060003F4 RID: 1012 RVA: 0x0001B76B File Offset: 0x0001996B
		// (set) Token: 0x060003F5 RID: 1013 RVA: 0x0001B77D File Offset: 0x0001997D
		[ParserTarget("heightOffset")]
		public NumericParser<float> HeightOffset
		{
			get
			{
				return base.GetFloat("_HeightOffset");
			}
			set
			{
				base.SetFloat("_HeightOffset", value);
			}
		}

		// Token: 0x170000E9 RID: 233
		// (get) Token: 0x060003F6 RID: 1014 RVA: 0x0001B791 File Offset: 0x00019991
		// (set) Token: 0x060003F7 RID: 1015 RVA: 0x0001B7A3 File Offset: 0x000199A3
		[ParserTarget("nearFieldEnd")]
		public NumericParser<float> NearFieldEnd
		{
			get
			{
				return base.GetFloat("_NearFieldEnd");
			}
			set
			{
				base.SetFloat("_NearFieldEnd", value);
			}
		}

		// Token: 0x170000EA RID: 234
		// (get) Token: 0x060003F8 RID: 1016 RVA: 0x0001B7B7 File Offset: 0x000199B7
		// (set) Token: 0x060003F9 RID: 1017 RVA: 0x0001B7C9 File Offset: 0x000199C9
		[ParserTarget("blendWidth")]
		public NumericParser<float> BlendWidth
		{
			get
			{
				return base.GetFloat("_BlendWidth");
			}
			set
			{
				base.SetFloat("_BlendWidth", value);
			}
		}

		// Token: 0x170000EB RID: 235
		// (get) Token: 0x060003FA RID: 1018 RVA: 0x0001B7DD File Offset: 0x000199DD
		// (set) Token: 0x060003FB RID: 1019 RVA: 0x0001B7EF File Offset: 0x000199EF
		[ParserTarget("tiling")]
		public NumericParser<float> Tiling
		{
			get
			{
				return base.GetFloat("_Tiling");
			}
			set
			{
				base.SetFloat("_Tiling", value);
			}
		}

		// Token: 0x170000EC RID: 236
		// (get) Token: 0x060003FC RID: 1020 RVA: 0x0001B803 File Offset: 0x00019A03
		// (set) Token: 0x060003FD RID: 1021 RVA: 0x0001B815 File Offset: 0x00019A15
		[ParserTarget("displacementScale")]
		public NumericParser<float> DisplacementScale
		{
			get
			{
				return base.GetFloat("_DisplacementScale");
			}
			set
			{
				base.SetFloat("_DisplacementScale", value);
			}
		}

		// Token: 0x170000ED RID: 237
		// (get) Token: 0x060003FE RID: 1022 RVA: 0x0001B829 File Offset: 0x00019A29
		// (set) Token: 0x060003FF RID: 1023 RVA: 0x0001B83B File Offset: 0x00019A3B
		[ParserTarget("displacementOffset")]
		public NumericParser<float> DisplacementOffset
		{
			get
			{
				return base.GetFloat("_DisplacementOffset");
			}
			set
			{
				base.SetFloat("_DisplacementOffset", value);
			}
		}

		// Token: 0x170000EE RID: 238
		// (get) Token: 0x06000400 RID: 1024 RVA: 0x0001B84F File Offset: 0x00019A4F
		// (set) Token: 0x06000401 RID: 1025 RVA: 0x0001B861 File Offset: 0x00019A61
		[ParserTarget("lowMidBlendStart")]
		public NumericParser<float> LowMidBlendStart
		{
			get
			{
				return base.GetFloat("_LowMidBlendStart");
			}
			set
			{
				base.SetFloat("_LowMidBlendStart", value);
			}
		}

		// Token: 0x170000EF RID: 239
		// (get) Token: 0x06000402 RID: 1026 RVA: 0x0001B875 File Offset: 0x00019A75
		// (set) Token: 0x06000403 RID: 1027 RVA: 0x0001B887 File Offset: 0x00019A87
		[ParserTarget("lowMidBlendEnd")]
		public NumericParser<float> LowMidBlendEnd
		{
			get
			{
				return base.GetFloat("_LowMidBlendEnd");
			}
			set
			{
				base.SetFloat("_LowMidBlendEnd", value);
			}
		}

		// Token: 0x170000F0 RID: 240
		// (get) Token: 0x06000404 RID: 1028 RVA: 0x0001B89B File Offset: 0x00019A9B
		// (set) Token: 0x06000405 RID: 1029 RVA: 0x0001B8AD File Offset: 0x00019AAD
		[ParserTarget("midHighBlendStart")]
		public NumericParser<float> MidHighBlendStart
		{
			get
			{
				return base.GetFloat("_MidHighBlendStart");
			}
			set
			{
				base.SetFloat("_MidHighBlendStart", value);
			}
		}

		// Token: 0x170000F1 RID: 241
		// (get) Token: 0x06000406 RID: 1030 RVA: 0x0001B8C1 File Offset: 0x00019AC1
		// (set) Token: 0x06000407 RID: 1031 RVA: 0x0001B8D3 File Offset: 0x00019AD3
		[ParserTarget("midHighBlendEnd")]
		public NumericParser<float> MidHighBlendEnd
		{
			get
			{
				return base.GetFloat("_MidHighBlendEnd");
			}
			set
			{
				base.SetFloat("_MidHighBlendEnd", value);
			}
		}

		// Token: 0x170000F2 RID: 242
		// (get) Token: 0x06000408 RID: 1032 RVA: 0x0001B8E7 File Offset: 0x00019AE7
		// (set) Token: 0x06000409 RID: 1033 RVA: 0x0001B8F9 File Offset: 0x00019AF9
		[ParserTarget("steepPower")]
		public NumericParser<float> SteepPower
		{
			get
			{
				return base.GetFloat("_SteepPower");
			}
			set
			{
				base.SetFloat("_SteepPower", value);
			}
		}

		// Token: 0x170000F3 RID: 243
		// (get) Token: 0x0600040A RID: 1034 RVA: 0x0001B90D File Offset: 0x00019B0D
		// (set) Token: 0x0600040B RID: 1035 RVA: 0x0001B91F File Offset: 0x00019B1F
		[ParserTarget("steepContrast")]
		public NumericParser<float> SteepContrast
		{
			get
			{
				return base.GetFloat("_SteepContrast");
			}
			set
			{
				base.SetFloat("_SteepContrast", value);
			}
		}

		// Token: 0x170000F4 RID: 244
		// (get) Token: 0x0600040C RID: 1036 RVA: 0x0001B933 File Offset: 0x00019B33
		// (set) Token: 0x0600040D RID: 1037 RVA: 0x0001B945 File Offset: 0x00019B45
		[ParserTarget("steepMidpoint")]
		public NumericParser<float> SteepMidpoint
		{
			get
			{
				return base.GetFloat("_SteepMidpoint");
			}
			set
			{
				base.SetFloat("_SteepMidpoint", value);
			}
		}

		// Token: 0x170000F5 RID: 245
		// (get) Token: 0x0600040E RID: 1038 RVA: 0x0001B959 File Offset: 0x00019B59
		// (set) Token: 0x0600040F RID: 1039 RVA: 0x0001B96B File Offset: 0x00019B6B
		[ParserTarget("specularPower")]
		public NumericParser<float> SpecularPower
		{
			get
			{
				return base.GetFloat("_SpecularPower");
			}
			set
			{
				base.SetFloat("_SpecularPower", value);
			}
		}

		// Token: 0x170000F6 RID: 246
		// (get) Token: 0x06000410 RID: 1040 RVA: 0x0001B97F File Offset: 0x00019B7F
		// (set) Token: 0x06000411 RID: 1041 RVA: 0x0001B991 File Offset: 0x00019B91
		[ParserTarget("specularIntensity")]
		public NumericParser<float> SpecularIntensity
		{
			get
			{
				return base.GetFloat("_SpecularIntensity");
			}
			set
			{
				base.SetFloat("_SpecularIntensity", value);
			}
		}

		// Token: 0x170000F7 RID: 247
		// (get) Token: 0x06000412 RID: 1042 RVA: 0x0001B9A5 File Offset: 0x00019BA5
		// (set) Token: 0x06000413 RID: 1043 RVA: 0x0001B9B7 File Offset: 0x00019BB7
		[ParserTarget("fresnelPower")]
		public NumericParser<float> FresnelPower
		{
			get
			{
				return base.GetFloat("_FresnelPower");
			}
			set
			{
				base.SetFloat("_FresnelPower", value);
			}
		}

		// Token: 0x170000F8 RID: 248
		// (get) Token: 0x06000414 RID: 1044 RVA: 0x0001B9CB File Offset: 0x00019BCB
		// (set) Token: 0x06000415 RID: 1045 RVA: 0x0001B9DD File Offset: 0x00019BDD
		[ParserTarget("environmentMapFactor")]
		public NumericParser<float> EnvironmentMapFactor
		{
			get
			{
				return base.GetFloat("_EnvironmentMapFactor");
			}
			set
			{
				base.SetFloat("_EnvironmentMapFactor", value);
			}
		}

		// Token: 0x170000F9 RID: 249
		// (get) Token: 0x06000416 RID: 1046 RVA: 0x0001B9F1 File Offset: 0x00019BF1
		// (set) Token: 0x06000417 RID: 1047 RVA: 0x0001BA03 File Offset: 0x00019C03
		[ParserTarget("refractionIntensity")]
		public NumericParser<float> RefractionIntensity
		{
			get
			{
				return base.GetFloat("_RefractionIntensity");
			}
			set
			{
				base.SetFloat("_RefractionIntensity", value);
			}
		}

		// Token: 0x170000FA RID: 250
		// (get) Token: 0x06000418 RID: 1048 RVA: 0x0001BA17 File Offset: 0x00019C17
		// (set) Token: 0x06000419 RID: 1049 RVA: 0x0001BA29 File Offset: 0x00019C29
		[ParserTarget("hapke")]
		public NumericParser<float> Hapke
		{
			get
			{
				return base.GetFloat("_Hapke");
			}
			set
			{
				base.SetFloat("_Hapke", value);
			}
		}

		// Token: 0x170000FB RID: 251
		// (get) Token: 0x0600041A RID: 1050 RVA: 0x0001BA3D File Offset: 0x00019C3D
		// (set) Token: 0x0600041B RID: 1051 RVA: 0x0001BA4F File Offset: 0x00019C4F
		[ParserTarget("bumpScale")]
		public NumericParser<float> BumpScale
		{
			get
			{
				return base.GetFloat("_BumpScale");
			}
			set
			{
				base.SetFloat("_BumpScale", value);
			}
		}

		// Token: 0x170000FC RID: 252
		// (get) Token: 0x0600041C RID: 1052 RVA: 0x0001BA63 File Offset: 0x00019C63
		// (set) Token: 0x0600041D RID: 1053 RVA: 0x0001BA75 File Offset: 0x00019C75
		[ParserTarget("emissionColor")]
		public ColorParser EmissionColor
		{
			get
			{
				return base.GetColor("_EmissionColor");
			}
			set
			{
				base.SetColor("_EmissionColor", value);
			}
		}

		// Token: 0x0600041E RID: 1054 RVA: 0x0001BA8C File Offset: 0x00019C8C
		public override void PostApply(ConfigNode node)
		{
			base.PostApply(node);
			foreach (string keyword in node.GetValues("keyword"))
			{
				base.SetKeyword(keyword, true);
			}
		}

		// Token: 0x0600041F RID: 1055 RVA: 0x0001BACA File Offset: 0x00019CCA
		public ParallaxShaderLoader()
		{
		}

		// Token: 0x06000420 RID: 1056 RVA: 0x0001BAE4 File Offset: 0x00019CE4
		public ParallaxShaderLoader(Material material)
		{
			base.Value = material;
		}

		// Token: 0x040002E0 RID: 736
		public const string SHADER_NAME = "Mirage/Parallax";

		// Token: 0x040002E1 RID: 737
		private static Shader s_Shader;
	}
}
