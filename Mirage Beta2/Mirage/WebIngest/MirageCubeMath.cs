using System;

namespace Mirage.WebIngest
{
	/// <summary>
	/// The CUBE half of the cube↔mercator boundary (WebIngest §4) — face UV ↔ direction ↔ lat/lon, plus the
	/// per-face rotation that maps raw PQS face UVs into the corrected space tiles are stored in.
	///
	/// Unity-free (System only) on purpose, three ways over:
	///  - it is the single source of truth for <c>CorrectFaceUV</c>, which <c>MirageTileMath</c> forwards to
	///    (CLAUDE.md: face-UV handling must never be duplicated — the C# and shader sides have to move together,
	///    and a second copy is how they silently drift);
	///  - the offline packer links it, so the face basis can be VERIFIED against real canonical tiles without a
	///    KSP session;
	///  - it stays callable from a Burst job (plain double math, no allocations).
	///
	/// <b>The face basis below is measured, not assumed.</b> See <see cref="F:Mirage.WebIngest.MirageCubeMath.FaceU" />.
	/// </summary>
	// Token: 0x02000022 RID: 34
	public static class MirageCubeMath
	{
		/// <summary>True once the basis is pinned. It ships pinned (see <see cref="F:Mirage.WebIngest.MirageCubeMath.FaceU" />); the setter exists
		/// so the packer's orientation search can drive candidates through this exact production code path
		/// rather than a lookalike.</summary>
		// Token: 0x17000026 RID: 38
		// (get) Token: 0x060000CE RID: 206 RVA: 0x00007B3E File Offset: 0x00005D3E
		// (set) Token: 0x060000CF RID: 207 RVA: 0x00007B45 File Offset: 0x00005D45
		public static bool BasisPinned { get; private set; }

		/// <summary>Pin the per-face tangent basis. Called with the measured result; exposed so the packer's
		/// orientation search can drive candidates through the exact production code path.</summary>
		// Token: 0x060000D0 RID: 208 RVA: 0x00007B50 File Offset: 0x00005D50
		public static void SetFaceBasis(double[][] u, double[][] v)
		{
			bool flag = u == null || v == null || u.Length != 6 || v.Length != 6;
			if (flag)
			{
				throw new ArgumentException("face basis must have 6 U and 6 V axes");
			}
			for (int f = 0; f < 6; f++)
			{
				MirageCubeMath.FaceU[f] = u[f];
				MirageCubeMath.FaceV[f] = v[f];
			}
			MirageCubeMath.BasisPinned = true;
		}

		/// <summary>
		/// RAW face UV (u, v ∈ [0,1], the space PQS bakes into UV3) → unit direction in the body frame.
		/// The standard cube-sphere map: offset the face's outward axis by the in-face tangents scaled to
		/// [-1, 1], then normalize.
		/// </summary>
		// Token: 0x060000D1 RID: 209 RVA: 0x00007BB4 File Offset: 0x00005DB4
		public static void FaceUVToDirection(int face, double u, double v, out double x, out double y, out double z)
		{
			bool flag = !MirageCubeMath.BasisPinned;
			if (flag)
			{
				throw new InvalidOperationException("MirageCubeMath: face basis not pinned — run ArchivePacker --test-cube to measure it.");
			}
			double s = 2.0 * u - 1.0;
			double t = 2.0 * v - 1.0;
			double[] i = MirageCubeMath.FaceAxis[face];
			double[] uu = MirageCubeMath.FaceU[face];
			double[] vv = MirageCubeMath.FaceV[face];
			x = i[0] + s * uu[0] + t * vv[0];
			y = i[1] + s * uu[1] + t * vv[1];
			z = i[2] + s * uu[2] + t * vv[2];
			double inv = 1.0 / Math.Sqrt(x * x + y * y + z * z);
			x *= inv;
			y *= inv;
			z *= inv;
		}

		/// <summary>
		/// Inverse of <see cref="M:Mirage.WebIngest.MirageCubeMath.FaceUVToDirection(System.Int32,System.Double,System.Double,System.Double@,System.Double@,System.Double@)" />: unit direction → cube face + RAW face UV.
		///
		/// The face is the dominant axis of the direction, which needs no basis at all. Within that face, the
		/// direction is <c>norm(N + s·U + t·V)</c>, so with an orthonormal basis <c>dir·N = 1/len</c> and
		/// <c>dir·U = s/len</c> — hence <c>s = (dir·U)/(dir·N)</c>, and likewise for t.
		/// </summary>
		// Token: 0x060000D2 RID: 210 RVA: 0x00007C98 File Offset: 0x00005E98
		public static void DirectionToFaceUV(double x, double y, double z, out int face, out double u, out double v)
		{
			double ax = Math.Abs(x);
			double ay = Math.Abs(y);
			double az = Math.Abs(z);
			bool flag = ax >= ay && ax >= az;
			if (flag)
			{
				face = ((x >= 0.0) ? 0 : 1);
			}
			else
			{
				bool flag2 = ay >= az;
				if (flag2)
				{
					face = ((y >= 0.0) ? 2 : 3);
				}
				else
				{
					face = ((z >= 0.0) ? 4 : 5);
				}
			}
			double[] i = MirageCubeMath.FaceAxis[face];
			double[] uu = MirageCubeMath.FaceU[face];
			double[] vv = MirageCubeMath.FaceV[face];
			double dn = x * i[0] + y * i[1] + z * i[2];
			double du = x * uu[0] + y * uu[1] + z * uu[2];
			double dv = x * vv[0] + y * vv[1] + z * vv[2];
			double s = du / dn;
			double t = dv / dn;
			u = (s + 1.0) * 0.5;
			v = (t + 1.0) * 0.5;
		}

		/// <summary>
		/// How big a tile at this face position REALLY is, as a multiple of the per-level average
		/// <c>(π/2·R)/2^L</c> — normalised so the face CENTRE is exactly 1.0.
		///
		/// <see cref="M:Mirage.WebIngest.MirageCubeMath.FaceUVToDirection(System.Int32,System.Double,System.Double,System.Double@,System.Double@,System.Double@)" /> is gnomonic: it offsets along the tangent plane (<c>s = 2u−1</c>)
		/// and only then normalises. Equal steps in face UV are therefore NOT equal steps in angle — a tile at
		/// the face centre covers 2x the ground of one at a face edge, and 2.1x one at a corner. Anything that
		/// converts between "a level" and "metres on the ground" and treats a level as one fixed size is wrong
		/// by up to a full level across a face.
		///
		/// Derivation: for <c>dir = norm(N + sU + tV)</c> with <c>r² = 1+s²+t²</c>, the angular scale along s is
		/// <c>√(1+t²)/r²</c> and along t is <c>√(1+s²)/r²</c>. This returns their geometric mean (the
		/// area-equivalent scalar), divided by the face-centre value of 1 to normalise. Verified against tile
		/// geometry measured through the full production chain: agrees to &lt;0.4% everywhere on a face.
		///
		/// <b>Takes CORRECTED UV, and that is safe by symmetry, not by accident:</b> the per-face rotation in
		/// <see cref="M:Mirage.WebIngest.MirageCubeMath.CorrectFaceUV(System.Int32,System.Double,System.Double,System.Double@,System.Double@)" /> only permutes and flips the two axes, and this expression is symmetric in
		/// s²,t² — so raw and corrected UV give the identical answer, and no un-rotation is needed.
		///
		/// <b>Mirrored in the shader</b> as <c>VTFaceExtentScale</c> (MirageVT.cginc). The CPU streamer picks
		/// which tiles to fetch with this and the shader picks which level to sample with it; if the two drift
		/// the shader asks for levels nothing streamed. Change both together (CLAUDE.md).
		/// </summary>
		// Token: 0x060000D3 RID: 211 RVA: 0x00007DB4 File Offset: 0x00005FB4
		public static double FaceExtentScale(double cu, double cv)
		{
			double s = 2.0 * cu - 1.0;
			double t = 2.0 * cv - 1.0;
			double s2 = s * s;
			double t2 = t * t;
			return Math.Sqrt(Math.Sqrt((1.0 + s2) * (1.0 + t2))) / (1.0 + s2 + t2);
		}

		/// <summary>
		/// Lat/lon → the CORRECTED tile coordinate containing it at a given level. The inverse of the chain
		/// <see cref="M:Mirage.WebIngest.MirageCubeMath.TileTexelToLatLon(System.Int32,System.Int32,System.Int32,System.Int32,System.Double,System.Double,System.Int32,System.Int32,System.Double@,System.Double@)" /> walks, so it answers "which tile covers this place?".
		/// </summary>
		// Token: 0x060000D4 RID: 212 RVA: 0x00007E2C File Offset: 0x0000602C
		public static void LatLonToTile(double lat, double lon, int level, out int face, out int tx, out int ty)
		{
			double x;
			double y;
			double z;
			MirageCubeMath.LatLonToDirection(lat, lon, out x, out y, out z);
			double u;
			double v;
			MirageCubeMath.DirectionToFaceUV(x, y, z, out face, out u, out v);
			double cu;
			double cv;
			MirageCubeMath.CorrectFaceUV(face, u, v, out cu, out cv);
			int grid = 1 << level;
			tx = (int)Math.Floor(cu * (double)grid);
			ty = (int)Math.Floor(cv * (double)grid);
			bool flag = tx < 0;
			if (flag)
			{
				tx = 0;
			}
			bool flag2 = tx >= grid;
			if (flag2)
			{
				tx = grid - 1;
			}
			bool flag3 = ty < 0;
			if (flag3)
			{
				ty = 0;
			}
			bool flag4 = ty >= grid;
			if (flag4)
			{
				ty = grid - 1;
			}
		}

		/// <summary>Unit direction → lat/lon in degrees, using KSP's convention (+Y north, lon 0° at +X,
		/// lon 90°E at +Z) — the same one <c>CelestialBody.GetRelSurfaceNVector</c> uses.</summary>
		// Token: 0x060000D5 RID: 213 RVA: 0x00007EDC File Offset: 0x000060DC
		public static void DirectionToLatLon(double x, double y, double z, out double lat, out double lon)
		{
			lat = Math.Asin(Math.Max(-1.0, Math.Min(1.0, y))) * 57.29577951308232;
			lon = Math.Atan2(z, x) * 57.29577951308232;
		}

		/// <summary>Lat/lon in degrees → unit direction. Inverse of <see cref="M:Mirage.WebIngest.MirageCubeMath.DirectionToLatLon(System.Double,System.Double,System.Double,System.Double@,System.Double@)" />.</summary>
		// Token: 0x060000D6 RID: 214 RVA: 0x00007F2C File Offset: 0x0000612C
		public static void LatLonToDirection(double lat, double lon, out double x, out double y, out double z)
		{
			double la = lat * 0.017453292519943295;
			double lo = lon * 0.017453292519943295;
			double cl = Math.Cos(la);
			x = cl * Math.Cos(lo);
			y = Math.Sin(la);
			z = cl * Math.Sin(lo);
		}

		/// <summary>
		/// Apply the per-face rotation mapping a RAW face UV into the CORRECTED UV space tiles are stored in.
		///
		/// THE SINGLE SOURCE OF TRUTH for this rotation on the C# side — <c>MirageTileMath.CorrectFaceUV</c>
		/// forwards here, and it must stay in lockstep with <c>MirageVT.cginc:CorrectFaceUV</c> on the GPU side.
		/// If you change one, change both (CLAUDE.md).
		/// </summary>
		// Token: 0x060000D7 RID: 215 RVA: 0x00007F78 File Offset: 0x00006178
		public static void CorrectFaceUV(int face, double u, double v, out double cu, out double cv)
		{
			switch (face)
			{
			case 0:
				cu = v;
				cv = 1.0 - u;
				break;
			case 1:
				cu = 1.0 - v;
				cv = u;
				break;
			case 2:
			case 3:
			case 4:
				cu = 1.0 - u;
				cv = 1.0 - v;
				break;
			default:
				cu = u;
				cv = v;
				break;
			}
		}

		/// <summary>
		/// Inverse of <see cref="M:Mirage.WebIngest.MirageCubeMath.CorrectFaceUV(System.Int32,System.Double,System.Double,System.Double@,System.Double@)" />: CORRECTED UV → RAW face UV. The baker needs this direction —
		/// it starts from a tile coordinate (which is in corrected space) and has to find the direction, hence
		/// the lat/lon, each of its texels covers.
		/// </summary>
		// Token: 0x060000D8 RID: 216 RVA: 0x00007FF4 File Offset: 0x000061F4
		public static void UncorrectFaceUV(int face, double cu, double cv, out double u, out double v)
		{
			switch (face)
			{
			case 0:
				u = 1.0 - cv;
				v = cu;
				break;
			case 1:
				u = cv;
				v = 1.0 - cu;
				break;
			case 2:
			case 3:
			case 4:
				u = 1.0 - cu;
				v = 1.0 - cv;
				break;
			default:
				u = cu;
				v = cv;
				break;
			}
		}

		/// <summary>
		/// Full chain for one output texel of a tile being baked: CORRECTED tile coord + texel → lat/lon.
		///
		/// <paramref name="px" />/<paramref name="py" /> are texel indices in the tile's SLOT, which includes the
		/// border: slot = tileSize + 2·borderPx, and the border over-fetches neighbouring imagery so filtering
		/// across a tile edge stays seamless (§9).
		///
		/// <b>Texel indices are sample POINTS, not cell centres — there is deliberately no +0.5 here.</b> This
		/// must match how Mirage READS a tile, and it does not get a vote. The sampler
		/// (<c>BatchPQSMod_MirageTerrain.HeightJob</c>, mirroring <c>MirageVT.cginc</c>) computes
		/// <c>px = borderPx + (cu·g − tx)·tileSize</c> and then Mitchell-Netravali-interpolates about
		/// <c>floor(px)</c>; at fractional 0 the MN weights collapse to a blend centred exactly on
		/// <c>tile[floor(px)]</c>. So corrected-UV <c>tx/g</c> lands on texel index <c>borderPx</c> exactly, and
		/// UV <c>(tx+1)/g</c> on <c>borderPx + tileSize</c> — a point-sampled grid.
		///
		/// Writing tiles on a cell-centred grid (with the +0.5 this originally had) while the sampler reads them
		/// point-sampled offsets every baked tile by half a texel against the canonical ones. It is far too
		/// small to look wrong and far too systematic to be harmless: `--test-reproject` caught it as a
		/// correlation peak at (0,−1) instead of (0,0) on 12 of 16 tiles.
		/// </summary>
		// Token: 0x060000D9 RID: 217 RVA: 0x00008070 File Offset: 0x00006270
		public static void TileTexelToLatLon(int face, int level, int tx, int ty, double px, double py, int tileSize, int borderPx, out double lat, out double lon)
		{
			int grid = 1 << level;
			double tileSpan = 1.0 / (double)grid;
			double texelSpan = tileSpan / (double)tileSize;
			double cu = (double)tx * tileSpan + (px - (double)borderPx) * texelSpan;
			double cv = (double)ty * tileSpan + (py - (double)borderPx) * texelSpan;
			double u;
			double v;
			MirageCubeMath.UncorrectFaceUV(face, cu, cv, out u, out v);
			double x;
			double y;
			double z;
			MirageCubeMath.FaceUVToDirection(face, u, v, out x, out y, out z);
			MirageCubeMath.DirectionToLatLon(x, y, z, out lat, out lon);
		}

		// Token: 0x060000DA RID: 218 RVA: 0x000080E4 File Offset: 0x000062E4
		// Note: this type is marked as 'beforefieldinit'.
		static MirageCubeMath()
		{
			double[][] array = new double[6][];
			int num = 0;
			double[] array2 = new double[3];
			array2[0] = 1.0;
			array[num] = array2;
			int num2 = 1;
			double[] array3 = new double[3];
			array3[0] = -1.0;
			array[num2] = array3;
			int num3 = 2;
			double[] array4 = new double[3];
			array4[1] = 1.0;
			array[num3] = array4;
			int num4 = 3;
			double[] array5 = new double[3];
			array5[1] = -1.0;
			array[num4] = array5;
			array[4] = new double[]
			{
				0.0,
				0.0,
				1.0
			};
			array[5] = new double[]
			{
				0.0,
				0.0,
				-1.0
			};
			MirageCubeMath.FaceAxis = array;
			double[][] array6 = new double[6][];
			int num5 = 0;
			double[] array7 = new double[3];
			array7[1] = 1.0;
			array6[num5] = array7;
			int num6 = 1;
			double[] array8 = new double[3];
			array8[1] = -1.0;
			array6[num6] = array8;
			int num7 = 2;
			double[] array9 = new double[3];
			array9[0] = -1.0;
			array6[num7] = array9;
			int num8 = 3;
			double[] array10 = new double[3];
			array10[0] = -1.0;
			array6[num8] = array10;
			int num9 = 4;
			double[] array11 = new double[3];
			array11[0] = -1.0;
			array6[num9] = array11;
			int num10 = 5;
			double[] array12 = new double[3];
			array12[0] = -1.0;
			array6[num10] = array12;
			MirageCubeMath.FaceU = array6;
			double[][] array13 = new double[6][];
			array13[0] = new double[]
			{
				0.0,
				0.0,
				-1.0
			};
			array13[1] = new double[]
			{
				0.0,
				0.0,
				-1.0
			};
			array13[2] = new double[]
			{
				0.0,
				0.0,
				-1.0
			};
			array13[3] = new double[]
			{
				0.0,
				0.0,
				1.0
			};
			int num11 = 4;
			double[] array14 = new double[3];
			array14[1] = 1.0;
			array13[num11] = array14;
			int num12 = 5;
			double[] array15 = new double[3];
			array15[1] = -1.0;
			array13[num12] = array15;
			MirageCubeMath.FaceV = array13;
			MirageCubeMath.BasisPinned = true;
		}

		// Token: 0x040000B5 RID: 181
		public static readonly string[] FaceNames = new string[]
		{
			"Xp",
			"Xn",
			"Yp",
			"Yn",
			"Zp",
			"Zn"
		};

		/// <summary>
		/// Outward axis of each cube face. Confirmed against direct observation of the shipped Earth archive
		/// (six independent checks, all agreeing), read through KSP's own lat/lon convention
		/// <c>x = cos(lat)·cos(lon), y = sin(lat), z = cos(lat)·sin(lon)</c> — i.e. +Y is north, lon 0° is +X,
		/// lon 90°E is +Z:
		///
		///   Xp (+X, lon 0°)    → Africa + the eastern edge of Brazil     (Greenwich at the equator)
		///   Xn (−X, lon 180°)  → New Zealand + half of Australia
		///   Yp (+Y, north)     → North America, N. Europe, N. Eurasia
		///   Yn (−Y, south)     → Antarctica, southern tip of South America
		///   Zp (+Z, lon 90°E)  → half of Australia + Eurasia
		///   Zn (−Z, lon 90°W)  → North and South America
		/// </summary>
		// Token: 0x040000B6 RID: 182
		public static readonly double[][] FaceAxis;

		/// <summary>
		/// In-face tangent for RAW face U (the axis raw u runs along, from u=0 to u=1).
		///
		/// Unlike <see cref="F:Mirage.WebIngest.MirageCubeMath.FaceAxis" />, which follows from the lat/lon convention, the in-face orientation
		/// does NOT: for each face there are eight possible (U, V) dihedral choices, and a wrong one yields
		/// tiles that are rotated or mirrored — plausible-looking, catastrophically misplaced, and exactly the
		/// failure the ingest doc's risk summary is about. Nothing in Mirage computes this today (the shader
		/// consumes face UVs that PQS bakes into UV3, so the mapping lives inside KSP), so it cannot be read
		/// off the existing code. It is therefore MEASURED, not derived.
		/// </summary>
		/// <remarks>
		/// MEASURED by `ArchivePacker --test-cube` against Sol's shipped Earth height archive, correlated
		/// against an independent public DEM (Terrarium terrain-RGB) — the same planet measured twice, by two
		/// unrelated parties. All six faces resolved decisively:
		///
		///     face   winner          r        best runner-up
		///     Xp     U=+Y V=-Z       0.995    0.435
		///     Xn     U=-Y V=-Z       0.985    0.562
		///     Yp     U=-X V=-Z       0.996    0.415
		///     Yn     U=-X V=+Z       0.994    0.655
		///     Zp     U=-X V=+Y       0.994    0.422
		///     Zn     U=-X V=-Y       0.995    0.442
		///
		/// Three things corroborate it beyond the correlation itself:
		///  - Every face satisfies <c>U × V = −N</c>: one consistent handedness across the cube. Nothing in the
		///    search enforced that — each face was scored independently — so a basis assembled from noise would
		///    have no reason to come out self-consistent.
		///  - Zn's answer means u runs WEST and v runs SOUTH, i.e. north-up / east-left — matching the
		///    independent human description of the shipped tiles ("positive East, so flipped horizontally").
		///  - An earlier, far noisier imagery-based test independently picked the same Zn basis.
		/// </remarks>
		// Token: 0x040000B7 RID: 183
		public static readonly double[][] FaceU;

		/// <summary>In-face tangent for RAW face V. Measured alongside <see cref="F:Mirage.WebIngest.MirageCubeMath.FaceU" />.</summary>
		// Token: 0x040000B8 RID: 184
		public static readonly double[][] FaceV;
	}
}
