using System;
using BurstPQS;
using Mirage.VirtualTexture;
using Unity.Burst;
using Unity.Mathematics;

namespace Mirage.KopernicusMods
{
	/// <summary>BurstPQS fast path for <see cref="T:Mirage.KopernicusMods.PQSMod_PlanetUV" />.</summary>
	// Token: 0x02000075 RID: 117
	[BurstCompile]
	[BatchPQSMod(typeof(PQSMod_PlanetUV))]
	public class BatchPQSMod_PlanetUV : BatchPQSMod<PQSMod_PlanetUV>
	{
		/// <summary>BurstPQS fast path for <see cref="T:Mirage.KopernicusMods.PQSMod_PlanetUV" />.</summary>
		// Token: 0x0600036C RID: 876 RVA: 0x0001A1FF File Offset: 0x000183FF
		public BatchPQSMod_PlanetUV(PQSMod_PlanetUV mod) : base(mod)
		{
		}

		// Token: 0x0600036D RID: 877 RVA: 0x0001A20C File Offset: 0x0001840C
		public override void OnQuadPreBuild(PQ quad, BatchPQSJobSet jobSet)
		{
			base.OnQuadPreBuild(quad, jobSet);
			BatchPQSMod_PlanetUV.BuildJob buildJob = default(BatchPQSMod_PlanetUV.BuildJob);
			buildJob.uvSW = new float2(quad.uvSW.x, quad.uvSW.y);
			buildJob.uvDelta = new float2(quad.uvDelta.x, quad.uvDelta.y);
			buildJob.face = quad.plane;
			jobSet.Add<BatchPQSMod_PlanetUV.BuildJob>(ref buildJob);
		}

		// Token: 0x0600036E RID: 878 RVA: 0x0001A285 File Offset: 0x00018485
		public override void OnQuadBuilt(PQ quad)
		{
		}

		// Token: 0x020000E4 RID: 228
		[BurstCompile]
		private struct BuildJob : IBatchPQSMeshJob
		{
			// Token: 0x060004F0 RID: 1264 RVA: 0x00022C9C File Offset: 0x00020E9C
			public readonly void BuildMesh(in BuildMeshData data)
			{
				int sideVerts = MirageTileMath.GridSide(data.VertexCount);
				float step = 1f / (float)(sideVerts - 1);
				for (int i = 0; i < data.VertexCount; i++)
				{
					float localU = (float)(i % sideVerts) * step;
					float localV = (float)(i / sideVerts) * step;
					data.uv3s[i].x = (float)this.face + math.min(this.uvSW.x + localU * this.uvDelta.x, 0.999999f);
					data.uv3s[i].y = this.uvSW.y + localV * this.uvDelta.y;
				}
			}

			// Token: 0x060004F1 RID: 1265 RVA: 0x00022D5E File Offset: 0x00020F5E
			void IBatchPQSMeshJob.BuildMesh(in BuildMeshData data)
			{
				this.BuildMesh(data);
			}

			// Token: 0x040005D4 RID: 1492
			public float2 uvSW;

			// Token: 0x040005D5 RID: 1493
			public float2 uvDelta;

			// Token: 0x040005D6 RID: 1494
			public int face;
		}
	}
}
