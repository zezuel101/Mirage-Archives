using System;
using UnityEngine;

namespace Mirage.Subdivision
{
	// Token: 0x02000059 RID: 89
	public class SubdivisionQuad
	{
		// Token: 0x06000279 RID: 633 RVA: 0x00015BE0 File Offset: 0x00013DE0
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

		// Token: 0x0600027A RID: 634 RVA: 0x00015C68 File Offset: 0x00013E68
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

		// Token: 0x0600027B RID: 635 RVA: 0x00015CE8 File Offset: 0x00013EE8
		public void OnNormalUpdate()
		{
			bool flag = this.active;
			if (flag)
			{
				this.Deactivate();
			}
		}

		// Token: 0x0600027C RID: 636 RVA: 0x00015D08 File Offset: 0x00013F08
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

		// Token: 0x0600027D RID: 637 RVA: 0x00015D68 File Offset: 0x00013F68
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

		// Token: 0x0600027E RID: 638 RVA: 0x00015EBC File Offset: 0x000140BC
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

		// Token: 0x0600027F RID: 639 RVA: 0x00015F60 File Offset: 0x00014160
		public void Cleanup()
		{
			bool flag = !this.isMaxLevel;
			if (!flag)
			{
				this.Deactivate();
			}
		}

		// Token: 0x04000260 RID: 608
		private readonly PQ quad;

		// Token: 0x04000261 RID: 609
		private readonly int subdivisionLevel;

		// Token: 0x04000262 RID: 610
		private readonly float subdivisionRange;

		// Token: 0x04000263 RID: 611
		private readonly bool isMaxLevel;

		// Token: 0x04000264 RID: 612
		private readonly float activationSqrDist;

		// Token: 0x04000265 RID: 613
		private GameObject fakeQuad;

		// Token: 0x04000266 RID: 614
		private SubdivisionComponent subdivisionComponent;

		// Token: 0x04000267 RID: 615
		private MeshRenderer quadRenderer;

		// Token: 0x04000268 RID: 616
		private Mesh sourceMesh;

		// Token: 0x04000269 RID: 617
		private bool active;
	}
}
