using System;
using Mirage.Configuration;
using Mirage.VirtualTexture;
using UnityEngine;

namespace Mirage.KopernicusMods
{
	/// <summary>CPU height displacement from Mirage VT tiles, replacing stock VertexHeightMapBicubic.</summary>
	// Token: 0x02000078 RID: 120
	[AddComponentMenu("PQuadSphere/Mods/Misc/Mirage Terrain")]
	public class PQSMod_MirageTerrain : PQSMod
	{
		// Token: 0x1700008A RID: 138
		// (get) Token: 0x0600037A RID: 890 RVA: 0x0001A380 File Offset: 0x00018580
		// (set) Token: 0x0600037B RID: 891 RVA: 0x0001A388 File Offset: 0x00018588
		public HeightTileLayer HeightLayer { get; private set; }

		// Token: 0x0600037C RID: 892 RVA: 0x0001A391 File Offset: 0x00018591
		public override void OnSetup()
		{
			this.requirements = 66;
			this.EnsureResolved();
		}

		/// <summary>Resolve config into a height layer, retrying until the registry is populated.</summary>
		// Token: 0x0600037D RID: 893 RVA: 0x0001A3A4 File Offset: 0x000185A4
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
				if (this.bodyName == null)
				{
					this.bodyName = this.ResolveBodyName();
				}
				VirtualTextureConfig cfg;
				bool flag2 = this.bodyName == null || !MirageBodyRegistry.TryGetConfig(this.bodyName, out cfg);
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
						this.bodyName,
						"' height=",
						(this.HeightLayer != null) ? "yes" : "no",
						" ",
						string.Format("deformity={0} offset={1}", this.deformity, this.offset)
					}));
					result = true;
				}
			}
			return result;
		}

		// Token: 0x0600037E RID: 894 RVA: 0x0001A484 File Offset: 0x00018684
		public override void OnVertexBuildHeight(PQS.VertexBuildData data)
		{
			bool flag = !this.EnsureResolved() || this.HeightLayer == null;
			if (!flag)
			{
				PQSMod_MirageTerrain.TileAddress address = (data.buildQuad != null) ? this.AddressInQuad(data.buildQuad, data.vertIndex) : this.AddressByDirection(data.directionFromCenter);
				double height = this.HeightLayer.Sample(address.Face, address.U, address.V, address.Level);
				bool flag2 = !double.IsNaN(height);
				if (flag2)
				{
					data.vertHeight += this.offset + this.deformity * height;
				}
			}
		}

		// Token: 0x0600037F RID: 895 RVA: 0x0001A528 File Offset: 0x00018728
		private PQSMod_MirageTerrain.TileAddress AddressInQuad(PQ quad, int vertIndex)
		{
			int sideVerts = MirageTileMath.GridSide(PQS.cacheVertCount);
			float step = 1f / (float)(sideVerts - 1);
			float localU = (float)(vertIndex % sideVerts) * step;
			float localV = (float)(vertIndex / sideVerts) * step;
			return new PQSMod_MirageTerrain.TileAddress(quad.plane, (double)(quad.uvSW.x + localU * quad.uvDelta.x), (double)(quad.uvSW.y + localV * quad.uvDelta.y), Mathf.Min(quad.subdivision, this.HeightLayer.MaxLevel));
		}

		// Token: 0x06000380 RID: 896 RVA: 0x0001A5B8 File Offset: 0x000187B8
		private PQSMod_MirageTerrain.TileAddress AddressByDirection(Vector3d directionFromCenter)
		{
			PQS.QuadPlane plane;
			double rawU;
			double rawV;
			PQSMod_GnomonicTest.GetGnomonicMapCoords(directionFromCenter, ref plane, ref rawU, ref rawV);
			int level = (TimeWarp.CurrentRate > 1f && TimeWarp.WarpMode == 0) ? 2 : this.HeightLayer.MaxLevel;
			return new PQSMod_MirageTerrain.TileAddress(plane, rawU, rawV, Mathf.Min(level, this.HeightLayer.MaxLevel));
		}

		// Token: 0x06000381 RID: 897 RVA: 0x0001A620 File Offset: 0x00018820
		private string ResolveBodyName()
		{
			bool flag = this.sphere == null;
			string result;
			if (flag)
			{
				result = null;
			}
			else
			{
				bool flag2 = FlightGlobals.Bodies != null;
				if (flag2)
				{
					foreach (CelestialBody body in FlightGlobals.Bodies)
					{
						bool flag3 = body != null && body.pqsController == this.sphere;
						if (flag3)
						{
							return body.name;
						}
					}
				}
				result = this.sphere.name;
			}
			return result;
		}

		// Token: 0x04000340 RID: 832
		public double deformity = 1000.0;

		// Token: 0x04000341 RID: 833
		public double offset = 0.0;

		// Token: 0x04000342 RID: 834
		private const int OnRailsAltitudeLevel = 2;

		// Token: 0x04000344 RID: 836
		private string bodyName;

		// Token: 0x04000345 RID: 837
		private bool resolved;

		// Token: 0x020000E5 RID: 229
		private readonly struct TileAddress
		{
			// Token: 0x060004F2 RID: 1266 RVA: 0x00022D67 File Offset: 0x00020F67
			public TileAddress(int face, double u, double v, int level)
			{
				this.Face = face;
				this.U = u;
				this.V = v;
				this.Level = level;
			}

			// Token: 0x040005D7 RID: 1495
			public readonly int Face;

			// Token: 0x040005D8 RID: 1496
			public readonly double U;

			// Token: 0x040005D9 RID: 1497
			public readonly double V;

			// Token: 0x040005DA RID: 1498
			public readonly int Level;
		}
	}
}
