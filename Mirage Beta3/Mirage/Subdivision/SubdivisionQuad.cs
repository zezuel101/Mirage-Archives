using System;
using UnityEngine;

namespace Mirage.Subdivision
{
	// Token: 0x02000064 RID: 100
	public class SubdivisionQuad
	{
		// Token: 0x060002DE RID: 734 RVA: 0x00016E00 File Offset: 0x00015000
		public SubdivisionQuad(PQ quad, int subdivisionLevel, float subdivisionRange, bool isMaxLevel)
		{
			this.quad = quad;
			this.subdivisionLevel = subdivisionLevel;
			this.subdivisionRange = subdivisionRange;
			this.isMaxLevel = isMaxLevel;
			if (isMaxLevel)
			{
				float side = 6.2831855f * (float)quad.sphereRoot.radius / 4f / Mathf.Pow(2f, (float)quad.sphereRoot.maxLevel);
				this.activationSqrDist = side * side;
				this.quadRenderer = quad.gameObject.GetComponent<MeshRenderer>();
			}
		}

		// Token: 0x060002DF RID: 735 RVA: 0x00016E88 File Offset: 0x00015088
		public void RangeCheck()
		{
			bool flag = !this.isMaxLevel;
			if (!flag)
			{
				float sqrDist = (float)(this.quad.PrecisePosition - SubdivisionRuntime.CameraPositionV3).sqrMagnitude;
				bool flag2 = sqrDist < this.activationSqrDist && !this.active;
				if (flag2)
				{
					this.TryActivate();
				}
				bool flag3 = sqrDist > this.activationSqrDist && this.active;
				if (flag3)
				{
					this.Deactivate();
				}
			}
		}

		// Token: 0x060002E0 RID: 736 RVA: 0x00016F08 File Offset: 0x00015108
		public void OnNormalUpdate()
		{
			bool flag = this.active;
			if (flag)
			{
				this.Deactivate();
			}
		}

		// Token: 0x060002E1 RID: 737 RVA: 0x00016F28 File Offset: 0x00015128
		private bool TryActivate()
		{
			bool isQueuedForNormalUpdate = this.quad.isQueuedForNormalUpdate;
			bool result;
			if (isQueuedForNormalUpdate)
			{
				result = false;
			}
			else
			{
				this.sourceMesh = Object.Instantiate<Mesh>(this.quad.mesh);
				bool flag = this.sourceMesh == null;
				if (flag)
				{
					result = false;
				}
				else
				{
					this.CreateFakeQuad();
					this.active = true;
					result = true;
				}
			}
			return result;
		}

		// Token: 0x060002E2 RID: 738 RVA: 0x00016F88 File Offset: 0x00015188
		private void CreateFakeQuad()
		{
			this.fakeQuad = new GameObject(this.quad.name + "_Subdivided");
			Transform t = this.quad.gameObject.transform;
			this.fakeQuad.transform.SetPositionAndRotation(t.position, t.rotation);
			this.fakeQuad.transform.localScale = t.localScale;
			this.fakeQuad.transform.SetParent(t, true);
			this.fakeQuad.layer = this.quad.gameObject.layer;
			this.fakeQuad.tag = this.quad.gameObject.tag;
			MeshFilter mf = this.fakeQuad.AddComponent<MeshFilter>();
			MeshRenderer mr = this.fakeQuad.AddComponent<MeshRenderer>();
			mf.sharedMesh = this.sourceMesh;
			mr.sharedMaterial = this.quadRenderer.sharedMaterial;
			this.fakeQuad.SetActive(true);
			this.quadRenderer.enabled = false;
			this.subdivisionComponent = this.fakeQuad.AddComponent<SubdivisionComponent>();
			this.subdivisionComponent.maxSubdivisionLevel = this.subdivisionLevel;
			this.subdivisionComponent.subdivisionRange = this.subdivisionRange;
			this.subdivisionComponent.mesh = this.sourceMesh;
		}

		// Token: 0x060002E3 RID: 739 RVA: 0x000170DC File Offset: 0x000152DC
		private void Deactivate()
		{
			bool flag = this.subdivisionComponent != null;
			if (flag)
			{
				this.subdivisionComponent.Cleanup();
				this.subdivisionComponent = null;
			}
			bool flag2 = this.fakeQuad != null;
			if (flag2)
			{
				Object.Destroy(this.fakeQuad);
				this.fakeQuad = null;
			}
			bool flag3 = this.sourceMesh != null;
			if (flag3)
			{
				Object.Destroy(this.sourceMesh);
				this.sourceMesh = null;
			}
			bool flag4 = this.quadRenderer != null;
			if (flag4)
			{
				this.quadRenderer.enabled = true;
			}
			this.active = false;
		}

		// Token: 0x060002E4 RID: 740 RVA: 0x00017180 File Offset: 0x00015380
		public void Cleanup()
		{
			bool flag = !this.isMaxLevel;
			if (!flag)
			{
				this.Deactivate();
			}
		}

		// Token: 0x040002CB RID: 715
		private readonly PQ quad;

		// Token: 0x040002CC RID: 716
		private readonly int subdivisionLevel;

		// Token: 0x040002CD RID: 717
		private readonly float subdivisionRange;

		// Token: 0x040002CE RID: 718
		private readonly bool isMaxLevel;

		// Token: 0x040002CF RID: 719
		private readonly float activationSqrDist;

		// Token: 0x040002D0 RID: 720
		private GameObject fakeQuad;

		// Token: 0x040002D1 RID: 721
		private SubdivisionComponent subdivisionComponent;

		// Token: 0x040002D2 RID: 722
		private MeshRenderer quadRenderer;

		// Token: 0x040002D3 RID: 723
		private Mesh sourceMesh;

		// Token: 0x040002D4 RID: 724
		private bool active;
	}
}
