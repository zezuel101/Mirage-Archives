using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Mirage.Subdivision
{
	// Token: 0x02000061 RID: 97
	[BurstCompile]
	public struct RemoveVertexPairsJob : IJob
	{
		// Token: 0x060002DB RID: 731 RVA: 0x00016A78 File Offset: 0x00014C78
		public unsafe void Execute()
		{
			for (int index = 0; index < this.foreachCount; index++)
			{
				int items = this.triReader.BeginForEachIndex(index);
				for (int i = 0; i < items; i++)
				{
					SubdividableTriangle val = *this.triReader.Read<SubdividableTriangle>();
					bool flag = this.vertices.TryAdd(val.v1 * 0.001f, this.count);
					if (flag)
					{
						this.count++;
					}
					bool flag2 = this.vertices.TryAdd(val.v2 * 0.001f, this.count);
					if (flag2)
					{
						this.count++;
					}
					bool flag3 = this.vertices.TryAdd(val.v3 * 0.001f, this.count);
					if (flag3)
					{
						this.count++;
					}
				}
				this.triReader.EndForEachIndex();
			}
		}

		// Token: 0x040002BB RID: 699
		[ReadOnly]
		public NativeStream.Reader triReader;

		// Token: 0x040002BC RID: 700
		[WriteOnly]
		public NativeHashMap<float3, int> vertices;

		// Token: 0x040002BD RID: 701
		[ReadOnly]
		public int foreachCount;

		// Token: 0x040002BE RID: 702
		public int count;
	}
}
