using System;
using Kopernicus.ConfigParser.Attributes;
using Kopernicus.ConfigParser.BuiltinTypeParsers;
using Kopernicus.Configuration.Attributes;
using Kopernicus.Configuration.MaterialLoader;
using Kopernicus.Configuration.MaterialLoader.Parsing;
using UnityEngine;

namespace Mirage.Configuration
{
	/// <summary>
	/// Kopernicus material loader for the <c>Mirage/Scaled</c> scaled-space shader. Kopernicus picks
	/// this up from a <c>scaledVersion { Material { shader = Mirage/Scaled … } }</c> block, creates the
	/// material (loading the scattering/surge maps on-demand), and assigns it to the body's scaled mesh.
	/// Mirage's runtime then binds the VT atlas/page-table uniforms to that material
	/// (<see cref="M:Mirage.VirtualTexture.TileCache.BindToMaterial(UnityEngine.Material)" />) — those are not part of the config
	/// surface.
	///
	/// <code>
	/// scaledVersion
	/// {
	///     Material
	///     {
	///         shader = Mirage/Scaled
	///         scatteringTex = MyMod/PluginData/Earth/Scattering.dds
	///         surgeTex      = MyMod/PluginData/Earth/Surge.dds
	///         hapke = 1
	///         theta = 15
	///         lightBoost = 1
	///         ...
	///     }
	/// }
	/// </code>
	///
	/// Any property not surfaced here can still be set ad-hoc via its raw underscore name (the base
	/// loader routes <c>_X = …</c> keys through the shader's reflected property table and handles
	/// on-demand texture loading).
	/// </summary>
	// Token: 0x02000073 RID: 115
	[RequireConfigType(1)]
	[MaterialLoader("Mirage/Scaled")]
	public class MirageScaledShaderLoader : CustomMaterialLoader
	{
		// Token: 0x060003A4 RID: 932 RVA: 0x0001ACC7 File Offset: 0x00018EC7
		public static bool UsesSameShader(Material m)
		{
			return m != null && m.shader != null && m.shader.name == "Mirage/Scaled";
		}

		// Token: 0x170000C4 RID: 196
		// (get) Token: 0x060003A5 RID: 933 RVA: 0x0001ACF8 File Offset: 0x00018EF8
		// (set) Token: 0x060003A6 RID: 934 RVA: 0x0001AD0A File Offset: 0x00018F0A
		[ParserTarget("scatteringTex")]
		public MaterialTextureParser ScatteringTex
		{
			get
			{
				return base.GetTextureName("_ScatteringTex");
			}
			set
			{
				base.SetTexture("_ScatteringTex", value);
			}
		}

		// Token: 0x170000C5 RID: 197
		// (get) Token: 0x060003A7 RID: 935 RVA: 0x0001AD19 File Offset: 0x00018F19
		// (set) Token: 0x060003A8 RID: 936 RVA: 0x0001AD2B File Offset: 0x00018F2B
		[ParserTarget("surgeTex")]
		public MaterialTextureParser SurgeTex
		{
			get
			{
				return base.GetTextureName("_SurgeTex");
			}
			set
			{
				base.SetTexture("_SurgeTex", value);
			}
		}

		// Token: 0x170000C6 RID: 198
		// (get) Token: 0x060003A9 RID: 937 RVA: 0x0001AD3A File Offset: 0x00018F3A
		// (set) Token: 0x060003AA RID: 938 RVA: 0x0001AD4C File Offset: 0x00018F4C
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

		// Token: 0x170000C7 RID: 199
		// (get) Token: 0x060003AB RID: 939 RVA: 0x0001AD60 File Offset: 0x00018F60
		// (set) Token: 0x060003AC RID: 940 RVA: 0x0001AD72 File Offset: 0x00018F72
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

		// Token: 0x170000C8 RID: 200
		// (get) Token: 0x060003AD RID: 941 RVA: 0x0001AD86 File Offset: 0x00018F86
		// (set) Token: 0x060003AE RID: 942 RVA: 0x0001AD98 File Offset: 0x00018F98
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

		// Token: 0x170000C9 RID: 201
		// (get) Token: 0x060003AF RID: 943 RVA: 0x0001ADAC File Offset: 0x00018FAC
		// (set) Token: 0x060003B0 RID: 944 RVA: 0x0001ADBE File Offset: 0x00018FBE
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

		// Token: 0x170000CA RID: 202
		// (get) Token: 0x060003B1 RID: 945 RVA: 0x0001ADD2 File Offset: 0x00018FD2
		// (set) Token: 0x060003B2 RID: 946 RVA: 0x0001ADEB File Offset: 0x00018FEB
		[ParserTarget("disableDisplacement")]
		public NumericParser<bool> DisableDisplacement
		{
			get
			{
				return base.GetFloat("_DisableDisplacement") > 0.5f;
			}
			set
			{
				base.SetFloat("_DisableDisplacement", value ? 1f : 0f);
			}
		}

		// Token: 0x170000CB RID: 203
		// (get) Token: 0x060003B3 RID: 947 RVA: 0x0001AE0D File Offset: 0x0001900D
		// (set) Token: 0x060003B4 RID: 948 RVA: 0x0001AE26 File Offset: 0x00019026
		[ParserTarget("noShadowsUnderwater")]
		public NumericParser<bool> NoShadowsUnderwater
		{
			get
			{
				return base.GetFloat("_NoShadowsUnderwater") > 0.5f;
			}
			set
			{
				base.SetFloat("_NoShadowsUnderwater", value ? 1f : 0f);
			}
		}

		// Token: 0x170000CC RID: 204
		// (get) Token: 0x060003B5 RID: 949 RVA: 0x0001AE48 File Offset: 0x00019048
		// (set) Token: 0x060003B6 RID: 950 RVA: 0x0001AE5A File Offset: 0x0001905A
		[ParserTarget("blend")]
		public NumericParser<float> Blend
		{
			get
			{
				return base.GetFloat("_Blend");
			}
			set
			{
				base.SetFloat("_Blend", value);
			}
		}

		// Token: 0x170000CD RID: 205
		// (get) Token: 0x060003B7 RID: 951 RVA: 0x0001AE6E File Offset: 0x0001906E
		// (set) Token: 0x060003B8 RID: 952 RVA: 0x0001AE80 File Offset: 0x00019080
		[ParserTarget("theta")]
		public NumericParser<float> Theta
		{
			get
			{
				return base.GetFloat("_Theta");
			}
			set
			{
				base.SetFloat("_Theta", value);
			}
		}

		// Token: 0x170000CE RID: 206
		// (get) Token: 0x060003B9 RID: 953 RVA: 0x0001AE94 File Offset: 0x00019094
		// (set) Token: 0x060003BA RID: 954 RVA: 0x0001AEA6 File Offset: 0x000190A6
		[ParserTarget("porosityCoefficient")]
		public NumericParser<float> PorosityCoefficient
		{
			get
			{
				return base.GetFloat("_porosityCoeffient");
			}
			set
			{
				base.SetFloat("_porosityCoeffient", value);
			}
		}

		// Token: 0x170000CF RID: 207
		// (get) Token: 0x060003BB RID: 955 RVA: 0x0001AEBA File Offset: 0x000190BA
		// (set) Token: 0x060003BC RID: 956 RVA: 0x0001AECC File Offset: 0x000190CC
		[ParserTarget("lightBoost")]
		public NumericParser<float> LightBoost
		{
			get
			{
				return base.GetFloat("_LightBoost");
			}
			set
			{
				base.SetFloat("_LightBoost", value);
			}
		}

		// Token: 0x170000D0 RID: 208
		// (get) Token: 0x060003BD RID: 957 RVA: 0x0001AEE0 File Offset: 0x000190E0
		// (set) Token: 0x060003BE RID: 958 RVA: 0x0001AEF2 File Offset: 0x000190F2
		[ParserTarget("gammaBoost")]
		public NumericParser<float> GammaBoost
		{
			get
			{
				return base.GetFloat("_GammaBoost");
			}
			set
			{
				base.SetFloat("_GammaBoost", value);
			}
		}

		// Token: 0x170000D1 RID: 209
		// (get) Token: 0x060003BF RID: 959 RVA: 0x0001AF06 File Offset: 0x00019106
		// (set) Token: 0x060003C0 RID: 960 RVA: 0x0001AF18 File Offset: 0x00019118
		[ParserTarget("planetBumpScale")]
		public NumericParser<float> PlanetBumpScale
		{
			get
			{
				return base.GetFloat("_PlanetBumpScale");
			}
			set
			{
				base.SetFloat("_PlanetBumpScale", value);
			}
		}

		// Token: 0x170000D2 RID: 210
		// (get) Token: 0x060003C1 RID: 961 RVA: 0x0001AF2C File Offset: 0x0001912C
		// (set) Token: 0x060003C2 RID: 962 RVA: 0x0001AF3E File Offset: 0x0001913E
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

		// Token: 0x170000D3 RID: 211
		// (get) Token: 0x060003C3 RID: 963 RVA: 0x0001AF52 File Offset: 0x00019152
		// (set) Token: 0x060003C4 RID: 964 RVA: 0x0001AF64 File Offset: 0x00019164
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

		// Token: 0x170000D4 RID: 212
		// (get) Token: 0x060003C5 RID: 965 RVA: 0x0001AF78 File Offset: 0x00019178
		// (set) Token: 0x060003C6 RID: 966 RVA: 0x0001AF8A File Offset: 0x0001918A
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

		// Token: 0x170000D5 RID: 213
		// (get) Token: 0x060003C7 RID: 967 RVA: 0x0001AF9E File Offset: 0x0001919E
		// (set) Token: 0x060003C8 RID: 968 RVA: 0x0001AFB0 File Offset: 0x000191B0
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

		// Token: 0x060003C9 RID: 969 RVA: 0x0001AFC4 File Offset: 0x000191C4
		public override void PostApply(ConfigNode node)
		{
			base.PostApply(node);
			foreach (string keyword in node.GetValues("keyword"))
			{
				base.SetKeyword(keyword, true);
			}
		}

		// Token: 0x060003CA RID: 970 RVA: 0x0001B002 File Offset: 0x00019202
		public MirageScaledShaderLoader()
		{
		}

		// Token: 0x060003CB RID: 971 RVA: 0x0001B00C File Offset: 0x0001920C
		public MirageScaledShaderLoader(Material material) : base(material)
		{
		}

		// Token: 0x040002D4 RID: 724
		public const string SHADER_NAME = "Mirage/Scaled";
	}
}
