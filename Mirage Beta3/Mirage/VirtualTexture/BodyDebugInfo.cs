using System;

namespace Mirage.VirtualTexture
{
	/// <summary>One body's streaming state for the Alt+F12 debug screen.</summary>
	// Token: 0x02000056 RID: 86
	public struct BodyDebugInfo
	{
		// Token: 0x04000212 RID: 530
		public string sphereName;

		// Token: 0x04000213 RID: 531
		public int slots;

		// Token: 0x04000214 RID: 532
		public int total;

		// Token: 0x04000215 RID: 533
		public int[] levelCounts;

		// Token: 0x04000216 RID: 534
		public int queue;

		// Token: 0x04000217 RID: 535
		public int flight;

		// Token: 0x04000218 RID: 536
		public int loading;

		// Token: 0x04000219 RID: 537
		public int completed;

		// Token: 0x0400021A RID: 538
		public int missing;

		// Token: 0x0400021B RID: 539
		public int tilesRequested;

		// Token: 0x0400021C RID: 540
		public int tilesLoaded;

		// Token: 0x0400021D RID: 541
		public int desync;

		// Token: 0x0400021E RID: 542
		public int badIndirection;

		// Token: 0x0400021F RID: 543
		public int blocks;

		// Token: 0x04000220 RID: 544
		public int totalBlocks;

		// Token: 0x04000221 RID: 545
		public int dirLevel;

		// Token: 0x04000222 RID: 546
		public int maxLevel;

		// Token: 0x04000223 RID: 547
		public bool hasIngest;

		// Token: 0x04000224 RID: 548
		public int ingestPending;

		// Token: 0x04000225 RID: 549
		public int ingestActive;

		// Token: 0x04000226 RID: 550
		public int ingestBaked;

		// Token: 0x04000227 RID: 551
		public int ingestNoCoverage;

		// Token: 0x04000228 RID: 552
		public int ingestFailed;

		// Token: 0x04000229 RID: 553
		public long webPhysicalBytes;

		// Token: 0x0400022A RID: 554
		public long webLiveBytes;

		// Token: 0x0400022B RID: 555
		public long webCapBytes;

		// Token: 0x0400022C RID: 556
		public int webEvicted;

		// Token: 0x0400022D RID: 557
		public bool webCapTooSmall;
	}
}
