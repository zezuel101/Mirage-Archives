using System;
using System.Collections.Generic;
using Kopernicus.ConfigParser.Attributes;
using Kopernicus.ConfigParser.BuiltinTypeParsers;
using Kopernicus.Configuration.Attributes;
using Kopernicus.Configuration.MaterialLoader;
using Kopernicus.Configuration.Parsing;
using KSPTextureLoader;
using UnityEngine;

namespace Mirage.Configuration
{
	/// <summary>
	/// Kopernicus material loader for the splatmap terrain shader <c>Mirage/PQS</c>.
	///
	/// <para>Unlike <see cref="T:Mirage.Configuration.ParallaxShaderLoader" /> (altitude-band texture sets), this shader blends
	/// five surface layers (R,G,B,A,Steep) packed into DDS <c>Texture2DArray</c>s — one sampler each — and
	/// weights the four base layers with an equirectangular splatmap. Config:</para>
	///
	/// <code>
	/// PQS
	/// {
	///     Material
	///     {
	///         shader = Mirage/PQS
	///
	///         splatmap          = MyMod/PluginData/Earth/Splat.dds        // equirect RGBA weights
	///         albedoArray       = MyMod/PluginData/Earth/Albedo.dds       // 5-layer DDS array (R,G,B,A,S)
	///         normalArray       = MyMod/PluginData/Earth/Normal.dds
	///         maskArray         = MyMod/PluginData/Earth/Mask.dds   // BC5 RG: R=height, G=influence
	///         tiling = 0.05
	///         keyword = INFLUENCE_MAPPING
	///         ...
	///     }
	/// }
	/// </code>
	///
	/// <para>The arrays are loaded explicitly via <see cref="M:KSPTextureLoader.TextureLoader.LoadTexture``1(System.String,KSPTextureLoader.TextureLoadOptions)" />
	/// (Kopernicus' built-in texture parser only handles <c>Texture2D</c>) and bound with
	/// <c>Material.SetTexture</c>. Their handles are rooted for the session so the GPU textures stay
	/// resident. The VT atlas / page-table uniforms are still wired at runtime by
	/// <c>TileCache.BindToMaterial</c>.</para>
	/// </summary>
	// Token: 0x02000072 RID: 114
	[RequireConfigType(1)]
	[MaterialLoader("Mirage/PQS")]
	public class MiragePqsShaderLoader : PQSMaterialLoader
	{
		// Token: 0x170000A5 RID: 165
		// (get) Token: 0x06000360 RID: 864 RVA: 0x0001A5AE File Offset: 0x000187AE
		private static Shader Shader
		{
			get
			{
				return (MiragePqsShaderLoader.s_Shader != null) ? MiragePqsShaderLoader.s_Shader : (MiragePqsShaderLoader.s_Shader = Shader.Find("Mirage/PQS"));
			}
		}

		// Token: 0x170000A6 RID: 166
		// (get) Token: 0x06000361 RID: 865 RVA: 0x0001A5D4 File Offset: 0x000187D4
		// (set) Token: 0x06000362 RID: 866 RVA: 0x0001A5DC File Offset: 0x000187DC
		public override ShaderParser ShaderParser { get; set; } = MiragePqsShaderLoader.Shader;

		// Token: 0x06000363 RID: 867 RVA: 0x0001A5E5 File Offset: 0x000187E5
		public static bool UsesSameShader(Material m)
		{
			return m != null && m.shader != null && m.shader.name == "Mirage/PQS";
		}

		// Token: 0x170000A7 RID: 167
		// (get) Token: 0x06000364 RID: 868 RVA: 0x0001A616 File Offset: 0x00018816
		// (set) Token: 0x06000365 RID: 869 RVA: 0x0001A628 File Offset: 0x00018828
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

		// Token: 0x170000A8 RID: 168
		// (get) Token: 0x06000366 RID: 870 RVA: 0x0001A63C File Offset: 0x0001883C
		// (set) Token: 0x06000367 RID: 871 RVA: 0x0001A64E File Offset: 0x0001884E
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

		// Token: 0x170000A9 RID: 169
		// (get) Token: 0x06000368 RID: 872 RVA: 0x0001A662 File Offset: 0x00018862
		// (set) Token: 0x06000369 RID: 873 RVA: 0x0001A674 File Offset: 0x00018874
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

		// Token: 0x170000AA RID: 170
		// (get) Token: 0x0600036A RID: 874 RVA: 0x0001A688 File Offset: 0x00018888
		// (set) Token: 0x0600036B RID: 875 RVA: 0x0001A69A File Offset: 0x0001889A
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

		// Token: 0x170000AB RID: 171
		// (get) Token: 0x0600036C RID: 876 RVA: 0x0001A6AE File Offset: 0x000188AE
		// (set) Token: 0x0600036D RID: 877 RVA: 0x0001A6C0 File Offset: 0x000188C0
		[ParserTarget("maxAniso")]
		public NumericParser<float> MaxAniso
		{
			get
			{
				return base.GetFloat("_MaxAniso");
			}
			set
			{
				base.SetFloat("_MaxAniso", value);
			}
		}

		// Token: 0x170000AC RID: 172
		// (get) Token: 0x0600036E RID: 878 RVA: 0x0001A6D4 File Offset: 0x000188D4
		// (set) Token: 0x0600036F RID: 879 RVA: 0x0001A6E6 File Offset: 0x000188E6
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

		// Token: 0x170000AD RID: 173
		// (get) Token: 0x06000370 RID: 880 RVA: 0x0001A6FA File Offset: 0x000188FA
		// (set) Token: 0x06000371 RID: 881 RVA: 0x0001A70C File Offset: 0x0001890C
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

		// Token: 0x170000AE RID: 174
		// (get) Token: 0x06000372 RID: 882 RVA: 0x0001A720 File Offset: 0x00018920
		// (set) Token: 0x06000373 RID: 883 RVA: 0x0001A732 File Offset: 0x00018932
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

		// Token: 0x170000AF RID: 175
		// (get) Token: 0x06000374 RID: 884 RVA: 0x0001A746 File Offset: 0x00018946
		// (set) Token: 0x06000375 RID: 885 RVA: 0x0001A758 File Offset: 0x00018958
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

		// Token: 0x170000B0 RID: 176
		// (get) Token: 0x06000376 RID: 886 RVA: 0x0001A76C File Offset: 0x0001896C
		// (set) Token: 0x06000377 RID: 887 RVA: 0x0001A77E File Offset: 0x0001897E
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

		// Token: 0x170000B1 RID: 177
		// (get) Token: 0x06000378 RID: 888 RVA: 0x0001A792 File Offset: 0x00018992
		// (set) Token: 0x06000379 RID: 889 RVA: 0x0001A7A4 File Offset: 0x000189A4
		[ParserTarget("zoomBlendFrequency")]
		public NumericParser<float> ZoomBlendFrequency
		{
			get
			{
				return base.GetFloat("_ZoomBlendFrequency");
			}
			set
			{
				base.SetFloat("_ZoomBlendFrequency", value);
			}
		}

		// Token: 0x170000B2 RID: 178
		// (get) Token: 0x0600037A RID: 890 RVA: 0x0001A7B8 File Offset: 0x000189B8
		// (set) Token: 0x0600037B RID: 891 RVA: 0x0001A7CA File Offset: 0x000189CA
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

		// Token: 0x170000B3 RID: 179
		// (get) Token: 0x0600037C RID: 892 RVA: 0x0001A7DE File Offset: 0x000189DE
		// (set) Token: 0x0600037D RID: 893 RVA: 0x0001A7F0 File Offset: 0x000189F0
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

		// Token: 0x170000B4 RID: 180
		// (get) Token: 0x0600037E RID: 894 RVA: 0x0001A804 File Offset: 0x00018A04
		// (set) Token: 0x0600037F RID: 895 RVA: 0x0001A816 File Offset: 0x00018A16
		[ParserTarget("heightBlendDepth")]
		public NumericParser<float> HeightBlendDepth
		{
			get
			{
				return base.GetFloat("_HeightBlendDepth");
			}
			set
			{
				base.SetFloat("_HeightBlendDepth", value);
			}
		}

		// Token: 0x170000B5 RID: 181
		// (get) Token: 0x06000380 RID: 896 RVA: 0x0001A82A File Offset: 0x00018A2A
		// (set) Token: 0x06000381 RID: 897 RVA: 0x0001A83C File Offset: 0x00018A3C
		[ParserTarget("layerCutoff")]
		public NumericParser<float> LayerCutoff
		{
			get
			{
				return base.GetFloat("_LayerCutoff");
			}
			set
			{
				base.SetFloat("_LayerCutoff", value);
			}
		}

		// Token: 0x170000B6 RID: 182
		// (get) Token: 0x06000382 RID: 898 RVA: 0x0001A850 File Offset: 0x00018A50
		// (set) Token: 0x06000383 RID: 899 RVA: 0x0001A862 File Offset: 0x00018A62
		[ParserTarget("detailCullFootprint")]
		public NumericParser<float> DetailCullFootprint
		{
			get
			{
				return base.GetFloat("_DetailCullFootprint");
			}
			set
			{
				base.SetFloat("_DetailCullFootprint", value);
			}
		}

		// Token: 0x170000B7 RID: 183
		// (get) Token: 0x06000384 RID: 900 RVA: 0x0001A876 File Offset: 0x00018A76
		// (set) Token: 0x06000385 RID: 901 RVA: 0x0001A888 File Offset: 0x00018A88
		[ParserTarget("stochasticContrast")]
		public NumericParser<float> StochasticContrast
		{
			get
			{
				return base.GetFloat("_StochasticContrast");
			}
			set
			{
				base.SetFloat("_StochasticContrast", value);
			}
		}

		// Token: 0x170000B8 RID: 184
		// (get) Token: 0x06000386 RID: 902 RVA: 0x0001A89C File Offset: 0x00018A9C
		// (set) Token: 0x06000387 RID: 903 RVA: 0x0001A8AE File Offset: 0x00018AAE
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

		// Token: 0x170000B9 RID: 185
		// (get) Token: 0x06000388 RID: 904 RVA: 0x0001A8C2 File Offset: 0x00018AC2
		// (set) Token: 0x06000389 RID: 905 RVA: 0x0001A8D4 File Offset: 0x00018AD4
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

		// Token: 0x170000BA RID: 186
		// (get) Token: 0x0600038A RID: 906 RVA: 0x0001A8E8 File Offset: 0x00018AE8
		// (set) Token: 0x0600038B RID: 907 RVA: 0x0001A8FA File Offset: 0x00018AFA
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

		// Token: 0x170000BB RID: 187
		// (get) Token: 0x0600038C RID: 908 RVA: 0x0001A90E File Offset: 0x00018B0E
		// (set) Token: 0x0600038D RID: 909 RVA: 0x0001A920 File Offset: 0x00018B20
		[ParserTarget("specularF0")]
		public NumericParser<float> SpecularF0
		{
			get
			{
				return base.GetFloat("_SpecularF0");
			}
			set
			{
				base.SetFloat("_SpecularF0", value);
			}
		}

		// Token: 0x170000BC RID: 188
		// (get) Token: 0x0600038E RID: 910 RVA: 0x0001A934 File Offset: 0x00018B34
		// (set) Token: 0x0600038F RID: 911 RVA: 0x0001A946 File Offset: 0x00018B46
		[ParserTarget("roughnessScale")]
		public NumericParser<float> RoughnessScale
		{
			get
			{
				return base.GetFloat("_RoughnessScale");
			}
			set
			{
				base.SetFloat("_RoughnessScale", value);
			}
		}

		// Token: 0x170000BD RID: 189
		// (get) Token: 0x06000390 RID: 912 RVA: 0x0001A95A File Offset: 0x00018B5A
		// (set) Token: 0x06000391 RID: 913 RVA: 0x0001A96C File Offset: 0x00018B6C
		[ParserTarget("roughnessInvert")]
		public NumericParser<float> RoughnessInvert
		{
			get
			{
				return base.GetFloat("_RoughnessInvert");
			}
			set
			{
				base.SetFloat("_RoughnessInvert", value);
			}
		}

		// Token: 0x170000BE RID: 190
		// (get) Token: 0x06000392 RID: 914 RVA: 0x0001A980 File Offset: 0x00018B80
		// (set) Token: 0x06000393 RID: 915 RVA: 0x0001A992 File Offset: 0x00018B92
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

		// Token: 0x170000BF RID: 191
		// (get) Token: 0x06000394 RID: 916 RVA: 0x0001A9A6 File Offset: 0x00018BA6
		// (set) Token: 0x06000395 RID: 917 RVA: 0x0001A9B8 File Offset: 0x00018BB8
		[ParserTarget("windSpeed")]
		public NumericParser<float> WindSpeed
		{
			get
			{
				return base.GetFloat("_WindSpeed");
			}
			set
			{
				base.SetFloat("_WindSpeed", value);
			}
		}

		// Token: 0x170000C0 RID: 192
		// (get) Token: 0x06000396 RID: 918 RVA: 0x0001A9CC File Offset: 0x00018BCC
		// (set) Token: 0x06000397 RID: 919 RVA: 0x0001A9DE File Offset: 0x00018BDE
		[ParserTarget("fresnelF0")]
		public NumericParser<float> FresnelF0
		{
			get
			{
				return base.GetFloat("_FresnelF0");
			}
			set
			{
				base.SetFloat("_FresnelF0", value);
			}
		}

		// Token: 0x170000C1 RID: 193
		// (get) Token: 0x06000398 RID: 920 RVA: 0x0001A9F2 File Offset: 0x00018BF2
		// (set) Token: 0x06000399 RID: 921 RVA: 0x0001AA04 File Offset: 0x00018C04
		[ParserTarget("specularMax")]
		public NumericParser<float> SpecularMax
		{
			get
			{
				return base.GetFloat("_SpecularMax");
			}
			set
			{
				base.SetFloat("_SpecularMax", value);
			}
		}

		// Token: 0x170000C2 RID: 194
		// (get) Token: 0x0600039A RID: 922 RVA: 0x0001AA18 File Offset: 0x00018C18
		// (set) Token: 0x0600039B RID: 923 RVA: 0x0001AA2A File Offset: 0x00018C2A
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

		// Token: 0x170000C3 RID: 195
		// (get) Token: 0x0600039C RID: 924 RVA: 0x0001AA3E File Offset: 0x00018C3E
		// (set) Token: 0x0600039D RID: 925 RVA: 0x0001AA50 File Offset: 0x00018C50
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

		// Token: 0x0600039E RID: 926 RVA: 0x0001AA64 File Offset: 0x00018C64
		public override void PostApply(ConfigNode node)
		{
			base.PostApply(node);
			this.LoadTexture2D(node, "splatmap", "_Splatmap", true);
			this.LoadTexture2DArray(node, "albedoArray", "_AlbedoArray", false);
			this.LoadTexture2DArray(node, "normalArray", "_NormalArray", true);
			this.LoadTexture2DArray(node, "maskArray", "_MaskArray", true);
			foreach (string keyword in node.GetValues("keyword"))
			{
				base.SetKeyword(keyword, true);
			}
		}

		// Token: 0x0600039F RID: 927 RVA: 0x0001AAF0 File Offset: 0x00018CF0
		private void LoadTexture2DArray(ConfigNode node, string key, string property, bool linear)
		{
			string path = node.GetValue(key);
			bool flag = string.IsNullOrEmpty(path);
			if (!flag)
			{
				try
				{
					TextureLoadOptions textureLoadOptions = new TextureLoadOptions();
					textureLoadOptions.Linear = new bool?(linear);
					textureLoadOptions.Unreadable = true;
					TextureLoadOptions options = textureLoadOptions;
					TextureHandle<Texture2DArray> handle = TextureLoader.LoadTexture<Texture2DArray>(path, options);
					Texture2DArray tex = handle.GetTexture();
					base.Value.SetTexture(property, tex);
					MiragePqsShaderLoader.s_RootedHandles.Add(handle);
				}
				catch (Exception e)
				{
					MirageDebug.LogError(string.Concat(new string[]
					{
						"MiragePqsShaderLoader: failed to load array '",
						path,
						"' for ",
						property,
						": ",
						e.Message
					}));
				}
			}
		}

		// Token: 0x060003A0 RID: 928 RVA: 0x0001ABB8 File Offset: 0x00018DB8
		private void LoadTexture2D(ConfigNode node, string key, string property, bool linear)
		{
			string path = node.GetValue(key);
			bool flag = string.IsNullOrEmpty(path);
			if (!flag)
			{
				try
				{
					TextureLoadOptions textureLoadOptions = new TextureLoadOptions();
					textureLoadOptions.Linear = new bool?(linear);
					textureLoadOptions.Unreadable = true;
					TextureLoadOptions options = textureLoadOptions;
					TextureHandle<Texture2D> handle = TextureLoader.LoadTexture<Texture2D>(path, options);
					Texture2D tex = handle.GetTexture();
					base.Value.SetTexture(property, tex);
					MiragePqsShaderLoader.s_RootedHandles.Add(handle);
				}
				catch (Exception e)
				{
					MirageDebug.LogError(string.Concat(new string[]
					{
						"MiragePqsShaderLoader: failed to load texture '",
						path,
						"' for ",
						property,
						": ",
						e.Message
					}));
				}
			}
		}

		// Token: 0x060003A1 RID: 929 RVA: 0x0001AC80 File Offset: 0x00018E80
		public MiragePqsShaderLoader()
		{
		}

		// Token: 0x060003A2 RID: 930 RVA: 0x0001AC9A File Offset: 0x00018E9A
		public MiragePqsShaderLoader(Material material)
		{
			base.Value = material;
		}

		// Token: 0x040002D0 RID: 720
		public const string SHADER_NAME = "Mirage/PQS";

		// Token: 0x040002D1 RID: 721
		private static Shader s_Shader;

		// Token: 0x040002D3 RID: 723
		private static readonly List<object> s_RootedHandles = new List<object>();
	}
}
