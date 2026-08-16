using System;

namespace Mirage.WebIngest
{
	/// <summary>
	/// Quantises reprojected elevation (metres) into the R16 height tile the archive stores. WebIngest §4, the
	/// DEM bake.
	///
	/// <b>The mapping is the body's, not ours.</b> Every consumer of a height tile — the GPU's displacement, and
	/// <c>PQSMod_MirageTerrain.OnVertexBuildHeight</c> building the CPU collision mesh — reads it as:
	/// <code>
	///   metres = offset + deformity · (R16 / 65535)
	/// </code>
	/// with <c>deformity</c>/<c>offset</c> configured per body (Earth: 19519.46387 / −10919, i.e. the R16 range
	/// spans −10919 m to +8600 m). So a baked tile is only interchangeable with a canonical one if it uses that
	/// body's exact constants: the ingest must read them off the PQSMod rather than assume a normalisation of
	/// its own, or web tiles would step at the canonical boundary and — because the CPU sampler resolves the
	/// same tiles (design §11) — craft would collide with a surface at the wrong altitude.
	///
	/// <b>Clamping is lossy and is counted, not hidden.</b> The R16 range is whatever Sol's source DEM spanned,
	/// and an independent DEM need not share those extremes — Earth's ceiling is +8600 m, below Everest's real
	/// 8848 m, so the tallest summits must flatten once the bake reaches a level fine enough to resolve them.
	/// (Measured at L5: nothing clamped anywhere, because at that resolution a peak is averaged well below the
	/// ceiling. The clamping is a prediction for fine levels, not an observation yet.) Baking is still the right
	/// call — a clipped summit against no L8+ detail at all — but the caller gets the counts so the trade stays
	/// visible. Widening the range is NOT an option here: it would reinterpret every canonical tile.
	/// </summary>
	// Token: 0x02000019 RID: 25
	public static class HeightFromDem
	{
		/// <summary>The elevation step of one R16 unit, in metres — the bake's noise floor. Earth: ~0.30 m.</summary>
		// Token: 0x0600009A RID: 154 RVA: 0x0000699C File Offset: 0x00004B9C
		public static double QuantStepMetres(double deformity)
		{
			return deformity / 65535.0;
		}

		/// <summary>R16 value back to metres. The exact read every consumer performs; the inverse of
		/// <see cref="M:Mirage.WebIngest.HeightFromDem.Quantize(System.Single[],System.Int32,System.Double,System.Double,System.Int32@,System.Int32@)" /> up to rounding.</summary>
		// Token: 0x0600009B RID: 155 RVA: 0x000069A9 File Offset: 0x00004BA9
		public static double R16ToMetres(int value, double deformity, double offset)
		{
			return offset + deformity * ((double)value / 65535.0);
		}

		/// <summary>
		/// Quantise <paramref name="metres" /> (row-major, one float per texel — the reprojector's 1-channel
		/// output) into raw little-endian R16 bytes, ready for <c>MirageArchiveFormat.EncodeForWeb</c>.
		///
		/// <paramref name="clampedLow" />/<paramref name="clampedHigh" /> count texels that fell outside the
		/// body's range and were pinned to it — see the class remarks.
		/// </summary>
		// Token: 0x0600009C RID: 156 RVA: 0x000069BC File Offset: 0x00004BBC
		public static byte[] Quantize(float[] metres, int count, double deformity, double offset, out int clampedLow, out int clampedHigh)
		{
			bool flag = metres == null || metres.Length < count;
			if (flag)
			{
				throw new ArgumentException(string.Format("height quantise: source has {0} floats, need {1}", (metres != null) ? metres.Length : 0, count));
			}
			bool flag2 = deformity <= 0.0;
			if (flag2)
			{
				throw new ArgumentException(string.Format("height quantise: deformity must be positive, got {0} — the R16 mapping would ", deformity) + "be degenerate or inverted.");
			}
			clampedLow = (clampedHigh = 0);
			byte[] r16 = new byte[count * 2];
			for (int i = 0; i < count; i++)
			{
				double frac = ((double)metres[i] - offset) / deformity;
				bool flag3 = frac < 0.0;
				if (flag3)
				{
					frac = 0.0;
					clampedLow++;
				}
				else
				{
					bool flag4 = frac > 1.0;
					if (flag4)
					{
						frac = 1.0;
						clampedHigh++;
					}
				}
				int v = (int)Math.Round(frac * 65535.0);
				r16[2 * i] = (byte)v;
				r16[2 * i + 1] = (byte)(v >> 8);
			}
			return r16;
		}
	}
}
