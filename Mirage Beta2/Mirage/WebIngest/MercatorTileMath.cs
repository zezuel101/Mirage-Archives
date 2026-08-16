using System;
using System.Runtime.CompilerServices;

namespace Mirage.WebIngest
{
	/// <summary>
	/// Web Mercator (EPSG:3857) slippy-tile math — the projection every imagery provider Mirage can legally use
	/// serves (EOX, GIBS). Ported from GeoStream.Core.TileMath, which is itself the tested extraction of
	/// GeoStream's shipped code; the antimeridian fix below came from that extraction and is carried over.
	///
	/// Deliberately Unity-free (System only) so it can be unit-tested without a game session and later called
	/// from a Burst job (the reprojection, WebIngest doc §4).
	///
	/// This is the CUBE↔MERCATOR boundary the ingest doc's risk summary warns about: "a coordinate-system
	/// boundary sits exactly on that seam, and correctness there is what separates 'streams beautifully' from
	/// 'stutters and corrupts'". Everything here is the mercator half; the cube half lives in MirageTileMath.
	/// </summary>
	// Token: 0x02000021 RID: 33
	public static class MercatorTileMath
	{
		/// <summary>Mercator zoom for a cube level, clamped to what the provider actually serves. Above the
		/// provider's maxZoom the source is upsampled rather than sharper — the tile is still baked (it is the
		/// best imagery that exists), it just stops gaining detail.</summary>
		// Token: 0x060000C6 RID: 198 RVA: 0x000078E3 File Offset: 0x00005AE3
		public static int LevelToZoom(int cubeLevel, int providerMaxZoom)
		{
			return Math.Min(cubeLevel + 2, providerMaxZoom);
		}

		/// <summary>Does a fixed <see cref="M:Mirage.WebIngest.MercatorTileMath.LevelToZoom(System.Int32,System.Int32)" /> still gain real detail at this level, or is the
		/// provider being upsampled? Callers can use this to stop ingesting past the provider's native limit.</summary>
		// Token: 0x060000C7 RID: 199 RVA: 0x000078EE File Offset: 0x00005AEE
		public static bool IsUpsampled(int cubeLevel, int providerMaxZoom)
		{
			return cubeLevel + 2 > providerMaxZoom;
		}

		/// <summary>False where Web Mercator has no data at all (|lat| past the cut). Such cube tiles must fall
		/// back to canonical/coarser imagery, never to a synthesized blank.</summary>
		// Token: 0x060000C8 RID: 200 RVA: 0x000078F6 File Offset: 0x00005AF6
		public static bool HasWebCoverage(double latitude)
		{
			return Math.Abs(latitude) <= 85.0511287798066;
		}

		// Token: 0x060000C9 RID: 201 RVA: 0x0000790C File Offset: 0x00005B0C
		[return: TupleElementNames(new string[]
		{
			"x",
			"y"
		})]
		public static ValueTuple<int, int> LatLonToTile(double latitude, double longitude, int zoomLevel)
		{
			double latRad = latitude * 0.017453292519943295;
			int i = 1 << zoomLevel;
			int x = (int)((longitude + 180.0) / 360.0 * (double)i);
			x = (x % i + i) % i;
			double yMerc = Math.Log(Math.Tan(latRad) + 1.0 / Math.Cos(latRad));
			int y = (int)((1.0 - yMerc / 3.141592653589793) / 2.0 * (double)i);
			bool flag = y < 0;
			if (flag)
			{
				y = 0;
			}
			else
			{
				bool flag2 = y >= i;
				if (flag2)
				{
					y = i - 1;
				}
			}
			return new ValueTuple<int, int>(x, y);
		}

		// Token: 0x060000CA RID: 202 RVA: 0x000079C4 File Offset: 0x00005BC4
		public static double TileYToLat(int y, int n)
		{
			double mercY = 3.141592653589793 * (1.0 - 2.0 * (double)y / (double)n);
			return Math.Atan(Math.Sinh(mercY)) * 180.0 / 3.141592653589793;
		}

		// Token: 0x060000CB RID: 203 RVA: 0x00007A1C File Offset: 0x00005C1C
		[return: TupleElementNames(new string[]
		{
			"lonMin",
			"lonMax",
			"latMin",
			"latMax"
		})]
		public static ValueTuple<double, double, double, double> TileToLatLonBounds(int x, int y, int zoomLevel)
		{
			int i = 1 << zoomLevel;
			double lonMin = (double)x / (double)i * 360.0 - 180.0;
			double lonMax = (double)(x + 1) / (double)i * 360.0 - 180.0;
			double latMax = MercatorTileMath.TileYToLat(y, i);
			double latMin = MercatorTileMath.TileYToLat(y + 1, i);
			return new ValueTuple<double, double, double, double>(lonMin, lonMax, latMin, latMax);
		}

		/// <summary>Normalized mercator UV in [0,1]² for a lat/lon — the continuous form of
		/// <see cref="M:Mirage.WebIngest.MercatorTileMath.LatLonToTile(System.Double,System.Double,System.Int32)" />, which is what the per-texel reproject (§4) actually needs. u runs east
		/// from -180°, v runs south from the north cut.</summary>
		// Token: 0x060000CC RID: 204 RVA: 0x00007A8C File Offset: 0x00005C8C
		[return: TupleElementNames(new string[]
		{
			"u",
			"v"
		})]
		public static ValueTuple<double, double> LatLonToMercatorUV(double latitude, double longitude)
		{
			double latRad = latitude * 0.017453292519943295;
			double u = (longitude + 180.0) / 360.0;
			double yMerc = Math.Log(Math.Tan(latRad) + 1.0 / Math.Cos(latRad));
			double v = (1.0 - yMerc / 3.141592653589793) / 2.0;
			return new ValueTuple<double, double>(u, v);
		}

		/// <summary>Ground resolution in metres per pixel at a latitude — the number that makes
		/// <see cref="M:Mirage.WebIngest.MercatorTileMath.LevelToZoom(System.Int32,System.Int32)" /> checkable against the archive doc's size ladder.</summary>
		// Token: 0x060000CD RID: 205 RVA: 0x00007B06 File Offset: 0x00005D06
		public static double MetersPerPixel(double latitude, int zoomLevel)
		{
			return 40075016.686 / (double)(1 << zoomLevel) / 256.0 * Math.Cos(latitude * 3.141592653589793 / 180.0);
		}

		// Token: 0x040000B0 RID: 176
		public const double EarthCircumferenceMeters = 40075016.686;

		/// <summary>Half the world circumference in metres — the EPSG:3857 origin shift.</summary>
		// Token: 0x040000B1 RID: 177
		public const double WebMercatorOriginShiftMeters = 20037508.342789244;

		/// <summary>Web Mercator's latitude limit. The projection's y → ±∞ at the poles, so it is cut here
		/// (the value that makes the projected world square). Cube tiles beyond this have NO web source at all —
		/// see <see cref="M:Mirage.WebIngest.MercatorTileMath.HasWebCoverage(System.Double)" />; the baker must never write a placeholder for them (§4: blank tiles
		/// cached as real imagery was one of GeoStream's real bugs).</summary>
		// Token: 0x040000B2 RID: 178
		public const double MaxMercatorLatitude = 85.0511287798066;

		// Token: 0x040000B3 RID: 179
		public const int TilePx = 256;

		/// <summary>
		/// The FIXED cube-level → mercator-zoom mapping (§11 decision 4). Must be a pure function of level and
		/// nothing else: a baked tile is only reusable across passes if cube level L *always* sourced zoom Z(L).
		/// GeoStream's altitude-dynamic zoom is explicitly NOT reused — it would bake inconsistent-resolution
		/// tiles for the same (face, level, tx, ty) on different flybys.
		///
		/// The mapping is resolution-matching, not a tuned constant. A cube face at level L is 256·2^L px across
		/// 90° of arc; mercator at zoom Z is 256·2^Z px across 360°. Equal px/degree gives
		///     2^L / 90 = 2^Z / 360  →  Z = L + 2.
		/// Sanity: L7 → z9 → 305.7 m/px at the equator, matching the archive doc's §6.6 figure of ~306 m/px for
		/// L7. And L12 → z14, which is exactly EOX s2cloudless's maxZoom AND ~9.55 m/px against Sentinel-2's
		/// ~10 m/px native resolution — Mirage's L12 ceiling and the provider's native limit are the same place.
		/// </summary>
		// Token: 0x040000B4 RID: 180
		public const int CubeLevelToZoomOffset = 2;
	}
}
