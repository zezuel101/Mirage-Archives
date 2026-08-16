using System;
using System.Globalization;
using System.Text;

namespace Mirage.WebIngest
{
	/// <summary>
	/// Fetches and decodes GMRT (Global Multi-Resolution Topography) elevation grids, used as a SECOND SOURCE
	/// for the height ingest path — NOT a new VT layer. It exists for one job: fill the ocean floor with real
	/// bathymetry (negative meters) where <see cref="T:Mirage.WebIngest.TerrariumElevation" /> reports flat sea-level zeros, so the
	/// baked heightmap sits BELOW Scatterer's sea-level ocean plane instead of z-fighting it. The output still
	/// flows into the same single R16 height tile; nothing downstream (slot map, atlases, page table, shader)
	/// changes.
	///
	/// <b>Why GMRT, and why this shape.</b> GMRT has no XYZ tile endpoint for raw elevation — only the GridServer
	/// bounding-box service (verified against the live service). But that returns an EQUIRECTANGULAR lat/lon grid,
	/// which the cube reprojection samples natively (cube texel → lat/lon → sample), with none of Terrarium's
	/// mercator detour. Data is CC BY 4.0 (attribution only — no NC/SA), so the client-generate posture is a
	/// courtesy, not a requirement.
	///
	/// <b>The request model (verified live).</b>
	///   GET https://www.gmrt.org/services/GridServer?west=&amp;east=&amp;south=&amp;north=&amp;resolution=&amp;layer=topo&amp;format=esriascii
	///   - resolution is a POWER OF TWO; the service rejects anything else ("must be a positive power of 2").
	///     cellsize(°) = <see cref="F:Mirage.WebIngest.GmrtElevation.BaseCellDeg" /> / resolution, from ~62 km/node at 1 to ~61 m/node at 1024.
	///     <see cref="M:Mirage.WebIngest.GmrtElevation.ResolutionForLevel(System.Int32,System.Int32)" /> picks the power of two whose node density matches a cube level, so a
	///     fetch is ~tileSize² nodes at EVERY level — no oversampling on fine tiles, no multi-degree timeouts on
	///     coarse ones.
	///   - layer=topo is the merged land+ocean grid (GEBCO fill in deep ocean); complete, so no-data is rare.
	///   - format=esriascii is a plain ASCII grid — a ~6-line header then whitespace floats — so no GeoTIFF or
	///     NetCDF decoder is needed. (GeoTIFF is ~half the bytes and a later optimisation if bandwidth bites.)
	///
	/// Unity-free like the rest of WebIngest, so tools/TestBench links it and the --test-gmrt gate exercises
	/// the exact parse the plugin runs, against the real service, offline.
	/// </summary>
	// Token: 0x02000013 RID: 19
	public static class GmrtElevation
	{
		/// <summary>
		/// The power-of-two <c>resolution</c> that yields roughly one grid node per cube texel at
		/// <paramref name="level" />, rounded UP so the grid is never coarser than the tile it feeds (the
		/// Mitchell-Netravali resample can always down-weight extra detail; it cannot invent missing detail).
		/// Clamped to <see cref="F:Mirage.WebIngest.GmrtElevation.MaxResolution" />.
		///
		/// A cube texel spans <c>90° / (2^level · tileSize)</c> (a face is a quarter great circle), so the target
		/// cellsize is that, and resolution = BaseCellDeg / cellsize.
		/// </summary>
		// Token: 0x06000081 RID: 129 RVA: 0x00004F34 File Offset: 0x00003134
		public static int ResolutionForLevel(int level, int tileSize)
		{
			double texelDeg = 90.0 / ((double)(1L << level) * (double)tileSize);
			double want = 0.5625 / texelDeg;
			int r = 1;
			while ((double)r < want && r < 1024)
			{
				r <<= 1;
			}
			return r;
		}

		/// <summary>Build a GridServer request URL for a lat/lon box at a power-of-two resolution. Invariant
		/// culture throughout — a comma decimal separator would corrupt the query.</summary>
		// Token: 0x06000082 RID: 130 RVA: 0x00004F88 File Offset: 0x00003188
		public static string BuildUrl(double west, double east, double south, double north, int resolution)
		{
			CultureInfo c = CultureInfo.InvariantCulture;
			return string.Concat(new string[]
			{
				"https://www.gmrt.org/services/GridServer?west=",
				west.ToString("R", c),
				"&east=",
				east.ToString("R", c),
				"&south=",
				south.ToString("R", c),
				"&north=",
				north.ToString("R", c),
				string.Format("&resolution={0}&layer=topo&format=esriascii", resolution)
			});
		}

		/// <summary>
		/// Parse an ArcASCII grid (as returned by GridServer's <c>format=esriascii</c>). Returns false on a
		/// malformed header or a truncated body — a partial grid must be declined, not half-used. Accepts both
		/// <c>xllcorner</c>/<c>yllcorner</c> and <c>xllcenter</c>/<c>yllcenter</c> registration, normalising the
		/// latter to a corner so the sampler has one convention to reason about.
		/// </summary>
		// Token: 0x06000083 RID: 131 RVA: 0x00005020 File Offset: 0x00003220
		public static bool TryParse(byte[] ascii, out GmrtElevation.Grid grid)
		{
			grid = default(GmrtElevation.Grid);
			bool flag = ascii == null || ascii.Length == 0;
			bool result;
			if (flag)
			{
				result = false;
			}
			else
			{
				GmrtElevation.AsciiTokenizer tok = new GmrtElevation.AsciiTokenizer(ascii);
				int cols = 0;
				int rows = 0;
				double cell = 0.0;
				double xll = 0.0;
				double yll = 0.0;
				bool haveCols = false;
				bool haveRows = false;
				bool haveCell = false;
				bool haveX = false;
				bool haveY = false;
				bool xIsCenter = false;
				bool yIsCenter = false;
				for (;;)
				{
					string key;
					bool flag2 = tok.TryPeekKey(out key);
					if (!flag2)
					{
						goto IL_22D;
					}
					string lower = key.ToLowerInvariant();
					bool flag3 = lower == "ncols";
					if (flag3)
					{
						tok.SkipToken();
						bool flag4 = !tok.TryReadInt(out cols);
						if (flag4)
						{
							break;
						}
						haveCols = true;
					}
					else
					{
						bool flag5 = lower == "nrows";
						if (flag5)
						{
							tok.SkipToken();
							bool flag6 = !tok.TryReadInt(out rows);
							if (flag6)
							{
								goto Block_7;
							}
							haveRows = true;
						}
						else
						{
							bool flag7 = lower == "xllcorner" || lower == "xllcenter";
							if (flag7)
							{
								tok.SkipToken();
								bool flag8 = !tok.TryReadDouble(out xll);
								if (flag8)
								{
									goto Block_10;
								}
								xIsCenter = (lower == "xllcenter");
								haveX = true;
							}
							else
							{
								bool flag9 = lower == "yllcorner" || lower == "yllcenter";
								if (flag9)
								{
									tok.SkipToken();
									bool flag10 = !tok.TryReadDouble(out yll);
									if (flag10)
									{
										goto Block_13;
									}
									yIsCenter = (lower == "yllcenter");
									haveY = true;
								}
								else
								{
									bool flag11 = lower == "cellsize";
									if (flag11)
									{
										tok.SkipToken();
										bool flag12 = !tok.TryReadDouble(out cell);
										if (flag12)
										{
											goto Block_15;
										}
										haveCell = true;
									}
									else
									{
										bool flag13 = lower == "nodata_value";
										if (!flag13)
										{
											goto IL_224;
										}
										tok.SkipToken();
										double num;
										bool flag14 = !tok.TryReadDouble(out num);
										if (flag14)
										{
											goto Block_17;
										}
									}
								}
							}
						}
					}
				}
				return false;
				Block_7:
				return false;
				Block_10:
				return false;
				Block_13:
				return false;
				Block_15:
				return false;
				Block_17:
				return false;
				IL_224:
				IL_22D:
				bool flag15 = !haveCols || !haveRows || !haveCell || !haveX || !haveY || cols <= 0 || rows <= 0;
				if (flag15)
				{
					result = false;
				}
				else
				{
					bool flag16 = cell <= 0.0;
					if (flag16)
					{
						result = false;
					}
					else
					{
						bool flag17 = xIsCenter;
						if (flag17)
						{
							xll -= cell * 0.5;
						}
						bool flag18 = yIsCenter;
						if (flag18)
						{
							yll -= cell * 0.5;
						}
						long i = (long)cols * (long)rows;
						bool flag19 = i > 67108864L;
						if (flag19)
						{
							result = false;
						}
						else
						{
							float[] data = new float[i];
							for (long j = 0L; j < i; j += 1L)
							{
								bool flag20 = !tok.TryReadFloat(out data[(int)(checked((IntPtr)j))]);
								if (flag20)
								{
									return false;
								}
							}
							grid = new GmrtElevation.Grid(cols, rows, xll, yll, cell, data);
							result = true;
						}
					}
				}
			}
			return result;
		}

		/// <summary>
		/// Bilinearly sample the grid at (lat, lon) in degrees, writing meters. Returns false when the point is
		/// outside the grid or any of the four taps is no-data — the caller then leaves that texel to its existing
		/// value (Terrarium's zero) rather than baking a hole. Rows run north→south, so the row axis is inverted
		/// relative to latitude.
		/// </summary>
		// Token: 0x06000084 RID: 132 RVA: 0x00005354 File Offset: 0x00003554
		public static bool TrySample(in GmrtElevation.Grid g, double lat, double lon, out double meters)
		{
			meters = 0.0;
			bool flag = g.Data == null;
			bool result;
			if (flag)
			{
				result = false;
			}
			else
			{
				double fc = (lon - g.XllCorner) / g.CellDeg - 0.5;
				double fr = (double)g.Rows - 0.5 - (lat - g.YllCorner) / g.CellDeg;
				int c0 = (int)Math.Floor(fc);
				int r0 = (int)Math.Floor(fr);
				bool flag2 = c0 < 0 || r0 < 0 || c0 + 1 >= g.Cols || r0 + 1 >= g.Rows;
				if (flag2)
				{
					bool flag3 = c0 < 0 || r0 < 0 || c0 >= g.Cols || r0 >= g.Rows;
					if (flag3)
					{
						return false;
					}
				}
				int c = Math.Min(c0 + 1, g.Cols - 1);
				int r = Math.Min(r0 + 1, g.Rows - 1);
				double tx = fc - (double)c0;
				double ty = fr - (double)r0;
				float v0;
				float v;
				float v2;
				float v3;
				bool flag4;
				checked
				{
					v0 = g.Data[(int)((IntPtr)(unchecked((long)r0 * (long)g.Cols + (long)c0)))];
					v = g.Data[(int)((IntPtr)(unchecked((long)r0 * (long)g.Cols + (long)c)))];
					v2 = g.Data[(int)((IntPtr)(unchecked((long)r * (long)g.Cols + (long)c0)))];
					v3 = g.Data[(int)((IntPtr)(unchecked((long)r * (long)g.Cols + (long)c)))];
					flag4 = (v0 == -2.1474836E+09f || v == -2.1474836E+09f || v2 == -2.1474836E+09f || v3 == -2.1474836E+09f);
				}
				if (flag4)
				{
					result = false;
				}
				else
				{
					double top = (double)v0 + (double)(v - v0) * tx;
					double bot = (double)v2 + (double)(v3 - v2) * tx;
					meters = top + (bot - top) * ty;
					result = true;
				}
			}
			return result;
		}

		/// <summary>GMRT's no-data marker (int.MinValue), emitted verbatim into the ASCII grid. Distinct from any
		/// real depth, so a simple equality test separates "no coverage here" from "deep ocean".</summary>
		// Token: 0x04000060 RID: 96
		public const float NoDataSentinel = -2.1474836E+09f;

		/// <summary>cellsize at resolution=1 (°/node). The service's pyramid is cellsize = BaseCellDeg/resolution
		/// with resolution a power of two — measured directly: res 1→0.5625°, 2→0.28125°, …, 1024→~5.49e-4°.</summary>
		// Token: 0x04000061 RID: 97
		public const double BaseCellDeg = 0.5625;

		/// <summary>Native GMRT resolution — ~61 m/node at the equator. The service caps here.</summary>
		// Token: 0x04000062 RID: 98
		public const int MaxResolution = 1024;

		/// <summary>
		/// A decoded ArcASCII grid. Corner-registered (xll/yll are the lower-LEFT CORNER, GMRT's convention), and
		/// the data rows run NORTH→SOUTH (row 0 is the northernmost), which is standard ArcASCII and what
		/// <see cref="M:Mirage.WebIngest.GmrtElevation.TrySample(Mirage.WebIngest.GmrtElevation.Grid@,System.Double,System.Double,System.Double@)" /> accounts for.
		/// </summary>
		// Token: 0x020000A4 RID: 164
		public readonly struct Grid
		{
			// Token: 0x06000444 RID: 1092 RVA: 0x0001F69E File Offset: 0x0001D89E
			public Grid(int cols, int rows, double xll, double yll, double cell, float[] data)
			{
				this.Cols = cols;
				this.Rows = rows;
				this.XllCorner = xll;
				this.YllCorner = yll;
				this.CellDeg = cell;
				this.Data = data;
			}

			// Token: 0x04000471 RID: 1137
			public readonly int Cols;

			// Token: 0x04000472 RID: 1138
			public readonly int Rows;

			// Token: 0x04000473 RID: 1139
			public readonly double XllCorner;

			// Token: 0x04000474 RID: 1140
			public readonly double YllCorner;

			// Token: 0x04000475 RID: 1141
			public readonly double CellDeg;

			// Token: 0x04000476 RID: 1142
			public readonly float[] Data;
		}

		/// <summary>Minimal whitespace tokenizer over an ASCII byte buffer — no substring allocation on the hot
		/// float path. Header keys are the only place a managed string is produced.</summary>
		// Token: 0x020000A5 RID: 165
		private struct AsciiTokenizer
		{
			// Token: 0x06000445 RID: 1093 RVA: 0x0001F6CE File Offset: 0x0001D8CE
			public AsciiTokenizer(byte[] bytes)
			{
				this.b = bytes;
				this.i = 0;
			}

			// Token: 0x06000446 RID: 1094 RVA: 0x0001F6DF File Offset: 0x0001D8DF
			private static bool IsSpace(byte c)
			{
				return c == 32 || c == 9 || c == 13 || c == 10;
			}

			// Token: 0x06000447 RID: 1095 RVA: 0x0001F6F8 File Offset: 0x0001D8F8
			private void SkipSpace()
			{
				while (this.i < this.b.Length && GmrtElevation.AsciiTokenizer.IsSpace(this.b[this.i]))
				{
					this.i++;
				}
			}

			/// <summary>Peek the next token as a string IF it starts with a letter (a header key). Does not
			/// advance. Returns false at EOF or when the next token is numeric (the data body).</summary>
			// Token: 0x06000448 RID: 1096 RVA: 0x0001F740 File Offset: 0x0001D940
			public bool TryPeekKey(out string key)
			{
				key = null;
				this.SkipSpace();
				bool flag = this.i >= this.b.Length;
				bool result;
				if (flag)
				{
					result = false;
				}
				else
				{
					byte c = this.b[this.i];
					bool alpha = (c >= 65 && c <= 90) || (c >= 97 && c <= 122);
					bool flag2 = !alpha;
					if (flag2)
					{
						result = false;
					}
					else
					{
						int i = this.i;
						while (i < this.b.Length && !GmrtElevation.AsciiTokenizer.IsSpace(this.b[i]))
						{
							i++;
						}
						key = Encoding.ASCII.GetString(this.b, this.i, i - this.i);
						result = true;
					}
				}
				return result;
			}

			// Token: 0x06000449 RID: 1097 RVA: 0x0001F80C File Offset: 0x0001DA0C
			public void SkipToken()
			{
				this.SkipSpace();
				while (this.i < this.b.Length && !GmrtElevation.AsciiTokenizer.IsSpace(this.b[this.i]))
				{
					this.i++;
				}
			}

			// Token: 0x0600044A RID: 1098 RVA: 0x0001F860 File Offset: 0x0001DA60
			private bool NextToken(out int start, out int len)
			{
				this.SkipSpace();
				start = this.i;
				while (this.i < this.b.Length && !GmrtElevation.AsciiTokenizer.IsSpace(this.b[this.i]))
				{
					this.i++;
				}
				len = this.i - start;
				return len > 0;
			}

			// Token: 0x0600044B RID: 1099 RVA: 0x0001F8D0 File Offset: 0x0001DAD0
			public bool TryReadInt(out int value)
			{
				value = 0;
				int s;
				int len;
				bool flag = !this.NextToken(out s, out len);
				return !flag && int.TryParse(Encoding.ASCII.GetString(this.b, s, len), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
			}

			// Token: 0x0600044C RID: 1100 RVA: 0x0001F918 File Offset: 0x0001DB18
			public bool TryReadDouble(out double value)
			{
				value = 0.0;
				int s;
				int len;
				bool flag = !this.NextToken(out s, out len);
				return !flag && double.TryParse(Encoding.ASCII.GetString(this.b, s, len), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
			}

			// Token: 0x0600044D RID: 1101 RVA: 0x0001F96C File Offset: 0x0001DB6C
			public bool TryReadFloat(out float value)
			{
				value = 0f;
				int s;
				int len;
				bool flag = !this.NextToken(out s, out len);
				return !flag && float.TryParse(Encoding.ASCII.GetString(this.b, s, len), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
			}

			// Token: 0x04000477 RID: 1143
			private readonly byte[] b;

			// Token: 0x04000478 RID: 1144
			private int i;
		}
	}
}
