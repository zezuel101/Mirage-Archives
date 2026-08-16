using System;
using UnityEngine;

namespace Mirage.VirtualTexture
{
	/// <summary>Frame-local context for projecting tile world extent to screen pixels.</summary>
	// Token: 0x02000048 RID: 72
	public readonly struct VTLevelContext
	{
		// Token: 0x060001D0 RID: 464 RVA: 0x0000DD24 File Offset: 0x0000BF24
		public VTLevelContext(Vector3 cameraPos, Vector3 planetOrigin, Quaternion planetRotation, float planetRadius, float pixelsPerUnitTangent, Plane[] frustumPlanes)
		{
			this.CameraPos = cameraPos;
			this.PlanetOrigin = planetOrigin;
			this.PlanetRotation = planetRotation;
			this.PlanetRadius = planetRadius;
			this.PixelsPerUnitTangent = pixelsPerUnitTangent;
			this.FrustumPlanes = frustumPlanes;
			Vector3 toCam = cameraPos - planetOrigin;
			float camDist = toCam.magnitude;
			this.CameraInsideSphere = (camDist <= planetRadius);
			this.CamDir = ((camDist > 1E-06f) ? (toCam / camDist) : Vector3.up);
			this.CosHorizon = ((camDist > 1E-06f) ? Mathf.Clamp(planetRadius / camDist, -1f, 1f) : 1f);
			this.SinHorizon = Mathf.Sqrt(Mathf.Max(0f, 1f - this.CosHorizon * this.CosHorizon));
		}

		// Token: 0x04000168 RID: 360
		public readonly Vector3 CameraPos;

		// Token: 0x04000169 RID: 361
		public readonly Vector3 PlanetOrigin;

		// Token: 0x0400016A RID: 362
		public readonly Quaternion PlanetRotation;

		// Token: 0x0400016B RID: 363
		public readonly float PlanetRadius;

		/// <summary>pixelHeight / (2*tan(fov/2)) — multiply by (worldExtent / distance) for projected pixels.</summary>
		// Token: 0x0400016C RID: 364
		public readonly float PixelsPerUnitTangent;

		/// <summary>Six frustum planes, world space, inward-facing. Read only — the body reuses the array.</summary>
		// Token: 0x0400016D RID: 365
		public readonly Plane[] FrustumPlanes;

		// Token: 0x0400016E RID: 366
		public readonly Vector3 CamDir;

		// Token: 0x0400016F RID: 367
		public readonly float CosHorizon;

		// Token: 0x04000170 RID: 368
		public readonly float SinHorizon;

		// Token: 0x04000171 RID: 369
		public readonly bool CameraInsideSphere;
	}
}
