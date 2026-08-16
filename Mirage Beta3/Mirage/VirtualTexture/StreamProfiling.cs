using System;
using Unity.Profiling;

namespace Mirage.VirtualTexture
{
	/// <summary>Profiler handles for the streamer pipeline.</summary>
	// Token: 0x02000051 RID: 81
	internal static class StreamProfiling
	{
		// Token: 0x040001A6 RID: 422
		public static readonly ProfiledPhase Leaves = new ProfiledPhase(ProfilePhase.Leaves, "Mirage.VT.EnumerateLeaves");

		// Token: 0x040001A7 RID: 423
		public static readonly ProfiledPhase LevelContext = new ProfiledPhase(ProfilePhase.LevelCtx, "Mirage.VT.LevelContext");

		// Token: 0x040001A8 RID: 424
		public static readonly ProfiledPhase Collect = new ProfiledPhase(ProfilePhase.Collect, "Mirage.VT.CollectRequired");

		// Token: 0x040001A9 RID: 425
		public static readonly ProfiledPhase RequiredPass = new ProfiledPhase(ProfilePhase.Lru, "Mirage.VT.RequiredPass");

		// Token: 0x040001AA RID: 426
		public static readonly ProfiledPhase SortQueues = new ProfiledPhase(ProfilePhase.Queues, "Mirage.VT.SortQueues");

		// Token: 0x040001AB RID: 427
		public static readonly ProfiledPhase Ingest = new ProfiledPhase(ProfilePhase.Ingest, "Mirage.VT.Ingest");

		// Token: 0x040001AC RID: 428
		public static readonly ProfiledPhase Commit = new ProfiledPhase(ProfilePhase.Commit, "Mirage.VT.Commit");

		// Token: 0x040001AD RID: 429
		public static readonly ProfiledPhase StartLoads = new ProfiledPhase(ProfilePhase.StartLoads, "Mirage.VT.StartLoads");

		// Token: 0x040001AE RID: 430
		public static readonly ProfiledPhase Drain = new ProfiledPhase(ProfilePhase.Drain, "Mirage.VT.DrainInFlight");

		// Token: 0x040001AF RID: 431
		public static readonly ProfiledPhase ApplyPageTable = new ProfiledPhase(ProfilePhase.ApplyPage, "Mirage.VT.ApplyPageTable");

		// Token: 0x040001B0 RID: 432
		public static readonly ProfiledPhase Metrics = new ProfiledPhase(ProfilePhase.Metrics, "Mirage.VT.Metrics");

		// Token: 0x040001B1 RID: 433
		public static readonly ProfiledPhase GetTexture = new ProfiledPhase(ProfilePhase.GetTex, "Mirage.VT.Drain.GetTexture");

		// Token: 0x040001B2 RID: 434
		public static readonly ProfiledPhase Upload = new ProfiledPhase(ProfilePhase.Upload, "Mirage.VT.Drain.Upload");

		// Token: 0x040001B3 RID: 435
		public static readonly ProfiledPhase DisposeHandles = new ProfiledPhase(ProfilePhase.Dispose, "Mirage.VT.Drain.Dispose");

		// Token: 0x040001B4 RID: 436
		public static readonly ProfilerMarker EnforceBudget = new ProfilerMarker("Mirage.VT.EnforceBudget");

		// Token: 0x040001B5 RID: 437
		public static readonly ProfilerMarker BudgetTouch = new ProfilerMarker("Mirage.VT.Lru.BudgetTouch");

		// Token: 0x040001B6 RID: 438
		public static readonly ProfilerMarker TouchBlock = new ProfilerMarker("Mirage.VT.Lru.TouchBlock");

		// Token: 0x040001B7 RID: 439
		public static readonly ProfilerMarker QueueOnDisk = new ProfilerMarker("Mirage.VT.Queue.OnDisk");

		// Token: 0x040001B8 RID: 440
		public static readonly ProfilerMarker BeginLoad = new ProfilerMarker("Mirage.VT.StartLoads.BeginLoad");

		// Token: 0x040001B9 RID: 441
		public static readonly ProfilerMarker PollHandles = new ProfilerMarker("Mirage.VT.Drain.PollHandles");

		// Token: 0x040001BA RID: 442
		public static readonly ProfilerMarker Seeds = new ProfilerMarker("Mirage.VT.Collect.Seeds");

		// Token: 0x040001BB RID: 443
		public static readonly ProfilerMarker SeedDedupe = new ProfilerMarker("Mirage.VT.Collect.SeedDedupe");

		// Token: 0x040001BC RID: 444
		public static readonly ProfilerMarker AncestorWalk = new ProfilerMarker("Mirage.VT.Collect.AncestorWalk");

		// Token: 0x040001BD RID: 445
		public static readonly ProfilerMarker AncestorReplay = new ProfilerMarker("Mirage.VT.Collect.AncestorReplay");

		// Token: 0x040001BE RID: 446
		public static readonly ProfilerMarker Descend = new ProfilerMarker("Mirage.VT.Collect.Descend");

		// Token: 0x040001BF RID: 447
		public static readonly ProfilerMarker SeedCull = new ProfilerMarker("Mirage.VT.Collect.Descend.SeedCull");

		// Token: 0x040001C0 RID: 448
		public static readonly ProfilerMarker DescendWalk = new ProfilerMarker("Mirage.VT.Collect.Descend.Walk");
	}
}
