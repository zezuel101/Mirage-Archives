using System;

namespace Mirage.WebIngest
{
	/// <summary>
	/// BC5 encoder (two independent BC4 blocks: X in the first, Y in the second). Used for normal tiles.
	///
	/// BC5 stores only two channels; the shader reconstructs Z as <c>sqrt(1 − x² − y²)</c> via Unity's
	/// <c>UnpackNormal</c>, which is why a normal map costs 16 bytes per 4x4 block and carries no Z at all.
	///
	/// <b>Endpoint mode.</b> BC4 has two: when <c>e0 &gt; e1</c> the palette is the two endpoints plus SIX
	/// interpolants; when <c>e0 &lt;= e1</c> it is four interpolants plus hard 0.0 and 1.0. This always emits
	/// the first (8-value) mode — it has strictly more resolution, and the 6-value mode's implicit 0/1 only pay
	/// off for data that genuinely saturates, which a normal component does not.
	///
	/// Unity-free and dependency-free; validated against an independent decoder (BCnEncoder.NET) by the packer,
	/// exactly as the BC7 encoder is.
	/// </summary>
	// Token: 0x0200000D RID: 13
	public static class Bc5Encoder
	{
		/// <summary>
		/// Encode two 8-bit channel planes (X then Y) into BC5. Both planes are <c>width*height</c> bytes,
		/// row-major. Dimensions must be multiples of 4.
		/// </summary>
		// Token: 0x0600005D RID: 93 RVA: 0x00003EA8 File Offset: 0x000020A8
		public static byte[] EncodeXY(byte[] planeX, byte[] planeY, int width, int height)
		{
			bool flag = width % 4 != 0 || height % 4 != 0;
			if (flag)
			{
				throw new ArgumentException(string.Format("BC5: {0}x{1} is not a multiple of 4", width, height));
			}
			bool flag2 = planeX.Length < width * height || planeY.Length < width * height;
			if (flag2)
			{
				throw new ArgumentException("BC5: plane shorter than width*height");
			}
			int bw = width / 4;
			int bh = height / 4;
			byte[] outp = new byte[bw * bh * 16];
			byte[] blk = new byte[16];
			for (int by = 0; by < bh; by++)
			{
				for (int bx = 0; bx < bw; bx++)
				{
					int o = (by * bw + bx) * 16;
					Bc5Encoder.Gather(planeX, width, bx, by, blk);
					Bc5Encoder.EncodeBc4Block(blk, outp, o);
					Bc5Encoder.Gather(planeY, width, bx, by, blk);
					Bc5Encoder.EncodeBc4Block(blk, outp, o + 8);
				}
			}
			return outp;
		}

		// Token: 0x0600005E RID: 94 RVA: 0x00003F98 File Offset: 0x00002198
		private static void Gather(byte[] plane, int width, int bx, int by, byte[] blk)
		{
			for (int i = 0; i < 4; i++)
			{
				for (int j = 0; j < 4; j++)
				{
					blk[i * 4 + j] = plane[(by * 4 + i) * width + bx * 4 + j];
				}
			}
		}

		/// <summary>Encode one 4x4 single-channel block into 8 bytes: e0, e1, then 16 3-bit indices.</summary>
		// Token: 0x0600005F RID: 95 RVA: 0x00003FE0 File Offset: 0x000021E0
		private static void EncodeBc4Block(byte[] v, byte[] dst, int o)
		{
			int lo = 255;
			int hi = 0;
			for (int i = 0; i < 16; i++)
			{
				bool flag = (int)v[i] < lo;
				if (flag)
				{
					lo = (int)v[i];
				}
				bool flag2 = (int)v[i] > hi;
				if (flag2)
				{
					hi = (int)v[i];
				}
			}
			int e0 = hi;
			int e = lo;
			bool flag3 = e0 == e;
			if (flag3)
			{
				bool flag4 = e0 < 255;
				if (flag4)
				{
					e0++;
				}
				else
				{
					e--;
				}
			}
			dst[o] = (byte)e0;
			dst[o + 1] = (byte)e;
			Bc5Encoder.Span8 pal = default(Bc5Encoder.Span8);
			pal[0] = e0;
			pal[1] = e;
			for (int j = 1; j <= 6; j++)
			{
				pal[j + 1] = ((7 - j) * e0 + j * e) / 7;
			}
			ulong bits = 0UL;
			for (int k = 0; k < 16; k++)
			{
				int best = 0;
				int bestErr = int.MaxValue;
				for (int l = 0; l < 8; l++)
				{
					int d = (int)v[k] - pal[l];
					int err = d * d;
					bool flag5 = err < bestErr;
					if (flag5)
					{
						bestErr = err;
						best = l;
					}
				}
				bits |= (ulong)((ulong)((long)best) << k * 3);
			}
			for (int m = 0; m < 6; m++)
			{
				dst[o + 2 + m] = (byte)(bits >> m * 8);
			}
		}

		// Token: 0x0400003C RID: 60
		public const int BlockBytes = 16;

		/// <summary>Tiny fixed-size int buffer — avoids allocating a palette array per block (this runs ~4400
		/// times per tile) without needing Span/stackalloc, which net4.8 doesn't have.</summary>
		// Token: 0x02000080 RID: 128
		private struct Span8
		{
			// Token: 0x170000FE RID: 254
			public int this[int i]
			{
				get
				{
					if (!true)
					{
					}
					int result;
					switch (i)
					{
					case 0:
						result = this.a0;
						break;
					case 1:
						result = this.a1;
						break;
					case 2:
						result = this.a2;
						break;
					case 3:
						result = this.a3;
						break;
					case 4:
						result = this.a4;
						break;
					case 5:
						result = this.a5;
						break;
					case 6:
						result = this.a6;
						break;
					default:
						result = this.a7;
						break;
					}
					if (!true)
					{
					}
					return result;
				}
				set
				{
					switch (i)
					{
					case 0:
						this.a0 = value;
						break;
					case 1:
						this.a1 = value;
						break;
					case 2:
						this.a2 = value;
						break;
					case 3:
						this.a3 = value;
						break;
					case 4:
						this.a4 = value;
						break;
					case 5:
						this.a5 = value;
						break;
					case 6:
						this.a6 = value;
						break;
					default:
						this.a7 = value;
						break;
					}
				}
			}

			// Token: 0x04000301 RID: 769
			private int a0;

			// Token: 0x04000302 RID: 770
			private int a1;

			// Token: 0x04000303 RID: 771
			private int a2;

			// Token: 0x04000304 RID: 772
			private int a3;

			// Token: 0x04000305 RID: 773
			private int a4;

			// Token: 0x04000306 RID: 774
			private int a5;

			// Token: 0x04000307 RID: 775
			private int a6;

			// Token: 0x04000308 RID: 776
			private int a7;
		}
	}
}
