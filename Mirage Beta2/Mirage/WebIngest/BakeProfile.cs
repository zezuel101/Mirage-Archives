using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Mirage.WebIngest
{
	/// <summary>
	/// Where a bake's wall-clock actually goes, split NETWORK vs CPU.
	///
	/// This exists because the two have opposite fixes and the top-line rate cannot tell them apart. A bake slot
	/// is held for (fetch + CPU), so both inflate the same number: at ~1.3 tiles/s, "2 concurrent bakes x ~35
	/// fetches through a 6-wide pipe at ~130 ms" and "the per-texel reprojection is slow" predict the SAME
	/// throughput. Raising concurrency fixes the first and does nothing for the second; parallelising the texel
	/// loops does the reverse.
	///
	/// Deliberately allocation-free and lock-free (Interlocked on longs): bakes run concurrently on the thread
	/// pool, and a profiler that contends would measure itself.
	///
	/// Unity-free, like everything else here, so tools/ArchivePacker links it — a phase breakdown is as useful
	/// over a scripted offline bake as it is in flight.
	/// </summary>
	// Token: 0x02000008 RID: 8
	public static class BakeProfile
	{
		// Token: 0x06000036 RID: 54 RVA: 0x00002941 File Offset: 0x00000B41
		public static void AddCut(long t)
		{
			Interlocked.Add(ref BakeProfile.s_CutTicks, t);
		}

		// Token: 0x06000037 RID: 55 RVA: 0x0000294F File Offset: 0x00000B4F
		public static void AddDemFetch(long t)
		{
			Interlocked.Add(ref BakeProfile.s_DemFetchTicks, t);
		}

		// Token: 0x06000038 RID: 56 RVA: 0x0000295D File Offset: 0x00000B5D
		public static void AddDemReproject(long t)
		{
			Interlocked.Add(ref BakeProfile.s_DemReprojectTicks, t);
		}

		// Token: 0x06000039 RID: 57 RVA: 0x0000296B File Offset: 0x00000B6B
		public static void AddHeightQuant(long t)
		{
			Interlocked.Add(ref BakeProfile.s_HeightQuantTicks, t);
		}

		// Token: 0x0600003A RID: 58 RVA: 0x00002979 File Offset: 0x00000B79
		public static void AddNormal(long t)
		{
			Interlocked.Add(ref BakeProfile.s_NormalTicks, t);
		}

		// Token: 0x0600003B RID: 59 RVA: 0x00002987 File Offset: 0x00000B87
		public static void AddColorFetch(long t)
		{
			Interlocked.Add(ref BakeProfile.s_ColorFetchTicks, t);
		}

		// Token: 0x0600003C RID: 60 RVA: 0x00002995 File Offset: 0x00000B95
		public static void AddColorReproject(long t)
		{
			Interlocked.Add(ref BakeProfile.s_ColorReprojectTicks, t);
		}

		// Token: 0x0600003D RID: 61 RVA: 0x000029A3 File Offset: 0x00000BA3
		public static void AddColorEncode(long t)
		{
			Interlocked.Add(ref BakeProfile.s_ColorEncodeTicks, t);
		}

		// Token: 0x0600003E RID: 62 RVA: 0x000029B1 File Offset: 0x00000BB1
		public static void AddCommitEncode(long t)
		{
			Interlocked.Add(ref BakeProfile.s_CommitEncodeTicks, t);
		}

		// Token: 0x0600003F RID: 63 RVA: 0x000029BF File Offset: 0x00000BBF
		public static void AddDecode(long t)
		{
			Interlocked.Add(ref BakeProfile.s_DecodeTicks, t);
		}

		// Token: 0x06000040 RID: 64 RVA: 0x000029CD File Offset: 0x00000BCD
		public static void AddBathyFetch(long t)
		{
			Interlocked.Add(ref BakeProfile.s_BathyFetchTicks, t);
		}

		// Token: 0x06000041 RID: 65 RVA: 0x000029DB File Offset: 0x00000BDB
		public static void AddBathyFill(long t)
		{
			Interlocked.Add(ref BakeProfile.s_BathyFillTicks, t);
		}

		// Token: 0x06000042 RID: 66 RVA: 0x000029E9 File Offset: 0x00000BE9
		public static void AddWorldCover(long t)
		{
			Interlocked.Add(ref BakeProfile.s_WorldCoverTicks, t);
		}

		// Token: 0x06000043 RID: 67 RVA: 0x000029F7 File Offset: 0x00000BF7
		public static void CountTile()
		{
			Interlocked.Increment(ref BakeProfile.s_Tiles);
		}

		/// <summary>Start a stopwatch. Callers pair this with one of the Add* methods; kept explicit rather than
		/// hidden in an IDisposable so the phase boundaries are visible at the call site.</summary>
		// Token: 0x06000044 RID: 68 RVA: 0x00002A04 File Offset: 0x00000C04
		public static Stopwatch Start()
		{
			return Stopwatch.StartNew();
		}

		// Token: 0x17000011 RID: 17
		// (get) Token: 0x06000045 RID: 69 RVA: 0x00002A0B File Offset: 0x00000C0B
		public static long Tiles
		{
			get
			{
				return Interlocked.Read(ref BakeProfile.s_Tiles);
			}
		}

		/// <summary>
		/// Mean milliseconds per baked tile, per phase. These are SUMS over concurrent bakes divided by tiles, so
		/// they are per-tile cost, not wall-clock — a phase reading 400 ms with 2 bakes in flight is ~200 ms of
		/// elapsed time. That is the right number here: it is what one bake slot pays.
		/// </summary>
		// Token: 0x06000046 RID: 70 RVA: 0x00002A18 File Offset: 0x00000C18
		public static string Report()
		{
			BakeProfile.<>c__DisplayClass31_0 CS$<>8__locals1;
			CS$<>8__locals1.n = Interlocked.Read(ref BakeProfile.s_Tiles);
			bool flag = CS$<>8__locals1.n <= 0L;
			string result;
			if (flag)
			{
				result = "  bake[no tiles yet]";
			}
			else
			{
				double net = BakeProfile.<Report>g__ms|31_0(BakeProfile.s_DemFetchTicks, ref CS$<>8__locals1) + BakeProfile.<Report>g__ms|31_0(BakeProfile.s_ColorFetchTicks, ref CS$<>8__locals1) + BakeProfile.<Report>g__ms|31_0(BakeProfile.s_BathyFetchTicks, ref CS$<>8__locals1) + BakeProfile.<Report>g__ms|31_0(BakeProfile.s_WorldCoverTicks, ref CS$<>8__locals1);
				double cpu = BakeProfile.<Report>g__ms|31_0(BakeProfile.s_CutTicks, ref CS$<>8__locals1) + BakeProfile.<Report>g__ms|31_0(BakeProfile.s_DecodeTicks, ref CS$<>8__locals1) + BakeProfile.<Report>g__ms|31_0(BakeProfile.s_DemReprojectTicks, ref CS$<>8__locals1) + BakeProfile.<Report>g__ms|31_0(BakeProfile.s_HeightQuantTicks, ref CS$<>8__locals1) + BakeProfile.<Report>g__ms|31_0(BakeProfile.s_NormalTicks, ref CS$<>8__locals1) + BakeProfile.<Report>g__ms|31_0(BakeProfile.s_ColorReprojectTicks, ref CS$<>8__locals1) + BakeProfile.<Report>g__ms|31_0(BakeProfile.s_ColorEncodeTicks, ref CS$<>8__locals1) + BakeProfile.<Report>g__ms|31_0(BakeProfile.s_CommitEncodeTicks, ref CS$<>8__locals1) + BakeProfile.<Report>g__ms|31_0(BakeProfile.s_BathyFillTicks, ref CS$<>8__locals1);
				result = string.Concat(new string[]
				{
					string.Format("  bake/tile[net={0:F0}ms (dem={1:F0} col={2:F0} ", net, BakeProfile.<Report>g__ms|31_0(BakeProfile.s_DemFetchTicks, ref CS$<>8__locals1), BakeProfile.<Report>g__ms|31_0(BakeProfile.s_ColorFetchTicks, ref CS$<>8__locals1)),
					string.Format("bathy={0:F0} wc={1:F0}) ", BakeProfile.<Report>g__ms|31_0(BakeProfile.s_BathyFetchTicks, ref CS$<>8__locals1), BakeProfile.<Report>g__ms|31_0(BakeProfile.s_WorldCoverTicks, ref CS$<>8__locals1)),
					string.Format("cpu={0:F0}ms (cut={1:F0} decode={2:F0} ", cpu, BakeProfile.<Report>g__ms|31_0(BakeProfile.s_CutTicks, ref CS$<>8__locals1), BakeProfile.<Report>g__ms|31_0(BakeProfile.s_DecodeTicks, ref CS$<>8__locals1)),
					string.Format("demReproj={0:F0} hQuant={1:F0} ", BakeProfile.<Report>g__ms|31_0(BakeProfile.s_DemReprojectTicks, ref CS$<>8__locals1), BakeProfile.<Report>g__ms|31_0(BakeProfile.s_HeightQuantTicks, ref CS$<>8__locals1)),
					string.Format("normal={0:F0} colReproj={1:F0} ", BakeProfile.<Report>g__ms|31_0(BakeProfile.s_NormalTicks, ref CS$<>8__locals1), BakeProfile.<Report>g__ms|31_0(BakeProfile.s_ColorReprojectTicks, ref CS$<>8__locals1)),
					string.Format("bc7={0:F0} commitEnc={1:F0} ", BakeProfile.<Report>g__ms|31_0(BakeProfile.s_ColorEncodeTicks, ref CS$<>8__locals1), BakeProfile.<Report>g__ms|31_0(BakeProfile.s_CommitEncodeTicks, ref CS$<>8__locals1)),
					string.Format("bathyFill={0:F0}) n={1}]", BakeProfile.<Report>g__ms|31_0(BakeProfile.s_BathyFillTicks, ref CS$<>8__locals1), CS$<>8__locals1.n)
				});
			}
			return result;
		}

		// Token: 0x06000047 RID: 71 RVA: 0x00002C60 File Offset: 0x00000E60
		public static void Reset()
		{
			Interlocked.Exchange(ref BakeProfile.s_CutTicks, 0L);
			Interlocked.Exchange(ref BakeProfile.s_DemFetchTicks, 0L);
			Interlocked.Exchange(ref BakeProfile.s_DemReprojectTicks, 0L);
			Interlocked.Exchange(ref BakeProfile.s_HeightQuantTicks, 0L);
			Interlocked.Exchange(ref BakeProfile.s_NormalTicks, 0L);
			Interlocked.Exchange(ref BakeProfile.s_ColorFetchTicks, 0L);
			Interlocked.Exchange(ref BakeProfile.s_ColorReprojectTicks, 0L);
			Interlocked.Exchange(ref BakeProfile.s_ColorEncodeTicks, 0L);
			Interlocked.Exchange(ref BakeProfile.s_CommitEncodeTicks, 0L);
			Interlocked.Exchange(ref BakeProfile.s_DecodeTicks, 0L);
			Interlocked.Exchange(ref BakeProfile.s_BathyFetchTicks, 0L);
			Interlocked.Exchange(ref BakeProfile.s_BathyFillTicks, 0L);
			Interlocked.Exchange(ref BakeProfile.s_WorldCoverTicks, 0L);
			Interlocked.Exchange(ref BakeProfile.s_Tiles, 0L);
		}

		// Token: 0x06000048 RID: 72 RVA: 0x00002D24 File Offset: 0x00000F24
		[CompilerGenerated]
		internal static double <Report>g__ms|31_0(long ticks, ref BakeProfile.<>c__DisplayClass31_0 A_1)
		{
			return (double)ticks * 1000.0 / (double)Stopwatch.Frequency / (double)A_1.n;
		}

		// Token: 0x04000026 RID: 38
		private static long s_CutTicks;

		// Token: 0x04000027 RID: 39
		private static long s_DemFetchTicks;

		// Token: 0x04000028 RID: 40
		private static long s_DemReprojectTicks;

		// Token: 0x04000029 RID: 41
		private static long s_HeightQuantTicks;

		// Token: 0x0400002A RID: 42
		private static long s_NormalTicks;

		// Token: 0x0400002B RID: 43
		private static long s_ColorFetchTicks;

		// Token: 0x0400002C RID: 44
		private static long s_ColorReprojectTicks;

		// Token: 0x0400002D RID: 45
		private static long s_ColorEncodeTicks;

		// Token: 0x0400002E RID: 46
		private static long s_CommitEncodeTicks;

		// Token: 0x0400002F RID: 47
		private static long s_DecodeTicks;

		// Token: 0x04000030 RID: 48
		private static long s_BathyFetchTicks;

		// Token: 0x04000031 RID: 49
		private static long s_BathyFillTicks;

		// Token: 0x04000032 RID: 50
		private static long s_WorldCoverTicks;

		// Token: 0x04000033 RID: 51
		private static long s_Tiles;
	}
}
