using System;

namespace Mirage.WebIngest
{
	/// <summary>
	/// Evacuates a band around sea level so GPU-displaced terrain doesn't z-fight Scatterer's FFT ocean. The ocean
	/// surface oscillates a couple of metres either side of sea level; any terrain sharing that band flickers
	/// against it, badly at distance where depth precision is coarse. This remaps height so almost nothing lands
	/// in the band while staying <b>smooth</b> — there is no continuous map that fully skips an interval (the
	/// intermediate-value theorem forbids it), so instead the sea-level crossing is made very <i>steep</i>: terrain
	/// sweeps through the band over a razor-thin sliver of real elevation, leaving negligible area to flicker.
	///
	/// <para>The curve (ported verbatim from the pre-Mirage PQS mod that solved this): normalise the band to
	/// [0,1], steepen about the midpoint by <c>slope</c>, then a 7th-order <i>smootherstep</i>
	/// (C³ at both ends) so it eases back into the real DEM at the band edges rather than kinking. Sea level is the
	/// fixed point <b>only when the band is symmetric</b> (min = −max); the true fixed point is the midpoint.</para>
	///
	/// <para>Operates in the DEM's body (game) metres — the space Scatterer's waves live in — so the band is set
	/// directly in game metres regardless of the body's elevation rescale. Identity outside <c>[min,max]</c>, so
	/// real terrain above the band is never touched. Unity-free.</para>
	/// </summary>
	// Token: 0x02000026 RID: 38
	public static class SeaLevelFlatten
	{
		/// <summary>Remap one height through the sea-level evacuation curve. <paramref name="min" />/<paramref name="max" />
		/// bound the band (game metres); <paramref name="slope" /> is the steepening about the midpoint (higher =
		/// thinner band residue). Heights at or beyond the band are returned unchanged.</summary>
		// Token: 0x060000EA RID: 234 RVA: 0x00008D88 File Offset: 0x00006F88
		public static float Apply(float height, float min, float max, float slope)
		{
			bool flag = height <= min || height >= max;
			float result;
			if (flag)
			{
				result = height;
			}
			else
			{
				double range = (double)(max - min);
				double x = (double)(height - min) / range;
				x = (x - 0.5) * (double)slope + 0.5;
				bool flag2 = x < 0.0;
				if (flag2)
				{
					x = 0.0;
				}
				else
				{
					bool flag3 = x > 1.0;
					if (flag3)
					{
						x = 1.0;
					}
				}
				double x2 = x * x;
				double x3 = x2 * x2;
				double x4 = x3 * x;
				double x5 = x4 * x;
				double x6 = x5 * x;
				double y = -20.0 * x6 + 70.0 * x5 - 84.0 * x4 + 35.0 * x3;
				result = (float)(y * range + (double)min);
			}
			return result;
		}
	}
}
