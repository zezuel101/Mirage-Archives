using System;
using System.Collections.Generic;

namespace Mirage.Subdivision
{
	// Token: 0x02000051 RID: 81
	public static class InterlockedCounters
	{
		// Token: 0x06000252 RID: 594 RVA: 0x00013EC0 File Offset: 0x000120C0
		static InterlockedCounters()
		{
			InterlockedCounters.Reset();
		}

		// Token: 0x06000253 RID: 595 RVA: 0x00013EE4 File Offset: 0x000120E4
		private static void Reset()
		{
			InterlockedCounters.available.Clear();
			for (int i = 0; i < 1024; i++)
			{
				InterlockedCounters.triangleReadbackCounters[i] = -3;
				InterlockedCounters.available.Enqueue(i);
			}
		}

		// Token: 0x06000254 RID: 596 RVA: 0x00013F28 File Offset: 0x00012128
		public static void ResetCounter(int id)
		{
			InterlockedCounters.triangleReadbackCounters[id] = -3;
		}

		// Token: 0x06000255 RID: 597 RVA: 0x00013F34 File Offset: 0x00012134
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

		// Token: 0x06000256 RID: 598 RVA: 0x00013F71 File Offset: 0x00012171
		public static void Return(int id)
		{
			InterlockedCounters.triangleReadbackCounters[id] = -3;
			InterlockedCounters.available.Enqueue(id);
		}

		// Token: 0x04000216 RID: 534
		public static readonly int[] triangleReadbackCounters = new int[1024];

		// Token: 0x04000217 RID: 535
		private static readonly Queue<int> available = new Queue<int>();
	}
}
