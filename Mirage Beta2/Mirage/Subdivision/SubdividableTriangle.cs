using System;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;

namespace Mirage.Subdivision
{
	// Token: 0x02000052 RID: 82
	public struct SubdividableTriangle
	{
		// Token: 0x06000257 RID: 599 RVA: 0x00013F8C File Offset: 0x0001218C
		public SubdividableTriangle(float3 v1, float3 v2, float3 v3, float3 n1, float3 n2, float3 n3, float4 c1, float4 c2, float4 c3, float2 uv1, float2 uv2, float2 uv3)
		{
			this.v1 = v1;
			this.v2 = v2;
			this.v3 = v3;
			this.n1 = n1;
			this.n2 = n2;
			this.n3 = n3;
			this.c1 = c1;
			this.c2 = c2;
			this.c3 = c3;
			this.uv1 = uv1;
			this.uv2 = uv2;
			this.uv3 = uv3;
		}

		// Token: 0x06000258 RID: 600 RVA: 0x00013FF8 File Offset: 0x000121F8
		public void Subdivide(ref NativeStream.Writer tris, in int level, in float3 target, in int maxSubdivisionLevel, in float subdivisionRange, in float4x4 objectToWorld)
		{
			bool flag = level == maxSubdivisionLevel;
			if (!flag)
			{
				float3 worldV = math.mul(objectToWorld, new float4(this.v1, 1f)).xyz;
				float3 worldV2 = math.mul(objectToWorld, new float4(this.v2, 1f)).xyz;
				float3 worldV3 = math.mul(objectToWorld, new float4(this.v3, 1f)).xyz;
				int lvl = (int)math.lerp((float)maxSubdivisionLevel, 0f, this.CalcDistance(worldV, target, subdivisionRange));
				int lvl2 = (int)math.lerp((float)maxSubdivisionLevel, 0f, this.CalcDistance(worldV2, target, subdivisionRange));
				int lvl3 = (int)math.lerp((float)maxSubdivisionLevel, 0f, this.CalcDistance(worldV3, target, subdivisionRange));
				bool flag2 = this.AreTwoOutOfRange(level, lvl, lvl2, lvl3);
				if (flag2)
				{
					tris.Write<SubdividableTriangle>(this);
				}
				else
				{
					bool flag3 = this.IsOneOutOfRange(level, lvl, lvl2, lvl3);
					if (flag3)
					{
						bool flag4 = lvl < lvl2;
						if (flag4)
						{
							float3 mp = SubdividableTriangle.MidV(this.v2, this.v3);
							float3 mn = SubdividableTriangle.MidV(this.n2, this.n3);
							float4 mc = SubdividableTriangle.MidC(this.c2, this.c3);
							float2 mu = SubdividableTriangle.MidUV(this.uv2, this.uv3);
							tris.Write<SubdividableTriangle>(new SubdividableTriangle(this.v3, this.v1, mp, this.n3, this.n1, mn, this.c3, this.c1, mc, this.uv3, this.uv1, mu));
							tris.Write<SubdividableTriangle>(new SubdividableTriangle(this.v1, this.v2, mp, this.n1, this.n2, mn, this.c1, this.c2, mc, this.uv1, this.uv2, mu));
							return;
						}
						bool flag5 = lvl2 < lvl3;
						if (flag5)
						{
							float3 mp2 = SubdividableTriangle.MidV(this.v1, this.v3);
							float3 mn2 = SubdividableTriangle.MidV(this.n1, this.n3);
							float4 mc2 = SubdividableTriangle.MidC(this.c1, this.c3);
							float2 mu2 = SubdividableTriangle.MidUV(this.uv1, this.uv3);
							tris.Write<SubdividableTriangle>(new SubdividableTriangle(this.v1, this.v2, mp2, this.n1, this.n2, mn2, this.c1, this.c2, mc2, this.uv1, this.uv2, mu2));
							tris.Write<SubdividableTriangle>(new SubdividableTriangle(this.v2, this.v3, mp2, this.n2, this.n3, mn2, this.c2, this.c3, mc2, this.uv2, this.uv3, mu2));
							return;
						}
						bool flag6 = lvl3 < lvl;
						if (flag6)
						{
							float3 mp3 = SubdividableTriangle.MidV(this.v1, this.v2);
							float3 mn3 = SubdividableTriangle.MidV(this.n1, this.n2);
							float4 mc3 = SubdividableTriangle.MidC(this.c1, this.c2);
							float2 mu3 = SubdividableTriangle.MidUV(this.uv1, this.uv2);
							tris.Write<SubdividableTriangle>(new SubdividableTriangle(this.v3, this.v1, mp3, this.n3, this.n1, mn3, this.c3, this.c1, mc3, this.uv3, this.uv1, mu3));
							tris.Write<SubdividableTriangle>(new SubdividableTriangle(this.v2, this.v3, mp3, this.n2, this.n3, mn3, this.c2, this.c3, mc3, this.uv2, this.uv3, mu3));
							return;
						}
					}
					bool flag7 = this.AllThreeOutOfRange(level, lvl, lvl2, lvl3);
					if (flag7)
					{
						tris.Write<SubdividableTriangle>(this);
					}
					else
					{
						bool flag8 = Hint.Unlikely(this.IsCorrupt(lvl, lvl2, lvl3, level));
						if (flag8)
						{
							tris.Write<SubdividableTriangle>(this);
						}
						else
						{
							float3 tv = SubdividableTriangle.MidV(this.v1, this.v3);
							float3 tv2 = SubdividableTriangle.MidV(this.v3, this.v2);
							float3 tv3 = this.v3;
							float3 tn = SubdividableTriangle.MidV(this.n1, this.n3);
							float3 tn2 = SubdividableTriangle.MidV(this.n3, this.n2);
							float3 tn3 = this.n3;
							float4 tc = SubdividableTriangle.MidC(this.c1, this.c3);
							float4 tc2 = SubdividableTriangle.MidC(this.c3, this.c2);
							float4 tc3 = this.c3;
							float2 tu = SubdividableTriangle.MidUV(this.uv1, this.uv3);
							float2 tu2 = SubdividableTriangle.MidUV(this.uv3, this.uv2);
							float2 tu3 = this.uv3;
							SubdividableTriangle top = new SubdividableTriangle(tv, tv2, tv3, tn, tn2, tn3, tc, tc2, tc3, tu, tu2, tu3);
							int num = level + 1;
							top.Subdivide(ref tris, num, target, maxSubdivisionLevel, subdivisionRange, objectToWorld);
							float3 blv = this.v1;
							float3 blv2 = SubdividableTriangle.MidV(this.v1, this.v2);
							float3 blv3 = SubdividableTriangle.MidV(this.v1, this.v3);
							float3 bln = this.n1;
							float3 bln2 = SubdividableTriangle.MidV(this.n1, this.n2);
							float3 bln3 = SubdividableTriangle.MidV(this.n1, this.n3);
							float4 blc = this.c1;
							float4 blc2 = SubdividableTriangle.MidC(this.c1, this.c2);
							float4 blc3 = SubdividableTriangle.MidC(this.c1, this.c3);
							float2 blu = this.uv1;
							float2 blu2 = SubdividableTriangle.MidUV(this.uv1, this.uv2);
							float2 blu3 = SubdividableTriangle.MidUV(this.uv1, this.uv3);
							SubdividableTriangle bl = new SubdividableTriangle(blv, blv2, blv3, bln, bln2, bln3, blc, blc2, blc3, blu, blu2, blu3);
							num = level + 1;
							bl.Subdivide(ref tris, num, target, maxSubdivisionLevel, subdivisionRange, objectToWorld);
							float3 brv = SubdividableTriangle.MidV(this.v1, this.v2);
							float3 brv2 = this.v2;
							float3 brv3 = SubdividableTriangle.MidV(this.v3, this.v2);
							float3 brn = SubdividableTriangle.MidV(this.n1, this.n2);
							float3 brn2 = this.n2;
							float3 brn3 = SubdividableTriangle.MidV(this.n3, this.n2);
							float4 brc = SubdividableTriangle.MidC(this.c1, this.c2);
							float4 brc2 = this.c2;
							float4 brc3 = SubdividableTriangle.MidC(this.c3, this.c2);
							float2 bru = SubdividableTriangle.MidUV(this.uv1, this.uv2);
							float2 bru2 = this.uv2;
							float2 bru3 = SubdividableTriangle.MidUV(this.uv3, this.uv2);
							SubdividableTriangle br = new SubdividableTriangle(brv, brv2, brv3, brn, brn2, brn3, brc, brc2, brc3, bru, bru2, bru3);
							num = level + 1;
							br.Subdivide(ref tris, num, target, maxSubdivisionLevel, subdivisionRange, objectToWorld);
							float3 cv = SubdividableTriangle.MidV(this.v1, this.v2);
							float3 cv2 = SubdividableTriangle.MidV(this.v2, this.v3);
							float3 cv3 = SubdividableTriangle.MidV(this.v3, this.v1);
							float3 cn = SubdividableTriangle.MidV(this.n1, this.n2);
							float3 cn2 = SubdividableTriangle.MidV(this.n2, this.n3);
							float3 cn3 = SubdividableTriangle.MidV(this.n3, this.n1);
							float4 cc = SubdividableTriangle.MidC(this.c1, this.c2);
							float4 cc2 = SubdividableTriangle.MidC(this.c2, this.c3);
							float4 cc3 = SubdividableTriangle.MidC(this.c3, this.c1);
							float2 cu = SubdividableTriangle.MidUV(this.uv1, this.uv2);
							float2 cu2 = SubdividableTriangle.MidUV(this.uv2, this.uv3);
							float2 cu3 = SubdividableTriangle.MidUV(this.uv3, this.uv1);
							SubdividableTriangle cen = new SubdividableTriangle(cv, cv2, cv3, cn, cn2, cn3, cc, cc2, cc3, cu, cu2, cu3);
							num = level + 1;
							cen.Subdivide(ref tris, num, target, maxSubdivisionLevel, subdivisionRange, objectToWorld);
							bool flag9 = level == lvl && level == lvl2 && level == lvl3;
							if (flag9)
							{
								tris.Write<SubdividableTriangle>(top);
								tris.Write<SubdividableTriangle>(bl);
								tris.Write<SubdividableTriangle>(br);
								tris.Write<SubdividableTriangle>(cen);
							}
						}
					}
				}
			}
		}

		// Token: 0x06000259 RID: 601 RVA: 0x00014850 File Offset: 0x00012A50
		private float CalcDistance(in float3 pos, in float3 target, in float maxRange)
		{
			float log2SqrMax = math.log2(maxRange * maxRange);
			float dist = math.distance(pos, target) + 0.708f;
			float log2SqrDist = math.log2(dist * dist);
			return math.pow(math.saturate(log2SqrDist / log2SqrMax), 1.6f);
		}

		// Token: 0x0600025A RID: 602 RVA: 0x000148A0 File Offset: 0x00012AA0
		private bool AreTwoOutOfRange(in int thisLevel, in int l1, in int l2, in int l3)
		{
			bool flag = l1 == l2 && l3 > l2 && thisLevel == l2;
			bool result;
			if (flag)
			{
				result = true;
			}
			else
			{
				bool flag2 = l2 == l3 && l1 > l3 && thisLevel == l3;
				if (flag2)
				{
					result = true;
				}
				else
				{
					bool flag3 = l3 == l1 && l2 > l1 && thisLevel == l1;
					result = flag3;
				}
			}
			return result;
		}

		// Token: 0x0600025B RID: 603 RVA: 0x00014910 File Offset: 0x00012B10
		private bool IsOneOutOfRange(in int thisLevel, in int l1, in int l2, in int l3)
		{
			bool flag = l1 == l2 && l3 < l2 && thisLevel == l3;
			bool result;
			if (flag)
			{
				result = true;
			}
			else
			{
				bool flag2 = l2 == l3 && l1 < l3 && thisLevel == l1;
				if (flag2)
				{
					result = true;
				}
				else
				{
					bool flag3 = l3 == l1 && l2 < l1 && thisLevel == l2;
					result = flag3;
				}
			}
			return result;
		}

		// Token: 0x0600025C RID: 604 RVA: 0x0001497F File Offset: 0x00012B7F
		private bool AllThreeOutOfRange(in int thisLevel, in int l1, in int l2, in int l3)
		{
			return thisLevel == l1 && thisLevel == l2 && thisLevel == l3;
		}

		// Token: 0x0600025D RID: 605 RVA: 0x00014997 File Offset: 0x00012B97
		private bool IsCorrupt(int l1, int l2, int l3, int level)
		{
			return level - l1 > 1 || level - l2 > 1 || level - l3 > 1;
		}

		// Token: 0x0600025E RID: 606 RVA: 0x000149B1 File Offset: 0x00012BB1
		private static float3 MidV(in float3 a, in float3 b)
		{
			return (a + b) * 0.5f;
		}

		// Token: 0x0600025F RID: 607 RVA: 0x000149CE File Offset: 0x00012BCE
		private static float4 MidC(in float4 a, in float4 b)
		{
			return (a + b) * 0.5f;
		}

		// Token: 0x06000260 RID: 608 RVA: 0x000149EB File Offset: 0x00012BEB
		private static float2 MidUV(in float2 a, in float2 b)
		{
			return (a + b) * 0.5f;
		}

		// Token: 0x04000218 RID: 536
		public float3 v1;

		// Token: 0x04000219 RID: 537
		public float3 v2;

		// Token: 0x0400021A RID: 538
		public float3 v3;

		// Token: 0x0400021B RID: 539
		public float3 n1;

		// Token: 0x0400021C RID: 540
		public float3 n2;

		// Token: 0x0400021D RID: 541
		public float3 n3;

		// Token: 0x0400021E RID: 542
		public float4 c1;

		// Token: 0x0400021F RID: 543
		public float4 c2;

		// Token: 0x04000220 RID: 544
		public float4 c3;

		// Token: 0x04000221 RID: 545
		public float2 uv1;

		// Token: 0x04000222 RID: 546
		public float2 uv2;

		// Token: 0x04000223 RID: 547
		public float2 uv3;
	}
}
