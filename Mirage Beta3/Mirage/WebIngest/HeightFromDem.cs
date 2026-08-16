using System;

namespace Mirage.WebIngest
{
	/// <summary>
	/// Quantizes reprojected elevation (meters) into the R16 height tile the archive stores. WebIngest §4, the
	/// DEM bake.
	///
	/// <b>The mapping is the body's, not ours.</b> Every consumer of a height tile — the GPU's displacement, and
	/// <c>PQSMod_MirageTerrain.OnVertexBuildHeight</c> building the CPU collision mesh — reads it as:
	/// <code>
	///   meters = offset + deformity · (R16 / 65535)
	/// </code>
	/// with <c>deformity</c>/<c>offset</c> configured per body (Earth: 21000 / −12000, i.e. the R16 range spans
	/// −12000 m to +9000 m). So a baked tile is only interchangeable with a canonical one if it uses that
	/// body's exact constants: the ingest must read them off the PQSMod rather than assume a normalisation of
	/// its own, or web tiles would step at the canonical boundary and — because the CPU sampler resolves the
	/// same tiles (design §11) — craft would collide with a surface at the wrong altitude.
	///
	/// <b>Clamping is lossy and is counted, not hidden.</b> The R16 range need not contain an independent DEM's
	/// extremes, so the counts are how the caller sees what was lost.
	///
	/// This is not hypothetical — it happened. Earth's original range was [−10919, +8600.46] m, the span of
	/// Sol's source DEM, and that ceiling sits BELOW Everest's 8848.86 m. Nothing clamped while the web tier was
	/// coarse (a peak averages far below the ceiling at L5), but at L8–L12 the summit resolved and ~2500 texels
	/// pinned to 65535 — the top ~250 m rendered as a flat plateau. The fix was to widen the body's range to
	/// [−12000, +9000] and migrate the canonical pyramid to match with <c>ArchivePacker --remap-height</c>:
	/// both mappings are affine in R16, so it is a value transform, not a rebake. Widening is therefore possible
	/// but never local — it reinterprets every existing tile, so config, canonical archive, and any web archive
	/// have to move together or be discarded.
	/// </summary>
	// Token: 0x0200000D RID: 13
	public static class HeightFromDem
	{
		/// <summary>The elevation step of one R16 unit, in meters — the bake's noise floor. Earth: ~0.30 m.</summary>
		// Token: 0x0600005B RID: 91 RVA: 0x000033AC File Offset: 0x000015AC
		public static double QuantStepMeters(double deformity)
		{
			return deformity / 65535.0;
		}

		/// <summary>R16 value back to meters. The exact read every consumer performs; the inverse of
		/// <see cref="M:Mirage.WebIngest.HeightFromDem.Quantize(System.Single[],System.Int32,System.Double,System.Double,System.Int32@,System.Int32@)" /> up to rounding.</summary>
		// Token: 0x0600005C RID: 92 RVA: 0x000033B9 File Offset: 0x000015B9
		public static double R16ToMeters(int value, double deformity, double offset)
		{
			return offset + deformity * ((double)value / 65535.0);
		}

		/// <summary>
		/// Quantize <paramref name="meters" /> (row-major, one float per texel — the reprojector's 1-channel
		/// output) into raw little-endian R16 bytes, ready for <c>MirageArchiveFormat.EncodeForWeb</c>.
		///
		/// <paramref name="clampedLow" />/<paramref name="clampedHigh" /> count texels that fell outside the
		/// body's range and were pinned to it — see the class remarks.
		/// </summary>
		// Token: 0x0600005D RID: 93 RVA: 0x000033CC File Offset: 0x000015CC
		public static byte[] Quantize(float[] meters, int count, double deformity, double offset, out int clampedLow, out int clampedHigh)
		{
			bool flag = meters == null || meters.Length < count;
			if (flag)
			{
				throw new ArgumentException(string.Format("height quantize: source has {0} floats, need {1}", (meters != null) ? meters.Length : 0, count));
			}
			bool flag2 = deformity <= 0.0;
			if (flag2)
			{
				throw new ArgumentException(string.Format("height quantize: deformity must be positive, got {0} — the R16 mapping would ", deformity) + "be degenerate or inverted.");
			}
			clampedLow = (clampedHigh = 0);
			byte[] r16 = new byte[count * 2];
			for (int i = 0; i < count; i++)
			{
				double frac = ((double)meters[i] - offset) / deformity;
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
