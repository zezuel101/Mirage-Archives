using System;
using UnityEngine;

namespace Mirage.VirtualTexture
{
	/// <summary>
	/// The frame-local projection context behind <see cref="M:Mirage.VirtualTexture.IMirageBody.TryGetLevelContext(Mirage.VirtualTexture.VTLevelContext@)" />.
	///
	/// This exists because <c>PQ.subdivision</c> is NOT a statement about how much texture a quad needs. PQS
	/// subdivides to build a COLLISION MESH, and Mirage deliberately keeps <c>mindetaildist</c> low so it does
	/// as little CPU terrain building as possible — that is the entire point of the mod. Displacement and
	/// colour come from GPU tessellation over the virtual texture, which does not care how many triangles PQS
	/// chose. Gating the streamed level on subdivision therefore couples texture resolution to the one number
	/// specifically tuned to be small, and the terrain goes coarse a short distance from the craft.
	///
	/// Kept in Unity world space (floats): the camera and the planet are in the same floating-origin frame, so
	/// the subtraction is well-conditioned even though the radius is ~1e6 — float gives ~0.1 m there, which a
	/// distance metric does not care about. Nothing here feeds geometry.
	/// </summary>
	// Token: 0x0200003C RID: 60
	public readonly struct VTLevelContext
	{
		// Token: 0x0600018C RID: 396 RVA: 0x0000C294 File Offset: 0x0000A494
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

		/// <summary>Camera position, Unity world space.</summary>
		// Token: 0x0400014B RID: 331
		public readonly Vector3 CameraPos;

		/// <summary>Planet centre, Unity world space.</summary>
		// Token: 0x0400014C RID: 332
		public readonly Vector3 PlanetOrigin;

		/// <summary>Body-local (the frame cube-face directions live in) → world.</summary>
		// Token: 0x0400014D RID: 333
		public readonly Quaternion PlanetRotation;

		// Token: 0x0400014E RID: 334
		public readonly float PlanetRadius;

		/// <summary>Pixels per unit of tangent at the image plane — <c>pixelHeight / (2·tan(fov/2))</c>. Multiply
		/// by (worldExtent / distance) to get a projected size in pixels.</summary>
		// Token: 0x0400014F RID: 335
		public readonly float PixelsPerUnitTangent;

		/// <summary>
		/// The camera's six frustum planes (world space), inward-facing. Required, not optional: PQS reports a
		/// LEAF as visible, but the streamer descends into that leaf's whole footprint, and a leaf at low
		/// subdivision is a quarter of a cube face. Without these the descent walks ~625 km of ground that is
		/// behind the camera, and the projected-size test cannot stop it — a far tile is still angularly large
		/// at a coarse level, so it subdivides on merit while being entirely off screen.
		/// </summary>
		// Token: 0x04000150 RID: 336
		public readonly Plane[] FrustumPlanes;

		/// <summary>Unit direction planet-centre → camera. The axis of the visible cap.</summary>
		// Token: 0x04000151 RID: 337
		public readonly Vector3 CamDir;

		/// <summary>cos of the horizon cap's angular radius, = clamp(R / |cam-origin|). No acos taken.</summary>
		// Token: 0x04000152 RID: 338
		public readonly float CosHorizon;

		/// <summary>sin of the same angle, = sqrt(1 - CosHorizon²). Pairs with CosHorizon for the cosine-space
		/// angle-sum in Visible.</summary>
		// Token: 0x04000153 RID: 339
		public readonly float SinHorizon;

		/// <summary>Camera at or below the surface: there is no horizon to hide anything, so the cull is skipped
		/// and every frustum-passing tile counts as visible.</summary>
		// Token: 0x04000154 RID: 340
		public readonly bool CameraInsideSphere;
	}
}
