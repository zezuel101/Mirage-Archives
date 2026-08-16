using System;
using System.Threading.Tasks;

namespace Mirage.WebIngest
{
	/// <summary>
	/// A baseline JPEG decoder producing raw RGB24 bytes. WebIngest P1.
	///
	/// <b>Why we own the decode (§6):</b> Unity's decoder welds decode → Texture2D → GPU upload onto the main
	/// thread, and that upload is exactly the coupling being removed. This emits a plain byte[] that flows into
	/// the reproject job with no GC churn, no main-thread touch, and no GPU resource.
	///
	/// <b>Scope is baseline-only, and that is measured, not assumed.</b> §11 decision 3 asked whether EOX/GIBS
	/// serve baseline or progressive, warning that a naive baseline port fed a progressive frame emits garbage
	/// rather than failing. `ArchivePacker --probe-providers` answered it against the live endpoints: both serve
	/// SOF0 baseline, 3-component, 8-bit, 256x256. So this follows NanoJPEG's baseline-only shape rather than
	/// porting stb_image's progressive machinery, which would be dead code.
	///
	/// A progressive frame is <b>rejected loudly</b> (see <see cref="T:Mirage.WebIngest.JpegFrameKind" />), never decoded as if it
	/// were baseline — silence is how that class of bug hides.
	///
	/// <b>Restart markers are implemented but do not occur in practice.</b> Every probed tile reported
	/// restart=0. §6 names restart intervals as the seam for parallelising the serial Huffman decode; it isn't
	/// present in the real data, so the parallelism lives one level up — a cube tile gathers 1..N mercator tiles
	/// and each decodes independently on its own worker. Handling is kept because another provider may set DRI.
	///
	/// <b>Correctness first, speed later (§6 sequencing).</b> The IDCT here is a straightforward separable
	/// float transform, not AAN/integer-scaled — clear and obviously right, and validated against a reference
	/// decoder by `ArchivePacker --test-jpeg`. Chroma upsampling is nearest-neighbour (standard-conformant;
	/// libjpeg's "fancy" triangular upsample differs by a few LSBs at chroma edges, which is far below what the
	/// subsequent Mitchell-Netravali resample and BC7 quantisation do anyway).
	/// </summary>
	// Token: 0x0200000C RID: 12
	public static class BaselineJpegDecoder
	{
		// Token: 0x0600004E RID: 78 RVA: 0x00002DC4 File Offset: 0x00000FC4
		private static float[,] BuildCosTable()
		{
			float[,] t = new float[8, 8];
			for (int x = 0; x < 8; x++)
			{
				for (int u = 0; u < 8; u++)
				{
					double cu = (u == 0) ? (1.0 / Math.Sqrt(2.0)) : 1.0;
					t[x, u] = (float)(cu * Math.Cos((double)((2 * x + 1) * u) * 3.141592653589793 / 16.0) / 2.0);
				}
			}
			return t;
		}

		/// <summary>
		/// Decode off the main thread (§6: "get it correct off-thread first — a managed decoder on a Task
		/// achieves the decoupling"). Nothing in this decoder touches Unity or the GPU, so it is safe on any
		/// thread; per §6.1 the useful parallelism is across tiles, so a cube tile's gather should decode its
		/// 1..N mercator sources as concurrent tasks.
		/// </summary>
		// Token: 0x0600004F RID: 79 RVA: 0x00002E64 File Offset: 0x00001064
		public static Task<DecodedRgbTile> DecodeAsync(byte[] data)
		{
			return Task.Run<DecodedRgbTile>(delegate()
			{
				int w;
				int h;
				byte[] rgb = BaselineJpegDecoder.DecodeToRgb(data, out w, out h);
				return new DecodedRgbTile(rgb, w, h);
			});
		}

		/// <summary>
		/// Decode a baseline JPEG to tightly-packed RGB24 (3 bytes per pixel, row-major, top-left origin).
		/// Grayscale (1-component) is expanded to RGB. Throws <see cref="T:Mirage.WebIngest.JpegDecodeException" /> on anything
		/// malformed or out of scope.
		/// </summary>
		// Token: 0x06000050 RID: 80 RVA: 0x00002E8F File Offset: 0x0000108F
		public static byte[] DecodeToRgb(byte[] data, out int width, out int height)
		{
			return BaselineJpegDecoder.DecodeInternal(data, null, out width, out height);
		}

		/// <summary>As <see cref="M:Mirage.WebIngest.BaselineJpegDecoder.DecodeToRgb(System.Byte[],System.Int32@,System.Int32@)" /> but writes the RGB24 output into <paramref name="dst" />
		/// (length ≥ w·h·3) instead of allocating it, so a bake can recycle the buffer through
		/// <see cref="T:Mirage.WebIngest.BufferPool" />. Throws if <paramref name="dst" /> is null or too short.</summary>
		// Token: 0x06000051 RID: 81 RVA: 0x00002E9C File Offset: 0x0000109C
		public static byte[] DecodeToRgbInto(byte[] data, byte[] dst, out int width, out int height)
		{
			bool flag = dst == null;
			if (flag)
			{
				throw new JpegDecodeException("dst buffer is null");
			}
			return BaselineJpegDecoder.DecodeInternal(data, dst, out width, out height);
		}

		// Token: 0x06000052 RID: 82 RVA: 0x00002ECC File Offset: 0x000010CC
		private static byte[] DecodeInternal(byte[] data, byte[] dst, out int width, out int height)
		{
			bool flag = data == null || data.Length < 4;
			if (flag)
			{
				throw new JpegDecodeException("empty buffer");
			}
			bool flag2 = data[0] != byte.MaxValue || data[1] != 216;
			if (flag2)
			{
				throw new JpegDecodeException("missing SOI");
			}
			int[][] quant = new int[4][];
			BaselineJpegDecoder.HuffTable[] dcTables = new BaselineJpegDecoder.HuffTable[4];
			BaselineJpegDecoder.HuffTable[] acTables = new BaselineJpegDecoder.HuffTable[4];
			BaselineJpegDecoder.Component[] components = null;
			int frameWidth = 0;
			int frameHeight = 0;
			int restartInterval = 0;
			int hMax = 1;
			int vMax = 1;
			int mcusPerLine = 0;
			int mcusPerColumn = 0;
			int p = 2;
			byte marker;
			int segLen;
			int precision;
			int i;
			int th;
			int tq;
			for (;;)
			{
				bool flag3 = p + 1 >= data.Length;
				if (flag3)
				{
					break;
				}
				bool flag4 = data[p] != byte.MaxValue;
				if (flag4)
				{
					goto Block_6;
				}
				while (p < data.Length && data[p] == 255)
				{
					p++;
				}
				marker = data[p++];
				bool flag5 = marker == 217;
				if (flag5)
				{
					goto Block_9;
				}
				bool flag6 = marker == 1 || (marker >= 208 && marker <= 215);
				if (!flag6)
				{
					bool flag7 = p + 1 >= data.Length;
					if (flag7)
					{
						goto Block_13;
					}
					segLen = ((int)data[p] << 8 | (int)data[p + 1]);
					bool flag8 = segLen < 2 || p + segLen > data.Length;
					if (flag8)
					{
						goto Block_15;
					}
					int segStart = p + 2;
					int segEnd = p + segLen;
					switch (marker)
					{
					case 192:
					case 193:
					{
						precision = (int)data[segStart];
						bool flag9 = precision != 8;
						if (flag9)
						{
							goto Block_26;
						}
						frameHeight = ((int)data[segStart + 1] << 8 | (int)data[segStart + 2]);
						frameWidth = ((int)data[segStart + 3] << 8 | (int)data[segStart + 4]);
						i = (int)data[segStart + 5];
						bool flag10 = i != 1 && i != 3;
						if (flag10)
						{
							goto Block_28;
						}
						components = new BaselineJpegDecoder.Component[i];
						int q = segStart + 6;
						for (int j = 0; j < i; j++)
						{
							components[j] = new BaselineJpegDecoder.Component
							{
								Id = (int)data[q],
								H = data[q + 1] >> 4,
								V = (int)(data[q + 1] & 15),
								QuantTable = (int)data[q + 2]
							};
							bool flag11 = components[j].H < 1 || components[j].V < 1;
							if (flag11)
							{
								goto Block_30;
							}
							q += 3;
						}
						hMax = 1;
						vMax = 1;
						foreach (BaselineJpegDecoder.Component c in components)
						{
							bool flag12 = c.H > hMax;
							if (flag12)
							{
								hMax = c.H;
							}
							bool flag13 = c.V > vMax;
							if (flag13)
							{
								vMax = c.V;
							}
						}
						mcusPerLine = (frameWidth + 8 * hMax - 1) / (8 * hMax);
						mcusPerColumn = (frameHeight + 8 * vMax - 1) / (8 * vMax);
						foreach (BaselineJpegDecoder.Component c2 in components)
						{
							c2.PixelsPerLine = mcusPerLine * c2.H * 8;
							c2.PixelsPerColumn = mcusPerColumn * c2.V * 8;
							c2.Pixels = new byte[c2.PixelsPerLine * c2.PixelsPerColumn];
						}
						break;
					}
					case 194:
						goto IL_5F5;
					case 195:
					case 197:
					case 198:
					case 199:
					case 201:
					case 202:
					case 203:
					case 205:
					case 206:
					case 207:
						goto IL_600;
					case 196:
					{
						int q2 = segStart;
						while (q2 < segEnd)
						{
							int tc = data[q2] >> 4;
							th = (int)(data[q2] & 15);
							q2++;
							bool flag14 = th > 3;
							if (flag14)
							{
								goto Block_21;
							}
							int[] counts = new int[17];
							int total = 0;
							for (int k = 1; k <= 16; k++)
							{
								counts[k] = (int)data[q2++];
								total += counts[k];
							}
							bool flag15 = q2 + total > segEnd;
							if (flag15)
							{
								goto Block_23;
							}
							byte[] values = new byte[total];
							Array.Copy(data, q2, values, 0, total);
							q2 += total;
							BaselineJpegDecoder.HuffTable t = BaselineJpegDecoder.BuildHuffTable(counts, values);
							bool flag16 = tc == 0;
							if (flag16)
							{
								dcTables[th] = t;
							}
							else
							{
								acTables[th] = t;
							}
						}
						break;
					}
					case 218:
					{
						bool flag17 = components == null;
						if (flag17)
						{
							goto Block_36;
						}
						int ns = (int)data[segStart];
						int q3 = segStart + 1;
						BaselineJpegDecoder.Component[] scan = new BaselineJpegDecoder.Component[ns];
						for (int l = 0; l < ns; l++)
						{
							int cs = (int)data[q3];
							BaselineJpegDecoder.Component c3 = BaselineJpegDecoder.FindComponent(components, cs);
							c3.DcTable = data[q3 + 1] >> 4;
							c3.AcTable = (int)(data[q3 + 1] & 15);
							scan[l] = c3;
							q3 += 2;
						}
						q3 += 3;
						p = BaselineJpegDecoder.DecodeScan(data, q3, scan, dcTables, acTables, quant, mcusPerLine, mcusPerColumn, hMax, vMax, restartInterval);
						continue;
					}
					case 219:
					{
						int q4 = segStart;
						while (q4 < segEnd)
						{
							int pq = data[q4] >> 4;
							tq = (int)(data[q4] & 15);
							q4++;
							bool flag18 = tq > 3;
							if (flag18)
							{
								goto Block_17;
							}
							int[] table = new int[64];
							for (int m = 0; m < 64; m++)
							{
								bool flag19 = pq == 0;
								int v;
								if (flag19)
								{
									v = (int)data[q4++];
								}
								else
								{
									v = ((int)data[q4] << 8 | (int)data[q4 + 1]);
									q4 += 2;
								}
								table[BaselineJpegDecoder.ZigZag[m]] = v;
							}
							quant[tq] = table;
						}
						break;
					}
					case 221:
						restartInterval = ((int)data[segStart] << 8 | (int)data[segStart + 1]);
						break;
					}
					p = segEnd;
				}
			}
			throw new JpegDecodeException("truncated before EOI");
			Block_6:
			throw new JpegDecodeException(string.Format("lost marker sync at {0}", p));
			Block_9:
			bool flag20 = components == null || frameWidth <= 0 || frameHeight <= 0;
			if (flag20)
			{
				throw new JpegDecodeException("no frame decoded");
			}
			width = frameWidth;
			height = frameHeight;
			return BaselineJpegDecoder.ToRgb(components, dst, frameWidth, frameHeight, hMax, vMax);
			Block_13:
			throw new JpegDecodeException("truncated segment length");
			Block_15:
			throw new JpegDecodeException(string.Format("bad segment length {0} at {1}", segLen, p));
			Block_17:
			throw new JpegDecodeException(string.Format("bad quant table id {0}", tq));
			Block_21:
			throw new JpegDecodeException(string.Format("bad huffman table id {0}", th));
			Block_23:
			throw new JpegDecodeException("truncated huffman values");
			Block_26:
			throw new JpegDecodeException(string.Format("unsupported precision {0} (need 8)", precision));
			Block_28:
			throw new JpegDecodeException(string.Format("unsupported component count {0}", i));
			Block_30:
			throw new JpegDecodeException("bad sampling factor");
			IL_5F5:
			throw new JpegDecodeException("progressive JPEG (SOF2) is out of scope — the providers were measured as baseline; if this fires, a provider changed and the decoder needs a progressive path");
			IL_600:
			throw new JpegDecodeException(string.Format("unsupported frame type SOF marker 0x{0:X2}", marker));
			Block_36:
			throw new JpegDecodeException("SOS before SOF");
		}

		// Token: 0x06000053 RID: 83 RVA: 0x000035E0 File Offset: 0x000017E0
		private static BaselineJpegDecoder.Component FindComponent(BaselineJpegDecoder.Component[] components, int id)
		{
			foreach (BaselineJpegDecoder.Component c in components)
			{
				bool flag = c.Id == id;
				if (flag)
				{
					return c;
				}
			}
			throw new JpegDecodeException(string.Format("scan references unknown component {0}", id));
		}

		// Token: 0x06000054 RID: 84 RVA: 0x00003630 File Offset: 0x00001830
		private static BaselineJpegDecoder.HuffTable BuildHuffTable(int[] counts, byte[] values)
		{
			BaselineJpegDecoder.HuffTable t = new BaselineJpegDecoder.HuffTable
			{
				Values = values
			};
			int code = 0;
			int i = 0;
			for (int j = 1; j <= 16; j++)
			{
				t.ValPtr[j] = i;
				t.MinCode[j] = code;
				code += counts[j];
				i += counts[j];
				t.MaxCode[j] = ((counts[j] > 0) ? (code - 1) : -1);
				code <<= 1;
			}
			return t;
		}

		// Token: 0x06000055 RID: 85 RVA: 0x000036A4 File Offset: 0x000018A4
		private static int DecodeHuff(BaselineJpegDecoder.BitReader br, BaselineJpegDecoder.HuffTable t)
		{
			bool flag = t == null;
			if (flag)
			{
				throw new JpegDecodeException("scan references an undefined huffman table");
			}
			int code = br.ReadBit();
			int i = 1;
			while (i <= 16)
			{
				bool flag2 = t.MaxCode[i] >= 0 && code <= t.MaxCode[i];
				if (flag2)
				{
					int idx = t.ValPtr[i] + code - t.MinCode[i];
					bool flag3 = idx < 0 || idx >= t.Values.Length;
					if (flag3)
					{
						throw new JpegDecodeException("huffman code out of range");
					}
					return (int)t.Values[idx];
				}
				else
				{
					code = (code << 1 | br.ReadBit());
					i++;
				}
			}
			throw new JpegDecodeException("bad huffman code (>16 bits)");
		}

		/// <summary>Sign-extend an s-bit magnitude (spec F.12): values in the lower half of the range are
		/// negative.</summary>
		// Token: 0x06000056 RID: 86 RVA: 0x0000376A File Offset: 0x0000196A
		private static int Extend(int v, int s)
		{
			return (v < 1 << s - 1) ? (v - (1 << s) + 1) : v;
		}

		// Token: 0x06000057 RID: 87 RVA: 0x00003784 File Offset: 0x00001984
		private static int DecodeScan(byte[] data, int pos, BaselineJpegDecoder.Component[] scan, BaselineJpegDecoder.HuffTable[] dcTables, BaselineJpegDecoder.HuffTable[] acTables, int[][] quant, int mcusPerLine, int mcusPerColumn, int hMax, int vMax, int restartInterval)
		{
			BaselineJpegDecoder.BitReader br = new BaselineJpegDecoder.BitReader(data, pos);
			int[] coeffs = new int[64];
			float[] block = new float[64];
			float[] idctScratch = new float[64];
			foreach (BaselineJpegDecoder.Component c in scan)
			{
				c.Pred = 0;
			}
			int mcuCount = mcusPerLine * mcusPerColumn;
			int sinceRestart = 0;
			for (int mcu = 0; mcu < mcuCount; mcu++)
			{
				bool flag = restartInterval > 0 && sinceRestart == restartInterval;
				if (flag)
				{
					br.SyncToRestart();
					foreach (BaselineJpegDecoder.Component c2 in scan)
					{
						c2.Pred = 0;
					}
					sinceRestart = 0;
				}
				sinceRestart++;
				int mcuRow = mcu / mcusPerLine;
				int mcuCol = mcu % mcusPerLine;
				foreach (BaselineJpegDecoder.Component c3 in scan)
				{
					int[] array = quant[c3.QuantTable];
					if (array == null)
					{
						throw new JpegDecodeException(string.Format("component {0} references undefined quant table {1}", c3.Id, c3.QuantTable));
					}
					int[] qt = array;
					for (int by = 0; by < c3.V; by++)
					{
						for (int bx = 0; bx < c3.H; bx++)
						{
							Array.Clear(coeffs, 0, 64);
							int s = BaselineJpegDecoder.DecodeHuff(br, dcTables[c3.DcTable]);
							int diff = (s == 0) ? 0 : BaselineJpegDecoder.Extend(br.ReadBits(s), s);
							c3.Pred += diff;
							coeffs[0] = c3.Pred * qt[0];
							int i = 1;
							while (i < 64)
							{
								int rs = BaselineJpegDecoder.DecodeHuff(br, acTables[c3.AcTable]);
								int r = rs >> 4;
								int sz = rs & 15;
								bool flag2 = sz == 0;
								if (flag2)
								{
									bool flag3 = r != 15;
									if (flag3)
									{
										break;
									}
									i += 16;
								}
								else
								{
									i += r;
									bool flag4 = i > 63;
									if (flag4)
									{
										break;
									}
									int natural = BaselineJpegDecoder.ZigZag[i];
									coeffs[natural] = BaselineJpegDecoder.Extend(br.ReadBits(sz), sz) * qt[natural];
									i++;
								}
							}
							int px = (mcuCol * c3.H + bx) * 8;
							int py = (mcuRow * c3.V + by) * 8;
							BaselineJpegDecoder.Idct(coeffs, block, idctScratch);
							BaselineJpegDecoder.StoreBlock(c3, block, px, py);
						}
					}
				}
			}
			int q = br.Pos;
			while (q + 1 < data.Length && (data[q] != 255 || data[q + 1] == 0))
			{
				q++;
			}
			return q;
		}

		/// <summary>Separable 2D inverse DCT. Rows then columns against the precomputed cosine table —
		/// deliberately the clear formulation rather than a scaled/integer fast path (§6: correctness first).
		/// <paramref name="tmp" /> is caller-owned scratch: this runs ~1500 times per tile, so allocating it here
		/// would be pure GC churn. (No stackalloc/Span — this assembly targets net4.8, where Span is a package
		/// dependency Mirage doesn't take.)</summary>
		// Token: 0x06000058 RID: 88 RVA: 0x00003A68 File Offset: 0x00001C68
		private static void Idct(int[] coeffs, float[] outBlock, float[] tmp)
		{
			for (int y = 0; y < 8; y++)
			{
				int row = y * 8;
				for (int x = 0; x < 8; x++)
				{
					float sum = 0f;
					for (int u = 0; u < 8; u++)
					{
						int c = coeffs[row + u];
						bool flag = c != 0;
						if (flag)
						{
							sum += BaselineJpegDecoder.CosTable[x, u] * (float)c;
						}
					}
					tmp[row + x] = sum;
				}
			}
			for (int x2 = 0; x2 < 8; x2++)
			{
				for (int y2 = 0; y2 < 8; y2++)
				{
					float sum2 = 0f;
					for (int v = 0; v < 8; v++)
					{
						sum2 += BaselineJpegDecoder.CosTable[y2, v] * tmp[v * 8 + x2];
					}
					outBlock[y2 * 8 + x2] = sum2;
				}
			}
		}

		// Token: 0x06000059 RID: 89 RVA: 0x00003B60 File Offset: 0x00001D60
		private static void StoreBlock(BaselineJpegDecoder.Component c, float[] block, int px, int py)
		{
			for (int y = 0; y < 8; y++)
			{
				int oy = py + y;
				bool flag = oy >= c.PixelsPerColumn;
				if (flag)
				{
					break;
				}
				int dst = oy * c.PixelsPerLine + px;
				for (int x = 0; x < 8; x++)
				{
					int ox = px + x;
					bool flag2 = ox >= c.PixelsPerLine;
					if (flag2)
					{
						break;
					}
					int v = (int)Math.Round((double)block[y * 8 + x]) + 128;
					c.Pixels[dst + x] = ((v < 0) ? 0 : ((v > 255) ? byte.MaxValue : ((byte)v)));
				}
			}
		}

		// Token: 0x0600005A RID: 90 RVA: 0x00003C1C File Offset: 0x00001E1C
		private static byte[] ToRgb(BaselineJpegDecoder.Component[] components, byte[] dst, int width, int height, int hMax, int vMax)
		{
			int need = width * height * 3;
			bool flag = dst != null && dst.Length < need;
			if (flag)
			{
				throw new JpegDecodeException(string.Format("dst holds {0} bytes, need {1} for {2}x{3} RGB.", new object[]
				{
					dst.Length,
					need,
					width,
					height
				}));
			}
			byte[] rgb = dst ?? new byte[need];
			bool flag2 = components.Length == 1;
			byte[] result;
			if (flag2)
			{
				BaselineJpegDecoder.Component g = components[0];
				for (int y = 0; y < height; y++)
				{
					for (int x = 0; x < width; x++)
					{
						byte v = g.Pixels[y * g.PixelsPerLine + x];
						int o = (y * width + x) * 3;
						rgb[o] = (rgb[o + 1] = (rgb[o + 2] = v));
					}
				}
				result = rgb;
			}
			else
			{
				BaselineJpegDecoder.Component cy = components[0];
				BaselineJpegDecoder.Component cb = components[1];
				BaselineJpegDecoder.Component cr = components[2];
				for (int y2 = 0; y2 < height; y2++)
				{
					int yyRow = y2 * cy.V / vMax;
					int cbRow = y2 * cb.V / vMax;
					int crRow = y2 * cr.V / vMax;
					for (int x2 = 0; x2 < width; x2++)
					{
						float yy = (float)cy.Pixels[yyRow * cy.PixelsPerLine + x2 * cy.H / hMax];
						float pb = (float)cb.Pixels[cbRow * cb.PixelsPerLine + x2 * cb.H / hMax] - 128f;
						float pr = (float)cr.Pixels[crRow * cr.PixelsPerLine + x2 * cr.H / hMax] - 128f;
						int o2 = (y2 * width + x2) * 3;
						rgb[o2] = BaselineJpegDecoder.Clamp(yy + 1.402f * pr);
						rgb[o2 + 1] = BaselineJpegDecoder.Clamp(yy - 0.344136f * pb - 0.714136f * pr);
						rgb[o2 + 2] = BaselineJpegDecoder.Clamp(yy + 1.772f * pb);
					}
				}
				result = rgb;
			}
			return result;
		}

		// Token: 0x0600005B RID: 91 RVA: 0x00003E50 File Offset: 0x00002050
		private static byte Clamp(float v)
		{
			int i = (int)Math.Round((double)v);
			return (i < 0) ? 0 : ((i > 255) ? byte.MaxValue : ((byte)i));
		}

		// Token: 0x0400003A RID: 58
		private static readonly int[] ZigZag = new int[]
		{
			0,
			1,
			8,
			16,
			9,
			2,
			3,
			10,
			17,
			24,
			32,
			25,
			18,
			11,
			4,
			5,
			12,
			19,
			26,
			33,
			40,
			48,
			41,
			34,
			27,
			20,
			13,
			6,
			7,
			14,
			21,
			28,
			35,
			42,
			49,
			56,
			57,
			50,
			43,
			36,
			29,
			22,
			15,
			23,
			30,
			37,
			44,
			51,
			58,
			59,
			52,
			45,
			38,
			31,
			39,
			46,
			53,
			60,
			61,
			54,
			47,
			55,
			62,
			63
		};

		// Token: 0x0400003B RID: 59
		private static readonly float[,] CosTable = BaselineJpegDecoder.BuildCosTable();

		// Token: 0x0200007C RID: 124
		private sealed class HuffTable
		{
			// Token: 0x040002EE RID: 750
			public readonly int[] MinCode = new int[17];

			// Token: 0x040002EF RID: 751
			public readonly int[] MaxCode = new int[17];

			// Token: 0x040002F0 RID: 752
			public readonly int[] ValPtr = new int[17];

			// Token: 0x040002F1 RID: 753
			public byte[] Values;
		}

		// Token: 0x0200007D RID: 125
		private sealed class Component
		{
			// Token: 0x040002F2 RID: 754
			public int Id;

			// Token: 0x040002F3 RID: 755
			public int H;

			// Token: 0x040002F4 RID: 756
			public int V;

			// Token: 0x040002F5 RID: 757
			public int QuantTable;

			// Token: 0x040002F6 RID: 758
			public int DcTable;

			// Token: 0x040002F7 RID: 759
			public int AcTable;

			// Token: 0x040002F8 RID: 760
			public int Pred;

			// Token: 0x040002F9 RID: 761
			public int PixelsPerLine;

			// Token: 0x040002FA RID: 762
			public int PixelsPerColumn;

			// Token: 0x040002FB RID: 763
			public byte[] Pixels;
		}

		// Token: 0x0200007E RID: 126
		private sealed class BitReader
		{
			// Token: 0x0600042C RID: 1068 RVA: 0x0001BC94 File Offset: 0x00019E94
			public BitReader(byte[] data, int pos)
			{
				this.data = data;
				this.Pos = pos;
			}

			// Token: 0x0600042D RID: 1069 RVA: 0x0001BCAC File Offset: 0x00019EAC
			public void Reset()
			{
				this.buf = 0;
				this.count = 0;
			}

			// Token: 0x0600042E RID: 1070 RVA: 0x0001BCC0 File Offset: 0x00019EC0
			public int ReadBit()
			{
				bool flag = this.count == 0;
				if (flag)
				{
					bool flag2 = this.Pos >= this.data.Length;
					if (flag2)
					{
						return 0;
					}
					byte[] array = this.data;
					int pos = this.Pos;
					this.Pos = pos + 1;
					byte b = array[pos];
					bool flag3 = b == byte.MaxValue;
					if (flag3)
					{
						byte next = (this.Pos < this.data.Length) ? this.data[this.Pos] : 217;
						bool flag4 = next == 0;
						if (!flag4)
						{
							this.Pos--;
							return 0;
						}
						this.Pos++;
					}
					this.buf = (int)b;
					this.count = 8;
				}
				this.count--;
				return this.buf >> this.count & 1;
			}

			// Token: 0x0600042F RID: 1071 RVA: 0x0001BDB0 File Offset: 0x00019FB0
			public int ReadBits(int n)
			{
				int v = 0;
				for (int i = 0; i < n; i++)
				{
					v = (v << 1 | this.ReadBit());
				}
				return v;
			}

			/// <summary>Skip to just past an RSTn marker at a restart boundary.</summary>
			// Token: 0x06000430 RID: 1072 RVA: 0x0001BDE0 File Offset: 0x00019FE0
			public void SyncToRestart()
			{
				this.Reset();
				while (this.Pos + 1 < this.data.Length)
				{
					bool flag = this.data[this.Pos] == byte.MaxValue && this.data[this.Pos + 1] >= 208 && this.data[this.Pos + 1] <= 215;
					if (flag)
					{
						this.Pos += 2;
						break;
					}
					this.Pos++;
				}
			}

			// Token: 0x040002FC RID: 764
			private readonly byte[] data;

			// Token: 0x040002FD RID: 765
			public int Pos;

			// Token: 0x040002FE RID: 766
			private int buf;

			// Token: 0x040002FF RID: 767
			private int count;
		}
	}
}
