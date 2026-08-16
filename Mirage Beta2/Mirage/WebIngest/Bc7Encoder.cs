using System;

namespace Mirage.WebIngest
{
	/// <summary>
	/// A speed-optimised BC7 encoder, mode 6, <b>colour-priority</b>. WebIngest P3.
	///
	/// <b>Why this is load-bearing.</b> §1 commits Mirage to a single BC7 atlas — <c>EnsureAtlasAllocated</c>
	/// locks the atlas to the first tile's format, so a streamed tile MUST be BC7. There is no DXT1 fallback to
	/// retreat to. And Unity 2019.4's runtime <c>Texture2D.Compress</c> is DXT1/5 only; BC7 compression is
	/// editor-only (<c>EditorUtility.CompressTexture</c>) and unavailable in a KSP session. So this encoder is the
	/// only way a web-baked tile can enter the atlas at all.
	///
	/// <b>Mode 6 only (§5).</b> BC7's cost is the mode + partition search: 8 modes, up to 64 partitions, an
	/// endpoint fit per candidate. A speed encoder throws essentially all of it away. Mode 6 is single-subset RGBA
	/// with 7-bit endpoints + a per-endpoint p-bit (8-bit effective) and 4-bit indices — no partition search, fixed
	/// layout, branch-light. It also has the best colour capability of any BC7 mode (4-bit indices, 8-bit-effective
	/// endpoints), which is why the alpha modes (5/7, ≤2-bit colour) are not worth it HERE: see colour-priority.
	///
	/// <b>The colour tile's alpha is a secondary mask.</b> Alpha carries a <i>water mask</i> (white = water) that
	/// doubles as PQS smoothness; it is never a visible channel (PQS samples the RGB directly). Mode 6's one
	/// shared 4-bit index line serves RGB and A together, so getting a coast right takes two things.
	///
	/// <b>Alpha endpoint ordering (keeps the mask right).</b> The bounding box fixes the colour endpoints
	/// (index 0 = colour-min), but alpha can run either way along that same axis. At a coast the mask
	/// (255 water → 0 land) is ANTI-correlated with colour (dark → bright), so the axis-aligned box would pair
	/// colour-min with alpha-min and INVERT the mask. The encoder tries both alpha orderings and keeps the lower-
	/// error one, which pairs colour-min with alpha-max — so the ordinary anti-correlated coast reconstructs BOTH
	/// colour and mask correctly. (One extra index pass, only on non-uniform-alpha blocks.)
	///
	/// <b>Colour-priority (keeps the colour right where the mask can't co-fit).</b> Where the mask is coarse
	/// enough to jitter ACROSS the coast within a single block (WorldCover's 10 m grid vs fine imagery), no single
	/// line fits both no matter the ordering. An equal-weight encoder splits that contradiction into BOTH channels,
	/// and the colour half is the blocky coast users saw. So alpha error is down-weighted far below colour in every
	/// decision (the p-bit and each index), making it a near-pure tie-breaker: colour stays crisp on the line and
	/// the invisible mask absorbs the miss. See <see cref="F:Mirage.WebIngest.Bc7Encoder.AlphaErrShift" />. Opaque imagery (A ≡ 255) has zero
	/// alpha error, so it is unaffected. The mask is also softened to a ramp at bake time
	/// (<c>CubeTileBaker.waterMaskBlurPx</c>), which cleans up the anti-correlation for the ordering step and
	/// reads better as shoreline smoothness — complementary to both mechanisms, not a substitute for either.
	///
	/// <b>The bit packing is not derived cold</b> (§5 is explicit): the layout follows the BC7 specification, and
	/// correctness is verified against an INDEPENDENT decoder (BCnEncoder.NET) by `ArchivePacker --test-bc7`,
	/// including a misaligned-coast case that proves colour survives a mask that disagrees with it. That makes
	/// "the bits are right" a measurement, not a claim.
	///
	/// Unity-free and dependency-free: it ships inside Mirage.dll with nothing added to GameData, and the packer
	/// links this exact source so the test exercises shipped code.
	/// </summary>
	// Token: 0x0200000E RID: 14
	public static class Bc7Encoder
	{
		/// <summary>
		/// Encode an RGBA32 image to BC7. <paramref name="width" /> and <paramref name="height" /> must be multiples
		/// of 4 — Mirage's slot is tileSize + 2·borderPx = 264 by default, and §9 requires the slot stay a multiple
		/// of the 4x4 block size (264/4 = 66 ✓).
		/// </summary>
		// Token: 0x06000060 RID: 96 RVA: 0x0000415C File Offset: 0x0000235C
		public static byte[] EncodeRgba(byte[] rgba, int width, int height)
		{
			bool flag = width % 4 != 0 || height % 4 != 0;
			if (flag)
			{
				throw new ArgumentException(string.Format("BC7: {0}x{1} is not a multiple of 4 (the block size). Mirage's slot must stay ", width, height) + "block-aligned — see WebIngest §9.");
			}
			bool flag2 = rgba.Length < width * height * 4;
			if (flag2)
			{
				throw new ArgumentException("BC7: source shorter than width*height*4");
			}
			int bw = width / 4;
			int bh = height / 4;
			byte[] outp = new byte[bw * bh * 16];
			byte[] block = new byte[64];
			for (int by = 0; by < bh; by++)
			{
				for (int bx = 0; bx < bw; bx++)
				{
					for (int i = 0; i < 4; i++)
					{
						for (int j = 0; j < 4; j++)
						{
							int sx = bx * 4 + j;
							int sy = by * 4 + i;
							int s = (sy * width + sx) * 4;
							int d = (i * 4 + j) * 4;
							block[d] = rgba[s];
							block[d + 1] = rgba[s + 1];
							block[d + 2] = rgba[s + 2];
							block[d + 3] = rgba[s + 3];
						}
					}
					Bc7Encoder.EncodeBlock(block, outp, (by * bw + bx) * 16);
				}
			}
			return outp;
		}

		/// <summary>Encode one 4x4 RGBA block (64 bytes) into 16 bytes at <paramref name="dstOffset" />, mode 6,
		/// colour-priority (see <see cref="F:Mirage.WebIngest.Bc7Encoder.AlphaErrShift" />).</summary>
		// Token: 0x06000061 RID: 97 RVA: 0x000042B0 File Offset: 0x000024B0
		public static void EncodeBlock(byte[] px, byte[] dst, int dstOffset)
		{
			int r0 = 255;
			int g0 = 255;
			int b0 = 255;
			int a0 = 255;
			int r = 0;
			int g = 0;
			int b = 0;
			int a = 0;
			for (int i = 0; i < 16; i++)
			{
				int o = i * 4;
				bool flag = (int)px[o] < r0;
				if (flag)
				{
					r0 = (int)px[o];
				}
				bool flag2 = (int)px[o] > r;
				if (flag2)
				{
					r = (int)px[o];
				}
				bool flag3 = (int)px[o + 1] < g0;
				if (flag3)
				{
					g0 = (int)px[o + 1];
				}
				bool flag4 = (int)px[o + 1] > g;
				if (flag4)
				{
					g = (int)px[o + 1];
				}
				bool flag5 = (int)px[o + 2] < b0;
				if (flag5)
				{
					b0 = (int)px[o + 2];
				}
				bool flag6 = (int)px[o + 2] > b;
				if (flag6)
				{
					b = (int)px[o + 2];
				}
				bool flag7 = (int)px[o + 3] < a0;
				if (flag7)
				{
					a0 = (int)px[o + 3];
				}
				bool flag8 = (int)px[o + 3] > a;
				if (flag8)
				{
					a = (int)px[o + 3];
				}
			}
			int bestQ0r = 0;
			int bestQ1r = 0;
			int bestQ0g = 0;
			int bestQ1g = 0;
			int bestQ0b = 0;
			int bestQ1b = 0;
			int bestQ0a = 0;
			int bestQ1a = 0;
			int bestP0 = 0;
			int bestP = 0;
			int[] idx = new int[16];
			int[] bestIdx = new int[16];
			long bestTotal = long.MaxValue;
			int orderings = (a0 == a) ? 1 : 2;
			for (int ord = 0; ord < orderings; ord++)
			{
				int ae0 = (ord == 0) ? a0 : a;
				int ae = (ord == 0) ? a : a0;
				int q0r;
				int q0g;
				int q0b;
				int q0a;
				int p0;
				Bc7Encoder.ChooseEndpoint(r0, g0, b0, ae0, out q0r, out q0g, out q0b, out q0a, out p0);
				int q1r;
				int q1g;
				int q1b;
				int q1a;
				int p;
				Bc7Encoder.ChooseEndpoint(r, g, b, ae, out q1r, out q1g, out q1b, out q1a, out p);
				int e0r = q0r << 1 | p0;
				int e0g = q0g << 1 | p0;
				int e0b = q0b << 1 | p0;
				int e0a = q0a << 1 | p0;
				int e1r = q1r << 1 | p;
				int e1g = q1g << 1 | p;
				int e1b = q1b << 1 | p;
				int e1a = q1a << 1 | p;
				long total = 0L;
				for (int j = 0; j < 16; j++)
				{
					int o2 = j * 4;
					int best = 0;
					long bestErr = long.MaxValue;
					for (int k = 0; k < 16; k++)
					{
						int w = Bc7Encoder.Weights4[k];
						long dr = (long)((int)px[o2] - Bc7Encoder.Interp(e0r, e1r, w));
						long dg = (long)((int)px[o2 + 1] - Bc7Encoder.Interp(e0g, e1g, w));
						long db = (long)((int)px[o2 + 2] - Bc7Encoder.Interp(e0b, e1b, w));
						long da = (long)((int)px[o2 + 3] - Bc7Encoder.Interp(e0a, e1a, w));
						long err = dr * dr + dg * dg + db * db + (da * da >> 6);
						bool flag9 = err < bestErr;
						if (flag9)
						{
							bestErr = err;
							best = k;
						}
					}
					idx[j] = best;
					total += bestErr;
				}
				bool flag10 = total < bestTotal;
				if (flag10)
				{
					bestTotal = total;
					bestQ0r = q0r;
					bestQ1r = q1r;
					bestQ0g = q0g;
					bestQ1g = q1g;
					bestQ0b = q0b;
					bestQ1b = q1b;
					bestQ0a = q0a;
					bestQ1a = q1a;
					bestP0 = p0;
					bestP = p;
					Array.Copy(idx, bestIdx, 16);
				}
			}
			bool flag11 = bestIdx[0] >= 8;
			if (flag11)
			{
				Bc7Encoder.Swap(ref bestQ0r, ref bestQ1r);
				Bc7Encoder.Swap(ref bestQ0g, ref bestQ1g);
				Bc7Encoder.Swap(ref bestQ0b, ref bestQ1b);
				Bc7Encoder.Swap(ref bestQ0a, ref bestQ1a);
				Bc7Encoder.Swap(ref bestP0, ref bestP);
				for (int l = 0; l < 16; l++)
				{
					bestIdx[l] = 15 - bestIdx[l];
				}
			}
			Bc7Encoder.BitWriter w2 = new Bc7Encoder.BitWriter(dst, dstOffset);
			w2.Write(0, 6);
			w2.Write(1, 1);
			w2.Write(bestQ0r, 7);
			w2.Write(bestQ1r, 7);
			w2.Write(bestQ0g, 7);
			w2.Write(bestQ1g, 7);
			w2.Write(bestQ0b, 7);
			w2.Write(bestQ1b, 7);
			w2.Write(bestQ0a, 7);
			w2.Write(bestQ1a, 7);
			w2.Write(bestP0, 1);
			w2.Write(bestP, 1);
			w2.Write(bestIdx[0], 3);
			for (int m = 1; m < 16; m++)
			{
				w2.Write(bestIdx[m], 4);
			}
		}

		/// <summary>BC7 interpolation: <c>(a·(64−w) + b·w + 32) &gt;&gt; 6</c>, the spec's exact rounding.</summary>
		// Token: 0x06000062 RID: 98 RVA: 0x00004703 File Offset: 0x00002903
		private static int Interp(int a, int b, int w)
		{
			return a * (64 - w) + b * w + 32 >> 6;
		}

		/// <summary>
		/// Quantise one endpoint's RGBA to 7 bits per channel plus ONE shared p-bit. Both p-bit values are tried
		/// and the one with lower COLOUR-priority error wins (RGB full, alpha down-weighted — see
		/// <see cref="F:Mirage.WebIngest.Bc7Encoder.AlphaErrShift" />); the p-bit is a property of the endpoint, so choosing it per channel is not
		/// an option the format offers.
		/// </summary>
		// Token: 0x06000063 RID: 99 RVA: 0x00004714 File Offset: 0x00002914
		private static void ChooseEndpoint(int r, int g, int b, int a, out int qr, out int qg, out int qb, out int qa, out int p)
		{
			int bestP = 0;
			long bestErr = long.MaxValue;
			int br = 0;
			int bg = 0;
			int bb = 0;
			int ba = 0;
			for (int cand = 0; cand <= 1; cand++)
			{
				int tr = Bc7Encoder.Quant7(r, cand);
				int tg = Bc7Encoder.Quant7(g, cand);
				int tb = Bc7Encoder.Quant7(b, cand);
				int ta = Bc7Encoder.Quant7(a, cand);
				long dr = (long)((tr << 1 | cand) - r);
				long dg = (long)((tg << 1 | cand) - g);
				long db = (long)((tb << 1 | cand) - b);
				long da = (long)((ta << 1 | cand) - a);
				long err = dr * dr + dg * dg + db * db + (da * da >> 6);
				bool flag = err < bestErr;
				if (flag)
				{
					bestErr = err;
					bestP = cand;
					br = tr;
					bg = tg;
					bb = tb;
					ba = ta;
				}
			}
			qr = br;
			qg = bg;
			qb = bb;
			qa = ba;
			p = bestP;
		}

		/// <summary>Nearest 7-bit value whose expansion <c>(q&lt;&lt;1)|p</c> approximates <paramref name="v" />.</summary>
		// Token: 0x06000064 RID: 100 RVA: 0x00004804 File Offset: 0x00002A04
		private static int Quant7(int v, int p)
		{
			int q = v - p + 1 >> 1;
			return (q < 0) ? 0 : ((q > 127) ? 127 : q);
		}

		// Token: 0x06000065 RID: 101 RVA: 0x00004830 File Offset: 0x00002A30
		private static void Swap(ref int a, ref int b)
		{
			int t = a;
			a = b;
			b = t;
		}

		/// <summary>BC7's 4-bit interpolation weights (out of 64). Deliberately NOT i·64/15 — the spec's table is
		/// not exactly linear, and a linear approximation would put every index slightly off and show up as a
		/// uniform quality loss.</summary>
		// Token: 0x0400003D RID: 61
		private static readonly int[] Weights4 = new int[]
		{
			0,
			4,
			9,
			13,
			17,
			21,
			26,
			30,
			34,
			38,
			43,
			47,
			51,
			55,
			60,
			64
		};

		/// <summary>
		/// Alpha error is right-shifted by this before it competes with colour in every mode-6 decision (the
		/// shared p-bit, and each texel's index), making alpha a near-pure <b>tie-breaker</b>: it steers the index
		/// only where colour barely cares (a flat block), and never overrides a real colour difference.
		///
		/// Why so aggressive. The colour tile's alpha is a secondary water mask / PQS-smoothness channel, never a
		/// visible one (PQS samples the RGB directly). At a coast, alpha (255→0) runs OPPOSITE to colour
		/// (dark water → bright land) — anti-correlated — and a hard, coarse mask that disagrees with the fine
		/// imagery cannot be fit by mode 6's one shared line at all. If alpha is only mildly down-weighted, its
		/// (large) error can still exceed the colour cost of snapping to an alpha-matching index, so colour is
		/// STILL sacrificed and the block goes blocky. A shift of 6 (alpha counts 1/64) means alpha changes the
		/// index only when the colour alternatives are within a hair of each other — so colour is preserved at
		/// every coast, the invisible mask absorbs the miss, and a conflict-free binary mask (flat colour) still
		/// comes back near-lossless. Opaque blocks (A ≡ 255) have zero alpha error and are unaffected.
		/// </summary>
		// Token: 0x0400003E RID: 62
		private const int AlphaErrShift = 6;

		// Token: 0x0400003F RID: 63
		public const int BlockBytes = 16;

		/// <summary>LSB-first bit writer over a 16-byte block.</summary>
		// Token: 0x02000081 RID: 129
		private struct BitWriter
		{
			// Token: 0x06000435 RID: 1077 RVA: 0x0001BFB4 File Offset: 0x0001A1B4
			public BitWriter(byte[] dst, int origin)
			{
				this.dst = dst;
				this.origin = origin;
				this.bit = 0;
				for (int i = 0; i < 16; i++)
				{
					dst[origin + i] = 0;
				}
			}

			// Token: 0x06000436 RID: 1078 RVA: 0x0001BFF0 File Offset: 0x0001A1F0
			public void Write(int value, int bits)
			{
				for (int i = 0; i < bits; i++)
				{
					bool flag = (value >> i & 1) != 0;
					if (flag)
					{
						byte[] array = this.dst;
						int num = this.origin + (this.bit >> 3);
						array[num] |= (byte)(1 << (this.bit & 7));
					}
					this.bit++;
				}
			}

			// Token: 0x04000309 RID: 777
			private readonly byte[] dst;

			// Token: 0x0400030A RID: 778
			private readonly int origin;

			// Token: 0x0400030B RID: 779
			private int bit;
		}
	}
}
