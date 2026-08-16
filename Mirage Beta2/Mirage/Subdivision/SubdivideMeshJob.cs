using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Mirage.Subdivision
{
	// Token: 0x02000055 RID: 85
	[BurstCompile]
	public struct SubdivideMeshJob : IJobParallelFor
	{
		// Token: 0x06000274 RID: 628 RVA: 0x00015688 File Offset: 0x00013888
		public void Execute(int index)
		{
			this.tris.BeginForEachIndex(index);
			SubdividableTriangle tri = this.meshTriangles[index];
			float4 ws = math.mul(this.objectToWorldMatrix, new float4(tri.v1, 1f));
			float4 ws2 = math.mul(this.objectToWorldMatrix, new float4(tri.v2, 1f));
			float4 ws3 = math.mul(this.objectToWorldMatrix, new float4(tri.v3, 1f));
			float3 center = (ws.xyz + ws2.xyz + ws3.xyz) * 0.333f;
			float centerDist = this.SqrDist(this.target, center);
			int inside = 0;
			int i = 0;
			while (i < 6)
			{
				MiragePlane plane = this.cameraFrustumPlanes[i];
				if (centerDist < 75f)
				{
					goto IL_107;
				}
				float3 xyz = ws.xyz;
				if (plane.GetSide(xyz))
				{
					goto IL_107;
				}
				float3 xyz2 = ws2.xyz;
				if (plane.GetSide(xyz2))
				{
					goto IL_107;
				}
				float3 xyz3 = ws3.xyz;
				bool flag = plane.GetSide(xyz3);
				IL_108:
				bool flag2 = flag;
				if (flag2)
				{
					inside++;
				}
				i++;
				continue;
				IL_107:
				flag = true;
				goto IL_108;
			}
			bool flag3 = inside == 6;
			if (flag3)
			{
				int num = 0;
				tri.Subdivide(ref this.tris, num, this.target, this.maxSubdivisionLevel, this.sqrSubdivisionRange, this.objectToWorldMatrix);
			}
			else
			{
				this.tris.Write<SubdividableTriangle>(tri);
			}
			this.tris.EndForEachIndex();
		}

		// Token: 0x06000275 RID: 629 RVA: 0x0001580C File Offset: 0x00013A0C
		private float SqrDist(in float3 a, in float3 b)
		{
			float dx = b.x - a.x;
			float dy = b.y - a.y;
			float dz = b.z - a.z;
			return Mathf.Sqrt(dx * dx + dy * dy + dz * dz);
		}

		// Token: 0x04000246 RID: 582
		[ReadOnly]
		public NativeArray<SubdividableTriangle> meshTriangles;

		// Token: 0x04000247 RID: 583
		[ReadOnly]
		public NativeArray<float3> originalVerts;

		// Token: 0x04000248 RID: 584
		[ReadOnly]
		public NativeArray<float3> originalNormals;

		// Token: 0x04000249 RID: 585
		[ReadOnly]
		public NativeArray<float4> originalColors;

		// Token: 0x0400024A RID: 586
		[WriteOnly]
		public NativeStream.Writer tris;

		// Token: 0x0400024B RID: 587
		[ReadOnly]
		public float3 target;

		// Token: 0x0400024C RID: 588
		[ReadOnly]
		public float sqrSubdivisionRange;

		// Token: 0x0400024D RID: 589
		[ReadOnly]
		public int maxSubdivisionLevel;

		// Token: 0x0400024E RID: 590
		[ReadOnly]
		public NativeArray<MiragePlane> cameraFrustumPlanes;

		// Token: 0x0400024F RID: 591
		[ReadOnly]
		public float4x4 objectToWorldMatrix;
	}
}
