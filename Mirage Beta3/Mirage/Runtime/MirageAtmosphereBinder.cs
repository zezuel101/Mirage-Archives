using System;
using Mirage.Configuration;
using UnityEngine;

namespace Mirage.Runtime
{
	/// <summary>Binds Scatterer atmosphere uniforms to terrain materials, toggling MIRAGE_ATMOSPHERE.</summary>
	// Token: 0x0200006A RID: 106
	internal static class MirageAtmosphereBinder
	{
		// Token: 0x06000328 RID: 808 RVA: 0x000187C4 File Offset: 0x000169C4
		public static void Bind(MirageBody body, Material mat)
		{
			bool flag = mat == null || !TerrainMaterialBinder.UsesMirageShader(mat);
			if (!flag)
			{
				bool flag2 = !MirageScattererBridge.CanBindAtmosphere(mat);
				if (!flag2)
				{
					bool flag3 = MirageScattererBridge.TryBindAtmosphere(body.CelestialBody.name, mat);
					if (flag3)
					{
						mat.EnableKeyword("MIRAGE_ATMOSPHERE");
						MirageAtmosphereBinder.LogBindOnce(body, mat);
					}
					else
					{
						mat.DisableKeyword("MIRAGE_ATMOSPHERE");
					}
				}
			}
		}

		/// <summary>Periodic re-bind to track Scatterer's changing sunColor and atlas state.</summary>
		// Token: 0x06000329 RID: 809 RVA: 0x00018838 File Offset: 0x00016A38
		public static void Refresh(MirageBody body)
		{
			bool flag = body == null || Time.frameCount % 120 != 0;
			if (!flag)
			{
				PQS pqs = body.CelestialBody.pqsController;
				bool flag2 = pqs == null;
				if (!flag2)
				{
					MirageAtmosphereBinder.Bind(body, pqs.surfaceMaterial);
					MirageAtmosphereBinder.Bind(body, pqs.lowQualitySurfaceMaterial);
				}
			}
		}

		// Token: 0x0600032A RID: 810 RVA: 0x00018890 File Offset: 0x00016A90
		public static void Reset()
		{
			MirageAtmosphereBinder.s_BindLogged = false;
		}

		// Token: 0x0600032B RID: 811 RVA: 0x0001889C File Offset: 0x00016A9C
		private static void LogBindOnce(MirageBody body, Material mat)
		{
			bool flag = MirageAtmosphereBinder.s_BindLogged;
			if (!flag)
			{
				MirageAtmosphereBinder.s_BindLogged = true;
				Texture atlas = mat.GetTexture("AtmosphereAtlas");
				string atlasSize = (atlas != null) ? string.Format("{0}x{1}", atlas.width, atlas.height) : "NULL";
				MirageDebug.Log(string.Concat(new string[]
				{
					"MirageAtmosphere[",
					body.CelestialBody.name,
					"] bound: atlas=",
					atlasSize,
					" ",
					string.Format("Rg={0} betaR={1} HR={2} ", mat.GetFloat("Rg"), mat.GetVector("betaR"), mat.GetFloat("HR")),
					string.Format("sunColor={0} ", mat.GetColor("_sunColor")),
					string.Format("exposure={0}", mat.GetFloat("_AtmosphereExposure"))
				}));
			}
		}

		// Token: 0x04000308 RID: 776
		private const string Keyword = "MIRAGE_ATMOSPHERE";

		// Token: 0x04000309 RID: 777
		private const int RefreshInterval = 120;

		// Token: 0x0400030A RID: 778
		private static bool s_BindLogged;
	}
}
