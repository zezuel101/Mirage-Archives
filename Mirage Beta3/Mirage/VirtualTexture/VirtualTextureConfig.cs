using System;
using System.IO;
using Mirage.WebIngest;
using UnityEngine;

namespace Mirage.VirtualTexture
{
	/// <summary>Per-body virtual texture settings, parsed from a VirtualTexture { } config node.</summary>
	// Token: 0x0200005B RID: 91
	public class VirtualTextureConfig
	{
		/// <summary>Color grade for web imagery baking, or identity when colorGrade is off.</summary>
		// Token: 0x1700006A RID: 106
		// (get) Token: 0x060002A2 RID: 674 RVA: 0x00014BAC File Offset: 0x00012DAC
		public ColorGrade ColorGradeParams
		{
			get
			{
				return this.colorGrade ? new ColorGrade(this.colorExposure, this.colorBrightness, this.colorContrast, this.colorSaturation, this.colorGamma, this.colorTemperature, this.colorTint) : ColorGrade.Identity;
			}
		}

		/// <summary>True if the archive has at least one terrain layer installed.</summary>
		// Token: 0x1700006B RID: 107
		// (get) Token: 0x060002A3 RID: 675 RVA: 0x00014BEC File Offset: 0x00012DEC
		public bool IsValid
		{
			get
			{
				return this.HasLayer(VTLayer.Color) || this.HasLayer(VTLayer.Height) || this.HasLayer(VTLayer.Normal);
			}
		}

		/// <summary>Is the emissive layer both asked for and installed?</summary>
		// Token: 0x1700006C RID: 108
		// (get) Token: 0x060002A4 RID: 676 RVA: 0x00014C0A File Offset: 0x00012E0A
		public bool UseEmissiveLayer
		{
			get
			{
				return this.emissiveLayer && this.HasLayer(VTLayer.Emissive);
			}
		}

		/// <summary>Is bake-as-you-fly available? Needs the mod-wide opt-in, a writable tier, and canonical data.</summary>
		// Token: 0x1700006D RID: 109
		// (get) Token: 0x060002A5 RID: 677 RVA: 0x00014C1E File Offset: 0x00012E1E
		public bool UseWebIngest
		{
			get
			{
				return MirageSettings.WebIngest && !string.IsNullOrEmpty(this.webPath) && this.IsValid;
			}
		}

		/// <summary>Deepest level to stream this session: webMaxLevel when web is active, else canonicalMaxLevel.</summary>
		// Token: 0x1700006E RID: 110
		// (get) Token: 0x060002A6 RID: 678 RVA: 0x00014C3D File Offset: 0x00012E3D
		public int StreamingMaxLevel
		{
			get
			{
				return (MirageSettings.WebStreaming || MirageSettings.WebIngest) ? this.webMaxLevel : this.canonicalMaxLevel;
			}
		}

		/// <summary>Scaled-path streaming depth, capped at canonicalMaxLevel unless ScaledWebStreaming is on.</summary>
		// Token: 0x1700006F RID: 111
		// (get) Token: 0x060002A7 RID: 679 RVA: 0x00014C5B File Offset: 0x00012E5B
		public int ScaledStreamingMaxLevel
		{
			get
			{
				return MirageSettings.ScaledWebStreaming ? this.StreamingMaxLevel : Mathf.Min(this.StreamingMaxLevel, this.canonicalMaxLevel);
			}
		}

		// Token: 0x060002A8 RID: 680 RVA: 0x00014C80 File Offset: 0x00012E80
		private static int[] CreateLayerLevels()
		{
			int[] levels = new int[4];
			for (int i = 0; i < levels.Length; i++)
			{
				levels[i] = -1;
			}
			return levels;
		}

		// Token: 0x17000070 RID: 112
		// (get) Token: 0x060002A9 RID: 681 RVA: 0x00014CB0 File Offset: 0x00012EB0
		public string ResolvedArchivePath
		{
			get
			{
				return VirtualTextureConfig.ResolveGameDataPath(this.archivePath);
			}
		}

		// Token: 0x17000071 RID: 113
		// (get) Token: 0x060002AA RID: 682 RVA: 0x00014CBD File Offset: 0x00012EBD
		public string ResolvedWebPath
		{
			get
			{
				return VirtualTextureConfig.ResolveGameDataPath(this.webPath);
			}
		}

		/// <summary>Finest installed level for a layer (0..K), or -1 if absent.</summary>
		// Token: 0x060002AB RID: 683 RVA: 0x00014CCC File Offset: 0x00012ECC
		public int ArchiveLayerMaxLevel(VTLayer layer)
		{
			this.EnsureArchiveProbed();
			return this._archiveLayerK[(int)layer];
		}

		/// <summary>Does the archive ship any <c>Level_&lt;N&gt;</c> for this layer?</summary>
		// Token: 0x060002AC RID: 684 RVA: 0x00014CED File Offset: 0x00012EED
		public bool HasLayer(VTLayer layer)
		{
			return this.ArchiveLayerMaxLevel(layer) >= 0;
		}

		// Token: 0x060002AD RID: 685 RVA: 0x00014CFC File Offset: 0x00012EFC
		private void EnsureArchiveProbed()
		{
			bool archiveProbed = this._archiveProbed;
			if (!archiveProbed)
			{
				this._archiveProbed = true;
				string dir = this.ResolvedArchivePath;
				bool flag = !TileArchivePaths.HasArchive(dir);
				if (!flag)
				{
					for (int i = 0; i < this._archiveLayerK.Length; i++)
					{
						this._archiveLayerK[i] = TileArchivePaths.DetectMaxLevel(dir, (ArchiveLayer)i);
					}
				}
			}
		}

		// Token: 0x060002AE RID: 686 RVA: 0x00014D5C File Offset: 0x00012F5C
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

		/// <summary>This body's writable web tier for a layer, or null. Opened lazily, owned by this config.</summary>
		// Token: 0x060002AF RID: 687 RVA: 0x00014D9C File Offset: 0x00012F9C
		public WebTileArchive GetWebArchive(VTLayer layer)
		{
			bool flag = layer == VTLayer.Emissive;
			WebTileArchive result;
			if (flag)
			{
				result = null;
			}
			else
			{
				string web = this.ResolvedWebPath;
				bool flag2 = string.IsNullOrEmpty(web) || !this.HasLayer(layer);
				if (flag2)
				{
					result = null;
				}
				else
				{
					bool flag3 = this._webByLayer[(int)layer] != null;
					if (flag3)
					{
						result = this._webByLayer[(int)layer];
					}
					else
					{
						try
						{
							this._webByLayer[(int)layer] = new WebTileArchive(web, (ArchiveLayer)layer, this.tileSize, this.borderPx);
						}
						catch (Exception e)
						{
							MirageDebug.LogError(string.Format("VirtualTexture: could not open the {0} web tier at '{1}': {2}. ", layer, web, e.Message) + "Continuing with canonical only.");
						}
						result = this._webByLayer[(int)layer];
					}
				}
			}
			return result;
		}

		/// <summary>Flush and close every open web tier.</summary>
		// Token: 0x060002B0 RID: 688 RVA: 0x00014E68 File Offset: 0x00013068
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

		/// <summary>Build the archive-backed tile source for one layer.</summary>
		// Token: 0x060002B1 RID: 689 RVA: 0x00014EAC File Offset: 0x000130AC
		public ITileLayerSource CreateSource(VTLayer layer, bool linear)
		{
			ArchiveTileLayerSource source = new ArchiveTileLayerSource(this.ResolvedArchivePath, (ArchiveLayer)layer, linear, this.tileSize + 2 * this.borderPx);
			source.AttachWebArchive(this.GetWebArchive(layer));
			return source;
		}

		/// <summary>Register the optional emissive layer on a cache, capped at its own canonical depth.
		/// Does nothing when the body has not asked for it.</summary>
		// Token: 0x060002B2 RID: 690 RVA: 0x00014EEC File Offset: 0x000130EC
		public void AddEmissiveLayerTo(TileCache cache)
		{
			bool flag = !this.emissiveLayer;
			if (!flag)
			{
				bool flag2 = !this.HasLayer(VTLayer.Emissive);
				if (flag2)
				{
					MirageDebug.LogError("VirtualTexture: emissiveLayer is on but the archive ships no emissive pyramid. Continuing without it.");
				}
				else
				{
					cache.AddLayer(VTLayer.Emissive, "_Emissive", this.CreateSource(VTLayer.Emissive, true), Mathf.Min(this.ArchiveLayerMaxLevel(VTLayer.Emissive), cache.maxLevel));
				}
			}
		}

		/// <summary>CPU-side height sampler for PQS collision meshing, or null if no height layer.</summary>
		// Token: 0x060002B3 RID: 691 RVA: 0x00014F4C File Offset: 0x0001314C
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
				result = new HeightTileLayer(new CpuHeightArchive(this.ResolvedArchivePath, this.GetWebArchive(VTLayer.Height)), this.tileSize, this.borderPx, this.StreamingMaxLevel);
			}
			return result;
		}

		/// <summary>Effective tessellation ceiling — configured value wins, else derived from pyramid depth.</summary>
		// Token: 0x060002B4 RID: 692 RVA: 0x00014F9C File Offset: 0x0001319C
		public float ResolveMaxTessellation(int pinnedMaxLevel)
		{
			bool flag = this.maxTessellation > 0;
			float result;
			if (flag)
			{
				result = (float)Mathf.Min(this.maxTessellation, 64);
			}
			else
			{
				int streamedLevels = Mathf.Clamp(this.StreamingMaxLevel - pinnedMaxLevel, 0, 6);
				result = (float)Mathf.Clamp(1 << streamedLevels, 1, 64);
			}
			return result;
		}

		/// <summary>Effective tessellation distance cutoff in metres, or 0 for no cutoff.</summary>
		// Token: 0x060002B5 RID: 693 RVA: 0x00014FEB File Offset: 0x000131EB
		public float ResolveMaxTessellationRange()
		{
			return Mathf.Max(this.maxTessellationRange, 0f);
		}

		// Token: 0x04000264 RID: 612
		public string archivePath;

		// Token: 0x04000265 RID: 613
		public string webPath;

		// Token: 0x04000266 RID: 614
		public bool emissiveLayer;

		// Token: 0x04000267 RID: 615
		public int atlasSize = 8192;

		// Token: 0x04000268 RID: 616
		public int tileSize = 256;

		// Token: 0x04000269 RID: 617
		public int borderPx = 4;

		/// <summary>Sentinel for "resolve from the archive".</summary>
		// Token: 0x0400026A RID: 618
		public const int AutoMaxLevel = -1;

		/// <summary>Canonical depth assumed when the archive probe finds nothing.</summary>
		// Token: 0x0400026B RID: 619
		public const int DefaultMaxLevel = 3;

		// Token: 0x0400026C RID: 620
		public int webMaxLevel = -1;

		// Token: 0x0400026D RID: 621
		public int canonicalMaxLevel = -1;

		// Token: 0x0400026E RID: 622
		public string imageryProvider = "s2cloudless2024";

		// Token: 0x0400026F RID: 623
		public bool bathymetry;

		// Token: 0x04000270 RID: 624
		public bool heightDespike = true;

		// Token: 0x04000271 RID: 625
		public bool colorGrade = true;

		// Token: 0x04000272 RID: 626
		public float colorExposure = 0f;

		// Token: 0x04000273 RID: 627
		public float colorBrightness = 0f;

		// Token: 0x04000274 RID: 628
		public float colorContrast = 1f;

		// Token: 0x04000275 RID: 629
		public float colorSaturation = 1f;

		// Token: 0x04000276 RID: 630
		public float colorGamma = 1f;

		// Token: 0x04000277 RID: 631
		public float colorTemperature = 0f;

		// Token: 0x04000278 RID: 632
		public float colorTint = 0f;

		// Token: 0x04000279 RID: 633
		public bool waterMask = true;

		// Token: 0x0400027A RID: 634
		public float tessellationEdgeLength = 16f;

		// Token: 0x0400027B RID: 635
		public int maxTessellation = 0;

		// Token: 0x0400027C RID: 636
		public float maxTessellationRange = 0f;

		// Token: 0x0400027D RID: 637
		public const int TessellationHardCap = 64;

		// Token: 0x0400027E RID: 638
		private bool _archiveProbed;

		// Token: 0x0400027F RID: 639
		private readonly int[] _archiveLayerK = VirtualTextureConfig.CreateLayerLevels();

		// Token: 0x04000280 RID: 640
		private readonly WebTileArchive[] _webByLayer = new WebTileArchive[4];
	}
}
