using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Mirage.VirtualTexture;

namespace Mirage.WebIngest
{
	/// <summary>
	/// The real <see cref="T:Mirage.WebIngest.ITileBaker" />: one cube tile, from web sources, for every layer the body has.
	/// Composes the pieces each of which has its own gate — <see cref="T:Mirage.WebIngest.CubeTileReprojector" /> (`--test-reproject`),
	/// <see cref="T:Mirage.WebIngest.Bc7Encoder" /> (`--test-bc7`), <see cref="T:Mirage.WebIngest.HeightFromDem" /> + <see cref="T:Mirage.WebIngest.TerrariumElevation" />
	/// (`--test-height`), <see cref="T:Mirage.WebIngest.NormalFromHeight" /> + <see cref="T:Mirage.WebIngest.Bc5Encoder" /> (`--test-normals`).
	///
	/// <b>All three layers, always.</b> Not a preference — §4.4: the cache is lockstep (one slot map, three
	/// atlases, resident only when every present layer landed), so a colour-only bake past canonical's finest
	/// height level would fail the whole group and be discarded. Height must come too, and normals are derived
	/// from it rather than fetched.
	///
	/// <b>Fetching is a seam</b> (<see cref="T:Mirage.WebIngest.CubeTileBaker.FetchAsync" />), for two reasons. `WebTileFetcher` is a main-thread
	/// Unity API (coroutines, <c>Time.realtimeSinceStartup</c>) while <see cref="M:Mirage.WebIngest.CubeTileBaker.BakeAsync(System.Int32,System.Int32,System.Int32,System.Int32,System.Threading.CancellationToken)" /> runs on a worker,
	/// so the KSP path must marshal anyway; and the seam is what lets the packer drive this exact class over
	/// plain HTTP, offline, where it can be checked against the shipped archive.
	///
	/// <b>Ordering: DEM first, colour last.</b> The DEM decides everything — it is needed by two layers, it is
	/// the one that can say "no coverage", and it is the cheaper fetch. Discovering after a BC7 encode that the
	/// tile was unbakeable would waste the most expensive stage in the pipeline.
	/// </summary>
	// Token: 0x02000013 RID: 19
	public sealed class CubeTileBaker : ITileBaker
	{
		// Token: 0x17000019 RID: 25
		// (get) Token: 0x06000080 RID: 128 RVA: 0x0000529D File Offset: 0x0000349D
		private int Slot
		{
			get
			{
				return this.tileSize + 2 * this.borderPx;
			}
		}

		// Token: 0x06000081 RID: 129 RVA: 0x000052B0 File Offset: 0x000034B0
		public CubeTileBaker(CubeTileBaker.FetchAsync fetch, ImageryProvider colorProvider, int tileSize, int borderPx, double planetRadius, double deformity, double offset, bool wantColor = true, bool wantHeight = true, bool wantNormal = true, ImageryProvider demProvider = null, double demElevationScale = 0.0, CubeTileBaker.GmrtFetchAsync gmrtFetch = null, bool despikeHeight = true, ColorGrade colorGrade = default(ColorGrade), bool emitWaterMask = true, WorldCoverSource.RangeFetch worldCoverFetch = null, string worldCoverBaseUrl = null, string worldCoverPrefix = null, float worldCoverSeaSinkM = 0f, float worldCoverSeaSinkMaxM = 0f, float seaFlattenMin = 0f, float seaFlattenMax = 0f, float seaFlattenSlope = 0f, float waterMaskBlurPx = 0f)
		{
			if (fetch == null)
			{
				throw new ArgumentNullException("fetch");
			}
			this.fetch = fetch;
			this.gmrtFetch = gmrtFetch;
			this.worldCoverFetch = worldCoverFetch;
			this.worldCoverBaseUrl = worldCoverBaseUrl;
			this.worldCoverPrefix = worldCoverPrefix;
			this.worldCoverSeaSinkM = worldCoverSeaSinkM;
			this.worldCoverSeaSinkMaxM = worldCoverSeaSinkMaxM;
			this.seaFlattenMin = seaFlattenMin;
			this.seaFlattenMax = seaFlattenMax;
			this.seaFlattenSlope = seaFlattenSlope;
			this.waterMaskBlurPx = waterMaskBlurPx;
			this.despikeHeight = despikeHeight;
			this.colorGrade = ((colorGrade.Contrast == 0f && colorGrade.Gamma == 0f) ? ColorGrade.Identity : colorGrade);
			this.emitWaterMask = emitWaterMask;
			this.colorProvider = (colorProvider ?? ImageryProvider.Default);
			this.demProvider = (demProvider ?? ImageryProvider.TerrariumDem);
			this.tileSize = tileSize;
			this.borderPx = borderPx;
			this.planetRadius = planetRadius;
			this.deformity = deformity;
			this.offset = offset;
			this.demElevationScale = ((demElevationScale > 0.0) ? demElevationScale : (planetRadius / 6371000.0));
			this.wantColor = wantColor;
			this.wantHeight = wantHeight;
			this.wantNormal = wantNormal;
		}

		// Token: 0x06000082 RID: 130 RVA: 0x000053EC File Offset: 0x000035EC
		[DebuggerStepThrough]
		public Task<BakedTile> BakeAsync(int face, int level, int tx, int ty, CancellationToken ct)
		{
			CubeTileBaker.<BakeAsync>d__31 <BakeAsync>d__ = new CubeTileBaker.<BakeAsync>d__31();
			<BakeAsync>d__.<>t__builder = AsyncTaskMethodBuilder<BakedTile>.Create();
			<BakeAsync>d__.<>4__this = this;
			<BakeAsync>d__.face = face;
			<BakeAsync>d__.level = level;
			<BakeAsync>d__.tx = tx;
			<BakeAsync>d__.ty = ty;
			<BakeAsync>d__.ct = ct;
			<BakeAsync>d__.<>1__state = -1;
			<BakeAsync>d__.<>t__builder.Start<CubeTileBaker.<BakeAsync>d__31>(ref <BakeAsync>d__);
			return <BakeAsync>d__.<>t__builder.Task;
		}

		// Token: 0x06000083 RID: 131 RVA: 0x00005458 File Offset: 0x00003658
		[DebuggerStepThrough]
		private Task<BakedTile> BakeInner(int face, int level, int tx, int ty, BakeBuffers buf, CancellationToken ct)
		{
			CubeTileBaker.<BakeInner>d__32 <BakeInner>d__ = new CubeTileBaker.<BakeInner>d__32();
			<BakeInner>d__.<>t__builder = AsyncTaskMethodBuilder<BakedTile>.Create();
			<BakeInner>d__.<>4__this = this;
			<BakeInner>d__.face = face;
			<BakeInner>d__.level = level;
			<BakeInner>d__.tx = tx;
			<BakeInner>d__.ty = ty;
			<BakeInner>d__.buf = buf;
			<BakeInner>d__.ct = ct;
			<BakeInner>d__.<>1__state = -1;
			<BakeInner>d__.<>t__builder.Start<CubeTileBaker.<BakeInner>d__32>(ref <BakeInner>d__);
			return <BakeInner>d__.<>t__builder.Task;
		}

		/// <summary>Produce each layer's storage-ready form (<see cref="M:Mirage.VirtualTexture.MirageArchiveFormat.EncodeForWeb(System.Byte[],System.Int32,System.Int32,System.Int32,Mirage.VirtualTexture.TileCodec@)" /> + CRC)
		/// into <see cref="F:Mirage.WebIngest.BakedTile.stored" />. Runs on the bake worker; the main-thread commit then only appends
		/// the bytes. Leaves <see cref="F:Mirage.WebIngest.BakedTile.payload" /> (raw) intact for the offline test harness.</summary>
		// Token: 0x06000084 RID: 132 RVA: 0x000054CC File Offset: 0x000036CC
		private static void EncodePayloadsForCommit(BakedTile result, int slot)
		{
			for (int i = 0; i < result.payload.Length; i++)
			{
				byte[] raw = result.payload[i];
				bool flag = raw == null;
				if (!flag)
				{
					TileCodec codec;
					result.stored[i] = MirageArchiveFormat.EncodeForWeb(raw, result.format[i], slot, slot, out codec);
					result.codec[i] = codec;
					result.crc[i] = MirageArchiveFormat.Crc32(result.stored[i]);
				}
			}
		}

		// Token: 0x06000085 RID: 133 RVA: 0x00005540 File Offset: 0x00003740
		[DebuggerStepThrough]
		private Task<IngestOutcome> FillDem(MercatorGather gather, int face, int level, int tx, int ty, int zoom, BakeBuffers buf, CancellationToken ct)
		{
			CubeTileBaker.<FillDem>d__34 <FillDem>d__ = new CubeTileBaker.<FillDem>d__34();
			<FillDem>d__.<>t__builder = AsyncTaskMethodBuilder<IngestOutcome>.Create();
			<FillDem>d__.<>4__this = this;
			<FillDem>d__.gather = gather;
			<FillDem>d__.face = face;
			<FillDem>d__.level = level;
			<FillDem>d__.tx = tx;
			<FillDem>d__.ty = ty;
			<FillDem>d__.zoom = zoom;
			<FillDem>d__.buf = buf;
			<FillDem>d__.ct = ct;
			<FillDem>d__.<>1__state = -1;
			<FillDem>d__.<>t__builder.Start<CubeTileBaker.<FillDem>d__34>(ref <FillDem>d__);
			return <FillDem>d__.<>t__builder.Task;
		}

		/// <summary>
		/// Replace the flat-zero ocean texels of a COASTAL tile's height with real GMRT depth. Returns Baked when
		/// there is no ocean to fill (pure land — nothing to do) or the fill succeeds, and Failed only on a GMRT
		/// fetch/parse fault — which is transient, so the coastal tile is retried rather than committed flat (a
		/// flat commit would never self-correct, and the seabed still falls back to the coarse tile meanwhile).
		///
		/// Keyed off <c>metres == 0</c>: Terrarium writes exactly 0 for ocean, and the MN reproject leaves the
		/// coastal blend band slightly positive and land clearly positive, so 0 isolates the pure-ocean texels
		/// precisely. GMRT depth is scaled to the body exactly as Terrarium was (<see cref="F:Mirage.WebIngest.CubeTileBaker.demElevationScale" />).
		/// </summary>
		// Token: 0x06000086 RID: 134 RVA: 0x000055C4 File Offset: 0x000037C4
		[DebuggerStepThrough]
		private Task<IngestOutcome> FillBathymetry(float[] metres, int face, int level, int tx, int ty, CancellationToken ct)
		{
			CubeTileBaker.<FillBathymetry>d__35 <FillBathymetry>d__ = new CubeTileBaker.<FillBathymetry>d__35();
			<FillBathymetry>d__.<>t__builder = AsyncTaskMethodBuilder<IngestOutcome>.Create();
			<FillBathymetry>d__.<>4__this = this;
			<FillBathymetry>d__.metres = metres;
			<FillBathymetry>d__.face = face;
			<FillBathymetry>d__.level = level;
			<FillBathymetry>d__.tx = tx;
			<FillBathymetry>d__.ty = ty;
			<FillBathymetry>d__.ct = ct;
			<FillBathymetry>d__.<>1__state = -1;
			<FillBathymetry>d__.<>t__builder.Start<CubeTileBaker.<FillBathymetry>d__35>(ref <FillBathymetry>d__);
			return <FillBathymetry>d__.<>t__builder.Task;
		}

		/// <summary>Lat/lon bounding box of a tile's slot plus a ~4-texel margin (so GMRT's bilinear taps have
		/// neighbours at the tile edge). Returns false if the box wraps the antimeridian — a single GridServer
		/// request cannot express it, and the caller skips bathymetry for that tile. Poles need no handling:
		/// tiles past the ±85.05° mercator cut were ruled NoCoverage upstream and never reach here.</summary>
		/// <summary>Fetch the WorldCover class window(s) covering a cube tile, or null when WorldCover is disabled,
		/// the tile's bbox can't be expressed (antimeridian wrap), or the fetch fails. Every null case is safe:
		/// the water mask falls back to the DEM sea-level term. Timed (fetch-dominated) under the bake profile.</summary>
		// Token: 0x06000087 RID: 135 RVA: 0x00005638 File Offset: 0x00003838
		[DebuggerStepThrough]
		private Task<WorldCoverSource> PrepareWorldCoverAsync(int face, int level, int tx, int ty, CancellationToken ct)
		{
			CubeTileBaker.<PrepareWorldCoverAsync>d__36 <PrepareWorldCoverAsync>d__ = new CubeTileBaker.<PrepareWorldCoverAsync>d__36();
			<PrepareWorldCoverAsync>d__.<>t__builder = AsyncTaskMethodBuilder<WorldCoverSource>.Create();
			<PrepareWorldCoverAsync>d__.<>4__this = this;
			<PrepareWorldCoverAsync>d__.face = face;
			<PrepareWorldCoverAsync>d__.level = level;
			<PrepareWorldCoverAsync>d__.tx = tx;
			<PrepareWorldCoverAsync>d__.ty = ty;
			<PrepareWorldCoverAsync>d__.ct = ct;
			<PrepareWorldCoverAsync>d__.<>1__state = -1;
			<PrepareWorldCoverAsync>d__.<>t__builder.Start<CubeTileBaker.<PrepareWorldCoverAsync>d__36>(ref <PrepareWorldCoverAsync>d__);
			return <PrepareWorldCoverAsync>d__.<>t__builder.Task;
		}

		// Token: 0x06000088 RID: 136 RVA: 0x000056A4 File Offset: 0x000038A4
		private bool TryTileBBox(int face, int level, int tx, int ty, out double west, out double east, out double south, out double north)
		{
			int slot = this.Slot;
			double minLat = double.MaxValue;
			double maxLat = double.MinValue;
			double minLon = double.MaxValue;
			double maxLon = double.MinValue;
			int step = Math.Max(1, slot / 16);
			for (int y = 0; y <= slot; y += step)
			{
				for (int x = 0; x <= slot; x += step)
				{
					int sx = Math.Min(x, slot - 1);
					int sy = Math.Min(y, slot - 1);
					double lat;
					double lon;
					MirageCubeMath.TileTexelToLatLon(face, level, tx, ty, (double)sx, (double)sy, this.tileSize, this.borderPx, out lat, out lon);
					bool flag = lat < minLat;
					if (flag)
					{
						minLat = lat;
					}
					bool flag2 = lat > maxLat;
					if (flag2)
					{
						maxLat = lat;
					}
					bool flag3 = lon < minLon;
					if (flag3)
					{
						minLon = lon;
					}
					bool flag4 = lon > maxLon;
					if (flag4)
					{
						maxLon = lon;
					}
				}
			}
			bool flag5 = maxLon - minLon > 180.0;
			bool result;
			if (flag5)
			{
				west = (east = (south = (north = 0.0)));
				result = false;
			}
			else
			{
				double marginDeg = 90.0 / ((double)(1L << level) * (double)this.tileSize) * 4.0;
				west = minLon - marginDeg;
				east = maxLon + marginDeg;
				south = minLat - marginDeg;
				north = maxLat + marginDeg;
				result = true;
			}
			return result;
		}

		/// <summary>
		/// Derive the normal tile from the SAME gather the height came from. Not an optimisation: normals are the
		/// derivative of height, so sourcing them from a second fetch (or a different zoom) would let the two
		/// disagree, and lighting would contradict the silhouette.
		///
		/// The stencil is equirect, per <see cref="T:Mirage.WebIngest.NormalFromHeight" /> — the frame and the step are east/north,
		/// never cube-space, which is what keeps these tiles continuous with canonical's at the level boundary.
		/// </summary>
		// Token: 0x06000089 RID: 137 RVA: 0x0000582C File Offset: 0x00003A2C
		private bool BakeNormal(BakedTile result, MercatorGather demGather, int demZoom, int face, int level, int tx, int ty, BakeBuffers buf)
		{
			int slot = this.Slot;
			double worldPx = (double)(1 << demZoom) * 256.0;
			Func<NormalFromHeight.ElevationSampler> samplerFactory = delegate()
			{
				float[] one = new float[1];
				float[] tap = new float[1];
				double[] row = new double[4];
				return delegate(double lat, double lon, out double m)
				{
					m = 0.0;
					bool flag2 = !MercatorTileMath.HasWebCoverage(lat);
					bool result3;
					if (flag2)
					{
						result3 = false;
					}
					else
					{
						ValueTuple<double, double> valueTuple = MercatorTileMath.LatLonToMercatorUV(lat, lon);
						double mu = valueTuple.Item1;
						double mv = valueTuple.Item2;
						bool flag3 = !demGather.TrySample(mu * worldPx, mv * worldPx, one, tap, row);
						if (flag3)
						{
							result3 = false;
						}
						else
						{
							m = (double)one[0];
							result3 = true;
						}
					}
					return result3;
				};
			};
			double stepDeg = 90.0 / (double)((1 << level) * this.tileSize);
			byte[] planeX = buf.RentByte(slot * slot);
			byte[] planeY = buf.RentByte(slot * slot);
			bool flag = !NormalFromHeight.Build(samplerFactory, face, level, tx, ty, this.tileSize, this.borderPx, this.planetRadius, stepDeg, planeX, planeY);
			bool result2;
			if (flag)
			{
				MirageDebug.LogError(string.Format("CubeTileBaker: normal stencil left the gather for L{0} f{1} {2},{3} — the ", new object[]
				{
					level,
					face,
					tx,
					ty
				}) + "RequiredTiles neighbour expansion is too tight for the stencil's reach.");
				result2 = false;
			}
			else
			{
				result.payload[2] = Bc5Encoder.EncodeXY(planeX, planeY, slot, slot);
				result.format[2] = 27;
				result2 = true;
			}
			return result2;
		}

		// Token: 0x0600008A RID: 138 RVA: 0x00005944 File Offset: 0x00003B44
		[DebuggerStepThrough]
		private Task<IngestOutcome> BakeColor(BakedTile result, int face, int level, int tx, int ty, BakeBuffers buf, byte[] waterMask, CancellationToken ct)
		{
			CubeTileBaker.<BakeColor>d__39 <BakeColor>d__ = new CubeTileBaker.<BakeColor>d__39();
			<BakeColor>d__.<>t__builder = AsyncTaskMethodBuilder<IngestOutcome>.Create();
			<BakeColor>d__.<>4__this = this;
			<BakeColor>d__.result = result;
			<BakeColor>d__.face = face;
			<BakeColor>d__.level = level;
			<BakeColor>d__.tx = tx;
			<BakeColor>d__.ty = ty;
			<BakeColor>d__.buf = buf;
			<BakeColor>d__.waterMask = waterMask;
			<BakeColor>d__.ct = ct;
			<BakeColor>d__.<>1__state = -1;
			<BakeColor>d__.<>t__builder.Start<CubeTileBaker.<BakeColor>d__39>(ref <BakeColor>d__);
			return <BakeColor>d__.<>t__builder.Task;
		}

		// Token: 0x0600008B RID: 139 RVA: 0x000059C8 File Offset: 0x00003BC8
		private static byte Clamp255(float v)
		{
			int i = (int)Math.Round((double)v);
			return (i < 0) ? 0 : ((i > 255) ? byte.MaxValue : ((byte)i));
		}

		/// <summary>
		/// Soften the hard 0/255 water mask into a smooth 0..255 ramp with a subtle separable Gaussian, in place.
		///
		/// <b>Why.</b> The mask is nearest-sampled from WorldCover's 10 m grid, so its edge is a hard (and, on a
		/// tile finer than 10 m, blocky) 0→255 step. That step lives in the colour tile's ALPHA, which BC7 packs
		/// into the same block as the SMOOTH RGB imagery. A razor alpha edge that does not track the colour coast
		/// is not collinear with RGB, so the block's shared interpolation line fits neither and the compressor
		/// emits the blocky colour artefacts seen along coastlines — even though the source imagery is clean. A
		/// few-texel blur makes alpha a gentle ramp roughly co-located with the colour transition, which both
		/// compresses cleanly (the encoder can then keep mode 6's 4-bit colour) and reads better as PQS
		/// smoothness (a shoreline is a ramp, not a specular cliff). The mask still crosses 0.5 at the true
		/// coastline, so any water/not-water threshold downstream is unmoved.
		///
		/// Separable → O(slot·(2r+1)) per axis; runs on the bake worker. Edge taps clamp to the slot edge (the
		/// slot already carries a border, so the visible tile's ramp is fed real neighbours).
		/// </summary>
		// Token: 0x0600008C RID: 140 RVA: 0x000059FC File Offset: 0x00003BFC
		private static void BlurWaterMask(byte[] mask, int slot, float sigma, BakeBuffers buf)
		{
			int r = (int)Math.Ceiling(2.0 * (double)sigma);
			bool flag = r < 1;
			if (!flag)
			{
				float[] kernel = new float[2 * r + 1];
				double s2 = 2.0 * (double)sigma * (double)sigma;
				double sum = 0.0;
				for (int i = -r; i <= r; i++)
				{
					double w = Math.Exp((double)(-(double)(i * i)) / s2);
					kernel[i + r] = (float)w;
					sum += w;
				}
				float inv = (float)(1.0 / sum);
				for (int j = 0; j < kernel.Length; j++)
				{
					kernel[j] *= inv;
				}
				float[] tmp = buf.RentFloat(slot * slot);
				for (int y = 0; y < slot; y++)
				{
					int row = y * slot;
					for (int x = 0; x < slot; x++)
					{
						float acc = 0f;
						for (int t = -r; t <= r; t++)
						{
							int sx = x + t;
							bool flag2 = sx < 0;
							if (flag2)
							{
								sx = 0;
							}
							else
							{
								bool flag3 = sx >= slot;
								if (flag3)
								{
									sx = slot - 1;
								}
							}
							acc += kernel[t + r] * (float)mask[row + sx];
						}
						tmp[row + x] = acc;
					}
				}
				for (int y2 = 0; y2 < slot; y2++)
				{
					for (int x2 = 0; x2 < slot; x2++)
					{
						float acc2 = 0f;
						for (int t2 = -r; t2 <= r; t2++)
						{
							int sy = y2 + t2;
							bool flag4 = sy < 0;
							if (flag4)
							{
								sy = 0;
							}
							else
							{
								bool flag5 = sy >= slot;
								if (flag5)
								{
									sy = slot - 1;
								}
							}
							acc2 += kernel[t2 + r] * tmp[sy * slot + x2];
						}
						int v = (int)(acc2 + 0.5f);
						mask[y2 * slot + x2] = ((v < 0) ? 0 : ((v > 255) ? byte.MaxValue : ((byte)v)));
					}
				}
			}
		}

		/// <summary>Does any texel of this tile's slot fall past the mercator cut? Walks the full slot rather
		/// than the corners: latitude is not monotonic across a polar face's tile, so a corner test would miss
		/// an interior excursion past ±85.05° and bake a fabricated pole.</summary>
		// Token: 0x0600008D RID: 141 RVA: 0x00005C40 File Offset: 0x00003E40
		private bool AnyTexelPastCut(int face, int level, int tx, int ty)
		{
			int slot = this.Slot;
			for (int y = 0; y < slot; y++)
			{
				for (int x = 0; x < slot; x++)
				{
					double lat;
					double num;
					MirageCubeMath.TileTexelToLatLon(face, level, tx, ty, (double)x, (double)y, this.tileSize, this.borderPx, out lat, out num);
					bool flag = !MercatorTileMath.HasWebCoverage(lat);
					if (flag)
					{
						return true;
					}
				}
			}
			return false;
		}

		// Token: 0x04000061 RID: 97
		private readonly CubeTileBaker.FetchAsync fetch;

		// Token: 0x04000062 RID: 98
		private readonly CubeTileBaker.GmrtFetchAsync gmrtFetch;

		// Token: 0x04000063 RID: 99
		private readonly WorldCoverSource.RangeFetch worldCoverFetch;

		// Token: 0x04000064 RID: 100
		private readonly string worldCoverBaseUrl;

		// Token: 0x04000065 RID: 101
		private readonly string worldCoverPrefix;

		// Token: 0x04000066 RID: 102
		private readonly float worldCoverSeaSinkM;

		// Token: 0x04000067 RID: 103
		private readonly float worldCoverSeaSinkMaxM;

		// Token: 0x04000068 RID: 104
		private readonly float seaFlattenMin;

		// Token: 0x04000069 RID: 105
		private readonly float seaFlattenMax;

		// Token: 0x0400006A RID: 106
		private readonly float seaFlattenSlope;

		// Token: 0x0400006B RID: 107
		private readonly ImageryProvider colorProvider;

		// Token: 0x0400006C RID: 108
		private readonly ImageryProvider demProvider;

		// Token: 0x0400006D RID: 109
		private readonly int tileSize;

		// Token: 0x0400006E RID: 110
		private readonly int borderPx;

		// Token: 0x0400006F RID: 111
		private readonly double planetRadius;

		// Token: 0x04000070 RID: 112
		private readonly double deformity;

		// Token: 0x04000071 RID: 113
		private readonly double offset;

		/// <summary>
		/// Multiplier from the DEM's real-world metres to THIS body's metres. 1.0 for a real-scale Earth; 0.25
		/// for a quarter-scale one.
		///
		/// <b>Not cosmetic — without it the bake is garbage on any rescaled system.</b> Terrarium reports real
		/// Earth elevations (−10,919 m … +8,748 m). A quarter-scale Sol's Earth has deformity 4879.87 /
		/// offset −2729.75, i.e. an R16 range of just [−2729.75, +2150.11] m, so every real elevation above
		/// 2150 m would clamp flat — the entire Himalaya, Andes and Alps rendered as plateaux — while
		/// <see cref="T:Mirage.WebIngest.NormalFromHeight" /> combined real relief with a quarter-size radius and produced normals
		/// about 4x too steep.
		///
		/// Scaling here rather than at the quantiser is what keeps it correct in both places at once: the
		/// identity is <c>(k·m − k·offset) / (k·deformity) ≡ (m − offset) / deformity</c>, so a uniformly
		/// rescaled body reproduces canonical's R16 exactly, and normals are scale-invariant once elevation and
		/// radius are scaled together (similar triangles).
		///
		/// This assumes the rescale is UNIFORM — that deformity/offset were scaled by the same factor as the
		/// radius, which is what Kopernicus rescales do (verified against Sol-Test: 19519.46387/4 = 4879.8659675
		/// and −10919/4 = −2729.75, exactly). A config that scales the radius but keeps real terrain heights
		/// would need this passed explicitly.
		/// </summary>
		// Token: 0x04000072 RID: 114
		private readonly double demElevationScale;

		// Token: 0x04000073 RID: 115
		private readonly bool wantColor;

		// Token: 0x04000074 RID: 116
		private readonly bool wantHeight;

		// Token: 0x04000075 RID: 117
		private readonly bool wantNormal;

		/// <summary>Run <see cref="T:Mirage.WebIngest.HeightDespike" /> on each decoded Terrarium source tile to strip corrupt hot
		/// pixels before they enter the gather (and get smeared into a ring by the MN reproject).</summary>
		// Token: 0x04000076 RID: 118
		private readonly bool despikeHeight;

		/// <summary>Colour post-process making the web imagery match the canonical look (RGB only). Identity when
		/// grading is off.</summary>
		// Token: 0x04000077 RID: 119
		private readonly ColorGrade colorGrade;

		/// <summary>Write the colour tile's alpha as a water mask (white = water) derived from the height layer,
		/// the canonical convention. When off, alpha stays opaque like the pre-mask bake (for A/B).</summary>
		// Token: 0x04000078 RID: 120
		private readonly bool emitWaterMask;

		/// <summary>Gaussian sigma (in output texels) softening the hard 0/255 water mask into a smooth ramp
		/// before it enters colour-alpha and BC7. 0 = no blur (hard edge). See <see cref="M:Mirage.WebIngest.CubeTileBaker.BlurWaterMask(System.Byte[],System.Int32,System.Single,Mirage.WebIngest.BakeBuffers)" />.</summary>
		// Token: 0x04000079 RID: 121
		private readonly float waterMaskBlurPx;

		/// <summary>Sea level in the DEM's metres (Terrarium/GMRT are absolute, 0 = sea level; the body rescale is
		/// linear so 0 maps to 0). At or below this a texel is water.</summary>
		// Token: 0x0400007A RID: 122
		private const float SeaLevelMetres = 0f;

		/// <summary>Fetch one mercator tile. Implementations must fire for EVERY terminal outcome — a request
		/// that silently never completes strands the key in <c>ingestInProgress</c> forever (§7).</summary>
		// Token: 0x02000088 RID: 136
		// (Invoke) Token: 0x0600044C RID: 1100
		public delegate Task<TileFetchResult> FetchAsync(ImageryProvider provider, int z, int x, int y, CancellationToken ct);

		/// <summary>Fetch a GMRT bathymetry grid (raw ArcASCII bytes) for a lat/lon box at a power-of-two
		/// resolution — see <see cref="T:Mirage.WebIngest.GmrtElevation" />. Null bytes (or a throw) means the fetch failed; the bake
		/// then treats the coastal tile as transient and retries, rather than baking a flat-ocean version that
		/// would never self-correct. Null delegate = bathymetry disabled for this body.</summary>
		// Token: 0x02000089 RID: 137
		// (Invoke) Token: 0x06000450 RID: 1104
		public delegate Task<byte[]> GmrtFetchAsync(double west, double east, double south, double north, int resolution, CancellationToken ct);
	}
}
