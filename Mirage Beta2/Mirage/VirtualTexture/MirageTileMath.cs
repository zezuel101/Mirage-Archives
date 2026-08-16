using System;
using KSPTextureLoader;
using Mirage.WebIngest;

namespace Mirage.VirtualTexture
{
	/// <summary>
	/// Shared addressing math for sampling the Mirage VT tile pyramid on the CPU.
	///
	/// These mirror the GPU side exactly so CPU-built PQS geometry/colour agrees with the
	/// streamed GPU virtual texture:
	///   - <see cref="M:Mirage.VirtualTexture.MirageTileMath.CorrectFaceUV(System.Int32,System.Double,System.Double,System.Double@,System.Double@)" /> matches <c>MirageVT.cginc:CorrectFaceUV</c> and the
	///     corner rotation baked into <see cref="M:Mirage.VirtualTexture.TileStreamingManager.GetCorrectedTileCoord(System.Int32,System.Single,System.Single,System.Int32,System.Int32@,System.Int32@)" />.
	///   - <see cref="M:Mirage.VirtualTexture.MirageTileMath.TilePath(System.String,System.Int32,System.Int32,System.Int32,System.Int32)" /> / <see cref="F:Mirage.VirtualTexture.MirageTileMath.FaceNames" /> match
	///     <c>TileCache</c> / <c>TileStreamingManager</c>.
	///
	/// Kept as plain <c>double</c> math (no managed allocations) so the BurstPQS jobs can call
	/// <see cref="M:Mirage.VirtualTexture.MirageTileMath.CorrectFaceUV(System.Int32,System.Double,System.Double,System.Double@,System.Double@)" /> directly.
	/// </summary>
	// Token: 0x02000037 RID: 55
	public static class MirageTileMath
	{
		// Token: 0x0600014F RID: 335 RVA: 0x0000B144 File Offset: 0x00009344
		public static string TilePath(string rootPath, int face, int level, int tx, int ty)
		{
			return string.Format("{0}/level_{1}/{2}/tile_{3}_{4}.dds", new object[]
			{
				rootPath,
				level,
				MirageTileMath.FaceNames[face],
				tx,
				ty
			});
		}

		/// <summary>
		/// Probe the tile pyramid under <paramref name="rootPath" /> for its deepest level. Levels are
		/// contiguous from 0, and every level has a corner tile at <c>level_&lt;L&gt;/Xp/tile_0_0.dds</c>, so
		/// we walk L upward until that corner is missing and return the last level that existed. Returns
		/// <c>-1</c> when the root is empty or has no level-0 tile (nothing usable on disk). Uses the same
		/// <see cref="M:KSPTextureLoader.TextureLoader.TextureExists(System.String)" /> GameData-relative resolution as the tile loaders.
		/// </summary>
		// Token: 0x06000150 RID: 336 RVA: 0x0000B180 File Offset: 0x00009380
		public static int DetectMaxLevel(string rootPath)
		{
			bool flag = string.IsNullOrEmpty(rootPath);
			int result;
			if (flag)
			{
				result = -1;
			}
			else
			{
				int detected = -1;
				for (int level = 0; level <= 20; level++)
				{
					bool flag2 = !TextureLoader.TextureExists(MirageTileMath.TilePath(rootPath, 0, level, 0, 0));
					if (flag2)
					{
						break;
					}
					detected = level;
				}
				result = detected;
			}
			return result;
		}

		/// <summary>
		/// Apply the per-face rotation that maps a raw face UV (the value PQSMod_PlanetUV bakes into
		/// UV3) into the corrected UV space the tiles are stored in. Mirrors MirageVT.cginc:CorrectFaceUV.
		///
		/// Forwards to <see cref="M:Mirage.WebIngest.MirageCubeMath.CorrectFaceUV(System.Int32,System.Double,System.Double,System.Double@,System.Double@)" />, which is the one C#-side implementation.
		/// The web-ingest baker needs this rotation (and its inverse) from a Unity-free, packer-linkable
		/// assembly, and a second copy of it here is exactly the drift CLAUDE.md warns about — the C# and
		/// shader sides must move together, which is impossible to guarantee across duplicated code.
		/// Still callable from Burst: both sides are plain double math with no allocation.
		/// </summary>
		// Token: 0x06000151 RID: 337 RVA: 0x0000B1D7 File Offset: 0x000093D7
		public static void CorrectFaceUV(int face, double u, double v, out double cu, out double cv)
		{
			MirageCubeMath.CorrectFaceUV(face, u, v, out cu, out cv);
		}

		// Token: 0x04000120 RID: 288
		public static readonly string[] FaceNames = new string[]
		{
			"Xp",
			"Xn",
			"Yp",
			"Yn",
			"Zp",
			"Zn"
		};

		// Token: 0x04000121 RID: 289
		public const int MaxDetectableLevel = 20;
	}
}
