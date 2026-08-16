using System;
using System.Threading;
using Unity.Collections;
using Unity.Jobs;

namespace Mirage.Subdivision
{
	// Token: 0x02000063 RID: 99
	public struct ReadMeshTriangleDataJob : IJobParallelFor
	{
		// Token: 0x060002DD RID: 733 RVA: 0x00016D60 File Offset: 0x00014F60
		public unsafe void Execute(int index)
		{
			int items = this.newTris.BeginForEachIndex(index);
			for (int i = 0; i < items; i += 3)
			{
				int zone = Interlocked.Add(ref InterlockedCounters.triangleReadbackCounters[this.uniqueIndex], 3);
				this.outputTris[zone] = *this.newTris.Read<int>();
				this.outputTris[zone + 1] = *this.newTris.Read<int>();
				this.outputTris[zone + 2] = *this.newTris.Read<int>();
			}
			this.newTris.EndForEachIndex();
		}

		// Token: 0x040002C8 RID: 712
		[ReadOnly]
		public NativeStream.Reader newTris;

		// Token: 0x040002C9 RID: 713
		[WriteOnly]
		[NativeDisableParallelForRestriction]
		public NativeArray<int> outputTris;

		// Token: 0x040002CA RID: 714
		[ReadOnly]
		public int uniqueIndex;
	}
}
