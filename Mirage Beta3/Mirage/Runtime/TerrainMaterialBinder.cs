using System;
using Mirage.Configuration;
using Mirage.VirtualTexture;
using UnityEngine;

namespace Mirage.Runtime
{
	/// <summary>Binds VT caches and tessellation caps to a body's PQS materials on activation.</summary>
	// Token: 0x02000071 RID: 113
	internal static class TerrainMaterialBinder
	{
		/// <summary>True for either Mirage terrain shader.</summary>
		// Token: 0x0600035E RID: 862 RVA: 0x00019BC8 File Offset: 0x00017DC8
		public static bool UsesMirageShader(Material mat)
		{
			return ParallaxShaderLoader.UsesSameShader(mat) || MiragePqsShaderLoader.UsesSameShader(mat);
		}

		// Token: 0x0600035F RID: 863 RVA: 0x00019BDB File Offset: 0x00017DDB
		public static void BindCaches(MirageBody body, PQS pqs)
		{
			TerrainMaterialBinder.BindCachesTo(body, pqs.surfaceMaterial);
			TerrainMaterialBinder.BindCachesTo(body, pqs.lowQualitySurfaceMaterial);
		}

		// Token: 0x06000360 RID: 864 RVA: 0x00019BF8 File Offset: 0x00017DF8
		public static void ApplyTessellation(VirtualTextureConfig cfg, PQS pqs)
		{
			float maxTess = cfg.ResolveMaxTessellation(0);
			float maxTessRange = cfg.ResolveMaxTessellationRange();
			float edgeLength = cfg.tessellationEdgeLength;
			TerrainMaterialBinder.SetTessellationOn(pqs.surfaceMaterial, maxTess, maxTessRange, edgeLength);
			TerrainMaterialBinder.SetTessellationOn(pqs.lowQualitySurfaceMaterial, maxTess, maxTessRange, edgeLength);
			string rangeDesc = (maxTessRange > 0f) ? string.Format("{0:0}m", maxTessRange) : "off";
			MirageDebug.Log(string.Format("  Tessellation: max={0} ({1}) ", maxTess, (cfg.maxTessellation > 0) ? "config" : "auto") + string.Format("edge={0:0.#}px cutoff={1}", edgeLength, rangeDesc));
		}

		// Token: 0x06000361 RID: 865 RVA: 0x00019C9C File Offset: 0x00017E9C
		private static void BindCachesTo(MirageBody body, Material mat)
		{
			bool flag = !TerrainMaterialBinder.UsesMirageShader(mat);
			if (!flag)
			{
				TileCache cache = body.Cache;
				if (cache != null)
				{
					cache.BindToMaterial(mat);
				}
				MirageAtmosphereBinder.Bind(body, mat);
			}
		}

		// Token: 0x06000362 RID: 866 RVA: 0x00019CD4 File Offset: 0x00017ED4
		private static void SetTessellationOn(Material mat, float maxTess, float maxTessRange, float edgeLength)
		{
			bool flag = mat == null || !TerrainMaterialBinder.UsesMirageShader(mat);
			if (!flag)
			{
				mat.SetFloat(TerrainMaterialBinder.s_MaxTessellationID, maxTess);
				mat.SetFloat(TerrainMaterialBinder.s_MaxTessellationRangeID, maxTessRange);
				bool flag2 = edgeLength > 0f;
				if (flag2)
				{
					mat.SetFloat(TerrainMaterialBinder.s_TessellationEdgeLengthID, edgeLength);
				}
			}
		}

		// Token: 0x0400033B RID: 827
		private static readonly int s_MaxTessellationID = Shader.PropertyToID("_MaxTessellation");

		// Token: 0x0400033C RID: 828
		private static readonly int s_MaxTessellationRangeID = Shader.PropertyToID("_MaxTessellationRange");

		// Token: 0x0400033D RID: 829
		private static readonly int s_TessellationEdgeLengthID = Shader.PropertyToID("_TessellationEdgeLength");
	}
}
