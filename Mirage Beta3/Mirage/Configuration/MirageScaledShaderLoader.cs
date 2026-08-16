using System;
using Kopernicus.ConfigParser.Attributes;
using Kopernicus.ConfigParser.BuiltinTypeParsers;
using Kopernicus.Configuration.Attributes;
using Kopernicus.Configuration.MaterialLoader;
using UnityEngine;

namespace Mirage.Configuration
{
	/// <summary>Kopernicus material loader for <c>Mirage/Scaled</c> — handles Int toggles and keywords.</summary>
	// Token: 0x02000086 RID: 134
	[RequireConfigType(1)]
	[MaterialLoader("Mirage/Scaled")]
	public class MirageScaledShaderLoader : CustomMaterialLoader
	{
		// Token: 0x060003C3 RID: 963 RVA: 0x0001C3FC File Offset: 0x0001A5FC
		public MirageScaledShaderLoader()
		{
		}

		// Token: 0x060003C4 RID: 964 RVA: 0x0001C406 File Offset: 0x0001A606
		public MirageScaledShaderLoader(Material material) : base(material)
		{
		}

		// Token: 0x060003C5 RID: 965 RVA: 0x0001C411 File Offset: 0x0001A611
		public static bool UsesSameShader(Material m)
		{
			return m != null && m.shader != null && m.shader.name == "Mirage/Scaled";
		}

		// Token: 0x17000093 RID: 147
		// (get) Token: 0x060003C6 RID: 966 RVA: 0x0001C442 File Offset: 0x0001A642
		// (set) Token: 0x060003C7 RID: 967 RVA: 0x0001C45B File Offset: 0x0001A65B
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

		// Token: 0x17000094 RID: 148
		// (get) Token: 0x060003C8 RID: 968 RVA: 0x0001C47D File Offset: 0x0001A67D
		// (set) Token: 0x060003C9 RID: 969 RVA: 0x0001C496 File Offset: 0x0001A696
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

		// Token: 0x17000095 RID: 149
		// (get) Token: 0x060003CA RID: 970 RVA: 0x0001C4B8 File Offset: 0x0001A6B8
		// (set) Token: 0x060003CB RID: 971 RVA: 0x0001C4D1 File Offset: 0x0001A6D1
		[ParserTarget("clampOcean")]
		public NumericParser<bool> ClampOcean
		{
			get
			{
				return base.GetFloat("_ClampOcean") > 0.5f;
			}
			set
			{
				base.SetFloat("_ClampOcean", value ? 1f : 0f);
			}
		}

		// Token: 0x17000096 RID: 150
		// (get) Token: 0x060003CC RID: 972 RVA: 0x0001C4F3 File Offset: 0x0001A6F3
		// (set) Token: 0x060003CD RID: 973 RVA: 0x0001C505 File Offset: 0x0001A705
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

		// Token: 0x060003CE RID: 974 RVA: 0x0001C51C File Offset: 0x0001A71C
		public override void PostApply(ConfigNode node)
		{
			base.PostApply(node);
			foreach (string keyword in node.GetValues("keyword"))
			{
				base.SetKeyword(keyword, true);
			}
		}

		// Token: 0x0400036C RID: 876
		public const string SHADER_NAME = "Mirage/Scaled";
	}
}
