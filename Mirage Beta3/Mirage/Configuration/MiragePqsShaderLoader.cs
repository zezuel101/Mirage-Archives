using System;
using System.Collections.Generic;
using Kopernicus.ConfigParser.Attributes;
using Kopernicus.Configuration.Attributes;
using Kopernicus.Configuration.MaterialLoader;
using Kopernicus.Configuration.Parsing;
using KSPTextureLoader;
using UnityEngine;

namespace Mirage.Configuration
{
	/// <summary>Kopernicus material loader for <c>Mirage/PQS</c> — handles linear textures and keywords.</summary>
	// Token: 0x02000085 RID: 133
	[RequireConfigType(1)]
	[MaterialLoader("Mirage/PQS")]
	public class MiragePqsShaderLoader : PQSMaterialLoader
	{
		// Token: 0x17000091 RID: 145
		// (get) Token: 0x060003BA RID: 954 RVA: 0x0001C1F2 File Offset: 0x0001A3F2
		private static Shader Shader
		{
			get
			{
				return (MiragePqsShaderLoader.s_Shader != null) ? MiragePqsShaderLoader.s_Shader : (MiragePqsShaderLoader.s_Shader = Shader.Find("Mirage/PQS"));
			}
		}

		// Token: 0x060003BB RID: 955 RVA: 0x0001C218 File Offset: 0x0001A418
		public MiragePqsShaderLoader()
		{
		}

		// Token: 0x060003BC RID: 956 RVA: 0x0001C232 File Offset: 0x0001A432
		public MiragePqsShaderLoader(Material material)
		{
			base.Value = material;
		}

		// Token: 0x17000092 RID: 146
		// (get) Token: 0x060003BD RID: 957 RVA: 0x0001C253 File Offset: 0x0001A453
		// (set) Token: 0x060003BE RID: 958 RVA: 0x0001C25B File Offset: 0x0001A45B
		public override ShaderParser ShaderParser { get; set; } = MiragePqsShaderLoader.Shader;

		// Token: 0x060003BF RID: 959 RVA: 0x0001C264 File Offset: 0x0001A464
		public static bool UsesSameShader(Material m)
		{
			return m != null && m.shader != null && m.shader.name == "Mirage/PQS";
		}

		// Token: 0x060003C0 RID: 960 RVA: 0x0001C298 File Offset: 0x0001A498
		public override void PostApply(ConfigNode node)
		{
			base.PostApply(node);
			this.LoadTexture<Texture2D>(node, "splatmap", "_Splatmap", true);
			this.LoadTexture<Texture2DArray>(node, "albedoArray", "_AlbedoArray", false);
			this.LoadTexture<Texture2DArray>(node, "normalArray", "_NormalArray", true);
			this.LoadTexture<Texture2DArray>(node, "maskArray", "_MaskArray", true);
			foreach (string keyword in node.GetValues("keyword"))
			{
				base.SetKeyword(keyword, true);
			}
		}

		// Token: 0x060003C1 RID: 961 RVA: 0x0001C324 File Offset: 0x0001A524
		private void LoadTexture<T>(ConfigNode node, string key, string property, bool linear) where T : Texture
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
					TextureHandle<T> handle = TextureLoader.LoadTexture<T>(path, options);
					base.Value.SetTexture(property, handle.GetTexture());
					MiragePqsShaderLoader.s_RootedHandles.Add(handle);
				}
				catch (Exception e)
				{
					MirageDebug.LogError(string.Concat(new string[]
					{
						"MiragePqsShaderLoader: failed to load '",
						path,
						"' for ",
						property,
						": ",
						e.Message
					}));
				}
			}
		}

		// Token: 0x04000368 RID: 872
		public const string SHADER_NAME = "Mirage/PQS";

		// Token: 0x04000369 RID: 873
		private static readonly List<object> s_RootedHandles = new List<object>();

		// Token: 0x0400036A RID: 874
		private static Shader s_Shader;
	}
}
