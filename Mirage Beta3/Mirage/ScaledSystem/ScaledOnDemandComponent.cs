using System;
using UnityEngine;

namespace Mirage.ScaledSystem
{
	/// <summary>Loads/unloads one body's scaled VT by apparent screen size.</summary>
	// Token: 0x02000069 RID: 105
	public class ScaledOnDemandComponent : MonoBehaviour
	{
		// Token: 0x06000320 RID: 800 RVA: 0x000184E0 File Offset: 0x000166E0
		public void Init(MirageScaledBody scaledBody, CelestialBody celestialBodyRef)
		{
			this.body = scaledBody;
			this.celestialBody = celestialBodyRef;
			this.meshRenderer = base.GetComponent<MeshRenderer>();
		}

		// Token: 0x06000321 RID: 801 RVA: 0x00018500 File Offset: 0x00016700
		private void OnEnable()
		{
			bool flag = this.body != null && this.body.IsLoading;
			if (flag)
			{
				this.body.AbortLoad();
			}
		}

		// Token: 0x06000322 RID: 802 RVA: 0x00018534 File Offset: 0x00016734
		private void Update()
		{
			bool flag = this.body == null || this.celestialBody == null || this.celestialBody.scaledBody == null;
			if (!flag)
			{
				bool flag2 = HighLogic.LoadedScene == 2;
				if (flag2)
				{
					this.EnsureLoading();
				}
				else
				{
					bool flag3 = this.meshRenderer != null && !this.meshRenderer.enabled;
					if (flag3)
					{
						this.EnsureLoading();
						this.body.PauseStreaming();
						this.pendingUnload = false;
					}
					else
					{
						Camera cam = MirageScaledBody.GetScaledSpaceCamera();
						bool flag4 = cam == null;
						if (!flag4)
						{
							float sizePixels = ScaledOnDemandComponent.ScreenSizePixels(this.celestialBody.scaledBody.transform.position, cam.transform.position, (float)this.celestialBody.Radius, cam.fieldOfView);
							bool flag5 = sizePixels > 3f;
							if (flag5)
							{
								this.pendingUnload = false;
								this.EnsureLoading();
								this.body.ResumeStreaming();
							}
							else
							{
								bool flag6 = sizePixels < 1.5f && FlightGlobals.currentMainBody != this.celestialBody && this.body.Loaded;
								if (flag6)
								{
									this.RequestUnload(sizePixels);
								}
							}
						}
					}
				}
			}
		}

		// Token: 0x06000323 RID: 803 RVA: 0x00018684 File Offset: 0x00016884
		private void RequestUnload(float sizePixels)
		{
			bool flag = !this.pendingUnload;
			if (flag)
			{
				this.pendingUnload = true;
				this.timeUnloadRequested = Time.time;
			}
			bool flag2 = Time.time - this.timeUnloadRequested > 10f || sizePixels < 0.5f;
			if (flag2)
			{
				this.pendingUnload = false;
				this.body.Unload();
			}
		}

		/// <summary>Start an async coarse load unless one is already loaded or running.</summary>
		// Token: 0x06000324 RID: 804 RVA: 0x000186EC File Offset: 0x000168EC
		private void EnsureLoading()
		{
			bool flag = !this.body.Loaded && !this.body.IsLoading;
			if (flag)
			{
				base.StartCoroutine(this.body.LoadAsync());
			}
		}

		// Token: 0x06000325 RID: 805 RVA: 0x0001872E File Offset: 0x0001692E
		private void OnDestroy()
		{
			MirageScaledBody mirageScaledBody = this.body;
			if (mirageScaledBody != null)
			{
				mirageScaledBody.Unload();
			}
		}

		/// <summary>The body's on-screen diameter in pixels.</summary>
		// Token: 0x06000326 RID: 806 RVA: 0x00018744 File Offset: 0x00016944
		private static float ScreenSizePixels(Vector3 bodyPos, Vector3 camPos, float radius, float fovDegrees)
		{
			float fov = fovDegrees * 0.017453292f;
			float d = Vector3.Distance(bodyPos, camPos);
			float r = radius * ScaledSpace.InverseScaleFactor;
			bool flag = d <= r;
			float result;
			if (flag)
			{
				result = float.MaxValue;
			}
			else
			{
				float projRadius = 1f / Mathf.Tan(fov * 0.5f) * r / Mathf.Sqrt(d * d - r * r);
				result = projRadius * (float)Screen.height;
			}
			return result;
		}

		// Token: 0x040002FF RID: 767
		public const float LoadPixels = 3f;

		// Token: 0x04000300 RID: 768
		public const float UnloadPixels = 1.5f;

		// Token: 0x04000301 RID: 769
		public const float ForceUnloadPixels = 0.5f;

		// Token: 0x04000302 RID: 770
		public const float UnloadDelaySeconds = 10f;

		// Token: 0x04000303 RID: 771
		private MirageScaledBody body;

		// Token: 0x04000304 RID: 772
		private CelestialBody celestialBody;

		// Token: 0x04000305 RID: 773
		private MeshRenderer meshRenderer;

		// Token: 0x04000306 RID: 774
		private bool pendingUnload;

		// Token: 0x04000307 RID: 775
		private float timeUnloadRequested = -1f;
	}
}
