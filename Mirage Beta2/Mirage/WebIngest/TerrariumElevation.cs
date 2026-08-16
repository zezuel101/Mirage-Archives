using System;

namespace Mirage.WebIngest
{
	/// <summary>
	/// Decodes Terrarium terrain-RGB tiles to metres, and rejects the ones that carry no data. WebIngest §4.5.
	///
	/// The encoding is <c>R·256 + G + B/256 − 32768</c> metres — see <see cref="F:Mirage.WebIngest.ImageryProvider.TerrariumDem" />
	/// for why this must be read from PNG and never JPEG.
	///
	/// <b>The no-data trap, and why it needs its own guard.</b> EOX's missing imagery announces itself: a
	/// <c>.jpg</c> endpoint returns a PNG, so the container disagrees with the request and
	/// <see cref="T:Mirage.WebIngest.JpegProbe" /> catches it. Terrarium's missing elevation announces nothing. It returns HTTP 200
	/// with a valid PNG that decodes to valid elevations — every one of them exactly 0 m. Nothing about the
	/// bytes is malformed; only the terrain is a lie. (Measured: the antimeridian at z12 is 757 bytes against
	/// 73–130 KB for real tiles, because a constant image deflates to nearly nothing.)
	///
	/// Baking that tile writes a sea-level plateau into the archive and — worse — the archive is authoritative,
	/// so the false flat would persist and the CPU collision mesh would agree with it. This is §4's "the baker
	/// must never write an empty/placeholder tile" in its hardest form.
	///
	/// <b>The rule is EXACTLY-constant-zero</b> (<see cref="M:Mirage.WebIngest.TerrariumElevation.IsNoData(System.Single[])" />), which is deliberately narrower than
	/// "flat". Real terrain — including bathymetry, which is interpolated from sparse soundings and is the
	/// flattest real thing here — is never flat to the millimetre across 65k texels. Note this is a different
	/// test from the flat-colour check §12 explicitly forbids for imagery: EOX's deep-ocean tiles are
	/// legitimately uniform, so a "looks flat" heuristic would reject good data there. Here the constant is not
	/// merely uniform, it is uniform at precisely the encoding's zero.
	/// </summary>
	// Token: 0x02000027 RID: 39
	public static class TerrariumElevation
	{
		/// <summary>Decode a Terrarium PNG to metres, row-major, one float per texel.
		/// Returns false if the PNG is undecodable OR if the tile is the all-zero no-data sentinel — in both
		/// cases there is no elevation here and the caller must decline the tile rather than bake a flat one.</summary>
		// Token: 0x060000EB RID: 235 RVA: 0x00008E6C File Offset: 0x0000706C
		public static bool TryDecode(byte[] png, out float[] metres, out int width, out int height)
		{
			metres = null;
			width = (height = 0);
			byte[] rgb;
			bool flag = !TerrariumElevation.DecodeRgb(png, null, out rgb, out width, out height);
			bool result;
			if (flag)
			{
				result = false;
			}
			else
			{
				int count = width * height;
				metres = new float[count];
				result = TerrariumElevation.Fill(rgb, metres, count);
			}
			return result;
		}

		/// <summary>
		/// As <see cref="M:Mirage.WebIngest.TerrariumElevation.TryDecode(System.Byte[],System.Single[]@,System.Int32@,System.Int32@)" /> but writes into a caller-supplied
		/// buffer, so a bake can recycle it (see <see cref="T:Mirage.WebIngest.BufferPool" />) — and returns the tri-state
		/// <see cref="T:Mirage.WebIngest.TerrariumElevation.DemDecode" /> rather than a bool, because "all-zero ocean" and "undecodable" must not be
		/// treated alike: the former is sea level (bake it), the latter is a fetch/decode fault (retry). On
		/// <see cref="F:Mirage.WebIngest.TerrariumElevation.DemDecode.NoData" /> the buffer holds valid zeros; on <see cref="F:Mirage.WebIngest.TerrariumElevation.DemDecode.DecodeError" /> it
		/// is left as-is.
		///
		/// <paramref name="dst" /> must hold width*height floats — for a web source that is always
		/// <c>MercatorTileMath.TilePx</c> squared, which the caller knows before decoding. A short buffer throws
		/// rather than silently decoding part of a tile.
		///
		/// <paramref name="rgbScratch" /> is an optional RGB24 buffer (length ≥ w·h·3) for the intermediate PNG
		/// decode. Supplying a pooled one keeps the ~196 KB/tile intermediate off the GC (see
		/// <see cref="T:Mirage.WebIngest.BufferPool" />); null allocates it per call.
		/// </summary>
		// Token: 0x060000EC RID: 236 RVA: 0x00008EBC File Offset: 0x000070BC
		public static TerrariumElevation.DemDecode DecodeInto(byte[] png, float[] dst, out int width, out int height, byte[] rgbScratch = null)
		{
			width = (height = 0);
			bool flag = dst == null;
			if (flag)
			{
				throw new ArgumentNullException("dst");
			}
			byte[] rgb;
			bool flag2 = !TerrariumElevation.DecodeRgb(png, rgbScratch, out rgb, out width, out height);
			TerrariumElevation.DemDecode result;
			if (flag2)
			{
				result = TerrariumElevation.DemDecode.DecodeError;
			}
			else
			{
				int count = width * height;
				bool flag3 = dst.Length < count;
				if (flag3)
				{
					throw new ArgumentException(string.Format("TerrariumElevation: dst holds {0} floats, need {1} ({2}x{3}).", new object[]
					{
						dst.Length,
						count,
						width,
						height
					}), "dst");
				}
				result = (TerrariumElevation.Fill(rgb, dst, count) ? TerrariumElevation.DemDecode.Ok : TerrariumElevation.DemDecode.NoData);
			}
			return result;
		}

		// Token: 0x060000ED RID: 237 RVA: 0x00008F68 File Offset: 0x00007168
		private static bool DecodeRgb(byte[] png, byte[] rgbScratch, out byte[] rgb, out int width, out int height)
		{
			rgb = null;
			width = (height = 0);
			bool result;
			try
			{
				rgb = ((rgbScratch != null) ? PngDecoder.DecodeToRgbInto(png, rgbScratch, out width, out height) : PngDecoder.DecodeToRgb(png, out width, out height));
				result = true;
			}
			catch (Exception)
			{
				result = false;
			}
			return result;
		}

		// Token: 0x060000EE RID: 238 RVA: 0x00008FB8 File Offset: 0x000071B8
		private static bool Fill(byte[] rgb, float[] dst, int count)
		{
			bool anyNonZero = false;
			for (int i = 0; i < count; i++)
			{
				float j = (float)((double)rgb[i * 3] * 256.0 + (double)rgb[i * 3 + 1] + (double)rgb[i * 3 + 2] / 256.0 - 32768.0);
				dst[i] = j;
				anyNonZero |= (j != 0f);
			}
			return anyNonZero;
		}

		/// <summary>True if every texel is exactly 0 m — Terrarium's silent no-data. See the class remarks for
		/// why exactness (rather than flatness) is the test.</summary>
		// Token: 0x060000EF RID: 239 RVA: 0x0000902C File Offset: 0x0000722C
		public static bool IsNoData(float[] metres)
		{
			bool flag = metres == null || metres.Length == 0;
			bool result;
			if (flag)
			{
				result = true;
			}
			else
			{
				for (int i = 0; i < metres.Length; i++)
				{
					bool flag2 = metres[i] != 0f;
					if (flag2)
					{
						return false;
					}
				}
				result = true;
			}
			return result;
		}

		/// <summary>Mean radius of the planet this DEM actually measures. Terrarium is Earth, in real metres,
		/// and Kopernicus systems are frequently rescaled — Sol-Test runs Earth at quarter scale (radius
		/// 1,592,753 m). Elevations must be scaled to the body before they mean anything, and this is the
		/// reference the scale factor is taken against. See <c>CubeTileBaker.demElevationScale</c>.</summary>
		// Token: 0x040000BD RID: 189
		public const double EarthRadiusMetres = 6371000.0;

		/// <summary>Outcome of decoding one Terrarium source tile — the two failure modes are NOT the same and a
		/// caller that conflates them mis-handles coastlines. <see cref="F:Mirage.WebIngest.TerrariumElevation.DemDecode.Ok" />: real elevation. <see cref="F:Mirage.WebIngest.TerrariumElevation.DemDecode.NoData" />:
		/// the all-zero sentinel — the destination buffer IS filled (with sea-level zeros, which are valid samples),
		/// and the caller decides whether the containing cube tile is entirely ocean (skip) or merely touches it
		/// (bake, using these zeros). <see cref="F:Mirage.WebIngest.TerrariumElevation.DemDecode.DecodeError" />: an undecodable PNG — a transient problem, and the
		/// destination buffer is untouched.</summary>
		// Token: 0x020000A1 RID: 161
		public enum DemDecode
		{
			// Token: 0x04000441 RID: 1089
			Ok,
			// Token: 0x04000442 RID: 1090
			NoData,
			// Token: 0x04000443 RID: 1091
			DecodeError
		}
	}
}
