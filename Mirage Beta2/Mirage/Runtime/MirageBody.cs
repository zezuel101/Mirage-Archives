using System;
using System.Collections.Generic;
using Mirage.VirtualTexture;
using UnityEngine;

namespace Mirage.Runtime
{
	/// <summary>
	/// Concrete <see cref="T:Mirage.VirtualTexture.IMirageBody" /> backed by a KSP <see cref="P:Mirage.Runtime.MirageBody.CelestialBody" />.
	/// Owns the one unified tile cache (color / height / normal as lockstep layers,
	/// whichever the config enables) and walks the PQS quad tree to feed Mirage's
	/// streaming manager with the body's currently-visible leaf quads.
	///
	/// One instance lives per "currently active" body and is recreated when the
	/// main body changes — see <see cref="T:Mirage.Runtime.MirageRuntime" />.
	/// </summary>
	// Token: 0x02000060 RID: 96
	public sealed class MirageBody : IMirageBody, IDisposable
	{
		// Token: 0x17000078 RID: 120
		// (get) Token: 0x060002C2 RID: 706 RVA: 0x00017450 File Offset: 0x00015650
		public CelestialBody CelestialBody { get; }

		// Token: 0x17000079 RID: 121
		// (get) Token: 0x060002C3 RID: 707 RVA: 0x00017458 File Offset: 0x00015658
		public string SphereName { get; }

		// Token: 0x1700007A RID: 122
		// (get) Token: 0x060002C4 RID: 708 RVA: 0x00017460 File Offset: 0x00015660
		public PQS Pqs { get; }

		// Token: 0x1700007B RID: 123
		// (get) Token: 0x060002C5 RID: 709 RVA: 0x00017468 File Offset: 0x00015668
		public VirtualTextureConfig Config { get; }

		// Token: 0x1700007C RID: 124
		// (get) Token: 0x060002C6 RID: 710 RVA: 0x00017470 File Offset: 0x00015670
		// (set) Token: 0x060002C7 RID: 711 RVA: 0x00017478 File Offset: 0x00015678
		public TileCache Cache { get; private set; }

		/// <summary>The surface follows the config's own cap — the full web tier when web streaming/ingest is
		/// on. (Scaled caps itself separately; see <see cref="P:Mirage.VirtualTexture.IMirageBody.StreamingMaxLevel" />.)</summary>
		// Token: 0x1700007D RID: 125
		// (get) Token: 0x060002C8 RID: 712 RVA: 0x00017481 File Offset: 0x00015681
		public int StreamingMaxLevel
		{
			get
			{
				return this.Config.StreamingMaxLevel;
			}
		}

		// Token: 0x060002C9 RID: 713 RVA: 0x00017490 File Offset: 0x00015690
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
			this.Cache = new TileCache(cfg.atlasSize, cfg.tileSize, cfg.borderPx, cfg.webMaxLevel, MirageVTPageFormat.UseRgba32, cfg.canonicalMaxLevel);
			bool flag4 = cfg.HasLayer(VTLayer.Color);
			if (flag4)
			{
				this.Cache.AddLayer(VTLayer.Color, "_Color", cfg.CreateSource(VTLayer.Color, false));
			}
			bool flag5 = cfg.HasLayer(VTLayer.Height);
			if (flag5)
			{
				this.Cache.AddLayer(VTLayer.Height, "_Height", cfg.CreateSource(VTLayer.Height, false));
			}
			bool flag6 = cfg.HasLayer(VTLayer.Normal);
			if (flag6)
			{
				this.Cache.AddLayer(VTLayer.Normal, "_Normal", cfg.CreateSource(VTLayer.Normal, true));
			}
			this.Cache.BootstrapCoarseLevels(2);
		}

		/// <summary>
		/// Recursively walks the body's PQS quad tree (six root quads, one per cube
		/// face) and appends every currently-visible leaf to <paramref name="output" />.
		/// Mirage clears the list before calling this each frame.
		/// </summary>
		// Token: 0x060002CA RID: 714 RVA: 0x000175C0 File Offset: 0x000157C0
		public void EnumerateVisibleLeafQuads(List<LeafQuad> output)
		{
			PQS pqs = this.Pqs;
			PQ[] roots = (pqs != null) ? pqs.quads : null;
			bool flag = roots == null;
			if (!flag)
			{
				foreach (PQ root in roots)
				{
					bool flag2 = root != null;
					if (flag2)
					{
						MirageBody.CollectVisibleLeaves(root, output);
					}
				}
			}
		}

		/// <summary>
		/// Project against the active scene camera. <c>Camera.main</c> is the near flight camera, which is the
		/// one the PQS terrain is actually rendered by — so the pixels it measures are the pixels the user sees.
		/// </summary>
		// Token: 0x060002CB RID: 715 RVA: 0x0001761C File Offset: 0x0001581C
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
					ctx = new VTLevelContext(cam.transform.position, this.Pqs.transform.position, this.Pqs.transform.rotation, (float)this.Pqs.radius, (float)cam.pixelHeight / (2f * tanHalf), MirageBody.s_FrustumPlanes);
					result = true;
				}
			}
			return result;
		}

		// Token: 0x060002CC RID: 716 RVA: 0x000176FC File Offset: 0x000158FC
		private static void CollectVisibleLeaves(PQ quad, List<LeafQuad> output)
		{
			bool isSubdivided = quad.isSubdivided;
			if (isSubdivided)
			{
				PQ[] subs = quad.subNodes;
				bool flag = subs != null;
				if (flag)
				{
					foreach (PQ sub in subs)
					{
						bool flag2 = sub != null;
						if (flag2)
						{
							MirageBody.CollectVisibleLeaves(sub, output);
						}
					}
				}
			}
			else
			{
				bool flag3 = !quad.isVisible;
				if (!flag3)
				{
					output.Add(new LeafQuad(quad.plane, (double)quad.uvSW.x, (double)quad.uvSW.y, quad.subdivision));
				}
			}
		}

		// Token: 0x060002CD RID: 717 RVA: 0x0001779B File Offset: 0x0001599B
		public void Dispose()
		{
			TileCache cache = this.Cache;
			if (cache != null)
			{
				cache.Dispose();
			}
			this.Cache = null;
		}

		// Token: 0x040002A3 RID: 675
		private static readonly Plane[] s_FrustumPlanes = new Plane[6];
	}
}
