using System;
using System.Collections.Generic;

namespace Mirage.Subdivision
{
	// Token: 0x0200005C RID: 92
	public static class InterlockedCounters
	{
		// Token: 0x060002B7 RID: 695 RVA: 0x000150E0 File Offset: 0x000132E0
		static InterlockedCounters()
		{
			InterlockedCounters.Reset();
		}

		// Token: 0x060002B8 RID: 696 RVA: 0x00015104 File Offset: 0x00013304
		private static void Reset()
		{
			InterlockedCounters.available.Clear();
			for (int i = 0; i < 1024; i++)
			{
				InterlockedCounters.triangleReadbackCounters[i] = -3;
				InterlockedCounters.available.Enqueue(i);
			}
		}

		// Token: 0x060002B9 RID: 697 RVA: 0x00015148 File Offset: 0x00013348
		public static void ResetCounter(int id)
		{
			InterlockedCounters.triangleReadbackCounters[id] = -3;
		}

		// Token: 0x060002BA RID: 698 RVA: 0x00015154 File Offset: 0x00013354
		public static int Request()
		{
			bool flag = InterlockedCounters.available.Count == 0;
			int result;
			if (flag)
			{
				MirageDebug.LogError("[Mirage] InterlockedCounters exhausted — too many quads subdividing simultaneously (max 1024). Returning slot 0; subdivision may glitch.");
				result = 0;
			}
			else
			{
				result = InterlockedCounters.available.Dequeue();
			}
			return result;
		}

		// Token: 0x060002BB RID: 699 RVA: 0x00015191 File Offset: 0x00013391
		public static void Return(int id)
		{
			InterlockedCounters.triangleReadbackCounters[id] = -3;
			InterlockedCounters.available.Enqueue(id);
		}

		// Token: 0x04000281 RID: 641
		public static readonly int[] triangleReadbackCounters = new int[1024];

		// Token: 0x04000282 RID: 642
		private static readonly Queue<int> available = new Queue<int>();
	}
}
