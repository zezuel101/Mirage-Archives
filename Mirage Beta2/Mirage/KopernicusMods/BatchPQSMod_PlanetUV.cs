using System;
using BurstPQS;
using Unity.Burst;
using Unity.Mathematics;

namespace Mirage.KopernicusMods
{
	/// <summary>
	/// BurstPQS batch mod for <see cref="T:Mirage.KopernicusMods.PQSMod_PlanetUV" />.
	/// Writes global cube-face UVs into UV3 (mesh.uv3) for GPU-side virtual-texture sampling.
	/// UV3.x encodes the face index in the integer part and the face U coordinate in the fractional part.
	/// UV3.y is the face V coordinate.
	///
	/// Shader unpacking:
	///   float faceIndex = floor(texcoord2.x);
	///   float2 faceUV   = float2(frac(texcoord2.x), texcoord2.y);
	///
	/// </summary>
	// Token: 0x02000065 RID: 101
	[BurstCompile]
	[BatchPQSMod(typeof(PQSMod_PlanetUV))]
	public class BatchPQSMod_PlanetUV : BatchPQSMod<PQSMod_PlanetUV>
	{
		/// <summary>
		/// BurstPQS batch mod for <see cref="T:Mirage.KopernicusMods.PQSMod_PlanetUV" />.
		/// Writes global cube-face UVs into UV3 (mesh.uv3) for GPU-side virtual-texture sampling.
		/// UV3.x encodes the face index in the integer part and the face U coordinate in the fractional part.
		/// UV3.y is the face V coordinate.
		///
		/// Shader unpacking:
		///   float faceIndex = floor(texcoord2.x);
		///   float2 faceUV   = float2(frac(texcoord2.x), texcoord2.y);
		///
		/// </summary>
		// Token: 0x060002EA RID: 746 RVA: 0x000187A8 File Offset: 0x000169A8
		public BatchPQSMod_PlanetUV(PQSMod_PlanetUV mod) : base(mod)
		{
		}

		// Token: 0x060002EB RID: 747 RVA: 0x000187B4 File Offset: 0x000169B4
		public override void OnQuadPreBuild(PQ quad, BatchPQSJobSet jobSet)
		{
			base.OnQuadPreBuild(quad, jobSet);
			BatchPQSMod_PlanetUV.BuildJob buildJob = default(BatchPQSMod_PlanetUV.BuildJob);
			buildJob.uvSW = new float2(quad.uvSW.x, quad.uvSW.y);
			buildJob.uvDelta = new float2(quad.uvDelta.x, quad.uvDelta.y);
			buildJob.face = quad.plane;
			jobSet.Add<BatchPQSMod_PlanetUV.BuildJob>(ref buildJob);
		}

		// Token: 0x020000C9 RID: 201
		[BurstCompile]
		private struct BuildJob : IBatchPQSMeshJob
		{
			// Token: 0x06000504 RID: 1284 RVA: 0x00021EC0 File Offset: 0x000200C0
			public readonly void BuildMesh(in BuildMeshData data)
			{
				int sideVerts = (int)math.sqrt((float)data.VertexCount);
				float step = 1f / (float)(sideVerts - 1);
				for (int i = 0; i < data.VertexCount; i++)
				{
					float localU = (float)(i % sideVerts) * step;
					float localV = (float)(i / sideVerts) * step;
					data.uv3s[i].x = (float)this.face + (this.uvSW.x + localU * this.uvDelta.x);
					data.uv3s[i].y = this.uvSW.y + localV * this.uvDelta.y;
				}
			}

			// Token: 0x06000505 RID: 1285 RVA: 0x00021F77 File Offset: 0x00020177
			void IBatchPQSMeshJob.BuildMesh(in BuildMeshData data)
			{
				this.BuildMesh(data);
			}

			// Token: 0x04000548 RID: 1352
			public float2 uvSW;

			// Token: 0x04000549 RID: 1353
			public float2 uvDelta;

			// Token: 0x0400054A RID: 1354
			public int face;
		}
	}
}
