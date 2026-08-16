using System;

namespace Mirage.WebIngest
{
	/// <summary>
	/// The ESA WorldCover 3°×3° tiling scheme and class codes — the geo glue between a lat/lon and the COG that
	/// covers it. Pure arithmetic, Unity-free.
	///
	/// Tiles are named by their SOUTH-WEST corner snapped to a multiple of 3°, e.g. <c>N54W009</c> = lat [54,57],
	/// lon [-9,-6]. Ocean-only areas have NO tile (the product maps land + a coastal buffer), so a missing tile
	/// is not an error — it means "no land here", which the caller resolves through the sea-level term.
	///
	/// Class codes are the raw 8-bit sample values (10…100); <see cref="F:Mirage.WebIngest.WorldCoverGrid.WaterClass" /> (80, permanent water) is
	/// the only one the mask uses — 90 (herbaceous wetland) and 95 (mangrove) are vegetated and stay land, so a
	/// salt marsh is not rendered as glossy open water.
	/// </summary>
	// Token: 0x02000033 RID: 51
	public static class WorldCoverGrid
	{
		/// <summary>True if a class code should count as water for the mask.</summary>
		// Token: 0x06000135 RID: 309 RVA: 0x0000A656 File Offset: 0x00008856
		public static bool IsWater(byte cls)
		{
			return cls == 80;
		}

		/// <summary>South-west corner (multiple of 3°) of the tile containing <paramref name="lat" />/<paramref name="lon" />.</summary>
		// Token: 0x06000136 RID: 310 RVA: 0x0000A65D File Offset: 0x0000885D
		public static void TileSouthWest(double lat, double lon, out int swLat, out int swLon)
		{
			swLat = (int)Math.Floor(lat / 3.0) * 3;
			swLon = (int)Math.Floor(lon / 3.0) * 3;
		}

		/// <summary>The tile name for a SW corner, e.g. (54,-9) → <c>N54W009</c>.</summary>
		// Token: 0x06000137 RID: 311 RVA: 0x0000A68C File Offset: 0x0000888C
		public static string TileName(int swLat, int swLon)
		{
			char ns = (swLat < 0) ? 'S' : 'N';
			char ew = (swLon < 0) ? 'W' : 'E';
			return string.Format("{0}{1:D2}{2}{3:D3}", new object[]
			{
				ns,
				Math.Abs(swLat),
				ew,
				Math.Abs(swLon)
			});
		}

		/// <summary>Full COG filename for a tile, e.g. <c>ESA_WorldCover_10m_2021_v200_N54W009_Map.tif</c>.</summary>
		// Token: 0x06000138 RID: 312 RVA: 0x0000A6F2 File Offset: 0x000088F2
		public static string TileFileName(int swLat, int swLon, string productPrefix)
		{
			return productPrefix + "_" + WorldCoverGrid.TileName(swLat, swLon) + "_Map.tif";
		}

		/// <summary>Map a lat/lon to a full-resolution pixel (col,row) within the tile whose SW corner is given and
		/// whose full-res image is <paramref name="imgSize" /> px square. Origin is the NW corner (row 0 = north).
		/// Fractional pixels are truncated; callers clamp to the image bounds.</summary>
		// Token: 0x06000139 RID: 313 RVA: 0x0000A70C File Offset: 0x0000890C
		public static void LatLonToPixel(int swLat, int swLon, int imgSize, double lat, double lon, out int col, out int row)
		{
			double scale = 3.0 / (double)imgSize;
			col = (int)((lon - (double)swLon) / scale);
			row = (int)(((double)(swLat + 3) - lat) / scale);
		}

		/// <summary>Degrees spanned by one tile, each axis.</summary>
		// Token: 0x0400010C RID: 268
		public const int TileDeg = 3;

		/// <summary>Permanent water bodies — the class the water mask keys on.</summary>
		// Token: 0x0400010D RID: 269
		public const byte WaterClass = 80;
	}
}
