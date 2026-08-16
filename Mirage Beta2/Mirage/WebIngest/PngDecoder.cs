using System;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace Mirage.WebIngest
{
	/// <summary>
	/// A minimal PNG decoder — 8-bit, non-interlaced, greyscale/RGB/RGBA. WebIngest, DEM ingest.
	///
	/// <b>Why this exists.</b> DEM providers serve terrain-RGB as PNG (lossy JPEG would corrupt elevation: the
	/// encoding packs metres into R·256 + G + B/256, so a single LSB of chroma error is a 25 cm step and a
	/// chroma-subsampled one is far worse). Imagery is JPEG; elevation must be PNG. Unity's decoder is not an
	/// option for the same reason as §6 — it welds decode → Texture2D → GPU upload onto the main thread, which
	/// is the coupling the whole ingest path exists to avoid.
	///
	/// <b>On DEFLATE.</b> The Archive doc (§8) rules out gzip/DEFLATE — "DEFLATE decode in Mono is slow and
	/// colour sits on the latency-sensitive read path". That reasoning is about the READ path, and it still
	/// holds there: nothing in the archive uses DEFLATE. This is the BAKE path — once per tile, off-thread,
	/// never during a frame's tile reads — so <c>System.IO.Compression</c> is an appropriate tool here and adds
	/// no dependency (it is BCL).
	///
	/// Scope is deliberately narrow, and out-of-scope inputs throw rather than decode to something plausible —
	/// the same discipline as the JPEG decoder's progressive rejection. Interlaced and 16-bit PNGs are the
	/// realistic surprises; both are rejected loudly.
	/// </summary>
	// Token: 0x02000025 RID: 37
	public static class PngDecoder
	{
		/// <summary>
		/// Decode to tightly-packed RGB24 (3 bytes/px, row-major, top-left origin). Greyscale expands to RGB;
		/// RGBA drops alpha (terrain-RGB has none).
		/// </summary>
		// Token: 0x060000E2 RID: 226 RVA: 0x0000872B File Offset: 0x0000692B
		public static byte[] DecodeToRgb(byte[] data, out int width, out int height)
		{
			return PngDecoder.DecodeInternal(data, null, out width, out height);
		}

		/// <summary>As <see cref="M:Mirage.WebIngest.PngDecoder.DecodeToRgb(System.Byte[],System.Int32@,System.Int32@)" /> but writes the RGB24 output into <paramref name="dst" />
		/// (length ≥ w·h·3) instead of allocating it, so a bake can recycle the buffer through
		/// <see cref="T:Mirage.WebIngest.BufferPool" />. Throws if <paramref name="dst" /> is null or too short.</summary>
		// Token: 0x060000E3 RID: 227 RVA: 0x00008738 File Offset: 0x00006938
		public static byte[] DecodeToRgbInto(byte[] data, byte[] dst, out int width, out int height)
		{
			bool flag = dst == null;
			if (flag)
			{
				throw new ArgumentNullException("dst");
			}
			return PngDecoder.DecodeInternal(data, dst, out width, out height);
		}

		// Token: 0x060000E4 RID: 228 RVA: 0x00008768 File Offset: 0x00006968
		private static byte[] DecodeInternal(byte[] data, byte[] dst, out int width, out int height)
		{
			bool flag = data == null || data.Length < 8;
			if (flag)
			{
				throw new PngDecodeException("empty buffer");
			}
			for (int i = 0; i < 8; i++)
			{
				bool flag2 = data[i] != PngDecoder.Signature[i];
				if (flag2)
				{
					throw new PngDecodeException("not a PNG (bad signature)");
				}
			}
			int w = 0;
			int h = 0;
			int bitDepth = 0;
			int colorType = 0;
			int interlace = 0;
			bool haveHeader = false;
			MemoryStream idat = new MemoryStream();
			int p = 8;
			while (p + 8 <= data.Length)
			{
				int len = PngDecoder.ReadBe32(data, p);
				bool flag3 = len < 0 || p + 12 + len > data.Length;
				if (flag3)
				{
					throw new PngDecodeException(string.Format("truncated chunk at {0}", p));
				}
				string type = Encoding.ASCII.GetString(data, p + 4, 4);
				int body = p + 8;
				string text = type;
				string a = text;
				if (!(a == "IHDR"))
				{
					if (!(a == "IDAT"))
					{
						if (a == "IEND")
						{
							p = data.Length;
							continue;
						}
					}
					else
					{
						idat.Write(data, body, len);
					}
				}
				else
				{
					bool flag4 = len < 13;
					if (flag4)
					{
						throw new PngDecodeException("short IHDR");
					}
					w = PngDecoder.ReadBe32(data, body);
					h = PngDecoder.ReadBe32(data, body + 4);
					bitDepth = (int)data[body + 8];
					colorType = (int)data[body + 9];
					interlace = (int)data[body + 12];
					haveHeader = true;
				}
				p = body + len + 4;
			}
			bool flag5 = !haveHeader;
			if (flag5)
			{
				throw new PngDecodeException("no IHDR");
			}
			bool flag6 = w <= 0 || h <= 0;
			if (flag6)
			{
				throw new PngDecodeException(string.Format("bad dimensions {0}x{1}", w, h));
			}
			bool flag7 = bitDepth != 8;
			if (flag7)
			{
				throw new PngDecodeException(string.Format("unsupported bit depth {0} (only 8 is in scope; a 16-bit PNG would silently ", bitDepth) + "halve elevation precision if misread)");
			}
			bool flag8 = interlace != 0;
			if (flag8)
			{
				throw new PngDecodeException("interlaced (Adam7) PNG is out of scope");
			}
			if (!true)
			{
			}
			int num;
			if (colorType != 0)
			{
				if (colorType != 2)
				{
					if (colorType != 6)
					{
						throw new PngDecodeException(string.Format("unsupported colour type {0} (palette/alpha-grey not in scope)", colorType));
					}
					num = 4;
				}
				else
				{
					num = 3;
				}
			}
			else
			{
				num = 1;
			}
			if (!true)
			{
			}
			int channels = num;
			byte[] raw = PngDecoder.Inflate(idat.ToArray(), h * (1 + w * channels));
			byte[] rgb = PngDecoder.Unfilter(raw, dst, w, h, channels);
			width = w;
			height = h;
			return rgb;
		}

		// Token: 0x060000E5 RID: 229 RVA: 0x000089EB File Offset: 0x00006BEB
		private static int ReadBe32(byte[] d, int o)
		{
			return (int)d[o] << 24 | (int)d[o + 1] << 16 | (int)d[o + 2] << 8 | (int)d[o + 3];
		}

		/// <summary>Inflate a zlib stream. .NET Framework has no ZLibStream (that arrived in .NET 6), so the
		/// 2-byte zlib header is skipped by hand and the raw DEFLATE payload handed to DeflateStream; the
		/// trailing Adler-32 is simply not read.</summary>
		// Token: 0x060000E6 RID: 230 RVA: 0x00008A0C File Offset: 0x00006C0C
		private static byte[] Inflate(byte[] zlib, int expected)
		{
			bool flag = zlib.Length < 3;
			if (flag)
			{
				throw new PngDecodeException("empty IDAT");
			}
			bool flag2 = (zlib[0] & 15) != 8;
			if (flag2)
			{
				throw new PngDecodeException(string.Format("unsupported zlib compression method {0}", (int)(zlib[0] & 15)));
			}
			byte[] outp = new byte[expected];
			byte[] result;
			using (MemoryStream src = new MemoryStream(zlib, 2, zlib.Length - 2))
			{
				using (DeflateStream inflate = new DeflateStream(src, CompressionMode.Decompress))
				{
					int i;
					for (int read = 0; read < expected; read += i)
					{
						i = inflate.Read(outp, read, expected - read);
						bool flag3 = i <= 0;
						if (flag3)
						{
							throw new PngDecodeException(string.Format("IDAT inflated to {0} bytes, expected {1} (truncated stream)", read, expected));
						}
					}
					result = outp;
				}
			}
			return result;
		}

		/// <summary>
		/// Reverse PNG's per-scanline filters and emit RGB24. Each scanline is one filter byte followed by
		/// <c>width*channels</c> bytes, and filters reference the already-reconstructed bytes to the left (a)
		/// and above (b) — so this must run in order and in place.
		/// </summary>
		// Token: 0x060000E7 RID: 231 RVA: 0x00008B00 File Offset: 0x00006D00
		private static byte[] Unfilter(byte[] raw, byte[] dst, int w, int h, int channels)
		{
			int stride = w * channels;
			byte[] cur = new byte[stride];
			byte[] prev = new byte[stride];
			int need = w * h * 3;
			bool flag = dst != null && dst.Length < need;
			if (flag)
			{
				throw new PngDecodeException(string.Format("dst holds {0} bytes, need {1} for {2}x{3} RGB.", new object[]
				{
					dst.Length,
					need,
					w,
					h
				}));
			}
			byte[] rgb = dst ?? new byte[need];
			int p = 0;
			for (int y = 0; y < h; y++)
			{
				int filter = (int)raw[p++];
				Buffer.BlockCopy(raw, p, cur, 0, stride);
				p += stride;
				for (int i = 0; i < stride; i++)
				{
					int a = (int)((i >= channels) ? cur[i - channels] : 0);
					int b = (int)prev[i];
					int c = (int)((i >= channels) ? prev[i - channels] : 0);
					int x = (int)cur[i];
					byte[] array = cur;
					int num = i;
					if (!true)
					{
					}
					byte b2;
					switch (filter)
					{
					case 0:
						b2 = (byte)x;
						break;
					case 1:
						b2 = (byte)(x + a);
						break;
					case 2:
						b2 = (byte)(x + b);
						break;
					case 3:
						b2 = (byte)(x + (a + b >> 1));
						break;
					case 4:
						b2 = (byte)(x + PngDecoder.Paeth(a, b, c));
						break;
					default:
						throw new PngDecodeException(string.Format("bad filter type {0} on row {1}", filter, y));
					}
					if (!true)
					{
					}
					array[num] = b2;
				}
				for (int x2 = 0; x2 < w; x2++)
				{
					int s = x2 * channels;
					int d = (y * w + x2) * 3;
					bool flag2 = channels == 1;
					if (flag2)
					{
						rgb[d] = (rgb[d + 1] = (rgb[d + 2] = cur[s]));
					}
					else
					{
						rgb[d] = cur[s];
						rgb[d + 1] = cur[s + 1];
						rgb[d + 2] = cur[s + 2];
					}
				}
				byte[] t = prev;
				prev = cur;
				cur = t;
			}
			return rgb;
		}

		/// <summary>PNG's Paeth predictor: pick whichever of left/above/above-left is closest to a+b−c.</summary>
		// Token: 0x060000E8 RID: 232 RVA: 0x00008D2C File Offset: 0x00006F2C
		private static int Paeth(int a, int b, int c)
		{
			int pa = Math.Abs(b - c);
			int pb = Math.Abs(a - c);
			int pc = Math.Abs(a + b - 2 * c);
			return (pa <= pb && pa <= pc) ? a : ((pb <= pc) ? b : c);
		}

		// Token: 0x040000BC RID: 188
		private static readonly byte[] Signature = new byte[]
		{
			137,
			80,
			78,
			71,
			13,
			10,
			26,
			10
		};
	}
}
