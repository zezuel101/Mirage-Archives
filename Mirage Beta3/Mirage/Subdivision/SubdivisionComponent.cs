using System;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Mirage.Subdivision
{
	// Token: 0x0200005F RID: 95
	public class SubdivisionComponent : MonoBehaviour
	{
		// Token: 0x060002C9 RID: 713 RVA: 0x00015C74 File Offset: 0x00013E74
		private void Start()
		{
			this.mesh = base.GetComponent<MeshFilter>().sharedMesh;
			this.mesh.MarkDynamic();
			this.AcquireId();
			this.InitArrays();
		}

		// Token: 0x060002CA RID: 714 RVA: 0x00015CA2 File Offset: 0x00013EA2
		private void AcquireId()
		{
			this.uniqueId = InterlockedCounters.Request();
			this.hasId = true;
		}

		// Token: 0x060002CB RID: 715 RVA: 0x00015CB8 File Offset: 0x00013EB8
		private void InitArrays()
		{
			this.vertices = new NativeArray<Vector3>(this.mesh.vertices, 4).Reinterpret<float3>();
			this.normals = new NativeArray<Vector3>(this.mesh.normals, 4).Reinterpret<float3>();
			Color[] cols = this.mesh.colors;
			this.colors = new NativeArray<float4>(this.vertices.Length, 4, 1);
			for (int i = 0; i < cols.Length; i++)
			{
				this.colors[i] = new float4(cols[i].r, cols[i].g, cols[i].b, cols[i].a);
			}
			Vector2[] uv3arr = this.mesh.uv3;
			this.uv3s = ((uv3arr != null && uv3arr.Length == this.vertices.Length) ? new NativeArray<Vector2>(uv3arr, 4).Reinterpret<float2>() : new NativeArray<float2>(this.vertices.Length, 4, 1));
			this.triangles = new NativeArray<int>(this.mesh.triangles, 4);
			this.storedVerts = new NativeHashMap<float3, int>(3500, 4);
			this.frustumPlanes = new NativeArray<MiragePlane>(6, 4, 1);
			this.BuildTriangleStructs();
		}

		// Token: 0x060002CC RID: 716 RVA: 0x00015E04 File Offset: 0x00014004
		private void BuildTriangleStructs()
		{
			int triCount = this.triangles.Length / 3;
			this.meshTriangles = new NativeArray<SubdividableTriangle>(triCount, 4, 1);
			this.streamForeachCount = triCount;
			for (int i = 0; i < this.triangles.Length; i += 3)
			{
				int a = this.triangles[i];
				int b = this.triangles[i + 1];
				int c = this.triangles[i + 2];
				this.meshTriangles[i / 3] = new SubdividableTriangle(this.vertices[a], this.vertices[b], this.vertices[c], this.normals[a], this.normals[b], this.normals[c], this.colors[a], this.colors[b], this.colors[c], this.uv3s[a], this.uv3s[b], this.uv3s[c]);
			}
		}

		// Token: 0x060002CD RID: 717 RVA: 0x00015F30 File Offset: 0x00014130
		private void Update()
		{
			bool flag = this.firstRun || (!this.processingSubdivision && !this.generatingMesh);
			if (flag)
			{
				this.DispatchSubdivide();
				this.DispatchRemovePairs();
				this.processingSubdivision = true;
				this.firstRun = false;
			}
			bool flag2 = this.processingSubdivision && this.removePairsHandle.IsCompleted && this.subdivideHandle.IsCompleted;
			if (flag2)
			{
				this.removePairsHandle.Complete();
				this.processingSubdivision = false;
				this.DispatchMeshConstruction();
				this.DispatchTriangleReadback(this.tris.AsReader().ComputeItemCount() * 3);
				this.generatingMesh = true;
			}
			bool flag3 = this.generatingMesh && this.triangleHandle.IsCompleted && this.constructHandle.IsCompleted;
			if (flag3)
			{
				this.triangleHandle.Complete();
				this.generatingMesh = false;
				this.UploadMesh();
				this.FreePostUploadBuffers();
				this.FreePostReadbackBuffers();
			}
		}

		// Token: 0x060002CE RID: 718 RVA: 0x00016038 File Offset: 0x00014238
		private void DispatchSubdivide()
		{
			this.frustumPlanes.CopyFrom(SubdivisionRuntime.FrustumPlanes);
			this.tris = new NativeStream(this.streamForeachCount, 4);
			this.subdivideJob = new SubdivideMeshJob
			{
				meshTriangles = this.meshTriangles,
				originalVerts = this.vertices,
				originalNormals = this.normals,
				originalColors = this.colors,
				tris = this.tris.AsWriter(),
				target = SubdivisionRuntime.CameraPosition,
				sqrSubdivisionRange = this.subdivisionRange,
				maxSubdivisionLevel = this.maxSubdivisionLevel,
				cameraFrustumPlanes = this.frustumPlanes,
				objectToWorldMatrix = base.transform.localToWorldMatrix
			};
			this.subdivideHandle = IJobParallelForExtensions.Schedule<SubdivideMeshJob>(this.subdivideJob, this.meshTriangles.Length, 4, default(JobHandle));
		}

		// Token: 0x060002CF RID: 719 RVA: 0x00016130 File Offset: 0x00014330
		private void DispatchRemovePairs()
		{
			this.storedVerts.Clear();
			this.removePairsJob = new RemoveVertexPairsJob
			{
				triReader = this.tris.AsReader(),
				vertices = this.storedVerts,
				foreachCount = this.streamForeachCount,
				count = 0
			};
			this.removePairsHandle = IJobExtensions.Schedule<RemoveVertexPairsJob>(this.removePairsJob, this.subdivideHandle);
		}

		// Token: 0x060002D0 RID: 720 RVA: 0x000161A4 File Offset: 0x000143A4
		private void DispatchMeshConstruction()
		{
			this.newVerts = new NativeArray<float3>(this.storedVerts.Count(), 4, 1);
			this.newNormals = new NativeArray<float3>(this.storedVerts.Count(), 4, 1);
			this.newColors = new NativeArray<float4>(this.storedVerts.Count(), 4, 1);
			this.newUV3s = new NativeArray<float2>(this.storedVerts.Count(), 4, 1);
			this.newTriangles = new NativeStream(this.meshTriangles.Length, 4);
			this.meshJob = new ConstructMeshJob
			{
				triArray = this.tris.AsReader(),
				newVerts = this.newVerts,
				newNormals = this.newNormals,
				newColors = this.newColors,
				newUV3s = this.newUV3s,
				newTris = this.newTriangles.AsWriter(),
				storedVertTris = this.storedVerts,
				count = this.streamForeachCount,
				interlockedCount = -1
			};
			this.constructHandle = IJobParallelForExtensions.Schedule<ConstructMeshJob>(this.meshJob, this.streamForeachCount, 4, default(JobHandle));
		}

		// Token: 0x060002D1 RID: 721 RVA: 0x000162D4 File Offset: 0x000144D4
		private void DispatchTriangleReadback(int count)
		{
			InterlockedCounters.ResetCounter(this.uniqueId);
			this.outputTriIndices = new NativeArray<int>(count, 4, 1);
			this.triangleReadJob = new ReadMeshTriangleDataJob
			{
				newTris = this.newTriangles.AsReader(),
				outputTris = this.outputTriIndices,
				uniqueIndex = this.uniqueId
			};
			this.triangleHandle = IJobParallelForExtensions.Schedule<ReadMeshTriangleDataJob>(this.triangleReadJob, this.streamForeachCount, 4, this.constructHandle);
		}

		// Token: 0x060002D2 RID: 722 RVA: 0x00016358 File Offset: 0x00014558
		private void UploadMesh()
		{
			this.mesh.Clear();
			this.mesh.SetVertexBufferParams(this.newVerts.Length, new VertexAttributeDescriptor[]
			{
				new VertexAttributeDescriptor(0, 0, 3, 0)
			});
			this.mesh.SetVertexBufferData<float3>(this.newVerts, 0, 0, this.newVerts.Length, 0, 0);
			this.mesh.SetIndexBufferParams(this.outputTriIndices.Length, 1);
			this.mesh.SetIndexBufferData<int>(this.outputTriIndices, 0, 0, this.outputTriIndices.Length, 0);
			this.mesh.SetSubMesh(0, new SubMeshDescriptor(0, this.outputTriIndices.Length, 0), 0);
			this.mesh.SetNormals<float3>(this.newNormals);
			this.mesh.SetColors<float4>(this.newColors);
			this.mesh.SetUVs<float2>(2, this.newUV3s);
			this.mesh.RecalculateBounds();
			base.GetComponent<MeshFilter>().sharedMesh = this.mesh;
		}

		// Token: 0x060002D3 RID: 723 RVA: 0x00016470 File Offset: 0x00014670
		private void FreePostUploadBuffers()
		{
			bool isCreated = this.newVerts.IsCreated;
			if (isCreated)
			{
				this.newVerts.Dispose();
			}
			bool isCreated2 = this.newNormals.IsCreated;
			if (isCreated2)
			{
				this.newNormals.Dispose();
			}
			bool isCreated3 = this.newColors.IsCreated;
			if (isCreated3)
			{
				this.newColors.Dispose();
			}
			bool isCreated4 = this.newUV3s.IsCreated;
			if (isCreated4)
			{
				this.newUV3s.Dispose();
			}
			bool isCreated5 = this.outputTriIndices.IsCreated;
			if (isCreated5)
			{
				this.outputTriIndices.Dispose();
			}
			bool isCreated6 = this.tris.IsCreated;
			if (isCreated6)
			{
				this.tris.Dispose();
			}
		}

		// Token: 0x060002D4 RID: 724 RVA: 0x00016524 File Offset: 0x00014724
		private void FreePostReadbackBuffers()
		{
			bool isCreated = this.newTriangles.IsCreated;
			if (isCreated)
			{
				this.newTriangles.Dispose();
			}
		}

		// Token: 0x060002D5 RID: 725 RVA: 0x00016550 File Offset: 0x00014750
		private void ResetToOriginalMesh()
		{
			this.mesh.Clear();
			this.mesh.SetVertexBufferParams(this.vertices.Length, new VertexAttributeDescriptor[]
			{
				new VertexAttributeDescriptor(0, 0, 3, 0)
			});
			this.mesh.SetVertexBufferData<float3>(this.vertices, 0, 0, this.vertices.Length, 0, 0);
			this.mesh.SetIndexBufferParams(this.triangles.Length, 1);
			this.mesh.SetIndexBufferData<int>(this.triangles, 0, 0, this.triangles.Length, 0);
			this.mesh.SetSubMesh(0, new SubMeshDescriptor(0, this.triangles.Length, 0), 0);
			this.mesh.SetNormals<float3>(this.normals);
			this.mesh.SetColors<float4>(this.colors);
			this.mesh.SetUVs<float2>(2, this.uv3s);
			base.GetComponent<MeshFilter>().sharedMesh = this.mesh;
		}

		// Token: 0x060002D6 RID: 726 RVA: 0x0001665C File Offset: 0x0001485C
		public void Cleanup()
		{
			this.subdivideHandle.Complete();
			this.removePairsHandle.Complete();
			this.constructHandle.Complete();
			this.triangleHandle.Complete();
			this.ResetToOriginalMesh();
			bool isCreated = this.vertices.IsCreated;
			if (isCreated)
			{
				this.vertices.Dispose();
			}
			bool isCreated2 = this.normals.IsCreated;
			if (isCreated2)
			{
				this.normals.Dispose();
			}
			bool isCreated3 = this.colors.IsCreated;
			if (isCreated3)
			{
				this.colors.Dispose();
			}
			bool isCreated4 = this.uv3s.IsCreated;
			if (isCreated4)
			{
				this.uv3s.Dispose();
			}
			bool isCreated5 = this.triangles.IsCreated;
			if (isCreated5)
			{
				this.triangles.Dispose();
			}
			bool isCreated6 = this.meshTriangles.IsCreated;
			if (isCreated6)
			{
				this.meshTriangles.Dispose();
			}
			bool isCreated7 = this.storedVerts.IsCreated;
			if (isCreated7)
			{
				this.storedVerts.Dispose();
			}
			bool isCreated8 = this.frustumPlanes.IsCreated;
			if (isCreated8)
			{
				this.frustumPlanes.Dispose();
			}
			bool isCreated9 = this.newVerts.IsCreated;
			if (isCreated9)
			{
				this.newVerts.Dispose();
			}
			bool isCreated10 = this.newNormals.IsCreated;
			if (isCreated10)
			{
				this.newNormals.Dispose();
			}
			bool isCreated11 = this.newColors.IsCreated;
			if (isCreated11)
			{
				this.newColors.Dispose();
			}
			bool isCreated12 = this.newUV3s.IsCreated;
			if (isCreated12)
			{
				this.newUV3s.Dispose();
			}
			bool isCreated13 = this.outputTriIndices.IsCreated;
			if (isCreated13)
			{
				this.outputTriIndices.Dispose();
			}
			bool isCreated14 = this.tris.IsCreated;
			if (isCreated14)
			{
				this.tris.Dispose();
			}
			bool isCreated15 = this.newTriangles.IsCreated;
			if (isCreated15)
			{
				this.newTriangles.Dispose();
			}
			bool flag = this.hasId;
			if (flag)
			{
				InterlockedCounters.Return(this.uniqueId);
				this.hasId = false;
			}
		}

		// Token: 0x060002D7 RID: 727 RVA: 0x0001686D File Offset: 0x00014A6D
		private void OnDisable()
		{
			this.Cleanup();
		}

		// Token: 0x04000291 RID: 657
		private int uniqueId = -1;

		// Token: 0x04000292 RID: 658
		private bool hasId = false;

		// Token: 0x04000293 RID: 659
		private SubdivideMeshJob subdivideJob;

		// Token: 0x04000294 RID: 660
		private ConstructMeshJob meshJob;

		// Token: 0x04000295 RID: 661
		private ReadMeshTriangleDataJob triangleReadJob;

		// Token: 0x04000296 RID: 662
		private RemoveVertexPairsJob removePairsJob;

		// Token: 0x04000297 RID: 663
		private JobHandle subdivideHandle;

		// Token: 0x04000298 RID: 664
		private JobHandle constructHandle;

		// Token: 0x04000299 RID: 665
		private JobHandle triangleHandle;

		// Token: 0x0400029A RID: 666
		private JobHandle removePairsHandle;

		// Token: 0x0400029B RID: 667
		private NativeArray<SubdividableTriangle> meshTriangles;

		// Token: 0x0400029C RID: 668
		private NativeArray<float3> vertices;

		// Token: 0x0400029D RID: 669
		private NativeArray<float3> normals;

		// Token: 0x0400029E RID: 670
		private NativeArray<float4> colors;

		// Token: 0x0400029F RID: 671
		private NativeArray<float2> uv3s;

		// Token: 0x040002A0 RID: 672
		private NativeArray<int> triangles;

		// Token: 0x040002A1 RID: 673
		private NativeArray<MiragePlane> frustumPlanes;

		// Token: 0x040002A2 RID: 674
		private NativeStream tris;

		// Token: 0x040002A3 RID: 675
		private NativeHashMap<float3, int> storedVerts;

		// Token: 0x040002A4 RID: 676
		private NativeArray<float3> newVerts;

		// Token: 0x040002A5 RID: 677
		private NativeArray<float3> newNormals;

		// Token: 0x040002A6 RID: 678
		private NativeArray<float4> newColors;

		// Token: 0x040002A7 RID: 679
		private NativeArray<float2> newUV3s;

		// Token: 0x040002A8 RID: 680
		private NativeStream newTriangles;

		// Token: 0x040002A9 RID: 681
		private NativeArray<int> outputTriIndices;

		// Token: 0x040002AA RID: 682
		public Mesh mesh;

		// Token: 0x040002AB RID: 683
		public int maxSubdivisionLevel = 7;

		// Token: 0x040002AC RID: 684
		public float subdivisionRange = 50f;

		// Token: 0x040002AD RID: 685
		private int streamForeachCount;

		// Token: 0x040002AE RID: 686
		private bool processingSubdivision;

		// Token: 0x040002AF RID: 687
		private bool generatingMesh;

		// Token: 0x040002B0 RID: 688
		private bool firstRun = true;
	}
}
