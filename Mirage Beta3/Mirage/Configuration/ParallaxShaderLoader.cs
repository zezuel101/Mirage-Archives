using System;
using Kopernicus.ConfigParser.Attributes;
using Kopernicus.Configuration.Attributes;
using Kopernicus.Configuration.MaterialLoader;
using Kopernicus.Configuration.Parsing;
using UnityEngine;

namespace Mirage.Configuration
{
	/// <summary>Kopernicus material loader for <c>Mirage/Parallax</c> — handles keywords.</summary>
	// Token: 0x02000084 RID: 132
	[RequireConfigType(1)]
	[MaterialLoader("Mirage/Parallax")]
	public class ParallaxShaderLoader : PQSMaterialLoader
	{
		// Token: 0x1700008F RID: 143
		// (get) Token: 0x060003B3 RID: 947 RVA: 0x0001C10F File Offset: 0x0001A30F
		private static Shader Shader
		{
			get
			{
				return (ParallaxShaderLoader.s_Shader != null) ? ParallaxShaderLoader.s_Shader : (ParallaxShaderLoader.s_Shader = Shader.Find("Mirage/Parallax"));
			}
		}

		// Token: 0x060003B4 RID: 948 RVA: 0x0001C135 File Offset: 0x0001A335
		public ParallaxShaderLoader()
		{
		}

		// Token: 0x060003B5 RID: 949 RVA: 0x0001C14F File Offset: 0x0001A34F
		public ParallaxShaderLoader(Material material)
		{
			base.Value = material;
		}

		// Token: 0x17000090 RID: 144
		// (get) Token: 0x060003B6 RID: 950 RVA: 0x0001C170 File Offset: 0x0001A370
		// (set) Token: 0x060003B7 RID: 951 RVA: 0x0001C178 File Offset: 0x0001A378
		public override ShaderParser ShaderParser { get; set; } = ParallaxShaderLoader.Shader;

		// Token: 0x060003B8 RID: 952 RVA: 0x0001C181 File Offset: 0x0001A381
		public static bool UsesSameShader(Material m)
		{
			return m != null && m.shader != null && m.shader.name == "Mirage/Parallax";
		}

		// Token: 0x060003B9 RID: 953 RVA: 0x0001C1B4 File Offset: 0x0001A3B4
		public override void PostApply(ConfigNode node)
		{
			base.PostApply(node);
			foreach (string keyword in node.GetValues("keyword"))
			{
				base.SetKeyword(keyword, true);
			}
		}

		// Token: 0x04000365 RID: 869
		public const string SHADER_NAME = "Mirage/Parallax";

		// Token: 0x04000366 RID: 870
		private static Shader s_Shader;
	}
}
