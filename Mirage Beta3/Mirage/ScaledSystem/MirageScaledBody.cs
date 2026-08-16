using System;
using System.Collections;
using System.Collections.Generic;
using Mirage.VirtualTexture;
using Unity.Profiling;
using UnityEngine;

namespace Mirage.ScaledSystem
{
	/// <summary>One body's scaled-space virtual texture, camera-driven (no PQS quads in scaled space).</summary>
	// Token: 0x02000067 RID: 103
	public sealed class MirageScaledBody : IMirageBody
	{
		/// <summary>One body's scaled-space virtual texture, camera-driven (no PQS quads in scaled space).</summary>
		// Token: 0x060002FB RID: 763 RVA: 0x00017A04 File Offset: 0x00015C04
		public MirageScaledBody(string name, VirtualTextureConfig config, Material material, CelestialBody body)
		{
		}

		// Token: 0x17000075 RID: 117
		// (get) Token: 0x060002FC RID: 764 RVA: 0x00017A40 File Offset: 0x00015C40
		public string Name { get; } = name;

		// Token: 0x17000076 RID: 118
		// (get) Token: 0x060002FD RID: 765 RVA: 0x00017A48 File Offset: 0x00015C48
		public VirtualTextureConfig Config { get; } = config;

		// Token: 0x17000077 RID: 119
		// (get) Token: 0x060002FE RID: 766 RVA: 0x00017A50 File Offset: 0x00015C50
		public Material ScaledMaterial { get; } = material;

		// Token: 0x17000078 RID: 120
		// (get) Token: 0x060002FF RID: 767 RVA: 0x00017A58 File Offset: 0x00015C58
		public CelestialBody Body { get; } = body;

		// Token: 0x17000079 RID: 121
		// (get) Token: 0x06000300 RID: 768 RVA: 0x00017A60 File Offset: 0x00015C60
		// (set) Token: 0x06000301 RID: 769 RVA: 0x00017A68 File Offset: 0x00015C68
		public bool Loaded { get; private set; }

		// Token: 0x1700007A RID: 122
		// (get) Token: 0x06000302 RID: 770 RVA: 0x00017A71 File Offset: 0x00015C71
		// (set) Token: 0x06000303 RID: 771 RVA: 0x00017A79 File Offset: 0x00015C79
		public bool IsLoading { get; private set; }

		// Token: 0x1700007B RID: 123
		// (get) Token: 0x06000304 RID: 772 RVA: 0x00017A82 File Offset: 0x00015C82
		// (set) Token: 0x06000305 RID: 773 RVA: 0x00017A8A File Offset: 0x00015C8A
		public TileCache Cache { get; private set; }

		// Token: 0x1700007C RID: 124
		// (get) Token: 0x06000306 RID: 774 RVA: 0x00017A93 File Offset: 0x00015C93
		// (set) Token: 0x06000307 RID: 775 RVA: 0x00017A9B File Offset: 0x00015C9B
		public int LeafSetVersion { get; private set; }

		/// <summary>Capped at canonical unless <c>scaledWebStreaming</c> is on.</summary>
		// Token: 0x1700007D RID: 125
		// (get) Token: 0x06000308 RID: 776 RVA: 0x00017AA4 File Offset: 0x00015CA4
		public int StreamingMaxLevel
		{
			get
			{
				return this.Config.ScaledStreamingMaxLevel;
			}
		}

		// Token: 0x1700007E RID: 126
		// (get) Token: 0x06000309 RID: 777 RVA: 0x00017AB1 File Offset: 0x00015CB1
		private string StreamKey
		{
			get
			{
				return this.Name + " (scaled)";
			}
		}

		/// <summary>Bootstrap pinned levels over several frames. Drive with the owner's StartCoroutine.</summary>
		// Token: 0x0600030A RID: 778 RVA: 0x00017AC3 File Offset: 0x00015CC3
		public IEnumerator LoadAsync()
		{
			MirageScaledBody.<LoadAsync>d__45 <LoadAsync>d__ = new MirageScaledBody.<LoadAsync>d__45(0);
			<LoadAsync>d__.<>4__this = this;
			return <LoadAsync>d__;
		}

		// Token: 0x0600030B RID: 779 RVA: 0x00017AD4 File Offset: 0x00015CD4
		private void AddLayer(VTLayer layer, string uniformPrefix, bool linear)
		{
			bool flag = this.Config.HasLayer(layer);
			if (flag)
			{
				this.Cache.AddLayer(layer, uniformPrefix, this.Config.CreateSource(layer, linear), int.MaxValue);
			}
		}

		/// <summary>Reset a load interrupted by a scene transition.</summary>
		// Token: 0x0600030C RID: 780 RVA: 0x00017B14 File Offset: 0x00015D14
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

		/// <summary>Register for fine streaming.</summary>
		// Token: 0x0600030D RID: 781 RVA: 0x00017B8C File Offset: 0x00015D8C
		public void ResumeStreaming()
		{
			bool flag = !this.Loaded || this.streaming;
			if (!flag)
			{
				TileStreamingManager.RegisterBody(this.StreamKey, this);
				this.streaming = true;
			}
		}

		/// <summary>Stop fine streaming but keep the pinned floor resident.</summary>
		// Token: 0x0600030E RID: 782 RVA: 0x00017BC8 File Offset: 0x00015DC8
		public void PauseStreaming()
		{
			bool flag = !this.streaming;
			if (!flag)
			{
				TileStreamingManager.UnregisterBody(this.StreamKey);
				this.streaming = false;
			}
		}

		// Token: 0x0600030F RID: 783 RVA: 0x00017BF8 File Offset: 0x00015DF8
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

		/// <summary>Always false — the raycast grid already derives screen-space levels directly.</summary>
		// Token: 0x06000310 RID: 784 RVA: 0x00017C70 File Offset: 0x00015E70
		public bool TryGetLevelContext(out VTLevelContext ctx)
		{
			ctx = default(VTLevelContext);
			return false;
		}

		// Token: 0x06000311 RID: 785 RVA: 0x00017C8C File Offset: 0x00015E8C
		public void EnumerateVisibleLeafQuads(List<LeafQuad> output)
		{
			int frame = Time.frameCount;
			bool flag = frame - this.cachedFrame >= 8;
			if (flag)
			{
				using (MirageScaledBody.s_ScaledRecomputeMarker.Auto())
				{
					this.cachedLeaves.Clear();
					this.RecomputeVisibleLeaves(this.cachedLeaves);
				}
				this.cachedFrame = frame;
				int leafSetVersion = this.LeafSetVersion;
				this.LeafSetVersion = leafSetVersion + 1;
			}
			using (MirageScaledBody.s_ScaledCopyMarker.Auto())
			{
				output.AddRange(this.cachedLeaves);
			}
		}

		/// <summary>The camera that renders scaled bodies in the current scene.</summary>
		// Token: 0x06000312 RID: 786 RVA: 0x00017D54 File Offset: 0x00015F54
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

		/// <summary>Raycast a screen grid at the scaled sphere, emitting tiles at distance-derived levels.</summary>
		// Token: 0x06000313 RID: 787 RVA: 0x00017DAC File Offset: 0x00015FAC
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
						int pinned = 0;
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
									int level = MirageScaledBody.LevelForDistance(Vector3.Distance(camPos, hit), r, tanHalfFov, screenH, this.Config.tileSize, maxLevel);
									bool flag5 = level <= pinned;
									if (!flag5)
									{
										Vector3 bodyDir = worldToBody * (hit - center);
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

		// Token: 0x06000314 RID: 788 RVA: 0x0001800C File Offset: 0x0001620C
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

		/// <summary>Nearest ray-sphere intersection ahead of the origin.</summary>
		// Token: 0x06000315 RID: 789 RVA: 0x0001807C File Offset: 0x0001627C
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

		// Token: 0x040002F1 RID: 753
		private const int GridSamples = 24;

		// Token: 0x040002F2 RID: 754
		private const int ScaledEnumInterval = 8;

		// Token: 0x040002F3 RID: 755
		private const int ScaledLevelBias = 1;

		// Token: 0x040002F4 RID: 756
		private bool streaming;

		// Token: 0x040002F5 RID: 757
		private bool pendingUnload;

		// Token: 0x040002F6 RID: 758
		private int loadGeneration;

		// Token: 0x040002F7 RID: 759
		private readonly List<LeafQuad> cachedLeaves = new List<LeafQuad>();

		// Token: 0x040002F8 RID: 760
		private int cachedFrame = -1000;

		// Token: 0x040002F9 RID: 761
		private static readonly HashSet<long> s_Seen = new HashSet<long>();

		// Token: 0x040002FA RID: 762
		private static readonly int s_PlanetRadiusId = Shader.PropertyToID("_PlanetRadius");

		// Token: 0x040002FB RID: 763
		private static readonly ProfilerMarker s_ScaledRecomputeMarker = new ProfilerMarker("Mirage.VT.Leaves.ScaledRecompute");

		// Token: 0x040002FC RID: 764
		private static readonly ProfilerMarker s_ScaledCopyMarker = new ProfilerMarker("Mirage.VT.Leaves.ScaledCopyCached");
	}
}
