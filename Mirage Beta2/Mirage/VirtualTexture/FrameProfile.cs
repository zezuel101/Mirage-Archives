using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Mirage.WebIngest;

namespace Mirage.VirtualTexture
{
	/// <summary>
	/// Main-thread cost per streamer phase, tracked by MAX rather than mean — for hunting frame spikes.
	///
	/// Distinct from <see cref="T:Mirage.WebIngest.BakeProfile" />, which measures a bake's per-tile cost on worker
	/// threads where nothing can hitch a frame. This measures what runs inside <see cref="M:Mirage.VirtualTexture.TileStreamingManager.Update(System.Int32)" />,
	/// which is where 80 ms lands on the user.
	///
	/// <b>Max is the statistic, not the mean.</b> A spike is by definition rare — "worst 1% frames" — so an
	/// average over hundreds of frames buries it: a phase averaging 0.2 ms with a max of 60 ms is the entire
	/// answer, and the mean alone reports it as free. Counts come along because "cheap but called 400x" and
	/// "blocks once" are different bugs with different fixes.
	///
	/// Mirrors the phases the existing ProfilerMarkers already wrap, so the log line and a profiler timeline
	/// name the same things. Both exist because KSP ships as a release player and the Unity Profiler is not
	/// always attachable — a number in KSP.log needs no tooling.
	///
	/// Main-thread only, so no interlocks: <c>Update</c> and the phases it wraps are all one thread, and adding
	/// synchronisation would perturb what is being measured.
	/// </summary>
	// Token: 0x0200003A RID: 58
	public static class FrameProfile
	{
		// Token: 0x0600016A RID: 362 RVA: 0x0000BB01 File Offset: 0x00009D01
		public static Stopwatch Start()
		{
			return Stopwatch.StartNew();
		}

		// Token: 0x0600016B RID: 363 RVA: 0x0000BB08 File Offset: 0x00009D08
		private static void Add(ref FrameProfile.Phase p, long t)
		{
			p.ticks += t;
			p.calls++;
			bool flag = t > p.maxTicks;
			if (flag)
			{
				p.maxTicks = t;
			}
		}

		// Token: 0x0600016C RID: 364 RVA: 0x0000BB40 File Offset: 0x00009D40
		public static void AddLeaves(long t)
		{
			FrameProfile.Add(ref FrameProfile.s_Leaves, t);
		}

		// Token: 0x0600016D RID: 365 RVA: 0x0000BB4E File Offset: 0x00009D4E
		public static void AddLevelCtx(long t)
		{
			FrameProfile.Add(ref FrameProfile.s_LevelCtx, t);
		}

		// Token: 0x0600016E RID: 366 RVA: 0x0000BB5C File Offset: 0x00009D5C
		public static void AddMetrics(long t)
		{
			FrameProfile.Add(ref FrameProfile.s_Metrics, t);
		}

		// Token: 0x0600016F RID: 367 RVA: 0x0000BB6A File Offset: 0x00009D6A
		public static void AddCollect(long t)
		{
			FrameProfile.Add(ref FrameProfile.s_Collect, t);
		}

		// Token: 0x06000170 RID: 368 RVA: 0x0000BB78 File Offset: 0x00009D78
		public static void AddLru(long t)
		{
			FrameProfile.Add(ref FrameProfile.s_Lru, t);
		}

		// Token: 0x06000171 RID: 369 RVA: 0x0000BB86 File Offset: 0x00009D86
		public static void AddQueues(long t)
		{
			FrameProfile.Add(ref FrameProfile.s_Queues, t);
		}

		// Token: 0x06000172 RID: 370 RVA: 0x0000BB94 File Offset: 0x00009D94
		public static void AddStartLoads(long t)
		{
			FrameProfile.Add(ref FrameProfile.s_StartLoads, t);
		}

		// Token: 0x06000173 RID: 371 RVA: 0x0000BBA2 File Offset: 0x00009DA2
		public static void AddDrain(long t)
		{
			FrameProfile.Add(ref FrameProfile.s_Drain, t);
		}

		// Token: 0x06000174 RID: 372 RVA: 0x0000BBB0 File Offset: 0x00009DB0
		public static void AddGetTex(long t)
		{
			FrameProfile.Add(ref FrameProfile.s_GetTex, t);
		}

		// Token: 0x06000175 RID: 373 RVA: 0x0000BBBE File Offset: 0x00009DBE
		public static void AddUpload(long t)
		{
			FrameProfile.Add(ref FrameProfile.s_Upload, t);
		}

		// Token: 0x06000176 RID: 374 RVA: 0x0000BBCC File Offset: 0x00009DCC
		public static void AddBlit(long t)
		{
			FrameProfile.Add(ref FrameProfile.s_Blit, t);
		}

		// Token: 0x06000177 RID: 375 RVA: 0x0000BBDA File Offset: 0x00009DDA
		public static void AddPaint(long t)
		{
			FrameProfile.Add(ref FrameProfile.s_Paint, t);
		}

		// Token: 0x06000178 RID: 376 RVA: 0x0000BBE8 File Offset: 0x00009DE8
		public static void AddDispose(long t)
		{
			FrameProfile.Add(ref FrameProfile.s_Dispose, t);
		}

		// Token: 0x06000179 RID: 377 RVA: 0x0000BBF6 File Offset: 0x00009DF6
		public static void AddApplyPage(long t)
		{
			FrameProfile.Add(ref FrameProfile.s_ApplyPage, t);
		}

		// Token: 0x0600017A RID: 378 RVA: 0x0000BC04 File Offset: 0x00009E04
		public static void AddCommit(long t)
		{
			FrameProfile.Add(ref FrameProfile.s_Commit, t);
		}

		// Token: 0x0600017B RID: 379 RVA: 0x0000BC12 File Offset: 0x00009E12
		public static void AddTileLoad(long t)
		{
			FrameProfile.Add(ref FrameProfile.s_TileLoad, t);
		}

		// Token: 0x0600017C RID: 380 RVA: 0x0000BC20 File Offset: 0x00009E20
		public static void AddPump(long t)
		{
			FrameProfile.Add(ref FrameProfile.s_Pump, t);
		}

		// Token: 0x0600017D RID: 381 RVA: 0x0000BC2E File Offset: 0x00009E2E
		public static void AddIngest(long t)
		{
			FrameProfile.Add(ref FrameProfile.s_Ingest, t);
		}

		/// <summary>Sample the real frame interval (Time.unscaledDeltaTime, in ms). Called once per frame from
		/// the streamer's Update — the one place guaranteed to run every frame while a body is active.</summary>
		// Token: 0x0600017E RID: 382 RVA: 0x0000BC3C File Offset: 0x00009E3C
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

		/// <summary>Record ingest concurrency for this frame; the report keeps the interval's peak.</summary>
		// Token: 0x0600017F RID: 383 RVA: 0x0000BC74 File Offset: 0x00009E74
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

		// Token: 0x06000180 RID: 384 RVA: 0x0000BCB8 File Offset: 0x00009EB8
		private static string Fmt(string name, in FrameProfile.Phase p)
		{
			bool flag = p.calls == 0;
			string result;
			if (flag)
			{
				result = name + "[-]";
			}
			else
			{
				result = string.Format("{0}[n={1} avg={2:F2} max={3:F1}]", new object[]
				{
					name,
					p.calls,
					FrameProfile.<Fmt>g__ms|51_0(p.ticks) / (double)p.calls,
					FrameProfile.<Fmt>g__ms|51_0(p.maxTicks)
				});
			}
			return result;
		}

		// Token: 0x06000181 RID: 385 RVA: 0x0000BD36 File Offset: 0x00009F36
		private static double Ms(long t)
		{
			return (double)t * 1000.0 / (double)Stopwatch.Frequency;
		}

		/// <summary>Mean ms/frame this interval across the TOP-LEVEL Update phases — the nested ones (·getTex,
		/// ··blit, commit …) are already inside their parents and would double-count. Excludes tileLoad, which
		/// runs from PQS's build callback rather than from Update.
		///
		/// This is the number to subtract from <c>frame</c>. What's left is everything Mirage does NOT do on the
		/// main thread: KSP itself (the 60 fps baseline), plus — if it grows when ingest is enabled — the bake
		/// threads competing for cores.</summary>
		// Token: 0x06000182 RID: 386 RVA: 0x0000BD4C File Offset: 0x00009F4C
		private static double MineMs()
		{
			bool flag = FrameProfile.s_FrameCount == 0;
			double result;
			if (flag)
			{
				result = 0.0;
			}
			else
			{
				long t = FrameProfile.s_Leaves.ticks + FrameProfile.s_LevelCtx.ticks + FrameProfile.s_Collect.ticks + FrameProfile.s_Lru.ticks + FrameProfile.s_Queues.ticks + FrameProfile.s_StartLoads.ticks + FrameProfile.s_Drain.ticks + FrameProfile.s_ApplyPage.ticks + FrameProfile.s_Pump.ticks + FrameProfile.s_Ingest.ticks + FrameProfile.s_Metrics.ticks;
				result = FrameProfile.Ms(t) / (double)FrameProfile.s_FrameCount;
			}
			return result;
		}

		/// <summary>Cumulative since the last <see cref="M:Mirage.VirtualTexture.FrameProfile.Reset" />; report and reset together so each line
		/// describes only the interval it covers — a max never decays otherwise, and one early hitch would
		/// haunt every subsequent line.</summary>
		// Token: 0x06000183 RID: 387 RVA: 0x0000BE00 File Offset: 0x0000A000
		public static string Report()
		{
			return string.Concat(new string[]
			{
				"  frame(ms) ",
				(FrameProfile.s_FrameCount == 0) ? "frame[-]" : (string.Format("frame[n={0} avg={1:F1} max={2:F1} ", FrameProfile.s_FrameCount, FrameProfile.s_FrameMs / (double)FrameProfile.s_FrameCount, FrameProfile.s_FrameMaxMs) + string.Format("mine={0:F2} other={1:F1}]", FrameProfile.MineMs(), FrameProfile.s_FrameMs / (double)FrameProfile.s_FrameCount - FrameProfile.MineMs())),
				string.Format(" cpu[bake={0} dl={1} dlq={2} cores={3}] ", new object[]
				{
					FrameProfile.s_MaxBakes,
					FrameProfile.s_MaxDownloads,
					FrameProfile.s_MaxDlQueued,
					Environment.ProcessorCount
				}),
				FrameProfile.Fmt("leaves", FrameProfile.s_Leaves),
				" ",
				FrameProfile.Fmt("levelCtx", FrameProfile.s_LevelCtx),
				" ",
				FrameProfile.Fmt("collect", FrameProfile.s_Collect),
				" ",
				FrameProfile.Fmt("lru", FrameProfile.s_Lru),
				" ",
				FrameProfile.Fmt("queues", FrameProfile.s_Queues),
				" ",
				FrameProfile.Fmt("startLoads", FrameProfile.s_StartLoads),
				" ",
				FrameProfile.Fmt("drain", FrameProfile.s_Drain),
				" ",
				FrameProfile.Fmt("·getTex", FrameProfile.s_GetTex),
				" ",
				FrameProfile.Fmt("·upload", FrameProfile.s_Upload),
				" ",
				FrameProfile.Fmt("··blit", FrameProfile.s_Blit),
				" ",
				FrameProfile.Fmt("··paint", FrameProfile.s_Paint),
				" ",
				FrameProfile.Fmt("·dispose", FrameProfile.s_Dispose),
				" ",
				FrameProfile.Fmt("applyPage", FrameProfile.s_ApplyPage),
				" ",
				FrameProfile.Fmt("pump", FrameProfile.s_Pump),
				" ",
				FrameProfile.Fmt("ingest", FrameProfile.s_Ingest),
				" ",
				FrameProfile.Fmt("·commit", FrameProfile.s_Commit),
				" ",
				FrameProfile.Fmt("metrics", FrameProfile.s_Metrics),
				" ",
				FrameProfile.Fmt("tileLoad", FrameProfile.s_TileLoad),
				string.Format(" gc[0:{0} 1:{1} ", GC.CollectionCount(0) - FrameProfile.s_Gc0, GC.CollectionCount(1) - FrameProfile.s_Gc1),
				string.Format("2:{0} heap={1}MB ", GC.CollectionCount(2) - FrameProfile.s_Gc2, GC.GetTotalMemory(false) / 1048576L),
				string.Format("pool={0}MB]", BufferPool.PooledBytes / 1048576L)
			});
		}

		// Token: 0x06000184 RID: 388 RVA: 0x0000C144 File Offset: 0x0000A344
		public static void Reset()
		{
			FrameProfile.s_Leaves = default(FrameProfile.Phase);
			FrameProfile.s_LevelCtx = default(FrameProfile.Phase);
			FrameProfile.s_Collect = default(FrameProfile.Phase);
			FrameProfile.s_Lru = default(FrameProfile.Phase);
			FrameProfile.s_Queues = default(FrameProfile.Phase);
			FrameProfile.s_StartLoads = default(FrameProfile.Phase);
			FrameProfile.s_Drain = default(FrameProfile.Phase);
			FrameProfile.s_GetTex = default(FrameProfile.Phase);
			FrameProfile.s_Upload = default(FrameProfile.Phase);
			FrameProfile.s_Blit = default(FrameProfile.Phase);
			FrameProfile.s_Paint = default(FrameProfile.Phase);
			FrameProfile.s_Dispose = default(FrameProfile.Phase);
			FrameProfile.s_ApplyPage = default(FrameProfile.Phase);
			FrameProfile.s_Commit = default(FrameProfile.Phase);
			FrameProfile.s_TileLoad = default(FrameProfile.Phase);
			FrameProfile.s_Pump = default(FrameProfile.Phase);
			FrameProfile.s_Ingest = default(FrameProfile.Phase);
			FrameProfile.s_Metrics = default(FrameProfile.Phase);
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

		/// <summary>Heap at the last reset — paired with the live figure in <see cref="M:Mirage.VirtualTexture.FrameProfile.Report" />, the delta says
		/// whether the interval GREW the heap (a leak, or the collector losing ground) or merely churned it.</summary>
		// Token: 0x17000044 RID: 68
		// (get) Token: 0x06000185 RID: 389 RVA: 0x0000C278 File Offset: 0x0000A478
		public static long HeapAtResetBytes
		{
			get
			{
				return FrameProfile.s_HeapAtReset;
			}
		}

		// Token: 0x06000186 RID: 390 RVA: 0x0000C27F File Offset: 0x0000A47F
		[CompilerGenerated]
		internal static double <Fmt>g__ms|51_0(long t)
		{
			return (double)t * 1000.0 / (double)Stopwatch.Frequency;
		}

		// Token: 0x0400012F RID: 303
		private static FrameProfile.Phase s_Leaves;

		// Token: 0x04000130 RID: 304
		private static FrameProfile.Phase s_LevelCtx;

		// Token: 0x04000131 RID: 305
		private static FrameProfile.Phase s_Collect;

		// Token: 0x04000132 RID: 306
		private static FrameProfile.Phase s_Lru;

		// Token: 0x04000133 RID: 307
		private static FrameProfile.Phase s_Queues;

		// Token: 0x04000134 RID: 308
		private static FrameProfile.Phase s_StartLoads;

		// Token: 0x04000135 RID: 309
		private static FrameProfile.Phase s_Drain;

		// Token: 0x04000136 RID: 310
		private static FrameProfile.Phase s_GetTex;

		// Token: 0x04000137 RID: 311
		private static FrameProfile.Phase s_Upload;

		// Token: 0x04000138 RID: 312
		private static FrameProfile.Phase s_Blit;

		// Token: 0x04000139 RID: 313
		private static FrameProfile.Phase s_Paint;

		// Token: 0x0400013A RID: 314
		private static FrameProfile.Phase s_Dispose;

		// Token: 0x0400013B RID: 315
		private static FrameProfile.Phase s_ApplyPage;

		// Token: 0x0400013C RID: 316
		private static FrameProfile.Phase s_Commit;

		// Token: 0x0400013D RID: 317
		private static FrameProfile.Phase s_TileLoad;

		// Token: 0x0400013E RID: 318
		private static FrameProfile.Phase s_Metrics;

		// Token: 0x0400013F RID: 319
		private static FrameProfile.Phase s_Pump;

		// Token: 0x04000140 RID: 320
		private static FrameProfile.Phase s_Ingest;

		// Token: 0x04000141 RID: 321
		private static int s_Gc0;

		// Token: 0x04000142 RID: 322
		private static int s_Gc1;

		// Token: 0x04000143 RID: 323
		private static int s_Gc2;

		// Token: 0x04000144 RID: 324
		private static long s_HeapAtReset;

		// Token: 0x04000145 RID: 325
		private static double s_FrameMs;

		// Token: 0x04000146 RID: 326
		private static double s_FrameMaxMs;

		// Token: 0x04000147 RID: 327
		private static int s_FrameCount;

		// Token: 0x04000148 RID: 328
		private static int s_MaxBakes;

		// Token: 0x04000149 RID: 329
		private static int s_MaxDownloads;

		// Token: 0x0400014A RID: 330
		private static int s_MaxDlQueued;

		// Token: 0x020000B5 RID: 181
		public struct Phase
		{
			// Token: 0x040004BE RID: 1214
			public long ticks;

			// Token: 0x040004BF RID: 1215
			public long maxTicks;

			// Token: 0x040004C0 RID: 1216
			public int calls;
		}
	}
}
