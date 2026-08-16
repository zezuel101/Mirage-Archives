using System;
using Mirage.Configuration;
using Mirage.VirtualTexture;
using UnityEngine;

namespace Mirage.KopernicusMods
{
	/// <summary>
	/// Builds PQS vertex height + colour by sampling the Mirage virtual-texture tile pyramid on the
	/// CPU, replacing the global-16k-texture <c>VertexColorMap</c> / <c>VertexHeightMapBicubic</c>
	/// mods. Tile paths and pyramid dimensions are taken from the body's existing
	/// <c>MirageTerrain { Body { VirtualTexture { … } } }</c> config (via <see cref="T:Mirage.Configuration.MirageBodyRegistry" />),
	/// so there is a single source of truth for the tiles.
	///
	/// Each vertex is addressed exactly like <see cref="T:Mirage.KopernicusMods.PQSMod_PlanetUV" /> writes UV3 — face from
	/// <c>quad.plane</c>, raw face UV from <c>quad.uvSW</c>/<c>uvDelta</c> + <c>vertIndex</c> — so the
	/// CPU mesh aligns with the GPU VT. The tile level mirrors the streamer:
	/// <c>min(quad.subdivision, maxLevel)</c>.
	///
	/// This is the stock fallback path. When BurstPQS is installed,
	/// <see cref="T:Mirage.KopernicusMods.BatchPQSMod_MirageTerrain" /> takes over with the same sampling on the job threads.
	/// </summary>
	// Token: 0x02000068 RID: 104
	[AddComponentMenu("PQuadSphere/Mods/Misc/Mirage Terrain")]
	public class PQSMod_MirageTerrain : PQSMod
	{
		// Token: 0x17000081 RID: 129
		// (get) Token: 0x060002F8 RID: 760 RVA: 0x00018929 File Offset: 0x00016B29
		// (set) Token: 0x060002F9 RID: 761 RVA: 0x00018931 File Offset: 0x00016B31
		public HeightTileLayer HeightLayer { get; private set; }

		// Token: 0x060002FA RID: 762 RVA: 0x0001893A File Offset: 0x00016B3A
		public override void OnSetup()
		{
			this.requirements = 66;
			this.EnsureResolved();
		}

		/// <summary>
		/// Resolve the body's VT config into sampling layers, retrying until the registry is populated.
		/// Cheap once resolved (early-out). Called from the build path so a PQS that was set up before
		/// config load still picks the tiles up on the next quad build.
		/// </summary>
		// Token: 0x060002FB RID: 763 RVA: 0x0001894C File Offset: 0x00016B4C
		public bool EnsureResolved()
		{
			bool flag = this.resolved;
			bool result;
			if (flag)
			{
				result = true;
			}
			else
			{
				string bodyName = this.ResolveBodyName();
				VirtualTextureConfig cfg;
				bool flag2 = bodyName == null || !MirageBodyRegistry.TryGetConfig(bodyName, out cfg);
				if (flag2)
				{
					result = false;
				}
				else
				{
					this.HeightLayer = cfg.CreateCpuHeightLayer();
					this.resolved = true;
					MirageDebug.Log(string.Concat(new string[]
					{
						"PQSMod_MirageTerrain: resolved '",
						bodyName,
						"' height=",
						(this.HeightLayer != null) ? "y" : "n",
						" ",
						string.Format("deformity={0} offset={1}", this.deformity, this.offset)
					}));
					result = true;
				}
			}
			return result;
		}

		// Token: 0x060002FC RID: 764 RVA: 0x00018A10 File Offset: 0x00016C10
		public override void OnVertexBuildHeight(PQS.VertexBuildData data)
		{
			bool flag = !this.EnsureResolved();
			if (!flag)
			{
				bool flag2 = this.HeightLayer == null;
				if (!flag2)
				{
					PQ quad = data.buildQuad;
					bool flag3 = quad != null;
					int face;
					double rawU;
					double rawV;
					int subdivision;
					if (flag3)
					{
						face = quad.plane;
						int sideVerts = (int)Mathf.Sqrt((float)PQS.cacheVertCount);
						float step = 1f / (float)(sideVerts - 1);
						int i = data.vertIndex;
						float localU = (float)(i % sideVerts) * step;
						float localV = (float)(i / sideVerts) * step;
						rawU = (double)(quad.uvSW.x + localU * quad.uvDelta.x);
						rawV = (double)(quad.uvSW.y + localV * quad.uvDelta.y);
						subdivision = quad.subdivision;
					}
					else
					{
						PQS.QuadPlane plane;
						double gu;
						double gv;
						PQSMod_GnomonicTest.GetGnomonicMapCoords(data.directionFromCenter, ref plane, ref gu, ref gv);
						face = plane;
						rawU = gu;
						rawV = gv;
						subdivision = ((TimeWarp.CurrentRate > 1f && TimeWarp.WarpMode == 0) ? 2 : int.MaxValue);
					}
					int level = Mathf.Min(subdivision, this.HeightLayer.MaxLevel);
					double h = this.HeightLayer.Sample(face, rawU, rawV, level);
					bool flag4 = !double.IsNaN(h);
					if (flag4)
					{
						data.vertHeight += this.offset + this.deformity * h;
					}
				}
			}
		}

		// Token: 0x060002FD RID: 765 RVA: 0x00018B80 File Offset: 0x00016D80
		private string ResolveBodyName()
		{
			bool flag = this.sphere != null && FlightGlobals.Bodies != null;
			if (flag)
			{
				foreach (CelestialBody body in FlightGlobals.Bodies)
				{
					bool flag2 = body != null && body.pqsController == this.sphere;
					if (flag2)
					{
						return body.name;
					}
				}
			}
			return (this.sphere != null) ? this.sphere.name : null;
		}

		// Token: 0x040002BD RID: 701
		public double deformity = 1000.0;

		// Token: 0x040002BE RID: 702
		public double offset = 0.0;

		// Token: 0x040002C0 RID: 704
		private bool resolved;
	}
}
