using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Mirage.WebIngest
{
	/// <summary>
	/// A minimal reader for the exact Cloud-Optimized GeoTIFF flavour ESA WorldCover ships: classic
	/// little-endian TIFF (not BigTIFF), 8-bit single band, Deflate (compression 8), predictor 1 (none), tiled,
	/// with an overview IFD chain. It is deliberately NOT a general GeoTIFF reader — it assumes precisely that
	/// layout and throws on anything else, the same discipline as the baseline-only JPEG decoder.
	///
	/// <b>Range-friendly by construction (the point of COG).</b> <see cref="M:Mirage.WebIngest.CogReader.OpenAsync(Mirage.WebIngest.CogReader.RangeFetch,System.Threading.CancellationToken)" /> fetches only a small
	/// header block; a window read then fetches only the internal tiles it needs. The full-res band of a 3°
	/// WorldCover tile is 36000² px, so reading whole files would be absurd — a cube tile touches a handful of
	/// 1024² internal tiles at a matched overview.
	///
	/// <b>Pixel space only — no geo.</b> Georeferencing is left to the caller (<c>WorldCoverSource</c>), which
	/// knows each 3° tile's bounds from its name and maps lat/lon → pixel. Keeping this class geo-free makes it a
	/// plain tiled-raster reader that <c>tools/TestBench</c> can drive over HTTP against the live bucket.
	///
	/// Unity-free, like the rest of WebIngest.
	/// </summary>
	// Token: 0x02000032 RID: 50
	public sealed class CogReader
	{
		/// <summary>Resolution levels, full-res first, overviews following. Never empty.</summary>
		// Token: 0x1700003B RID: 59
		// (get) Token: 0x06000130 RID: 304 RVA: 0x00009F81 File Offset: 0x00008181
		public IReadOnlyList<CogReader.Level> Levels { get; }

		// Token: 0x06000131 RID: 305 RVA: 0x00009F89 File Offset: 0x00008189
		private CogReader(CogReader.RangeFetch fetch, List<CogReader.Level> levels)
		{
			this.fetch = fetch;
			this.Levels = levels;
		}

		/// <summary>Fetch and parse the COG header (all IFDs in the chain), validating the assumed layout.
		/// Throws <see cref="T:Mirage.WebIngest.CogFormatException" /> if the file is not the supported flavour.</summary>
		// Token: 0x06000132 RID: 306 RVA: 0x00009FA4 File Offset: 0x000081A4
		[DebuggerStepThrough]
		public static Task<CogReader> OpenAsync(CogReader.RangeFetch fetch, CancellationToken ct)
		{
			CogReader.<OpenAsync>d__20 <OpenAsync>d__ = new CogReader.<OpenAsync>d__20();
			<OpenAsync>d__.<>t__builder = AsyncTaskMethodBuilder<CogReader>.Create();
			<OpenAsync>d__.fetch = fetch;
			<OpenAsync>d__.ct = ct;
			<OpenAsync>d__.<>1__state = -1;
			<OpenAsync>d__.<>t__builder.Start<CogReader.<OpenAsync>d__20>(ref <OpenAsync>d__);
			return <OpenAsync>d__.<>t__builder.Task;
		}

		/// <summary>Pick the level whose resolution is closest to (but not finer than) <paramref name="targetDownsample" />
		/// full-res pixels per output sample — so a coarse cube tile reads a small overview instead of full res.
		/// Clamped to the coarsest available level.</summary>
		// Token: 0x06000133 RID: 307 RVA: 0x00009FF0 File Offset: 0x000081F0
		public int SelectLevel(double targetDownsample)
		{
			int best = 0;
			for (int i = 0; i < this.Levels.Count; i++)
			{
				bool flag = (double)this.Levels[i].Downsample <= targetDownsample + 1E-06;
				if (!flag)
				{
					break;
				}
				best = i;
			}
			return best;
		}

		/// <summary>Read a pixel-space window of class bytes from <paramref name="levelIndex" /> into a fresh
		/// <c>byte[w*h]</c>, row-major. Pixels outside the image are left 0. Fetches and inflates only the internal
		/// tiles the window overlaps.</summary>
		// Token: 0x06000134 RID: 308 RVA: 0x0000A050 File Offset: 0x00008250
		[DebuggerStepThrough]
		public Task<byte[]> ReadWindowAsync(int levelIndex, int px0, int py0, int w, int h, CancellationToken ct)
		{
			CogReader.<ReadWindowAsync>d__22 <ReadWindowAsync>d__ = new CogReader.<ReadWindowAsync>d__22();
			<ReadWindowAsync>d__.<>t__builder = AsyncTaskMethodBuilder<byte[]>.Create();
			<ReadWindowAsync>d__.<>4__this = this;
			<ReadWindowAsync>d__.levelIndex = levelIndex;
			<ReadWindowAsync>d__.px0 = px0;
			<ReadWindowAsync>d__.py0 = py0;
			<ReadWindowAsync>d__.w = w;
			<ReadWindowAsync>d__.h = h;
			<ReadWindowAsync>d__.ct = ct;
			<ReadWindowAsync>d__.<>1__state = -1;
			<ReadWindowAsync>d__.<>t__builder.Start<CogReader.<ReadWindowAsync>d__22>(ref <ReadWindowAsync>d__);
			return <ReadWindowAsync>d__.<>t__builder.Task;
		}

		/// <summary>Fetch and inflate one internal tile to its <c>TileW*TileH</c> class raster. Predictor is 1
		/// (none), so the inflated bytes are the pixel values directly.</summary>
		// Token: 0x06000135 RID: 309 RVA: 0x0000A0C4 File Offset: 0x000082C4
		[DebuggerStepThrough]
		private Task<byte[]> ReadTileAsync(CogReader.Level lvl, int idx, CancellationToken ct)
		{
			CogReader.<ReadTileAsync>d__23 <ReadTileAsync>d__ = new CogReader.<ReadTileAsync>d__23();
			<ReadTileAsync>d__.<>t__builder = AsyncTaskMethodBuilder<byte[]>.Create();
			<ReadTileAsync>d__.<>4__this = this;
			<ReadTileAsync>d__.lvl = lvl;
			<ReadTileAsync>d__.idx = idx;
			<ReadTileAsync>d__.ct = ct;
			<ReadTileAsync>d__.<>1__state = -1;
			<ReadTileAsync>d__.<>t__builder.Start<CogReader.<ReadTileAsync>d__23>(ref <ReadTileAsync>d__);
			return <ReadTileAsync>d__.<>t__builder.Task;
		}

		// Token: 0x06000136 RID: 310 RVA: 0x0000A120 File Offset: 0x00008320
		private static CogReader.Level ParseIfd(byte[] b, int ifd)
		{
			int i = CogReader.ReadU16(b, ifd);
			int width = 0;
			int height = 0;
			int tileW = 0;
			int tileH = 0;
			int bits = 0;
			int samples = 1;
			int comp = 0;
			int predictor = 1;
			int offArrPos = 0;
			int cntArrPos = 0;
			int ntiles = 0;
			for (int j = 0; j < i; j++)
			{
				int e = ifd + 2 + j * 12;
				int tag = CogReader.ReadU16(b, e);
				int type = CogReader.ReadU16(b, e + 2);
				int count = (int)CogReader.ReadU32(b, e + 4);
				long val = (long)((ulong)CogReader.ReadU32(b, e + 8));
				int num = tag;
				int num2 = num;
				switch (num2)
				{
				case 256:
					width = (int)val;
					break;
				case 257:
					height = (int)val;
					break;
				case 258:
					bits = (int)val;
					break;
				case 259:
					comp = (int)val;
					break;
				default:
					if (num2 != 277)
					{
						switch (num2)
						{
						case 317:
							predictor = (int)val;
							break;
						case 322:
							tileW = (int)val;
							break;
						case 323:
							tileH = (int)val;
							break;
						case 324:
							ntiles = count;
							offArrPos = ((count == 1) ? (e + 8) : ((int)val));
							break;
						case 325:
							cntArrPos = ((count == 1) ? (e + 8) : ((int)val));
							break;
						}
					}
					else
					{
						samples = (int)val;
					}
					break;
				}
			}
			bool flag = width == 0 || height == 0 || tileW == 0 || tileH == 0 || ntiles == 0;
			CogReader.Level result;
			if (flag)
			{
				result = null;
			}
			else
			{
				bool flag2 = bits != 8 || samples != 1;
				if (flag2)
				{
					throw new CogFormatException(string.Format("unsupported COG sample layout: bits={0} samples={1}.", bits, samples));
				}
				bool flag3 = comp != 8;
				if (flag3)
				{
					throw new CogFormatException(string.Format("unsupported COG compression {0} (only Deflate/8).", comp));
				}
				bool flag4 = predictor != 1;
				if (flag4)
				{
					throw new CogFormatException(string.Format("unsupported COG predictor {0} (only 1/none).", predictor));
				}
				int tpr = (width + tileW - 1) / tileW;
				int tpc = (height + tileH - 1) / tileH;
				bool flag5 = (long)tpr * (long)tpc != (long)ntiles;
				if (flag5)
				{
					throw new CogFormatException(string.Format("tile count {0} disagrees with grid {1}x{2} for {3}x{4}.", new object[]
					{
						ntiles,
						tpr,
						tpc,
						width,
						height
					}));
				}
				bool flag6 = offArrPos + ntiles * 4 > b.Length || cntArrPos + ntiles * 4 > b.Length;
				if (flag6)
				{
					throw new CogFormatException("COG tile-offset arrays lie beyond the fetched header block.");
				}
				CogReader.Level lvl = new CogReader.Level
				{
					Width = width,
					Height = height,
					TileW = tileW,
					TileH = tileH,
					TilesPerRow = tpr,
					TilesPerCol = tpc,
					TileOffsets = new long[ntiles],
					TileByteCounts = new uint[ntiles]
				};
				for (int k = 0; k < ntiles; k++)
				{
					lvl.TileOffsets[k] = (long)((ulong)CogReader.ReadU32(b, offArrPos + k * 4));
					lvl.TileByteCounts[k] = CogReader.ReadU32(b, cntArrPos + k * 4);
				}
				result = lvl;
			}
			return result;
		}

		// Token: 0x06000137 RID: 311 RVA: 0x0000A454 File Offset: 0x00008654
		private static int ReadU16(byte[] b, int o)
		{
			return (int)b[o] | (int)b[o + 1] << 8;
		}

		// Token: 0x06000138 RID: 312 RVA: 0x0000A461 File Offset: 0x00008661
		private static uint ReadU32(byte[] b, int o)
		{
			return (uint)((int)b[o] | (int)b[o + 1] << 8 | (int)b[o + 2] << 16 | (int)b[o + 3] << 24);
		}

		/// <summary>Inflate a TIFF Deflate strip/tile — a zlib stream (2-byte header), same shape as PNG IDAT, so
		/// the header is skipped by hand and the raw DEFLATE handed to <see cref="T:System.IO.Compression.DeflateStream" />.</summary>
		// Token: 0x06000139 RID: 313 RVA: 0x0000A480 File Offset: 0x00008680
		private static byte[] Inflate(byte[] zlib, int expected)
		{
			byte[] outp = new byte[expected];
			byte[] result;
			using (MemoryStream src = new MemoryStream(zlib, 2, zlib.Length - 2))
			{
				using (DeflateStream inf = new DeflateStream(src, CompressionMode.Decompress))
				{
					int read = 0;
					int i;
					while (read < expected && (i = inf.Read(outp, read, expected - read)) > 0)
					{
						read += i;
					}
					result = outp;
				}
			}
			return result;
		}

		// Token: 0x040000F3 RID: 243
		private const int TShort = 3;

		// Token: 0x040000F4 RID: 244
		private const int TLong = 4;

		// Token: 0x040000F5 RID: 245
		private const int TagImageWidth = 256;

		// Token: 0x040000F6 RID: 246
		private const int TagImageLength = 257;

		// Token: 0x040000F7 RID: 247
		private const int TagBitsPerSample = 258;

		// Token: 0x040000F8 RID: 248
		private const int TagCompression = 259;

		// Token: 0x040000F9 RID: 249
		private const int TagSamplesPerPixel = 277;

		// Token: 0x040000FA RID: 250
		private const int TagPredictor = 317;

		// Token: 0x040000FB RID: 251
		private const int TagTileWidth = 322;

		// Token: 0x040000FC RID: 252
		private const int TagTileLength = 323;

		// Token: 0x040000FD RID: 253
		private const int TagTileOffsets = 324;

		// Token: 0x040000FE RID: 254
		private const int TagTileByteCounts = 325;

		// Token: 0x040000FF RID: 255
		private const int CompressionDeflate = 8;

		// Token: 0x04000100 RID: 256
		private readonly CogReader.RangeFetch fetch;

		/// <summary>Fetch bytes <c>[from..toInclusive]</c> from the backing file. Callable from any thread.</summary>
		// Token: 0x020000BE RID: 190
		// (Invoke) Token: 0x0600048F RID: 1167
		public delegate Task<byte[]> RangeFetch(long from, long toInclusive, CancellationToken ct);

		/// <summary>One resolution level: the full-res image (index 0) or an overview. Only the fields this reader
		/// needs — the raster is single-band 8-bit, so bits/samples are validated then dropped.</summary>
		// Token: 0x020000BF RID: 191
		public sealed class Level
		{
			// Token: 0x040004DC RID: 1244
			public int Width;

			// Token: 0x040004DD RID: 1245
			public int Height;

			// Token: 0x040004DE RID: 1246
			public int TileW;

			// Token: 0x040004DF RID: 1247
			public int TileH;

			// Token: 0x040004E0 RID: 1248
			public int TilesPerRow;

			// Token: 0x040004E1 RID: 1249
			public int TilesPerCol;

			// Token: 0x040004E2 RID: 1250
			public long[] TileOffsets;

			// Token: 0x040004E3 RID: 1251
			public uint[] TileByteCounts;

			/// <summary>Downsample factor vs the full-res level (level 0 → 1, its half-res overview → 2, …).
			/// Derived from the width ratio, used to pick the overview matching a target resolution.</summary>
			// Token: 0x040004E4 RID: 1252
			public int Downsample;
		}
	}
}
