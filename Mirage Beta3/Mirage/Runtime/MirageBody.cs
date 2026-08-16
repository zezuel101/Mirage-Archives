using System;
using System.Collections.Generic;
using Mirage.VirtualTexture;
using UnityEngine;

namespace Mirage.Runtime
{
	/// <summary>Concrete <see cref="T:Mirage.VirtualTexture.IMirageBody" /> backed by a CelestialBody.</summary>
	// Token: 0x0200006C RID: 108
	public sealed class MirageBody : IMirageBody, IDisposable
	{
		// Token: 0x06000332 RID: 818 RVA: 0x00018B84 File Offset: 0x00016D84
		public MirageBody(CelestialBody body, VirtualTextureConfig cfg)
		{
			bool flag = body == null;
			if (flag)
			{
				throw new ArgumentNullException("body");
			}
			bool flag2 = cfg == null;
			if (flag2)
			{
				throw new ArgumentNullException("cfg");
			}
			bool flag3 = !cfg.IsValid;
			if (flag3)
			{
				throw new ArgumentException("VirtualTextureConfig has no colormap/heightmap/normalmap path set", "cfg");
			}
			this.CelestialBody = body;
			this.SphereName = body.name;
			this.Pqs = body.pqsController;
			this.Config = cfg;
			this.leaves = new PqsLeafCache(this.Pqs);
			this.Cache = new TileCache(cfg.atlasSize, cfg.tileSize, cfg.borderPx, cfg.webMaxLevel, cfg.canonicalMaxLevel);
			this.AddLayer(VTLayer.Color, "_Color", false);
			this.AddLayer(VTLayer.Height, "_Height", false);
			this.AddLayer(VTLayer.Normal, "_Normal", true);
			cfg.AddEmissiveLayerTo(this.Cache);
			this.Cache.BootstrapPinnedLevels(0);
		}

		// Token: 0x06000333 RID: 819 RVA: 0x00018C84 File Offset: 0x00016E84
		private void AddLayer(VTLayer layer, string suffix, bool linear)
		{
			bool flag = this.Config.HasLayer(layer);
			if (flag)
			{
				this.Cache.AddLayer(layer, suffix, this.Config.CreateSource(layer, linear), int.MaxValue);
			}
		}

		// Token: 0x1700007F RID: 127
		// (get) Token: 0x06000334 RID: 820 RVA: 0x00018CC2 File Offset: 0x00016EC2
		public CelestialBody CelestialBody { get; }

		// Token: 0x17000080 RID: 128
		// (get) Token: 0x06000335 RID: 821 RVA: 0x00018CCA File Offset: 0x00016ECA
		public string SphereName { get; }

		// Token: 0x17000081 RID: 129
		// (get) Token: 0x06000336 RID: 822 RVA: 0x00018CD2 File Offset: 0x00016ED2
		public PQS Pqs { get; }

		// Token: 0x17000082 RID: 130
		// (get) Token: 0x06000337 RID: 823 RVA: 0x00018CDA File Offset: 0x00016EDA
		public VirtualTextureConfig Config { get; }

		// Token: 0x17000083 RID: 131
		// (get) Token: 0x06000338 RID: 824 RVA: 0x00018CE2 File Offset: 0x00016EE2
		// (set) Token: 0x06000339 RID: 825 RVA: 0x00018CEA File Offset: 0x00016EEA
		public TileCache Cache { get; private set; }

		// Token: 0x17000084 RID: 132
		// (get) Token: 0x0600033A RID: 826 RVA: 0x00018CF3 File Offset: 0x00016EF3
		public int LeafSetVersion
		{
			get
			{
				return this.leaves.Version;
			}
		}

		// Token: 0x17000085 RID: 133
		// (get) Token: 0x0600033B RID: 827 RVA: 0x00018D00 File Offset: 0x00016F00
		public int StreamingMaxLevel
		{
			get
			{
				return this.Config.StreamingMaxLevel;
			}
		}

		// Token: 0x0600033C RID: 828 RVA: 0x00018D0D File Offset: 0x00016F0D
		public void EnumerateVisibleLeafQuads(List<LeafQuad> output)
		{
			this.leaves.EnumerateInto(output);
		}

		/// <summary>Build the camera/planet frame for VT level selection. False if unavailable.</summary>
		// Token: 0x0600033D RID: 829 RVA: 0x00018D1C File Offset: 0x00016F1C
		public bool TryGetLevelContext(out VTLevelContext ctx)
		{
			ctx = default(VTLevelContext);
			Camera cam = Camera.main;
			bool flag = cam == null || this.Pqs == null;
			bool result;
			if (flag)
			{
				result = false;
			}
			else
			{
				float tanHalf = Mathf.Tan(cam.fieldOfView * 0.017453292f * 0.5f);
				bool flag2 = tanHalf <= 1E-06f || cam.pixelHeight <= 0;
				if (flag2)
				{
					result = false;
				}
				else
				{
					GeometryUtility.CalculateFrustumPlanes(cam, MirageBody.s_FrustumPlanes);
					ref Plane ptr = ref MirageBody.s_FrustumPlanes[0];
					Plane[] array = MirageBody.s_FrustumPlanes;
					int num = 4;
					Plane plane = MirageBody.s_FrustumPlanes[4];
					Plane plane2 = MirageBody.s_FrustumPlanes[0];
					ptr = plane;
					array[num] = plane2;
					ctx = new VTLevelContext(cam.transform.position, this.Pqs.transform.position, this.Pqs.transform.rotation, (float)this.Pqs.radius, (float)cam.pixelHeight / (2f * tanHalf), MirageBody.s_FrustumPlanes);
					result = true;
				}
			}
			return result;
		}

		// Token: 0x0600033E RID: 830 RVA: 0x00018E3F File Offset: 0x0001703F
		public void Dispose()
		{
			this.leaves.Dispose();
			TileCache cache = this.Cache;
			if (cache != null)
			{
				cache.Dispose();
			}
			this.Cache = null;
		}

		// Token: 0x04000311 RID: 785
		private static readonly Plane[] s_FrustumPlanes = new Plane[6];

		// Token: 0x04000312 RID: 786
		private readonly PqsLeafCache leaves;
	}
}
