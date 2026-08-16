using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Mirage.Subdivision
{
	// Token: 0x02000062 RID: 98
	[BurstCompile]
	public struct ConstructMeshJob : IJobParallelFor
	{
		// Token: 0x060002DC RID: 732 RVA: 0x00016B88 File Offset: 0x00014D88
		public unsafe void Execute(int index)
		{
			int items = this.triArray.BeginForEachIndex(index);
			this.newTris.BeginForEachIndex(index);
			for (int i = 0; i < items; i++)
			{
				SubdividableTriangle tri = *this.triArray.Read<SubdividableTriangle>();
				int i2 = this.storedVertTris[tri.v1 * 0.001f];
				int i3 = this.storedVertTris[tri.v2 * 0.001f];
				int i4 = this.storedVertTris[tri.v3 * 0.001f];
				this.newVerts[i2] = tri.v1;
				this.newVerts[i3] = tri.v2;
				this.newVerts[i4] = tri.v3;
				this.newNormals[i2] = tri.n1;
				this.newNormals[i3] = tri.n2;
				this.newNormals[i4] = tri.n3;
				this.newColors[i2] = tri.c1;
				this.newColors[i3] = tri.c2;
				this.newColors[i4] = tri.c3;
				this.newUV3s[i2] = tri.uv1;
				this.newUV3s[i3] = tri.uv2;
				this.newUV3s[i4] = tri.uv3;
				this.newTris.Write<int>(i2);
				this.newTris.Write<int>(i3);
				this.newTris.Write<int>(i4);
			}
			this.newTris.EndForEachIndex();
			this.triArray.EndForEachIndex();
		}

		// Token: 0x040002BF RID: 703
		[ReadOnly]
		public NativeStream.Reader triArray;

		// Token: 0x040002C0 RID: 704
		[NativeDisableContainerSafetyRestriction]
		[WriteOnly]
		public NativeArray<float3> newVerts;

		// Token: 0x040002C1 RID: 705
		[NativeDisableContainerSafetyRestriction]
		[WriteOnly]
		public NativeArray<float3> newNormals;

		// Token: 0x040002C2 RID: 706
		[NativeDisableContainerSafetyRestriction]
		[WriteOnly]
		public NativeArray<float4> newColors;

		// Token: 0x040002C3 RID: 707
		[NativeDisableContainerSafetyRestriction]
		[WriteOnly]
		public NativeArray<float2> newUV3s;

		// Token: 0x040002C4 RID: 708
		[WriteOnly]
		public NativeStream.Writer newTris;

		// Token: 0x040002C5 RID: 709
		[ReadOnly]
		public NativeHashMap<float3, int> storedVertTris;

		// Token: 0x040002C6 RID: 710
		public int count;

		// Token: 0x040002C7 RID: 711
		public int interlockedCount;
	}
}
