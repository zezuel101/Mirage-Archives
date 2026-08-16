using System;
using System.Collections;
using System.Collections.Generic;
using Mirage.VirtualTexture;
using UnityEngine;

namespace Mirage.ScaledSystem
{
	/// <summary>
	/// One body's scaled-space VT: owns the colour/height/normal tile caches that feed the
	/// <c>Mirage/Scaled</c> material, and acts as an <see cref="T:Mirage.VirtualTexture.IMirageBody" /> so the existing
	/// <see cref="T:Mirage.VirtualTexture.TileStreamingManager" /> streams it — but driven by the <b>scaled camera</b> instead of
	/// PQS quads (there are no quads in scaled space). The coarse levels are pinned at bootstrap as a
	/// fallback; finer levels stream into the same caches based on what the camera is looking at, up to
	/// <see cref="P:Mirage.ScaledSystem.MirageScaledBody.StreamingMaxLevel" /> — <c>canonicalMaxLevel</c> by default, the full
	/// <see cref="F:Mirage.VirtualTexture.VirtualTextureConfig.webMaxLevel" /> pyramid only when <c>scaledWebStreaming</c> is on.
	/// </summary>
	// Token: 0x0200005C RID: 92
	public sealed class MirageScaledBody : IMirageBody
	{
		// Token: 0x1700006E RID: 110
		// (get) Token: 0x06000296 RID: 662 RVA: 0x00016734 File Offset: 0x00014934
		public string Name { get; }

		// Token: 0x1700006F RID: 111
		// (get) Token: 0x06000297 RID: 663 RVA: 0x0001673C File Offset: 0x0001493C
		public VirtualTextureConfig Config { get; }

		// Token: 0x17000070 RID: 112
		// (get) Token: 0x06000298 RID: 664 RVA: 0x00016744 File Offset: 0x00014944
		public Material ScaledMaterial { get; }

		// Token: 0x17000071 RID: 113
		// (get) Token: 0x06000299 RID: 665 RVA: 0x0001674C File Offset: 0x0001494C
		public CelestialBody Body { get; }

		// Token: 0x17000072 RID: 114
		// (get) Token: 0x0600029A RID: 666 RVA: 0x00016754 File Offset: 0x00014954
		// (set) Token: 0x0600029B RID: 667 RVA: 0x0001675C File Offset: 0x0001495C
		public bool Loaded { get; private set; }

		// Token: 0x17000073 RID: 115
		// (get) Token: 0x0600029C RID: 668 RVA: 0x00016765 File Offset: 0x00014965
		// (set) Token: 0x0600029D RID: 669 RVA: 0x0001676D File Offset: 0x0001496D
		public bool IsLoading { get; private set; }

		// Token: 0x17000074 RID: 116
		// (get) Token: 0x0600029E RID: 670 RVA: 0x00016776 File Offset: 0x00014976
		// (set) Token: 0x0600029F RID: 671 RVA: 0x0001677E File Offset: 0x0001497E
		public TileCache Cache { get; private set; }

		/// <summary>Capped at canonical unless <c>scaledWebStreaming</c> is on — the web tier is a surface-only
		/// feature by default. The cache below is BUILT to this depth too, so the cap isn't only a request
		/// filter: no fine-block atlas is allocated and the shader's own LOD pick is clamped with it.</summary>
		// Token: 0x17000075 RID: 117
		// (get) Token: 0x060002A0 RID: 672 RVA: 0x00016787 File Offset: 0x00014987
		public int StreamingMaxLevel
		{
			get
			{
				return this.Config.ScaledStreamingMaxLevel;
			}
		}

		// Token: 0x17000076 RID: 118
		// (get) Token: 0x060002A1 RID: 673 RVA: 0x00016794 File Offset: 0x00014994
		private string StreamKey
		{
			get
			{
				return this.Name + " (scaled)";
			}
		}

		// Token: 0x060002A2 RID: 674 RVA: 0x000167A6 File Offset: 0x000149A6
		public MirageScaledBody(string name, VirtualTextureConfig config, Material material, CelestialBody body)
		{
			this.Name = name;
			this.Config = config;
			this.ScaledMaterial = material;
			this.Body = body;
		}

		// Token: 0x17000077 RID: 119
		// (get) Token: 0x060002A3 RID: 675 RVA: 0x000167E3 File Offset: 0x000149E3
		private int Coarse
		{
			get
			{
				return 2;
			}
		}

		/// <summary>
		/// Bootstrap the coarse levels asynchronously (over frames) so loading at the PQS→scaled
		/// transition doesn't hitch. Binds each layer once its coarse set is resident. Fine streaming is
		/// controlled separately (<see cref="M:Mirage.ScaledSystem.MirageScaledBody.ResumeStreaming" />/<see cref="M:Mirage.ScaledSystem.MirageScaledBody.PauseStreaming" />). Drive with
		/// the owning MonoBehaviour's <c>StartCoroutine</c>.
		/// </summary>
		// Token: 0x060002A4 RID: 676 RVA: 0x000167E6 File Offset: 0x000149E6
		public IEnumerator LoadAsync()
		{
			MirageScaledBody.<LoadAsync>d__36 <LoadAsync>d__ = new MirageScaledBody.<LoadAsync>d__36(0);
			<LoadAsync>d__.<>4__this = this;
			return <LoadAsync>d__;
		}

		/// <summary>
		/// Reset a load that was interrupted by the scaledBody GameObject being deactivated during a scene
		/// transition: Unity kills the running <see cref="M:Mirage.ScaledSystem.MirageScaledBody.LoadAsync" /> coroutine but leaves
		/// <see cref="P:Mirage.ScaledSystem.MirageScaledBody.IsLoading" /> true, so <c>ScaledOnDemandComponent.EnsureLoading</c>'s <c>!IsLoading</c>
		/// guard would never restart it (the scaled VT silently never starts — seen going SPACECENTER →
		/// TRACKSTATION directly). Bumps the load generation so any still-alive coroutine bails at its next
		/// guard instead of touching the disposed caches, then clears the half-built state so a fresh load can
		/// run. Only call when the coroutine is known dead (the component's OnEnable, post-reactivation).
		/// </summary>
		// Token: 0x060002A5 RID: 677 RVA: 0x000167F8 File Offset: 0x000149F8
		public void AbortLoad()
		{
			bool flag = !this.IsLoading;
			if (!flag)
			{
				this.loadGeneration++;
				this.IsLoading = false;
				this.Loaded = false;
				this.pendingUnload = false;
				TileCache cache = this.Cache;
				if (cache != null)
				{
					cache.Dispose();
				}
				this.Cache = null;
				MirageDebug.Log("MirageScaledBody: aborted interrupted load of '" + this.Name + "' — will reload.");
			}
		}

		/// <summary>Register for fine streaming via the shared pipeline (camera-driven, see below).</summary>
		// Token: 0x060002A6 RID: 678 RVA: 0x00016870 File Offset: 0x00014A70
		public void ResumeStreaming()
		{
			bool flag = !this.Loaded || this.streaming;
			if (!flag)
			{
				TileStreamingManager.RegisterBody(this.StreamKey, this);
				this.streaming = true;
			}
		}

		/// <summary>Stop fine streaming but keep the coarse caches resident (e.g. while faded near the surface).</summary>
		// Token: 0x060002A7 RID: 679 RVA: 0x000168AC File Offset: 0x00014AAC
		public void PauseStreaming()
		{
			bool flag = !this.streaming;
			if (!flag)
			{
				TileStreamingManager.UnregisterBody(this.StreamKey);
				this.streaming = false;
			}
		}

		// Token: 0x060002A8 RID: 680 RVA: 0x000168DC File Offset: 0x00014ADC
		public void Unload()
		{
			bool isLoading = this.IsLoading;
			if (isLoading)
			{
				this.pendingUnload = true;
			}
			else
			{
				bool flag = !this.Loaded;
				if (!flag)
				{
					this.PauseStreaming();
					TileCache cache = this.Cache;
					if (cache != null)
					{
						cache.Dispose();
					}
					this.Cache = null;
					this.Loaded = false;
					MirageDebug.Log("MirageScaledBody: unloaded '" + this.Name + "'");
				}
			}
		}

		/// <summary>
		/// The camera that renders scaled bodies in the current scene. Flight uses the ScaledCamera rig;
		/// the tracking station and map view use the PlanetariumCamera. <see cref="P:ScaledCamera.Instance" />
		/// is set in the flight rig's Awake and is null in the tracking station when it's entered without
		/// going through flight — using it unconditionally silently stalled load/stream there.
		/// </summary>
		// Token: 0x060002A9 RID: 681 RVA: 0x00016954 File Offset: 0x00014B54
		public static Camera GetScaledSpaceCamera()
		{
			bool flag = HighLogic.LoadedScene == 8 || MapView.MapIsEnabled;
			if (flag)
			{
				Camera pc = PlanetariumCamera.Camera;
				bool flag2 = pc != null;
				if (flag2)
				{
					return pc;
				}
			}
			ScaledCamera sc = ScaledCamera.Instance;
			return (sc != null) ? sc.cam : null;
		}

		/// <summary>
		/// Deliberately none. The screen-space descent exists to undo PQS's subdivision being a collision-mesh
		/// number rather than a texture-resolution one — a problem the scaled path does not have: the raycast
		/// grid below already derives each leaf's Subdivision from what the view resolves. Handing the streamer
		/// a projection context here would make it descend on top of a level that is ALREADY screen-space,
		/// double-counting the same pixels.
		/// </summary>
		// Token: 0x060002AA RID: 682 RVA: 0x000169AC File Offset: 0x00014BAC
		public bool TryGetLevelContext(out VTLevelContext ctx)
		{
			ctx = default(VTLevelContext);
			return false;
		}

		// Token: 0x060002AB RID: 683 RVA: 0x000169C8 File Offset: 0x00014BC8
		public void EnumerateVisibleLeafQuads(List<LeafQuad> output)
		{
			int frame = Time.frameCount;
			bool flag = frame - this.cachedFrame >= 8;
			if (flag)
			{
				this.cachedLeaves.Clear();
				this.RecomputeVisibleLeaves(this.cachedLeaves);
				this.cachedFrame = frame;
			}
			output.AddRange(this.cachedLeaves);
		}

		// Token: 0x060002AC RID: 684 RVA: 0x00016A20 File Offset: 0x00014C20
		private void RecomputeVisibleLeaves(List<LeafQuad> output)
		{
			bool flag = !this.Loaded || this.Body == null || this.Body.scaledBody == null;
			if (!flag)
			{
				Camera cam = MirageScaledBody.GetScaledSpaceCamera();
				bool flag2 = cam == null;
				if (!flag2)
				{
					Transform bodyTf = this.Body.scaledBody.transform;
					Vector3 center = bodyTf.position;
					Vector3 camPos = cam.transform.position;
					float r = (float)this.Body.Radius * ScaledSpace.InverseScaleFactor;
					bool flag3 = r <= 0f;
					if (!flag3)
					{
						float tanHalfFov = Mathf.Tan(cam.fieldOfView * 0.017453292f * 0.5f);
						float screenH = (float)Screen.height;
						Quaternion worldToBody = Quaternion.Inverse(bodyTf.rotation);
						int maxLevel = this.StreamingMaxLevel;
						int coarse = 2;
						MirageScaledBody.s_Seen.Clear();
						for (int yi = 0; yi <= 24; yi++)
						{
							for (int xi = 0; xi <= 24; xi++)
							{
								Ray ray = cam.ViewportPointToRay(new Vector3((float)xi / 24f, (float)yi / 24f, 0f));
								float t;
								bool flag4 = !MirageScaledBody.RaySphere(ray.origin, ray.direction, center, r, out t);
								if (!flag4)
								{
									Vector3 hit = ray.origin + ray.direction * t;
									Vector3 bodyDir = worldToBody * (hit - center);
									int level = MirageScaledBody.LevelForDistance(Vector3.Distance(camPos, hit), r, tanHalfFov, screenH, this.Config.tileSize, maxLevel);
									bool flag5 = level <= coarse;
									if (!flag5)
									{
										PQS.QuadPlane plane;
										double u;
										double v;
										PQSMod_GnomonicTest.GetGnomonicMapCoords(bodyDir, ref plane, ref u, ref v);
										int face = plane;
										int g = 1 << level;
										int tx = Mathf.Clamp((int)(u * (double)g), 0, g - 1);
										int ty = Mathf.Clamp((int)(v * (double)g), 0, g - 1);
										bool flag6 = MirageScaledBody.s_Seen.Add(TileCache.PackKey(face, level, tx, ty));
										if (flag6)
										{
											output.Add(new LeafQuad(face, (double)tx / (double)g, (double)ty / (double)g, level));
										}
									}
								}
							}
						}
					}
				}
			}
		}

		// Token: 0x060002AD RID: 685 RVA: 0x00016C80 File Offset: 0x00014E80
		private static int LevelForDistance(float dist, float r, float tanHalfFov, float screenH, int tileSize, int maxLevel)
		{
			bool flag = dist <= 0f;
			int result;
			if (flag)
			{
				result = maxLevel;
			}
			else
			{
				float twoPowL = 1.5707964f * r * screenH * MirageSettings.Oversample / (dist * 2f * tanHalfFov * (float)Mathf.Max(tileSize, 1));
				int level = Mathf.FloorToInt(Mathf.Log(Mathf.Max(twoPowL, 1f), 2f)) + 1;
				result = Mathf.Min(level, maxLevel);
			}
			return result;
		}

		// Token: 0x060002AE RID: 686 RVA: 0x00016CF0 File Offset: 0x00014EF0
		private static bool RaySphere(Vector3 o, Vector3 d, Vector3 c, float radius, out float t)
		{
			Vector3 oc = o - c;
			float b = Vector3.Dot(oc, d);
			float cc = Vector3.Dot(oc, oc) - radius * radius;
			float disc = b * b - cc;
			bool flag = disc < 0f;
			bool result;
			if (flag)
			{
				t = 0f;
				result = false;
			}
			else
			{
				float sq = Mathf.Sqrt(disc);
				t = -b - sq;
				bool flag2 = t < 0f;
				if (flag2)
				{
					t = -b + sq;
				}
				result = (t >= 0f);
			}
			return result;
		}

		// Token: 0x04000282 RID: 642
		private bool streaming;

		// Token: 0x04000283 RID: 643
		private bool pendingUnload;

		// Token: 0x04000284 RID: 644
		private int loadGeneration;

		// Token: 0x04000286 RID: 646
		private const int GridSamples = 24;

		// Token: 0x04000287 RID: 647
		private static readonly HashSet<long> s_Seen = new HashSet<long>();

		/// <summary>
		/// Camera-driven "visibility": raycast a screen grid against the scaled sphere and emit the tile
		/// each hit needs at a distance-derived level. Frustum-limited (only on-screen samples hit) and
		/// distance-LOD'd (closer hits → finer level), so the required set stays bounded — whole-planet
		/// views ask for nothing beyond the pinned coarse floor; ground zoom asks for maxLevel over a
		/// tiny footprint. TileStreamingManager walks each leaf down to the coarse floor for fallback.
		/// </summary>
		// Token: 0x04000288 RID: 648
		private const int ScaledEnumInterval = 8;

		// Token: 0x04000289 RID: 649
		private readonly List<LeafQuad> cachedLeaves = new List<LeafQuad>();

		// Token: 0x0400028A RID: 650
		private int cachedFrame = -1000;

		// Token: 0x0400028B RID: 651
		private const int ScaledLevelBias = 1;
	}
}
