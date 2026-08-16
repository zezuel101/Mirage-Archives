using System;

namespace Mirage.WebIngest
{
	/// <summary>
	/// Removes isolated "hot" (or cold) pixels from an elevation grid — single texels whose value is wildly off
	/// from their neighbors, as some Terrarium source tiles carry (a corrupt terrain-RGB texel decodes to tens
	/// of thousands of meters). Each outlier is replaced by the mean of its GOOD neighbors, i.e. lerped from the
	/// surrounding terrain.
	///
	/// <b>Why local, not global.</b> A single mean/σ over a whole tile is useless for terrain: a coastal tile
	/// spans −4500 m ocean to +2000 m land, so its global σ is thousands and a threshold either misses spikes or
	/// flags real mountains. The test is instead per-pixel against its own 8-neighborhood, so it keys off how
	/// anomalous a pixel is <i>locally</i>. Legitimate features survive by construction: a coastline or a cliff
	/// is a COHERENT edge — its neighborhood genuinely has high variance, which widens the acceptance band — so
	/// only an ISOLATED spike (one texel unlike all of its neighbors) trips it.
	///
	/// <b>The center is excluded from its own statistics.</b> Include it and a huge spike inflates the very σ
	/// meant to catch it, and it escapes. A minimum absolute band (<see cref="F:Mirage.WebIngest.HeightDespike.DefaultMinBand" /> meters) then
	/// stops a dead-flat neighborhood (σ≈0) from flagging sensor noise as if it were a spike.
	///
	/// Operates in the grid's own units — call it on RAW meters (before any body rescale) so the band is a real
	/// elevation. Grid-shape agnostic (Terrarium's 256² source tiles today; the same call fits a GMRT grid).
	/// Unity-free, so tools/TestBench links it and --test-despike gates it offline.
	/// </summary>
	// Token: 0x0200000B RID: 11
	public static class HeightDespike
	{
		/// <summary>
		/// Detect and replace local outliers in place. Returns how many pixels were replaced (0 = clean tile, the
		/// overwhelmingly common case, and cheap — one pass finds nothing and the second is skipped).
		/// </summary>
		// Token: 0x06000059 RID: 89 RVA: 0x00002FE4 File Offset: 0x000011E4
		public static int Filter(float[] grid, int width, int height, float k = 6f, float minBand = 300f)
		{
			bool flag = grid == null || width <= 2 || height <= 2;
			int result;
			if (flag)
			{
				result = 0;
			}
			else
			{
				int i = width * height;
				bool[] hot = new bool[i];
				int count = 0;
				for (int y = 0; y < height; y++)
				{
					for (int x = 0; x < width; x++)
					{
						double sum = 0.0;
						double sumSq = 0.0;
						int c = 0;
						for (int dy = -1; dy <= 1; dy++)
						{
							for (int dx = -1; dx <= 1; dx++)
							{
								bool flag2 = dx == 0 && dy == 0;
								if (!flag2)
								{
									int nx = x + dx;
									int ny = y + dy;
									bool flag3 = nx < 0 || ny < 0 || nx >= width || ny >= height;
									if (!flag3)
									{
										float v = grid[ny * width + nx];
										sum += (double)v;
										sumSq += (double)v * (double)v;
										c++;
									}
								}
							}
						}
						bool flag4 = c < 4;
						if (!flag4)
						{
							double mean = sum / (double)c;
							double variance = sumSq / (double)c - mean * mean;
							double std = (variance > 0.0) ? Math.Sqrt(variance) : 0.0;
							double band = Math.Max((double)k * std, (double)minBand);
							bool flag5 = Math.Abs((double)grid[y * width + x] - mean) > band;
							if (flag5)
							{
								hot[y * width + x] = true;
								count++;
							}
						}
					}
				}
				bool flag6 = count == 0;
				if (flag6)
				{
					result = 0;
				}
				else
				{
					for (int y2 = 0; y2 < height; y2++)
					{
						for (int x2 = 0; x2 < width; x2++)
						{
							bool flag7 = !hot[y2 * width + x2];
							if (!flag7)
							{
								double sum2 = 0.0;
								int c2 = 0;
								for (int dy2 = -1; dy2 <= 1; dy2++)
								{
									for (int dx2 = -1; dx2 <= 1; dx2++)
									{
										bool flag8 = dx2 == 0 && dy2 == 0;
										if (!flag8)
										{
											int nx2 = x2 + dx2;
											int ny2 = y2 + dy2;
											bool flag9 = nx2 < 0 || ny2 < 0 || nx2 >= width || ny2 >= height;
											if (!flag9)
											{
												bool flag10 = hot[ny2 * width + nx2];
												if (!flag10)
												{
													sum2 += (double)grid[ny2 * width + nx2];
													c2++;
												}
											}
										}
									}
								}
								bool flag11 = c2 > 0;
								if (flag11)
								{
									grid[y2 * width + x2] = (float)(sum2 / (double)c2);
								}
							}
						}
					}
					result = count;
				}
			}
			return result;
		}

		/// <summary>A pixel must exceed BOTH k·σ of its neighborhood AND this absolute band to be replaced.
		/// Conservative: real terrain almost never jumps this far in a single step without its neighbors coming
		/// along (which raises the local mean and σ), while a corrupt texel is off by thousands.</summary>
		// Token: 0x0400003D RID: 61
		public const float DefaultSigma = 6f;

		// Token: 0x0400003E RID: 62
		public const float DefaultMinBand = 300f;
	}
}
