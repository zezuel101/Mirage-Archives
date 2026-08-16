using System;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Mirage.Subdivision
{
	// Token: 0x02000054 RID: 84
	public class SubdivisionComponent : MonoBehaviour
	{
		// Token: 0x06000264 RID: 612 RVA: 0x00014A54 File Offset: 0x00012C54
		private void Start()
		{
			this.mesh = base.GetComponent<MeshFilter>().sharedMesh;
			this.mesh.MarkDynamic();
			this.AcquireId();
			this.InitArrays();
		}

		// Token: 0x06000265 RID: 613 RVA: 0x00014A82 File Offset: 0x00012C82
		private void AcquireId()
		{
			this.uniqueId = InterlockedCounters.Request();
			this.hasId = true;
		}

		// Token: 0x06000266 RID: 614 RVA: 0x00014A98 File Offset: 0x00012C98
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

		// Token: 0x06000267 RID: 615 RVA: 0x00014BE4 File Offset: 0x00012DE4
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

		// Token: 0x06000268 RID: 616 RVA: 0x00014D10 File Offset: 0x00012F10
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

		// Token: 0x06000269 RID: 617 RVA: 0x00014E18 File Offset: 0x00013018
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

		// Token: 0x0600026A RID: 618 RVA: 0x00014F10 File Offset: 0x00013110
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

		// Token: 0x0600026B RID: 619 RVA: 0x00014F84 File Offset: 0x00013184
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

		// Token: 0x0600026C RID: 620 RVA: 0x000150B4 File Offset: 0x000132B4
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

		// Token: 0x0600026D RID: 621 RVA: 0x00015138 File Offset: 0x00013338
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

		// Token: 0x0600026E RID: 622 RVA: 0x00015250 File Offset: 0x00013450
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

		// Token: 0x0600026F RID: 623 RVA: 0x00015304 File Offset: 0x00013504
		private void FreePostReadbackBuffers()
		{
			bool isCreated = this.newTriangles.IsCreated;
			if (isCreated)
			{
				this.newTriangles.Dispose();
			}
		}

		// Token: 0x06000270 RID: 624 RVA: 0x00015330 File Offset: 0x00013530
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

		// Token: 0x06000271 RID: 625 RVA: 0x0001543C File Offset: 0x0001363C
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

		// Token: 0x06000272 RID: 626 RVA: 0x0001564D File Offset: 0x0001384D
		private void OnDisable()
		{
			this.Cleanup();
		}

		// Token: 0x04000226 RID: 550
		private int uniqueId = -1;

		// Token: 0x04000227 RID: 551
		private bool hasId = false;

		// Token: 0x04000228 RID: 552
		private SubdivideMeshJob subdivideJob;

		// Token: 0x04000229 RID: 553
		private ConstructMeshJob meshJob;

		// Token: 0x0400022A RID: 554
		private ReadMeshTriangleDataJob triangleReadJob;

		// Token: 0x0400022B RID: 555
		private RemoveVertexPairsJob removePairsJob;

		// Token: 0x0400022C RID: 556
		private JobHandle subdivideHandle;

		// Token: 0x0400022D RID: 557
		private JobHandle constructHandle;

		// Token: 0x0400022E RID: 558
		private JobHandle triangleHandle;

		// Token: 0x0400022F RID: 559
		private JobHandle removePairsHandle;

		// Token: 0x04000230 RID: 560
		private NativeArray<SubdividableTriangle> meshTriangles;

		// Token: 0x04000231 RID: 561
		private NativeArray<float3> vertices;

		// Token: 0x04000232 RID: 562
		private NativeArray<float3> normals;

		// Token: 0x04000233 RID: 563
		private NativeArray<float4> colors;

		// Token: 0x04000234 RID: 564
		private NativeArray<float2> uv3s;

		// Token: 0x04000235 RID: 565
		private NativeArray<int> triangles;

		// Token: 0x04000236 RID: 566
		private NativeArray<MiragePlane> frustumPlanes;

		// Token: 0x04000237 RID: 567
		private NativeStream tris;

		// Token: 0x04000238 RID: 568
		private NativeHashMap<float3, int> storedVerts;

		// Token: 0x04000239 RID: 569
		private NativeArray<float3> newVerts;

		// Token: 0x0400023A RID: 570
		private NativeArray<float3> newNormals;

		// Token: 0x0400023B RID: 571
		private NativeArray<float4> newColors;

		// Token: 0x0400023C RID: 572
		private NativeArray<float2> newUV3s;

		// Token: 0x0400023D RID: 573
		private NativeStream newTriangles;

		// Token: 0x0400023E RID: 574
		private NativeArray<int> outputTriIndices;

		// Token: 0x0400023F RID: 575
		public Mesh mesh;

		// Token: 0x04000240 RID: 576
		public int maxSubdivisionLevel = 7;

		// Token: 0x04000241 RID: 577
		public float subdivisionRange = 50f;

		// Token: 0x04000242 RID: 578
		private int streamForeachCount;

		// Token: 0x04000243 RID: 579
		private bool processingSubdivision;

		// Token: 0x04000244 RID: 580
		private bool generatingMesh;

		// Token: 0x04000245 RID: 581
		private bool firstRun = true;
	}
}
