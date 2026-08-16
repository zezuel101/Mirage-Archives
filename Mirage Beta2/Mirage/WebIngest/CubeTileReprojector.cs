using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Mirage.WebIngest
{
	/// <summary>
	/// Reprojects Web-Mercator source imagery into ONE Mirage cube tile. WebIngest §4, the cube↔mercator
	/// boundary the doc's risk summary calls out as separating "streams beautifully" from "stutters and
	/// corrupts above 150km".
	///
	/// The per-texel chain, all of it exact double math:
	///   corrected tile texel → corrected UV → RAW face UV (UncorrectFaceUV)
	///     → direction (measured face basis) → lat/lon → mercator UV → global mercator pixel
	///     → Mitchell-Netravali 4x4 sample
	///
	/// Correctness rests on <see cref="F:Mirage.WebIngest.MirageCubeMath.FaceU" /> being MEASURED rather than assumed — a wrong
	/// in-face orientation reprojects a rotated or mirrored planet, which looks like plausible terrain until it
	/// disagrees with the heightmap under it.
	///
	/// Channel-agnostic (see <see cref="T:Mirage.WebIngest.MercatorGather" />): the colour bake runs 3 channels, and the identical
	/// path reprojects a 1-channel DEM, which is what lets the whole thing be validated end-to-end against an
	/// independent elevation source instead of by eye.
	///
	/// Managed and correctness-first, mirroring §6's sequencing for the decoder (which then turned out fast
	/// enough that Burst was unnecessary). The math here is pure and allocation-free per texel, so Burst-ifying
	/// later is mechanical if a profile ever asks for it.
	/// </summary>
	// Token: 0x02000015 RID: 21
	public static class CubeTileReprojector
	{
		/// <summary>
		/// Every mercator tile needed to bake this cube tile, at the fixed zoom for its level.
		///
		/// Walks the output SLOT (border included — §9's border over-fetches neighbouring imagery so bilinear
		/// filtering across a tile edge stays seamless) on a stride, and expands each hit by its 8 neighbours so
		/// the 4x4 kernel's taps can never reach a tile that wasn't fetched. The stride is safe because at the
		/// resolution-matched zoom (Z = L+2) a cube texel and a mercator texel are within ~2x of each other, so
		/// a stride of a few texels cannot skip an entire 256px tile; the neighbour expansion covers the rest.
		/// </summary>
		// Token: 0x0600008E RID: 142 RVA: 0x00005CB8 File Offset: 0x00003EB8
		[return: TupleElementNames(new string[]
		{
			"x",
			"y"
		})]
		public static List<ValueTuple<int, int>> RequiredTiles(int face, int level, int tx, int ty, int tileSize, int borderPx, int zoom)
		{
			int slot = tileSize + 2 * borderPx;
			int i = 1 << zoom;
			HashSet<long> set = new HashSet<long>();
			for (int py = 0; py < slot; py += 4)
			{
				for (int px = 0; px < slot; px += 4)
				{
					double lat;
					double lon;
					MirageCubeMath.TileTexelToLatLon(face, level, tx, ty, (double)px, (double)py, tileSize, borderPx, out lat, out lon);
					bool flag = !MercatorTileMath.HasWebCoverage(lat);
					if (!flag)
					{
						ValueTuple<double, double> valueTuple = MercatorTileMath.LatLonToMercatorUV(lat, lon);
						double mu = valueTuple.Item1;
						double mv = valueTuple.Item2;
						int mx = (int)Math.Floor(mu * (double)i);
						int my = (int)Math.Floor(mv * (double)i);
						for (int dy = -1; dy <= 1; dy++)
						{
							for (int dx = -1; dx <= 1; dx++)
							{
								int qx = ((mx + dx) % i + i) % i;
								int qy = my + dy;
								bool flag2 = qy < 0 || qy >= i;
								if (!flag2)
								{
									set.Add((long)qx << 32 | (long)((ulong)qy));
								}
							}
						}
					}
				}
			}
			List<ValueTuple<int, int>> outp = new List<ValueTuple<int, int>>(set.Count);
			foreach (long j in set)
			{
				outp.Add(new ValueTuple<int, int>((int)(j >> 32), (int)((uint)j)));
			}
			return outp;
		}

		/// <summary>
		/// Reproject <paramref name="gather" /> into one cube tile's slot.
		/// <paramref name="dst" /> receives <c>slot*slot*channels</c> floats, row-major, channel-interleaved,
		/// with texel (0,0) at corrected-UV origin — the same layout the canonical tiles use.
		///
		/// Returns <see cref="F:Mirage.WebIngest.ReprojectOutcome.Incomplete" /> the moment any texel has no source, and leaves
		/// <paramref name="dst" /> untrusted: a partial tile must be discarded, never written.
		/// </summary>
		// Token: 0x0600008F RID: 143 RVA: 0x00005E4C File Offset: 0x0000404C
		public static ReprojectOutcome Reproject(int face, int level, int tx, int ty, int tileSize, int borderPx, MercatorGather gather, float[] dst)
		{
			int slot = tileSize + 2 * borderPx;
			int ch = gather.Channels;
			bool flag = dst.Length < slot * slot * ch;
			if (flag)
			{
				throw new ArgumentException(string.Format("reproject: dst has {0} floats, need {1}", dst.Length, slot * slot * ch));
			}
			int i = 1 << gather.Zoom;
			double worldPx = (double)i * 256.0;
			int incomplete = 0;
			Parallel.For<ValueTuple<float[], float[], double[]>>(0, slot, BakeScheduler.Options, () => new ValueTuple<float[], float[], double[]>(new float[ch], new float[ch], new double[4]), delegate(int y, ParallelLoopState loopState, [TupleElementNames(new string[]
			{
				"px",
				"tap",
				"row"
			})] ValueTuple<float[], float[], double[]> s)
			{
				int x = 0;
				while (x < slot)
				{
					double lat;
					double lon;
					MirageCubeMath.TileTexelToLatLon(face, level, tx, ty, (double)x, (double)y, tileSize, borderPx, out lat, out lon);
					bool flag2 = !MercatorTileMath.HasWebCoverage(lat);
					ValueTuple<float[], float[], double[]> result;
					if (flag2)
					{
						Interlocked.Exchange(ref incomplete, 1);
						loopState.Stop();
						result = s;
					}
					else
					{
						ValueTuple<double, double> valueTuple = MercatorTileMath.LatLonToMercatorUV(lat, lon);
						double mu = valueTuple.Item1;
						double mv = valueTuple.Item2;
						bool flag3 = !gather.TrySample(mu * worldPx, mv * worldPx, s.Item1, s.Item2, s.Item3);
						if (!flag3)
						{
							int o = (y * slot + x) * ch;
							for (int c = 0; c < ch; c++)
							{
								dst[o + c] = s.Item1[c];
							}
							x++;
							continue;
						}
						Interlocked.Exchange(ref incomplete, 1);
						loopState.Stop();
						result = s;
					}
					return result;
				}
				return s;
			}, delegate([TupleElementNames(new string[]
			{
				"px",
				"tap",
				"row"
			})] ValueTuple<float[], float[], double[]> _)
			{
			});
			return (incomplete != 0) ? ReprojectOutcome.Incomplete : ReprojectOutcome.Complete;
		}
	}
}
