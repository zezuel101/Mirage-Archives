using System;
using System.Threading;
using Unity.Collections;
using Unity.Jobs;

namespace Mirage.Subdivision
{
	// Token: 0x02000058 RID: 88
	public struct ReadMeshTriangleDataJob : IJobParallelFor
	{
		// Token: 0x06000278 RID: 632 RVA: 0x00015B40 File Offset: 0x00013D40
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

		// Token: 0x0400025D RID: 605
		[ReadOnly]
		public NativeStream.Reader newTris;

		// Token: 0x0400025E RID: 606
		[WriteOnly]
		[NativeDisableParallelForRestriction]
		public NativeArray<int> outputTris;

		// Token: 0x0400025F RID: 607
		[ReadOnly]
		public int uniqueIndex;
	}
}
