using System;
using Mirage.WebIngest;
using UnityEngine;

namespace Mirage.VirtualTexture
{
	/// <summary>Tile coordinate, placement, and visibility helpers (stateless).</summary>
	// Token: 0x02000058 RID: 88
	internal static class TileGeometry
	{
		// Token: 0x06000293 RID: 659 RVA: 0x00014650 File Offset: 0x00012850
		public static void GetCorrectedTileCoord(int face, float uvSwX, float uvSwY, int tileLevel, out int tx, out int ty)
		{
			int grid = 1 << tileLevel;
			int rawX = Mathf.Clamp(Mathf.FloorToInt(uvSwX * (float)grid), 0, grid - 1);
			int rawY = Mathf.Clamp(Mathf.FloorToInt(uvSwY * (float)grid), 0, grid - 1);
			switch (face)
			{
			case 0:
				tx = rawY;
				ty = grid - 1 - rawX;
				break;
			case 1:
				tx = grid - 1 - rawY;
				ty = rawX;
				break;
			case 2:
			case 3:
			case 4:
				tx = grid - 1 - rawX;
				ty = grid - 1 - rawY;
				break;
			default:
				tx = rawX;
				ty = rawY;
				break;
			}
		}

		// Token: 0x06000294 RID: 660 RVA: 0x000146E8 File Offset: 0x000128E8
		private static float[] BuildHorizonD(bool cosNotSin)
		{
			float[] table = new float[25];
			for (int level = 0; level <= 24; level++)
			{
				double d = Math.Sqrt(2.0) * 3.141592653589793 / Math.Pow(2.0, (double)(level + 2)) + 0.009999999776482582;
				table[level] = (float)(cosNotSin ? Math.Cos(d) : Math.Sin(d));
			}
			return table;
		}

		// Token: 0x06000295 RID: 661 RVA: 0x00014768 File Offset: 0x00012968
		public static bool Visible(in VTLevelContext ctx, Vector3 center, Vector3 dirWorld, float extent, int level)
		{
			float radius = extent * 0.75f;
			Plane[] planes = ctx.FrustumPlanes;
			for (int i = 0; i < planes.Length; i++)
			{
				bool flag = planes[i].GetDistanceToPoint(center) < -radius;
				if (flag)
				{
					return false;
				}
			}
			bool cameraInsideSphere = ctx.CameraInsideSphere;
			if (cameraInsideSphere)
			{
				return true;
			}
			int li = (level < TileGeometry.s_HorizonCosD.Length) ? level : (TileGeometry.s_HorizonCosD.Length - 1);
			float cosThreshold = ctx.CosHorizon * TileGeometry.s_HorizonCosD[li] - ctx.SinHorizon * TileGeometry.s_HorizonSinD[li];
			return Vector3.Dot(dirWorld, ctx.CamDir) >= cosThreshold;
		}

		/// <summary>World-space tile center, edge length, and unit direction for Visible.</summary>
		// Token: 0x06000296 RID: 662 RVA: 0x0001481C File Offset: 0x00012A1C
		public static void TileSphere(in DescentContext d, int face, int level, int tx, int ty, out Vector3 center, out float extent, out Vector3 dirWorld)
		{
			Vector3 local;
			float extentScale;
			TileGeometry.TileLocal(face, level, tx, ty, d.TileSize, d.BorderPx, out local, out extentScale);
			TileGeometry.PlaceTile(d.Ctx, local, extentScale, out center, out extent, out dirWorld);
		}

		/// <summary>Frame-independent half of TileSphere: body-local direction and extent fraction.</summary>
		// Token: 0x06000297 RID: 663 RVA: 0x0001485C File Offset: 0x00012A5C
		public static void TileLocal(int face, int level, int tx, int ty, int tileSize, int borderPx, out Vector3 local, out float extentScale)
		{
			double centerTexel = (double)borderPx + (double)tileSize * 0.5;
			double dx;
			double dy;
			double dz;
			MirageCubeMath.TileTexelToDirection(face, level, tx, ty, centerTexel, centerTexel, tileSize, borderPx, out dx, out dy, out dz);
			local = new Vector3((float)dx, (float)dy, (float)dz);
			int grid = 1 << level;
			double scale = MirageCubeMath.FaceExtentScale(((double)tx + 0.5) / (double)grid, ((double)ty + 0.5) / (double)grid);
			extentScale = (float)(1.5707963267948966 / (double)grid * scale);
		}

		/// <summary>Lift a TileLocal result into this frame's world space.</summary>
		// Token: 0x06000298 RID: 664 RVA: 0x000148E8 File Offset: 0x00012AE8
		public static void PlaceTile(in VTLevelContext ctx, Vector3 local, float extentScale, out Vector3 center, out float extent, out Vector3 dirWorld)
		{
			dirWorld = ctx.PlanetRotation * local;
			center = ctx.PlanetOrigin + dirWorld * ctx.PlanetRadius;
			extent = extentScale * ctx.PlanetRadius;
		}

		/// <summary>Does this tile still cover more than threshold pixels? (No sqrt.)</summary>
		// Token: 0x06000299 RID: 665 RVA: 0x00014938 File Offset: 0x00012B38
		public static bool UnderResolved(in DescentContext d, Vector3 center, float extent)
		{
			float reach = extent * d.ProjScale;
			bool flag = reach <= 1f;
			bool result;
			if (flag)
			{
				result = false;
			}
			else
			{
				float limit = reach + extent * 0.5f;
				result = ((center - d.Ctx.CameraPos).sqrMagnitude < limit * limit);
			}
			return result;
		}

		// Token: 0x04000258 RID: 600
		private const float HorizonReliefMargin = 0.01f;

		// Token: 0x04000259 RID: 601
		private const int MaxHorizonLevel = 24;

		// Token: 0x0400025A RID: 602
		private static readonly float[] s_HorizonCosD = TileGeometry.BuildHorizonD(true);

		// Token: 0x0400025B RID: 603
		private static readonly float[] s_HorizonSinD = TileGeometry.BuildHorizonD(false);
	}
}
