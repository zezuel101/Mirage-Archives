using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Mirage.WebIngest
{
	/// <summary>
	/// Samples ESA WorldCover land-cover class by lat/lon for one cube tile's footprint. The water-mask source
	/// (WebIngest §4): class 80 = permanent water is authoritative wherever WorldCover has data, so a coastline
	/// no longer depends on the DEM's ocean fill (3DEP/GLO-30 fill oceans with &gt;0 values that a height threshold
	/// cannot mask). The DEM sea-level term is kept only for the deep-ocean NoData gap the caller resolves.
	///
	/// <b>Shape.</b> <see cref="M:Mirage.WebIngest.WorldCoverSource.PrepareAsync(System.Double,System.Double,System.Double,System.Double,System.Double,System.Threading.CancellationToken)" /> is the one network step: it fetches the COG window(s) covering the
	/// cube tile's lat/lon box — usually one 3° cell, up to a handful at a cell boundary — at the overview matched
	/// to the tile's texel size (so a coarse cube tile reads a small overview, not 36000² full res). Then
	/// <see cref="M:Mirage.WebIngest.WorldCoverSource.ClassAt(System.Double,System.Double)" /> is a pure in-memory lookup the bake calls per texel, exactly as
	/// <c>FillBathymetry</c> samples its GMRT grid. A 3° cell with no file (ocean-only areas have none) simply
	/// contributes no data — those texels read NoData (0) and fall through to the sea-level term.
	///
	/// Unity-free; <c>tools/ArchivePacker</c> drives it over HTTP.
	/// </summary>
	// Token: 0x02000034 RID: 52
	public sealed class WorldCoverSource
	{
		/// <summary>Cells that actually resolved to data this prepare — for logging ("did WorldCover cover this
		/// tile at all?").</summary>
		// Token: 0x1700003C RID: 60
		// (get) Token: 0x0600013A RID: 314 RVA: 0x0000A73E File Offset: 0x0000893E
		// (set) Token: 0x0600013B RID: 315 RVA: 0x0000A746 File Offset: 0x00008946
		public int LoadedCells { get; private set; }

		// Token: 0x0600013C RID: 316 RVA: 0x0000A750 File Offset: 0x00008950
		public WorldCoverSource(WorldCoverSource.RangeFetch fetch, string baseUrl, string productPrefix)
		{
			if (fetch == null)
			{
				throw new ArgumentNullException("fetch");
			}
			this.fetch = fetch;
			this.baseUrl = (baseUrl ?? "").TrimEnd(new char[]
			{
				'/'
			});
			this.productPrefix = (productPrefix ?? "");
		}

		/// <summary>Fetch the class windows covering <c>[west,east] × [south,north]</c> at the overview whose
		/// pixel is closest to <paramref name="targetMetresPerTexel" />. Idempotent — replaces any prior load.</summary>
		// Token: 0x0600013D RID: 317 RVA: 0x0000A7B8 File Offset: 0x000089B8
		[DebuggerStepThrough]
		public Task PrepareAsync(double west, double east, double south, double north, double targetMetresPerTexel, CancellationToken ct)
		{
			WorldCoverSource.<PrepareAsync>d__12 <PrepareAsync>d__ = new WorldCoverSource.<PrepareAsync>d__12();
			<PrepareAsync>d__.<>t__builder = AsyncTaskMethodBuilder.Create();
			<PrepareAsync>d__.<>4__this = this;
			<PrepareAsync>d__.west = west;
			<PrepareAsync>d__.east = east;
			<PrepareAsync>d__.south = south;
			<PrepareAsync>d__.north = north;
			<PrepareAsync>d__.targetMetresPerTexel = targetMetresPerTexel;
			<PrepareAsync>d__.ct = ct;
			<PrepareAsync>d__.<>1__state = -1;
			<PrepareAsync>d__.<>t__builder.Start<WorldCoverSource.<PrepareAsync>d__12>(ref <PrepareAsync>d__);
			return <PrepareAsync>d__.<>t__builder.Task;
		}

		// Token: 0x0600013E RID: 318 RVA: 0x0000A82C File Offset: 0x00008A2C
		[DebuggerStepThrough]
		private Task<WorldCoverSource.Cell> LoadCellAsync(int swLat, int swLon, double west, double east, double south, double north, double targetMetresPerTexel, CancellationToken ct)
		{
			WorldCoverSource.<LoadCellAsync>d__13 <LoadCellAsync>d__ = new WorldCoverSource.<LoadCellAsync>d__13();
			<LoadCellAsync>d__.<>t__builder = AsyncTaskMethodBuilder<WorldCoverSource.Cell>.Create();
			<LoadCellAsync>d__.<>4__this = this;
			<LoadCellAsync>d__.swLat = swLat;
			<LoadCellAsync>d__.swLon = swLon;
			<LoadCellAsync>d__.west = west;
			<LoadCellAsync>d__.east = east;
			<LoadCellAsync>d__.south = south;
			<LoadCellAsync>d__.north = north;
			<LoadCellAsync>d__.targetMetresPerTexel = targetMetresPerTexel;
			<LoadCellAsync>d__.ct = ct;
			<LoadCellAsync>d__.<>1__state = -1;
			<LoadCellAsync>d__.<>t__builder.Start<WorldCoverSource.<LoadCellAsync>d__13>(ref <LoadCellAsync>d__);
			return <LoadCellAsync>d__.<>t__builder.Task;
		}

		/// <summary>Land-cover class at lat/lon, or 0 (NoData) if no loaded cell covers it.</summary>
		// Token: 0x0600013F RID: 319 RVA: 0x0000A8B0 File Offset: 0x00008AB0
		public byte ClassAt(double lat, double lon)
		{
			int deg = 3;
			for (int i = 0; i < this.cells.Count; i++)
			{
				WorldCoverSource.Cell c = this.cells[i];
				bool flag = c.Cls == null;
				if (!flag)
				{
					bool flag2 = lon < (double)c.SwLon || lon >= (double)(c.SwLon + deg) || lat < (double)c.SwLat || lat >= (double)(c.SwLat + deg);
					if (!flag2)
					{
						int col = (int)((lon - (double)c.SwLon) / c.Scale) - c.Px0;
						int row = (int)(((double)(c.SwLat + deg) - lat) / c.Scale) - c.Py0;
						bool flag3 = col < 0 || col >= c.W || row < 0 || row >= c.H;
						if (!flag3)
						{
							return c.Cls[row * c.W + col];
						}
					}
				}
			}
			return 0;
		}

		/// <summary>True if WorldCover maps lat/lon as permanent water (class 80).</summary>
		// Token: 0x06000140 RID: 320 RVA: 0x0000A9BA File Offset: 0x00008BBA
		public bool IsWaterAt(double lat, double lon)
		{
			return WorldCoverGrid.IsWater(this.ClassAt(lat, lon));
		}

		// Token: 0x06000141 RID: 321 RVA: 0x0000A9C9 File Offset: 0x00008BC9
		private static int Clamp(int v, int lo, int hi)
		{
			return (v < lo) ? lo : ((v > hi) ? hi : v);
		}

		// Token: 0x0400010E RID: 270
		private const double MetresPerDegree = 111320.0;

		// Token: 0x0400010F RID: 271
		private readonly WorldCoverSource.RangeFetch fetch;

		// Token: 0x04000110 RID: 272
		private readonly string baseUrl;

		// Token: 0x04000111 RID: 273
		private readonly string productPrefix;

		// Token: 0x04000112 RID: 274
		private readonly List<WorldCoverSource.Cell> cells = new List<WorldCoverSource.Cell>();

		/// <summary>Fetch a byte range from a WorldCover COG by URL. Null result = the tile does not exist (404) —
		/// an ocean-only 3° cell has no file, which is not an error.</summary>
		// Token: 0x020000AE RID: 174
		// (Invoke) Token: 0x060004B8 RID: 1208
		public delegate Task<byte[]> RangeFetch(string url, long from, long toInclusive, CancellationToken ct);

		// Token: 0x020000AF RID: 175
		private sealed class Cell
		{
			// Token: 0x04000475 RID: 1141
			public int SwLat;

			// Token: 0x04000476 RID: 1142
			public int SwLon;

			// Token: 0x04000477 RID: 1143
			public double Scale;

			// Token: 0x04000478 RID: 1144
			public int Px0;

			// Token: 0x04000479 RID: 1145
			public int Py0;

			// Token: 0x0400047A RID: 1146
			public int W;

			// Token: 0x0400047B RID: 1147
			public int H;

			// Token: 0x0400047C RID: 1148
			public byte[] Cls;
		}
	}
}
