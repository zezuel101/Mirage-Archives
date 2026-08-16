using System;
using UnityEngine;

namespace Mirage.ScaledSystem
{
	/// <summary>
	/// Drives load/unload of one body's scaled-space VT by the body's on-screen size. Attached to the
	/// body's <c>scaledBody</c> GameObject (which is DontDestroyOnLoad, so it persists across scenes).
	///
	/// There's no PQS to gate residency in scaled space and the map/menu camera can sweep the whole
	/// system instantly, so we load when the body grows past <see cref="F:Mirage.ScaledSystem.ScaledOnDemandComponent.LoadScreenSizePixels" /> and
	/// unload (after a delay, unless it shrinks below the force threshold) when it's small and not the
	/// current main body. Mirrors Parallax-Continued's ScaledOnDemandComponent.
	/// </summary>
	// Token: 0x0200005E RID: 94
	public class ScaledOnDemandComponent : MonoBehaviour
	{
		// Token: 0x060002B7 RID: 695 RVA: 0x00016FC4 File Offset: 0x000151C4
		public void Init(MirageScaledBody scaledBody, CelestialBody celestialBodyRef)
		{
			this.body = scaledBody;
			this.celestialBody = celestialBodyRef;
			this.meshRenderer = base.GetComponent<MeshRenderer>();
		}

		// Token: 0x060002B8 RID: 696 RVA: 0x00016FE4 File Offset: 0x000151E4
		private void OnEnable()
		{
			bool flag = this.body != null && this.body.IsLoading;
			if (flag)
			{
				this.body.AbortLoad();
			}
		}

		// Token: 0x060002B9 RID: 697 RVA: 0x00017018 File Offset: 0x00015218
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
							bool flag5 = sizePixels > ScaledOnDemandComponent.LoadScreenSizePixels;
							if (flag5)
							{
								this.pendingUnload = false;
								this.EnsureLoading();
								this.body.ResumeStreaming();
							}
							else
							{
								bool flag6 = sizePixels < ScaledOnDemandComponent.UnloadScreenSizePixels && FlightGlobals.currentMainBody != this.celestialBody && this.body.Loaded;
								if (flag6)
								{
									bool flag7 = !this.pendingUnload;
									if (flag7)
									{
										this.pendingUnload = true;
										this.timeUnloadRequested = Time.time;
									}
									bool flag8 = Time.time - this.timeUnloadRequested > ScaledOnDemandComponent.UnloadDelaySeconds || sizePixels < ScaledOnDemandComponent.ForceUnloadScreenSizePixels;
									if (flag8)
									{
										this.pendingUnload = false;
										this.body.Unload();
									}
								}
							}
						}
					}
				}
			}
		}

		// Token: 0x060002BA RID: 698 RVA: 0x000171C4 File Offset: 0x000153C4
		private void EnsureLoading()
		{
			bool flag = !this.body.Loaded && !this.body.IsLoading;
			if (flag)
			{
				base.StartCoroutine(this.body.LoadAsync());
			}
		}

		// Token: 0x060002BB RID: 699 RVA: 0x00017206 File Offset: 0x00015406
		private void OnDestroy()
		{
			MirageScaledBody mirageScaledBody = this.body;
			if (mirageScaledBody != null)
			{
				mirageScaledBody.Unload();
			}
		}

		// Token: 0x060002BC RID: 700 RVA: 0x0001721C File Offset: 0x0001541C
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

		// Token: 0x0400028D RID: 653
		public static float LoadScreenSizePixels = 3f;

		// Token: 0x0400028E RID: 654
		public static float UnloadScreenSizePixels = 1.5f;

		// Token: 0x0400028F RID: 655
		public static float ForceUnloadScreenSizePixels = 0.5f;

		// Token: 0x04000290 RID: 656
		public static float UnloadDelaySeconds = 10f;

		// Token: 0x04000291 RID: 657
		private MirageScaledBody body;

		// Token: 0x04000292 RID: 658
		private CelestialBody celestialBody;

		// Token: 0x04000293 RID: 659
		private MeshRenderer meshRenderer;

		// Token: 0x04000294 RID: 660
		private bool pendingUnload;

		// Token: 0x04000295 RID: 661
		private float timeUnloadRequested = -1f;
	}
}
