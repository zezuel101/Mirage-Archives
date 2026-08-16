using System;
using System.Diagnostics;
using System.Text;
using Mirage.WebIngest;

namespace Mirage.VirtualTexture
{
	/// <summary>Main-thread cost per streamer phase over a logging interval, tracked by max not mean.</summary>
	// Token: 0x0200004F RID: 79
	public static class FrameProfile
	{
		// Token: 0x060001E2 RID: 482 RVA: 0x0000DFD9 File Offset: 0x0000C1D9
		public static FrameProfile.Timer Start()
		{
			return FrameProfile.Timer.StartNew();
		}

		// Token: 0x060001E3 RID: 483 RVA: 0x0000DFE0 File Offset: 0x0000C1E0
		public static void Add(ProfilePhase phase, long ticks)
		{
			ref FrameProfile.PhaseStats p = ref FrameProfile.s_Phases[(int)phase];
			p.ticks += ticks;
			p.calls++;
			bool flag = ticks > p.maxTicks;
			if (flag)
			{
				p.maxTicks = ticks;
			}
		}

		// Token: 0x060001E4 RID: 484 RVA: 0x0000E024 File Offset: 0x0000C224
		public static void AddFrameTime(double ms)
		{
			FrameProfile.s_FrameMs += ms;
			FrameProfile.s_FrameCount++;
			bool flag = ms > FrameProfile.s_FrameMaxMs;
			if (flag)
			{
				FrameProfile.s_FrameMaxMs = ms;
			}
		}

		// Token: 0x060001E5 RID: 485 RVA: 0x0000E05C File Offset: 0x0000C25C
		public static void NoteIngest(int bakes, int downloads, int downloadsQueued)
		{
			bool flag = bakes > FrameProfile.s_MaxBakes;
			if (flag)
			{
				FrameProfile.s_MaxBakes = bakes;
			}
			bool flag2 = downloads > FrameProfile.s_MaxDownloads;
			if (flag2)
			{
				FrameProfile.s_MaxDownloads = downloads;
			}
			bool flag3 = downloadsQueued > FrameProfile.s_MaxDlQueued;
			if (flag3)
			{
				FrameProfile.s_MaxDlQueued = downloadsQueued;
			}
		}

		/// <summary>Everything measured since the last Reset, as one log line.</summary>
		// Token: 0x060001E6 RID: 486 RVA: 0x0000E0A0 File Offset: 0x0000C2A0
		public static string Report()
		{
			StringBuilder sb = new StringBuilder(1024);
			sb.Append("  frame(ms) ").Append(FrameProfile.FormatFrame()).Append(' ').Append(FrameProfile.FormatCpu());
			for (int i = 0; i < FrameProfile.s_Phases.Length; i++)
			{
				sb.Append(' ').Append(FrameProfile.Format(FrameProfile.Label(i), FrameProfile.s_Phases[i]));
			}
			return sb.Append(' ').Append(FrameProfile.FormatMemory()).ToString();
		}

		// Token: 0x060001E7 RID: 487 RVA: 0x0000E138 File Offset: 0x0000C338
		public static void Reset()
		{
			Array.Clear(FrameProfile.s_Phases, 0, FrameProfile.s_Phases.Length);
			FrameProfile.s_FrameMs = 0.0;
			FrameProfile.s_FrameMaxMs = 0.0;
			FrameProfile.s_FrameCount = 0;
			FrameProfile.s_MaxBakes = 0;
			FrameProfile.s_MaxDownloads = 0;
			FrameProfile.s_MaxDlQueued = 0;
			FrameProfile.s_Gc0 = GC.CollectionCount(0);
			FrameProfile.s_Gc1 = GC.CollectionCount(1);
			FrameProfile.s_Gc2 = GC.CollectionCount(2);
			FrameProfile.s_HeapAtReset = GC.GetTotalMemory(false);
		}

		// Token: 0x060001E8 RID: 488 RVA: 0x0000E1B9 File Offset: 0x0000C3B9
		private static double Ms(long ticks)
		{
			return (double)ticks * 1000.0 / (double)Stopwatch.Frequency;
		}

		// Token: 0x060001E9 RID: 489 RVA: 0x0000E1D0 File Offset: 0x0000C3D0
		private static string Label(int phase)
		{
			string result;
			if (phase >= FrameProfile.s_Labels.Length)
			{
				ProfilePhase profilePhase = (ProfilePhase)phase;
				result = profilePhase.ToString();
			}
			else
			{
				result = FrameProfile.s_Labels[phase];
			}
			return result;
		}

		// Token: 0x060001EA RID: 490 RVA: 0x0000E200 File Offset: 0x0000C400
		private static string Format(string label, in FrameProfile.PhaseStats p)
		{
			return (p.calls == 0) ? (label + "[-]") : string.Format("{0}[n={1} avg={2:F2} max={3:F1}]", new object[]
			{
				label,
				p.calls,
				FrameProfile.Ms(p.ticks) / (double)p.calls,
				FrameProfile.Ms(p.maxTicks)
			});
		}

		// Token: 0x060001EB RID: 491 RVA: 0x0000E274 File Offset: 0x0000C474
		private static string FormatFrame()
		{
			bool flag = FrameProfile.s_FrameCount == 0;
			string result;
			if (flag)
			{
				result = "frame[-]";
			}
			else
			{
				long ticks = 0L;
				foreach (ProfilePhase phase in FrameProfile.s_TopLevel)
				{
					ticks += FrameProfile.s_Phases[(int)phase].ticks;
				}
				double mine = FrameProfile.Ms(ticks) / (double)FrameProfile.s_FrameCount;
				double avg = FrameProfile.s_FrameMs / (double)FrameProfile.s_FrameCount;
				result = string.Format("frame[n={0} avg={1:F1} max={2:F1} ", FrameProfile.s_FrameCount, avg, FrameProfile.s_FrameMaxMs) + string.Format("mine={0:F2} other={1:F1}]", mine, avg - mine);
			}
			return result;
		}

		// Token: 0x060001EC RID: 492 RVA: 0x0000E338 File Offset: 0x0000C538
		private static string FormatCpu()
		{
			return string.Format("cpu[bake={0} dl={1} dlq={2} ", FrameProfile.s_MaxBakes, FrameProfile.s_MaxDownloads, FrameProfile.s_MaxDlQueued) + string.Format("cores={0}]", Environment.ProcessorCount);
		}

		// Token: 0x060001ED RID: 493 RVA: 0x0000E388 File Offset: 0x0000C588
		private static string FormatMemory()
		{
			long heap = GC.GetTotalMemory(false);
			return string.Format("gc[0:{0} 1:{1} ", GC.CollectionCount(0) - FrameProfile.s_Gc0, GC.CollectionCount(1) - FrameProfile.s_Gc1) + string.Format("2:{0} heap={1}MB ", GC.CollectionCount(2) - FrameProfile.s_Gc2, heap / 1048576L) + string.Format("grew={0}MB ", (heap - FrameProfile.s_HeapAtReset) / 1048576L) + string.Format("pool={0}MB]", BufferPool.PooledBytes / 1048576L);
		}

		// Token: 0x04000197 RID: 407
		private static readonly string[] s_Labels = new string[]
		{
			"leaves",
			"levelCtx",
			"collect",
			"lru",
			"queues",
			"startLoads",
			"drain",
			"·getTex",
			"·upload",
			"··gpuSync",
			"··blit",
			"··paint",
			"·dispose",
			"applyPage",
			"pump",
			"ingest",
			"·commit",
			"metrics",
			"tileLoad"
		};

		// Token: 0x04000198 RID: 408
		private static readonly ProfilePhase[] s_TopLevel = new ProfilePhase[]
		{
			ProfilePhase.Leaves,
			ProfilePhase.LevelCtx,
			ProfilePhase.Collect,
			ProfilePhase.Lru,
			ProfilePhase.Queues,
			ProfilePhase.StartLoads,
			ProfilePhase.Drain,
			ProfilePhase.ApplyPage,
			ProfilePhase.Pump,
			ProfilePhase.Ingest,
			ProfilePhase.Metrics
		};

		// Token: 0x04000199 RID: 409
		private static readonly FrameProfile.PhaseStats[] s_Phases = new FrameProfile.PhaseStats[Enum.GetValues(typeof(ProfilePhase)).Length];

		// Token: 0x0400019A RID: 410
		private static double s_FrameMs;

		// Token: 0x0400019B RID: 411
		private static double s_FrameMaxMs;

		// Token: 0x0400019C RID: 412
		private static int s_FrameCount;

		// Token: 0x0400019D RID: 413
		private static int s_MaxBakes;

		// Token: 0x0400019E RID: 414
		private static int s_MaxDownloads;

		// Token: 0x0400019F RID: 415
		private static int s_MaxDlQueued;

		// Token: 0x040001A0 RID: 416
		private static int s_Gc0;

		// Token: 0x040001A1 RID: 417
		private static int s_Gc1;

		// Token: 0x040001A2 RID: 418
		private static int s_Gc2;

		// Token: 0x040001A3 RID: 419
		private static long s_HeapAtReset;

		// Token: 0x020000CB RID: 203
		private struct PhaseStats
		{
			// Token: 0x04000567 RID: 1383
			public long ticks;

			// Token: 0x04000568 RID: 1384
			public long maxTicks;

			// Token: 0x04000569 RID: 1385
			public int calls;
		}

		/// <summary>Allocation-free stopwatch — Stopwatch.StartNew() allocates a class per call.</summary>
		// Token: 0x020000CC RID: 204
		public readonly struct Timer
		{
			// Token: 0x060004B0 RID: 1200 RVA: 0x00021DC1 File Offset: 0x0001FFC1
			private Timer(long start)
			{
				this.start = start;
			}

			// Token: 0x060004B1 RID: 1201 RVA: 0x00021DCA File Offset: 0x0001FFCA
			public static FrameProfile.Timer StartNew()
			{
				return new FrameProfile.Timer(Stopwatch.GetTimestamp());
			}

			// Token: 0x170000BB RID: 187
			// (get) Token: 0x060004B2 RID: 1202 RVA: 0x00021DD6 File Offset: 0x0001FFD6
			public long ElapsedTicks
			{
				get
				{
					return Stopwatch.GetTimestamp() - this.start;
				}
			}

			// Token: 0x0400056A RID: 1386
			private readonly long start;
		}
	}
}
