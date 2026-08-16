using System;
using System.Collections.Generic;
using Mirage.VirtualTexture;

namespace Mirage.WebIngest
{
	/// <summary>
	/// The set of decoded Web-Mercator tiles backing one cube tile's bake, sampled as if it were a single
	/// continuous global image. WebIngest §4's "gather".
	///
	/// <b>Deviation from the doc, deliberately.</b> §4 says to "assemble into one padded contiguous source
	/// buffer" padded by the kernel radius, because "a cube tile straddling 2–4 mercator tiles must be
	/// assembled before the job, or edge resample taps read across a seam into garbage". That risk is real for
	/// a per-tile sampler that clamps at tile edges — but it exists only because of the clamp. This instead
	/// addresses taps in GLOBAL mercator pixel space and resolves each tap to whichever tile owns it, so a tap
	/// that crosses a tile boundary simply lands in the neighbour. There is no seam to read across, and
	/// therefore no padding to get wrong: the doc's bug is designed out rather than compensated for.
	///
	/// It also gets the antimeridian right for free — x wraps modulo the world width, so a gather spanning
	/// lon 180° is not a special case.
	///
	/// (When this is Burst-ified later, the tiles flatten into one contiguous NativeArray + an offset table.
	/// That is a data-layout change; the addressing below is already the right one.)
	///
	/// Channel-agnostic on purpose: colour bakes 3 channels, and the same code reprojects a 1-channel DEM,
	/// which is what lets the reprojection be validated end-to-end against an independent elevation source.
	/// </summary>
	// Token: 0x02000020 RID: 32
	public sealed class MercatorGather
	{
		// Token: 0x17000023 RID: 35
		// (get) Token: 0x060000BA RID: 186 RVA: 0x000074EC File Offset: 0x000056EC
		public int Zoom
		{
			get
			{
				return this.zoom;
			}
		}

		// Token: 0x17000024 RID: 36
		// (get) Token: 0x060000BB RID: 187 RVA: 0x000074F4 File Offset: 0x000056F4
		public int Channels
		{
			get
			{
				return this.channels;
			}
		}

		// Token: 0x17000025 RID: 37
		// (get) Token: 0x060000BC RID: 188 RVA: 0x000074FC File Offset: 0x000056FC
		public int TileCount
		{
			get
			{
				return this.tiles.Count;
			}
		}

		// Token: 0x060000BD RID: 189 RVA: 0x0000750C File Offset: 0x0000570C
		public MercatorGather(int zoom, int tilePx, int channels)
		{
			this.zoom = zoom;
			this.tilePx = tilePx;
			this.channels = channels;
			this.tilesPerAxis = 1 << zoom;
			this.worldPx = this.tilesPerAxis * tilePx;
		}

		// Token: 0x060000BE RID: 190 RVA: 0x0000755B File Offset: 0x0000575B
		private static long Key(int x, int y)
		{
			return (long)x << 32 | (long)((ulong)y);
		}

		/// <summary>Add a decoded tile. <paramref name="interleaved" /> is channel-interleaved,
		/// <c>tilePx*tilePx*channels</c> floats, row-major.</summary>
		// Token: 0x060000BF RID: 191 RVA: 0x00007568 File Offset: 0x00005768
		public void Add(int x, int y, float[] interleaved)
		{
			bool flag = interleaved.Length != this.tilePx * this.tilePx * this.channels;
			if (flag)
			{
				throw new ArgumentException(string.Format("gather: tile {0},{1} has {2} floats, expected {3}", new object[]
				{
					x,
					y,
					interleaved.Length,
					this.tilePx * this.tilePx * this.channels
				}));
			}
			this.tiles[MercatorGather.Key(x, y)] = interleaved;
		}

		// Token: 0x060000C0 RID: 192 RVA: 0x000075F9 File Offset: 0x000057F9
		public bool Has(int x, int y)
		{
			return this.tiles.ContainsKey(MercatorGather.Key(this.WrapX(x), y));
		}

		// Token: 0x060000C1 RID: 193 RVA: 0x00007613 File Offset: 0x00005813
		private int WrapX(int x)
		{
			return (x % this.tilesPerAxis + this.tilesPerAxis) % this.tilesPerAxis;
		}

		/// <summary>Fetch one texel by GLOBAL mercator pixel coordinate. x wraps (the antimeridian is not an
		/// edge); y clamps (latitude terminates rather than wrapping — the poles are not adjacent). Returns
		/// false when the owning tile isn't in the gather, which is how a missing/no-coverage source propagates
		/// instead of silently becoming black.</summary>
		// Token: 0x060000C2 RID: 194 RVA: 0x0000762C File Offset: 0x0000582C
		private bool TryTexel(int gx, int gy, float[] dst)
		{
			gx = (gx % this.worldPx + this.worldPx) % this.worldPx;
			bool flag = gy < 0;
			if (flag)
			{
				gy = 0;
			}
			else
			{
				bool flag2 = gy >= this.worldPx;
				if (flag2)
				{
					gy = this.worldPx - 1;
				}
			}
			int tx = gx / this.tilePx;
			int ty = gy / this.tilePx;
			float[] t;
			bool flag3 = !this.tiles.TryGetValue(MercatorGather.Key(tx, ty), out t);
			bool result;
			if (flag3)
			{
				result = false;
			}
			else
			{
				int px = gx - tx * this.tilePx;
				int py = gy - ty * this.tilePx;
				int o = (py * this.tilePx + px) * this.channels;
				for (int c = 0; c < this.channels; c++)
				{
					dst[c] = t[o + c];
				}
				result = true;
			}
			return result;
		}

		/// <summary>
		/// Mitchell-Netravali (B=C=1/3) 4x4 sample at a continuous global mercator pixel coordinate — the
		/// resample quality upgrade §4 wants, using the SAME kernel as the shader and the CPU height sampler
		/// (<see cref="T:Mirage.VirtualTexture.MitchellNetravali" />) rather than a second copy that could drift.
		///
		/// <paramref name="gx" />/<paramref name="gy" /> are in texel units where integer+0.5 is a texel centre.
		/// Returns false if any of the 16 taps has no backing tile: a partially-covered output texel must not be
		/// invented, it must fail so the caller can decline to bake.
		/// </summary>
		// Token: 0x060000C3 RID: 195 RVA: 0x0000770C File Offset: 0x0000590C
		public bool TrySample(double gx, double gy, float[] outPx, float[] scratchTap, double[] scratchRow)
		{
			double fx = gx - 0.5;
			double fy = gy - 0.5;
			int x = (int)Math.Floor(fx);
			int y = (int)Math.Floor(fy);
			double dx = fx - (double)x;
			double dy = fy - (double)y;
			for (int c = 0; c < this.channels; c++)
			{
				outPx[c] = 0f;
			}
			for (int c2 = 0; c2 < this.channels; c2++)
			{
				for (int i = 0; i < 4; i++)
				{
					double p0;
					bool flag = !this.Tap(x - 1, y - 1 + i, c2, scratchTap, out p0);
					if (flag)
					{
						return false;
					}
					double p;
					bool flag2 = !this.Tap(x, y - 1 + i, c2, scratchTap, out p);
					if (flag2)
					{
						return false;
					}
					double p2;
					bool flag3 = !this.Tap(x + 1, y - 1 + i, c2, scratchTap, out p2);
					if (flag3)
					{
						return false;
					}
					double p3;
					bool flag4 = !this.Tap(x + 2, y - 1 + i, c2, scratchTap, out p3);
					if (flag4)
					{
						return false;
					}
					scratchRow[i] = MercatorGather.Kernel.Evaluate(p0, p, p2, p3, dx);
				}
				outPx[c2] = (float)MercatorGather.Kernel.Evaluate(scratchRow[0], scratchRow[1], scratchRow[2], scratchRow[3], dy);
			}
			return true;
		}

		// Token: 0x060000C4 RID: 196 RVA: 0x00007888 File Offset: 0x00005A88
		private bool Tap(int gx, int gy, int channel, float[] scratch, out double value)
		{
			bool flag = !this.TryTexel(gx, gy, scratch);
			bool result;
			if (flag)
			{
				value = 0.0;
				result = false;
			}
			else
			{
				value = (double)scratch[channel];
				result = true;
			}
			return result;
		}

		// Token: 0x040000A9 RID: 169
		private readonly Dictionary<long, float[]> tiles = new Dictionary<long, float[]>();

		// Token: 0x040000AA RID: 170
		private readonly int zoom;

		// Token: 0x040000AB RID: 171
		private readonly int tilePx;

		// Token: 0x040000AC RID: 172
		private readonly int channels;

		// Token: 0x040000AD RID: 173
		private readonly int worldPx;

		// Token: 0x040000AE RID: 174
		private readonly int tilesPerAxis;

		// Token: 0x040000AF RID: 175
		private static readonly MitchellNetravali Kernel = new MitchellNetravali(0.3333333333333333, 0.3333333333333333);
	}
}
