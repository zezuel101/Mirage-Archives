using System;

namespace Mirage.WebIngest
{
	/// <summary>
	/// Reads a JPEG's headers straight from the response bytes — no decode, no Texture2D, no GPU.
	///
	/// This replaces (rather than ports) GeoStream's download validation. That check ran on the *decoded*
	/// Texture2D's width/height, because it used DownloadHandlerTexture; WebIngest §3 swaps to
	/// DownloadHandlerBuffer precisely so Unity never decodes or uploads for us, which leaves no texture to
	/// measure. The bug being defended against is the same one and it is worth restating, because it poisoned
	/// GeoStream's cache permanently: an HTTP-200 non-image body (a maintenance page, a rate-limit notice) made
	/// Unity's loader hand back its 8x8 red-"?" placeholder, which then got upscaled into a perfectly VALID
	/// 256x256 garbage tile and cached forever. Structural validation of the actual bytes is strictly stronger
	/// than measuring whatever Unity produced from them.
	///
	/// It also answers WebIngest §11 decision 3 ("confirm baseline vs progressive from EOX + GIBS") empirically
	/// at runtime instead of by assumption — <see cref="F:Mirage.WebIngest.JpegInfo.Kind" /> is exactly that answer, per response.
	///
	/// Deliberately NOT a flat-colour check: EOX deep-ocean and polar ice tiles are legitimately near-uniform,
	/// so flatness must never gate a download (it would reject the entire ocean).
	/// </summary>
	// Token: 0x0200001E RID: 30
	public static class JpegProbe
	{
		// Token: 0x060000AC RID: 172 RVA: 0x00006E03 File Offset: 0x00005003
		public static JpegInfo Probe(byte[] data)
		{
			return JpegProbe.Probe(data, (data != null) ? data.Length : 0);
		}

		// Token: 0x060000AD RID: 173 RVA: 0x00006E14 File Offset: 0x00005014
		public static JpegInfo Probe(byte[] data, int length)
		{
			bool flag = data == null || length < 128;
			JpegInfo result;
			if (flag)
			{
				result = JpegInfo.Fail(string.Format("body too short ({0} bytes)", length));
			}
			else
			{
				bool flag2 = data[0] != byte.MaxValue || data[1] != 216;
				if (flag2)
				{
					result = JpegInfo.Fail(string.Format("not a JPEG (magic {0:X2}{1:X2}, expected FFD8)", data[0], data[1]));
				}
				else
				{
					int p = 2;
					int restartInterval = 0;
					while (p + 1 < length)
					{
						bool flag3 = data[p] != byte.MaxValue;
						if (flag3)
						{
							return JpegInfo.Fail(string.Format("lost marker sync at byte {0}", p));
						}
						while (p < length && data[p] == 255)
						{
							p++;
						}
						bool flag4 = p >= length;
						if (flag4)
						{
							return JpegInfo.Fail("truncated at marker");
						}
						byte marker = data[p++];
						bool flag5 = marker == 1 || (marker >= 208 && marker <= 215);
						if (!flag5)
						{
							bool flag6 = marker == 217;
							if (flag6)
							{
								return JpegInfo.Fail("EOI before frame header");
							}
							bool flag7 = p + 1 >= length;
							if (flag7)
							{
								return JpegInfo.Fail("truncated segment length");
							}
							int segLen = (int)data[p] << 8 | (int)data[p + 1];
							bool flag8 = segLen < 2 || p + segLen > length;
							if (flag8)
							{
								return JpegInfo.Fail(string.Format("bad segment length {0} at byte {1}", segLen, p));
							}
							byte b = marker;
							byte b2 = b;
							switch (b2)
							{
							case 192:
							case 193:
							case 194:
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
							{
								bool flag9 = segLen < 8;
								if (flag9)
								{
									return JpegInfo.Fail("truncated frame header");
								}
								int height = (int)data[p + 3] << 8 | (int)data[p + 4];
								int width = (int)data[p + 5] << 8 | (int)data[p + 6];
								int components = (int)data[p + 7];
								if (!true)
								{
								}
								JpegFrameKind jpegFrameKind;
								if (marker - 192 > 1)
								{
									if (marker != 194)
									{
										jpegFrameKind = JpegFrameKind.Unsupported;
									}
									else
									{
										jpegFrameKind = JpegFrameKind.Progressive;
									}
								}
								else
								{
									jpegFrameKind = JpegFrameKind.Baseline;
								}
								if (!true)
								{
								}
								JpegFrameKind kind = jpegFrameKind;
								bool flag10 = !JpegProbe.IsValidDimensions(width, height);
								if (flag10)
								{
									return JpegInfo.Fail(string.Format("implausible tile dimensions {0}x{1} (min {2})", width, height, 64));
								}
								int maxH = 1;
								int maxV = 1;
								for (int i = 0; i < components; i++)
								{
									int off = p + 8 + i * 3;
									bool flag11 = off + 1 >= p + segLen;
									if (flag11)
									{
										return JpegInfo.Fail("truncated component spec");
									}
									int h = data[off + 1] >> 4;
									int v = (int)(data[off + 1] & 15);
									bool flag12 = h > maxH;
									if (flag12)
									{
										maxH = h;
									}
									bool flag13 = v > maxV;
									if (flag13)
									{
										maxV = v;
									}
								}
								return new JpegInfo(true, kind, width, height, components, restartInterval, maxH, maxV, null);
							}
							case 196:
							case 200:
							case 204:
							case 208:
							case 209:
							case 210:
							case 211:
							case 212:
							case 213:
							case 214:
							case 215:
							case 216:
							case 217:
								break;
							case 218:
								return JpegInfo.Fail("scan started before any frame header");
							default:
								if (b2 == 221)
								{
									bool flag14 = segLen >= 4;
									if (flag14)
									{
										restartInterval = ((int)data[p + 2] << 8 | (int)data[p + 3]);
									}
								}
								break;
							}
							p += segLen;
						}
					}
					result = JpegInfo.Fail("no frame header found");
				}
			}
			return result;
		}

		// Token: 0x060000AE RID: 174 RVA: 0x000071E6 File Offset: 0x000053E6
		public static bool IsValidDimensions(int width, int height)
		{
			return width >= 64 && height >= 64;
		}

		/// <summary>
		/// PNG magic. Load-bearing, and NOT a theoretical case — this is how EOX signals "no imagery here".
		///
		/// Measured against the live endpoint: a `.jpg` tile URL over a region s2cloudless doesn't cover returns
		/// <b>HTTP 200</b>, <c>Content-Type: image/png</c>, and a 116-byte fully-transparent <b>256x256</b> PNG.
		/// Every field a naive validator checks is fine: the status is 200, the type starts with "image", and the
		/// dimensions are exactly the 256x256 a real tile has. GeoStream's own validation — Content-Type prefix
		/// plus a &gt;=64px dimension floor on the decoded texture — passes this and would cache a transparent tile
		/// as genuine imagery forever. That is precisely the failure §4 warns about ("the baker must never write
		/// an empty/placeholder tile — blank tiles cached as real imagery"), and dimension checks cannot catch it.
		///
		/// The reliable discriminator is the container: we asked a JPEG endpoint and got a PNG. Coverage gaps are
		/// not rare or polar-only — measured, s2cloudless has NO z14 over open Pacific, Greenland, or the
		/// antimeridian, while z9-z12 over the same spots return real JPEG.
		///
		/// <b>This tests ONLY "is this a PNG".</b> It was once called <c>IsPngSentinel</c>, and the name did the
		/// damage: a sentinel is a *mismatch* between what was asked for and what arrived, so the caller must
		/// supply the other half of that comparison. `WebTileFetcher` didn't — it called this unconditionally,
		/// and once a PNG DEM provider (Terrarium) existed, every legitimate elevation tile read as "no imagery
		/// here" and the whole planet was declined as permanently uncoverable. Use
		/// <see cref="P:Mirage.WebIngest.ImageryProvider.Format" /> to decide whether a PNG is a gap or the payload.
		/// </summary>
		// Token: 0x060000AF RID: 175 RVA: 0x000071F8 File Offset: 0x000053F8
		public static bool IsPng(byte[] data, int length)
		{
			return data != null && length >= 8 && data[0] == 137 && data[1] == 80 && data[2] == 78 && data[3] == 71 && data[4] == 13 && data[5] == 10 && data[6] == 26 && data[7] == 10;
		}

		/// <summary>
		/// Does the body's container match what this provider serves? A false means the endpoint answered with
		/// something other than its payload format — for a <c>.jpg</c> imagery endpoint that is EOX's coverage
		/// gap; for anything else it is a broken response.
		/// </summary>
		// Token: 0x060000B0 RID: 176 RVA: 0x0000724A File Offset: 0x0000544A
		public static bool ContainerMatches(TileImageFormat expected, byte[] data, int length)
		{
			return (expected == TileImageFormat.Png) ? JpegProbe.IsPng(data, length) : JpegProbe.IsJpeg(data, length);
		}

		// Token: 0x060000B1 RID: 177 RVA: 0x00007260 File Offset: 0x00005460
		private static bool IsJpeg(byte[] data, int length)
		{
			return data != null && length >= 2 && data[0] == byte.MaxValue && data[1] == 216;
		}

		/// <summary>Minimal structural gate for a PNG body: magic plus a plausible size. The full decode
		/// (<see cref="T:Mirage.WebIngest.PngDecoder" />) is the real validator — this only keeps obvious garbage out of the cache,
		/// which is the check whose absence poisoned GeoStream's.</summary>
		// Token: 0x060000B2 RID: 178 RVA: 0x00007280 File Offset: 0x00005480
		public static bool IsPlausiblePng(byte[] data, int length)
		{
			return JpegProbe.IsPng(data, length) && length >= 128;
		}

		/// <summary>Content-Type gate. Empty is tolerated — some CDNs omit the header — but a header that
		/// positively claims non-image (text/html on an error page) is a reject.</summary>
		// Token: 0x060000B3 RID: 179 RVA: 0x00007299 File Offset: 0x00005499
		public static bool ContentTypeLooksLikeImage(string contentType)
		{
			return string.IsNullOrEmpty(contentType) || contentType.StartsWith("image", StringComparison.OrdinalIgnoreCase);
		}

		/// <summary>Unity's failure placeholder is 8x8; real tiles are 256 (occasionally 512 elsewhere). 64 is a
		/// generous floor that rejects the placeholder and never a real tile.</summary>
		// Token: 0x040000A4 RID: 164
		public const int MinValidDimension = 64;

		/// <summary>Smallest plausible JPEG. Anything shorter is a truncated/empty body, not imagery.</summary>
		// Token: 0x040000A5 RID: 165
		private const int MinPlausibleBytes = 128;
	}
}
