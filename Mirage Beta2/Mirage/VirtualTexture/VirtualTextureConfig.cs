using System;
using System.IO;
using Mirage.WebIngest;
using UnityEngine;

namespace Mirage.VirtualTexture
{
	/// <summary>
	/// Per-body settings for the virtual texture cache. Populated by the host mod
	/// (typically from a "VirtualTexture" config subnode).
	///
	/// Example config consumed by the host:
	///   VirtualTexture
	///   {
	///       colormapTilePath  = SomeMod/PluginData/Earth/Color
	///       heightmapTilePath = SomeMod/PluginData/Earth/Height
	///       normalmapTilePath = SomeMod/PluginData/Earth/Normal
	///       atlasSize = 8192    // optional, default 8192 (applies to every cache)
	///       tileSize  = 256     // optional, default 256
	///       borderPx  = 4       // optional, default 4
	///       webMaxLevel = 12   // deepest level web ingest streams to (usually set)
	///       canonicalMaxLevel = 7  // optional; omit to auto-detect from the archive's installed depth
	///   }
	///
	/// Any subset of the three paths can be omitted to disable that cache. Each
	/// configured layer gets its own <see cref="T:Mirage.VirtualTexture.TileCache" /> (separate atlas + page
	/// table) but the three share the dimension settings for simplicity.
	/// </summary>
	// Token: 0x0200004F RID: 79
	public class VirtualTextureConfig
	{
		/// <summary>The colour grade to bake web imagery with — the configured look, or identity when
		/// <see cref="F:Mirage.VirtualTexture.VirtualTextureConfig.colorGrade" /> is off.</summary>
		// Token: 0x1700005B RID: 91
		// (get) Token: 0x06000224 RID: 548 RVA: 0x00012B69 File Offset: 0x00010D69
		public ColorGrade ColorGradeParams
		{
			get
			{
				return this.colorGrade ? new ColorGrade(this.colorExposure, this.colorBrightness, this.colorContrast, this.colorSaturation, this.colorGamma, this.colorTemperature, this.colorTint) : ColorGrade.Identity;
			}
		}

		/// <summary>Is bake-as-you-fly actually available for this body? Needs the mod-wide opt-in
		/// (<see cref="P:Mirage.MirageSettings.WebIngest" />) AND a writable tier AND an archive to sit on top of — web tiles
		/// are levels finer than canonical's K, so without a canonical floor the indirection would have nothing to
		/// fall back to (§6.2).</summary>
		// Token: 0x1700005C RID: 92
		// (get) Token: 0x06000225 RID: 549 RVA: 0x00012BA9 File Offset: 0x00010DA9
		public bool UseWebIngest
		{
			get
			{
				return MirageSettings.WebIngest && !string.IsNullOrEmpty(this.webPath) && this.UseArchive && this.IsValid;
			}
		}

		/// <summary>Deepest level to actually STREAM this session. Equals <see cref="F:Mirage.VirtualTexture.VirtualTextureConfig.webMaxLevel" /> when the web
		/// tier is in use — <see cref="P:Mirage.MirageSettings.WebStreaming" /> on, or <see cref="P:Mirage.MirageSettings.WebIngest" />
		/// on (ingest must stream what it bakes) — otherwise <see cref="F:Mirage.VirtualTexture.VirtualTextureConfig.canonicalMaxLevel" />: the descent stops at
		/// canonical, so no L8+ tile is ever requested, the web index is never consulted, and the "tile load
		/// failed" spam + its per-frame work disappear. The page-table structure is still built for the full
		/// <see cref="F:Mirage.VirtualTexture.VirtualTextureConfig.webMaxLevel" />, so turning streaming on needs no re-pack, just a reload.</summary>
		// Token: 0x1700005D RID: 93
		// (get) Token: 0x06000226 RID: 550 RVA: 0x00012BD0 File Offset: 0x00010DD0
		public int StreamingMaxLevel
		{
			get
			{
				return (MirageSettings.WebStreaming || MirageSettings.WebIngest) ? this.webMaxLevel : this.canonicalMaxLevel;
			}
		}

		/// <summary>Deepest level the SCALED path streams and builds its cache to. Same as
		/// <see cref="P:Mirage.VirtualTexture.VirtualTextureConfig.StreamingMaxLevel" /> when <see cref="P:Mirage.MirageSettings.ScaledWebStreaming" /> is on, otherwise
		/// capped at <see cref="F:Mirage.VirtualTexture.VirtualTextureConfig.canonicalMaxLevel" /> — the fine tier stays a surface-only feature. The two paths
		/// share this config object but not this number, which is why the cap is read per BODY
		/// (<see cref="P:Mirage.VirtualTexture.IMirageBody.StreamingMaxLevel" />) rather than off the config.</summary>
		// Token: 0x1700005E RID: 94
		// (get) Token: 0x06000227 RID: 551 RVA: 0x00012BEE File Offset: 0x00010DEE
		public int ScaledStreamingMaxLevel
		{
			get
			{
				return MirageSettings.ScaledWebStreaming ? this.StreamingMaxLevel : Mathf.Min(this.StreamingMaxLevel, this.canonicalMaxLevel);
			}
		}

		// Token: 0x1700005F RID: 95
		// (get) Token: 0x06000228 RID: 552 RVA: 0x00012C10 File Offset: 0x00010E10
		public bool HasColormap
		{
			get
			{
				return !string.IsNullOrEmpty(this.colormapTilePath);
			}
		}

		// Token: 0x17000060 RID: 96
		// (get) Token: 0x06000229 RID: 553 RVA: 0x00012C20 File Offset: 0x00010E20
		public bool HasHeightmap
		{
			get
			{
				return !string.IsNullOrEmpty(this.heightmapTilePath);
			}
		}

		// Token: 0x17000061 RID: 97
		// (get) Token: 0x0600022A RID: 554 RVA: 0x00012C30 File Offset: 0x00010E30
		public bool HasNormalmap
		{
			get
			{
				return !string.IsNullOrEmpty(this.normalmapTilePath);
			}
		}

		// Token: 0x17000062 RID: 98
		// (get) Token: 0x0600022B RID: 555 RVA: 0x00012C40 File Offset: 0x00010E40
		public bool IsValid
		{
			get
			{
				return this.HasLayer(VTLayer.Color) || this.HasLayer(VTLayer.Height) || this.HasLayer(VTLayer.Normal);
			}
		}

		/// <summary>Resolve a config path to an absolute filesystem path. Config values are GameData-relative
		/// (same convention as the loose <c>*TilePath</c> fields, e.g. <c>Sol-Textures/PluginData/…/Terrain</c>),
		/// so an archive is portable across installs. An already-absolute path is honoured verbatim. Archive
		/// blobs are read through KSPTextureLoader's <c>AsyncReadManager</c>, which needs a real filesystem
		/// path — GameData-relative resolution is Mirage's job, done here.</summary>
		// Token: 0x0600022C RID: 556 RVA: 0x00012C60 File Offset: 0x00010E60
		private static string ResolveGameDataPath(string p)
		{
			bool flag = string.IsNullOrEmpty(p);
			string result;
			if (flag)
			{
				result = null;
			}
			else
			{
				bool flag2 = Path.IsPathRooted(p);
				if (flag2)
				{
					result = p;
				}
				else
				{
					result = Path.Combine(KSPUtil.ApplicationRootPath, "GameData", p);
				}
			}
			return result;
		}

		// Token: 0x17000063 RID: 99
		// (get) Token: 0x0600022D RID: 557 RVA: 0x00012C9E File Offset: 0x00010E9E
		public string ResolvedArchivePath
		{
			get
			{
				return VirtualTextureConfig.ResolveGameDataPath(this.archivePath);
			}
		}

		// Token: 0x17000064 RID: 100
		// (get) Token: 0x0600022E RID: 558 RVA: 0x00012CAB File Offset: 0x00010EAB
		public string ResolvedWebPath
		{
			get
			{
				return VirtualTextureConfig.ResolveGameDataPath(this.webPath);
			}
		}

		// Token: 0x0600022F RID: 559 RVA: 0x00012CB8 File Offset: 0x00010EB8
		private void EnsureArchiveProbed()
		{
			bool archiveProbed = this._archiveProbed;
			if (!archiveProbed)
			{
				this._archiveProbed = true;
				string dir = this.ResolvedArchivePath;
				this._useArchive = TileArchivePaths.HasArchive(dir);
				bool useArchive = this._useArchive;
				if (useArchive)
				{
					for (int i = 0; i < 3; i++)
					{
						this._archiveLayerK[i] = TileArchivePaths.DetectMaxLevel(dir, (ArchiveLayer)i);
					}
				}
			}
		}

		/// <summary>This body reads from a tile archive (a <c>Level_&lt;N&gt;</c> tree exists under
		/// <c>archivePath</c>) rather than loose tiles. Cached after the first probe.</summary>
		// Token: 0x17000065 RID: 101
		// (get) Token: 0x06000230 RID: 560 RVA: 0x00012D18 File Offset: 0x00010F18
		public bool UseArchive
		{
			get
			{
				this.EnsureArchiveProbed();
				return this._useArchive;
			}
		}

		/// <summary>Finest installed level for a layer in the archive (0..K), or -1 if absent. Cached.</summary>
		// Token: 0x06000231 RID: 561 RVA: 0x00012D38 File Offset: 0x00010F38
		public int ArchiveLayerMaxLevel(VTLayer layer)
		{
			this.EnsureArchiveProbed();
			return this._archiveLayerK[(int)layer];
		}

		/// <summary>Is this layer available for this body — installed in the archive (any <c>Level_&lt;N&gt;</c>
		/// present) if archived, else the loose tile path.</summary>
		// Token: 0x06000232 RID: 562 RVA: 0x00012D5C File Offset: 0x00010F5C
		public bool HasLayer(VTLayer layer)
		{
			this.EnsureArchiveProbed();
			bool useArchive = this._useArchive;
			bool result;
			if (useArchive)
			{
				result = (this._archiveLayerK[(int)layer] >= 0);
			}
			else
			{
				if (!true)
				{
				}
				bool flag;
				switch (layer)
				{
				case VTLayer.Color:
					flag = this.HasColormap;
					break;
				case VTLayer.Height:
					flag = this.HasHeightmap;
					break;
				case VTLayer.Normal:
					flag = this.HasNormalmap;
					break;
				default:
					flag = false;
					break;
				}
				if (!true)
				{
				}
				result = flag;
			}
			return result;
		}

		/// <summary>This body's writable web tier for a layer, or null if it has none. Lazily opened, one per
		/// layer, owned by this config. A layer only gets a web tier when it is also installed canonically: the
		/// web set holds levels finer than canonical's K (§5), and without a canonical floor beneath it there'd
		/// be nothing for the indirection to fall back to, which is the no-holes invariant of §6.2.</summary>
		// Token: 0x06000233 RID: 563 RVA: 0x00012DCC File Offset: 0x00010FCC
		public WebTileArchive GetWebArchive(VTLayer layer)
		{
			string web = this.ResolvedWebPath;
			bool flag = string.IsNullOrEmpty(web) || !this.UseArchive || this.ArchiveLayerMaxLevel(layer) < 0;
			WebTileArchive result;
			if (flag)
			{
				result = null;
			}
			else
			{
				bool flag2 = this._webByLayer[(int)layer] == null;
				if (flag2)
				{
					try
					{
						this._webByLayer[(int)layer] = new WebTileArchive(web, (ArchiveLayer)layer, this.tileSize, this.borderPx);
					}
					catch (Exception e)
					{
						MirageDebug.LogError(string.Format("VirtualTexture: could not open the {0} web tier at '{1}': {2}. ", layer, web, e.Message) + "Continuing with canonical only.");
						return null;
					}
				}
				result = this._webByLayer[(int)layer];
			}
			return result;
		}

		/// <summary>Flush and close every open web tier for this body (index persisted; blob handle released).</summary>
		// Token: 0x06000234 RID: 564 RVA: 0x00012E88 File Offset: 0x00011088
		public void CloseWebArchives()
		{
			for (int i = 0; i < this._webByLayer.Length; i++)
			{
				WebTileArchive webTileArchive = this._webByLayer[i];
				if (webTileArchive != null)
				{
					webTileArchive.Dispose();
				}
				this._webByLayer[i] = null;
			}
		}

		/// <summary>Build the tile source for one layer — archive-backed when <see cref="P:Mirage.VirtualTexture.VirtualTextureConfig.UseArchive" />, else a
		/// loose-file reader over the layer's tile path. <paramref name="linear" /> is body-specific (the scaled
		/// and PQS shaders differ on height colour space), so the caller supplies it.</summary>
		// Token: 0x06000235 RID: 565 RVA: 0x00012ECC File Offset: 0x000110CC
		public ITileLayerSource CreateSource(VTLayer layer, bool linear)
		{
			int slotDim = this.tileSize + 2 * this.borderPx;
			bool useArchive = this.UseArchive;
			ITileLayerSource result;
			if (useArchive)
			{
				ArchiveTileLayerSource src = new ArchiveTileLayerSource(this.ResolvedArchivePath, (ArchiveLayer)layer, linear, slotDim);
				src.AttachWebArchive(this.GetWebArchive(layer));
				result = src;
			}
			else
			{
				result = new LooseFileTileSource(this.LoosePath(layer), linear);
			}
			return result;
		}

		// Token: 0x06000236 RID: 566 RVA: 0x00012F28 File Offset: 0x00011128
		private string LoosePath(VTLayer layer)
		{
			if (!true)
			{
			}
			string result;
			switch (layer)
			{
			case VTLayer.Color:
				result = this.colormapTilePath;
				break;
			case VTLayer.Height:
				result = this.heightmapTilePath;
				break;
			case VTLayer.Normal:
				result = this.normalmapTilePath;
				break;
			default:
				result = null;
				break;
			}
			if (!true)
			{
			}
			return result;
		}

		/// <summary>Build the CPU-side height sampler for PQS collision meshing, or null if this body has no
		/// height layer. Archive-backed bodies read the canonical height blob (same bytes as the GPU); loose
		/// bodies read the .dds pyramid.</summary>
		// Token: 0x06000237 RID: 567 RVA: 0x00012F74 File Offset: 0x00011174
		public HeightTileLayer CreateCpuHeightLayer()
		{
			bool flag = !this.HasLayer(VTLayer.Height);
			HeightTileLayer result;
			if (flag)
			{
				result = null;
			}
			else
			{
				bool useArchive = this.UseArchive;
				if (useArchive)
				{
					result = new HeightTileLayer(new CpuHeightArchive(this.ResolvedArchivePath, this.GetWebArchive(VTLayer.Height)), this.tileSize, this.borderPx, this.StreamingMaxLevel);
				}
				else
				{
					result = new HeightTileLayer(this.heightmapTilePath, this.tileSize, this.borderPx, this.StreamingMaxLevel);
				}
			}
			return result;
		}

		/// <summary>
		/// Effective <c>_MaxTessellation</c> ceiling for this body. When config sets
		/// <see cref="F:Mirage.VirtualTexture.VirtualTextureConfig.maxTessellation" /> (&gt; 0) that value wins. Otherwise it's
		/// auto-derived from VT pyramid depth so geometry is never subdivided finer
		/// than the heightmap can feed it: levels 0..<paramref name="coarseMaxLevel" />
		/// are the pinned coarse base, and each streamed level beyond that doubles
		/// linear texel density, so 2^(maxLevel − coarseMaxLevel) segments per base
		/// edge reaches the finest detail and no further. Clamped to
		/// <see cref="F:Mirage.VirtualTexture.VirtualTextureConfig.TessellationHardCap" /> (the shader's Range(1,64) limit).
		/// </summary>
		// Token: 0x06000238 RID: 568 RVA: 0x00012FEC File Offset: 0x000111EC
		public float ResolveMaxTessellation(int coarseMaxLevel)
		{
			bool flag = this.maxTessellation > 0;
			float result;
			if (flag)
			{
				result = (float)Mathf.Min(this.maxTessellation, 64);
			}
			else
			{
				int streamedLevels = Mathf.Max(0, this.StreamingMaxLevel - coarseMaxLevel);
				result = (float)Mathf.Clamp(1 << streamedLevels, 1, 64);
			}
			return result;
		}

		/// <summary>
		/// Effective <c>_MaxTessellationRange</c> (metres). This is now an optional
		/// hard distance cutoff for performance only, not the LOD driver — screen-space
		/// pixel size drives tessellation. Returns <see cref="F:Mirage.VirtualTexture.VirtualTextureConfig.maxTessellationRange" />
		/// when set (&gt; 0), otherwise 0, which the shader treats as "no cutoff".
		/// </summary>
		// Token: 0x06000239 RID: 569 RVA: 0x0001303C File Offset: 0x0001123C
		public float ResolveMaxTessellationRange()
		{
			return (this.maxTessellationRange > 0f) ? this.maxTessellationRange : 0f;
		}

		// Token: 0x040001EB RID: 491
		public string colormapTilePath;

		// Token: 0x040001EC RID: 492
		public string heightmapTilePath;

		// Token: 0x040001ED RID: 493
		public string normalmapTilePath;

		// Token: 0x040001EE RID: 494
		public string archivePath;

		// Token: 0x040001EF RID: 495
		public string webPath;

		// Token: 0x040001F0 RID: 496
		public string imageryProvider = "s2cloudless2024";

		// Token: 0x040001F1 RID: 497
		public bool bathymetry;

		// Token: 0x040001F2 RID: 498
		public bool heightDespike = true;

		// Token: 0x040001F3 RID: 499
		public bool colorGrade = true;

		// Token: 0x040001F4 RID: 500
		public float colorExposure = 0f;

		// Token: 0x040001F5 RID: 501
		public float colorBrightness = 0f;

		// Token: 0x040001F6 RID: 502
		public float colorContrast = 1f;

		// Token: 0x040001F7 RID: 503
		public float colorSaturation = 1f;

		// Token: 0x040001F8 RID: 504
		public float colorGamma = 1f;

		// Token: 0x040001F9 RID: 505
		public float colorTemperature = 0f;

		// Token: 0x040001FA RID: 506
		public float colorTint = 0f;

		// Token: 0x040001FB RID: 507
		public bool waterMask = true;

		// Token: 0x040001FC RID: 508
		public int atlasSize = 8192;

		// Token: 0x040001FD RID: 509
		public int tileSize = 256;

		// Token: 0x040001FE RID: 510
		public int borderPx = 4;

		// Token: 0x040001FF RID: 511
		public const int AutoMaxLevel = -1;

		// Token: 0x04000200 RID: 512
		public const int DefaultMaxLevel = 3;

		// Token: 0x04000201 RID: 513
		public int webMaxLevel = -1;

		// Token: 0x04000202 RID: 514
		public int canonicalMaxLevel = -1;

		// Token: 0x04000203 RID: 515
		public float tessellationEdgeLength = 16f;

		// Token: 0x04000204 RID: 516
		public int maxTessellation = 0;

		// Token: 0x04000205 RID: 517
		public float maxTessellationRange = 0f;

		// Token: 0x04000206 RID: 518
		public const int TessellationHardCap = 64;

		// Token: 0x04000207 RID: 519
		private bool _archiveProbed;

		// Token: 0x04000208 RID: 520
		private bool _useArchive;

		// Token: 0x04000209 RID: 521
		private readonly int[] _archiveLayerK = new int[]
		{
			-1,
			-1,
			-1
		};

		// Token: 0x0400020A RID: 522
		private readonly WebTileArchive[] _webByLayer = new WebTileArchive[3];
	}
}
