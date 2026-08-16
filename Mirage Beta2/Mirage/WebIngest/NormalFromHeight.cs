using System;
using System.Threading;
using System.Threading.Tasks;

namespace Mirage.WebIngest
{
	/// <summary>
	/// Derives a tangent-space normal tile from elevation. WebIngest, DEM ingest.
	///
	/// <b>Everything here is EQUIRECTANGULAR — the frame AND the stencil. Nothing is done in cube space.</b>
	/// The frame is east/north/radial (below). The stencil is the neighbour in +longitude and the neighbour in
	/// +latitude — NOT the neighbouring cube texels. That is not a stylistic choice:
	///  - it matches Sol's offline baker (NormalMapV2.py) derivation exactly, and these tiles sit directly on
	///    top of canonical L0–L7, so a different derivation would put a subtle normal discontinuity at the
	///    level boundary;
	///  - <c>cross(east, north)</c> is outward BY CONSTRUCTION, everywhere. A cube-space stencil would inherit
	///    the face's u/v handedness (the measured basis has <c>U × V = −N</c>), so the cross product would come
	///    out inward on some faces and need an orientation fix-up — a hack that only exists to undo a problem
	///    the equirect stencil never creates.
	///
	/// <b>The TBN frame is not a choice — the shader dictates it.</b> `MirageVT.cginc:TrySampleVTWorldNormal`
	/// reconstructs the world normal as:
	/// <code>
	///   N = normalize(worldPos - _PlanetOrigin);          // radial
	///   T = normalize(cross(float3(0,1,0), N));           // east
	///   B = cross(N, T);                                  // north
	///   worldNormal = tangentN.x*T + tangentN.y*B + tangentN.z*N;
	/// </code>
	/// So a stored normal's x is the EAST component and y the NORTH component, in a frame built from the world
	/// up axis — not from the cube face, and not from any mesh tangent. Sol's own offline normal baker
	/// (NormalMapV2.py) builds the identical frame: its worldTangent points at the +longitude neighbour (east)
	/// and its binormal is cross(normal, tangent) (north).
	///
	/// <b>The X inversion is deliberate and is carried over on purpose.</b> The offline baker stores
	/// <c>R = 0.5 − 0.5·x_east</c> (note the inversion), while Unity's <c>UnpackNormal</c> decodes
	/// <c>x = 2R − 1</c> — so the shader ends up reading <c>−x_east</c>. Whether that is a compensation for the
	/// equirect→cube step (the cube's +u runs WEST on the faces measured, which is the leading explanation) or a
	/// latent sign bug, this encoder must reproduce it either way: baked L8+ tiles sit directly on top of
	/// canonical L0–L7, and a convention mismatch would flip normals exactly at the level boundary — a seam far
	/// worse than a globally consistent sign. If the convention is ever corrected, it is corrected here, in the
	/// offline baker, and in the shader together — see <see cref="F:Mirage.WebIngest.NormalFromHeight.InvertX" />.
	/// </summary>
	// Token: 0x02000023 RID: 35
	public static class NormalFromHeight
	{
		/// <summary>
		/// Build BC5-ready X/Y planes for one cube tile's slot.
		///
		/// The tile's TEXEL GRID is cube-space (that is where the output lives), but every elevation sample is
		/// taken in lat/lon and every gradient step is east/north — see the class remarks. So
		/// The sampler from <paramref name="samplerFactory" /> is queried at three equirect-aligned points per
		/// texel rather than read from a pre-reprojected cube array; that is what keeps the derivation identical
		/// to the offline baker's. It is a factory because rows are built in parallel and each worker needs its
		/// own sampler — see the remarks in the body.
		///
		/// <paramref name="stepDegrees" /> is the finite-difference step. It should be about one texel of angular
		/// extent so the normal's detail scale matches the tile's — too small and it amplifies DEM quantisation
		/// into noise, too large and it smooths real relief away.
		/// </summary>
		// Token: 0x060000DB RID: 219 RVA: 0x000082D0 File Offset: 0x000064D0
		public static bool Build(Func<NormalFromHeight.ElevationSampler> samplerFactory, int face, int level, int tx, int ty, int tileSize, int borderPx, double planetRadius, double stepDegrees, byte[] planeX, byte[] planeY)
		{
			int slot = tileSize + 2 * borderPx;
			int noData = 0;
			Parallel.For<NormalFromHeight.ElevationSampler>(0, slot, BakeScheduler.Options, samplerFactory, delegate(int y, ParallelLoopState state, NormalFromHeight.ElevationSampler localSampler)
			{
				bool flag = Volatile.Read(ref noData) != 0;
				NormalFromHeight.ElevationSampler result;
				if (flag)
				{
					result = localSampler;
				}
				else
				{
					bool flag2 = !NormalFromHeight.BuildRow(localSampler, face, level, tx, ty, tileSize, borderPx, planetRadius, stepDegrees, y, slot, planeX, planeY);
					if (flag2)
					{
						Interlocked.Exchange(ref noData, 1);
					}
					result = localSampler;
				}
				return result;
			}, delegate(NormalFromHeight.ElevationSampler _)
			{
			});
			return noData == 0;
		}

		/// <summary>One row of <see cref="M:Mirage.WebIngest.NormalFromHeight.Build(System.Func{Mirage.WebIngest.NormalFromHeight.ElevationSampler},System.Int32,System.Int32,System.Int32,System.Int32,System.Int32,System.Int32,System.Double,System.Double,System.Byte[],System.Byte[])" />. Returns false where the DEM has no data — a normal must never
		/// be invented from a guessed elevation.</summary>
		// Token: 0x060000DC RID: 220 RVA: 0x00008398 File Offset: 0x00006598
		private static bool BuildRow(NormalFromHeight.ElevationSampler sample, int face, int level, int tx, int ty, int tileSize, int borderPx, double planetRadius, double stepDegrees, int y, int slot, byte[] planeX, byte[] planeY)
		{
			for (int x = 0; x < slot; x++)
			{
				double lat;
				double lon;
				MirageCubeMath.TileTexelToLatLon(face, level, tx, ty, (double)x, (double)y, tileSize, borderPx, out lat, out lon);
				double latN = lat + stepDegrees;
				double sgn = 1.0;
				bool flag = latN > 89.9;
				if (flag)
				{
					latN = lat - stepDegrees;
					sgn = -1.0;
				}
				double px;
				double py;
				double pz;
				double ex;
				double ey;
				double ez;
				double nx2;
				double ny2;
				double nz2;
				bool flag2 = !NormalFromHeight.Vertex(sample, lat, lon, planetRadius, out px, out py, out pz) || !NormalFromHeight.Vertex(sample, lat, lon + stepDegrees, planetRadius, out ex, out ey, out ez) || !NormalFromHeight.Vertex(sample, latN, lon, planetRadius, out nx2, out ny2, out nz2);
				if (flag2)
				{
					return false;
				}
				double ax = ex - px;
				double ay = ey - py;
				double az = ez - pz;
				double bx = (nx2 - px) * sgn;
				double by = (ny2 - py) * sgn;
				double bz = (nz2 - pz) * sgn;
				double nx3;
				double ny3;
				double nz3;
				NormalFromHeight.Cross(ax, ay, az, bx, by, bz, out nx3, out ny3, out nz3);
				NormalFromHeight.Normalize(ref nx3, ref ny3, ref nz3);
				double dnx;
				double dny;
				double dnz;
				MirageCubeMath.LatLonToDirection(lat, lon, out dnx, out dny, out dnz);
				double tx2;
				double ty2;
				double tz0;
				NormalFromHeight.Cross(0.0, 1.0, 0.0, dnx, dny, dnz, out tx2, out ty2, out tz0);
				double tl = Math.Sqrt(tx2 * tx2 + ty2 * ty2 + tz0 * tz0);
				bool flag3 = tl > 1E-09;
				if (flag3)
				{
					tx2 /= tl;
					ty2 /= tl;
					tz0 /= tl;
				}
				else
				{
					tx2 = 1.0;
					ty2 = 0.0;
					tz0 = 0.0;
				}
				double bx2;
				double by2;
				double bz2;
				NormalFromHeight.Cross(dnx, dny, dnz, tx2, ty2, tz0, out bx2, out by2, out bz2);
				double e = nx3 * tx2 + ny3 * ty2 + nz3 * tz0;
				double i = nx3 * bx2 + ny3 * by2 + nz3 * bz2;
				planeX[y * slot + x] = NormalFromHeight.Quant(0.5 - 0.5 * e);
				planeY[y * slot + x] = NormalFromHeight.Quant(0.5 - 0.5 * i);
			}
			return true;
		}

		/// <summary>Displaced world position at a lat/lon: radial direction scaled by radius + elevation.</summary>
		// Token: 0x060000DD RID: 221 RVA: 0x000085E0 File Offset: 0x000067E0
		private static bool Vertex(NormalFromHeight.ElevationSampler sample, double lat, double lon, double radius, out double x, out double y, out double z)
		{
			x = (y = (z = 0.0));
			double metres;
			bool flag = !sample(lat, lon, out metres);
			bool result;
			if (flag)
			{
				result = false;
			}
			else
			{
				MirageCubeMath.LatLonToDirection(lat, lon, out x, out y, out z);
				double r = radius + metres;
				x *= r;
				y *= r;
				z *= r;
				result = true;
			}
			return result;
		}

		// Token: 0x060000DE RID: 222 RVA: 0x0000864D File Offset: 0x0000684D
		private static void Cross(double ax, double ay, double az, double bx, double by, double bz, out double cx, out double cy, out double cz)
		{
			cx = ay * bz - az * by;
			cy = az * bx - ax * bz;
			cz = ax * by - ay * bx;
		}

		// Token: 0x060000DF RID: 223 RVA: 0x00008674 File Offset: 0x00006874
		private static void Normalize(ref double x, ref double y, ref double z)
		{
			double i = Math.Sqrt(x * x + y * y + z * z);
			bool flag = i <= 1E-12;
			if (flag)
			{
				x = 0.0;
				y = 0.0;
				z = 1.0;
			}
			else
			{
				x /= i;
				y /= i;
				z /= i;
			}
		}

		// Token: 0x060000E0 RID: 224 RVA: 0x000086E4 File Offset: 0x000068E4
		private static byte Quant(double v)
		{
			int i = (int)Math.Round(v * 255.0);
			return (i < 0) ? 0 : ((i > 255) ? byte.MaxValue : ((byte)i));
		}

		/// <summary>
		/// Matches Sol's offline baker: R = 0.5 − 0.5·x_east rather than the conventional 0.5 + 0.5·x.
		/// Flip this ONLY in lockstep with NormalMapV2.py and MirageVT.cginc — on its own it would put a
		/// sign discontinuity at the canonical/web level boundary.
		/// </summary>
		// Token: 0x040000BA RID: 186
		public const bool InvertX = true;

		/// <summary>
		/// Same question for the NORTH component, and it went unasked far too long: <c>--test-normals</c>
		/// correlated only the R plane, so <see cref="F:Mirage.WebIngest.NormalFromHeight.InvertX" /> was settled 6/6 against the shipped tiles while
		/// green had no gate on it at all. The first working in-game run showed the lighting flipped vertically
		/// — a whole channel's sign, wrong, behind a green test suite.
		///
		/// Now measured the same way (correlate the derived G against the shipped canonical G across independent
		/// high-relief tiles; the sign is the answer). Flip ONLY in lockstep with NormalMapV2.py and
		/// MirageVT.cginc, for the same reason as InvertX: a mismatch puts a normal discontinuity exactly at the
		/// canonical/web level boundary.
		/// </summary>
		// Token: 0x040000BB RID: 187
		public const bool InvertY = true;

		/// <summary>Samples elevation in METRES at a lat/lon (degrees). Returns false where the source has no
		/// data — a normal must never be invented from a guessed elevation.</summary>
		// Token: 0x0200009E RID: 158
		// (Invoke) Token: 0x0600048D RID: 1165
		public delegate bool ElevationSampler(double lat, double lon, out double metres);
	}
}
