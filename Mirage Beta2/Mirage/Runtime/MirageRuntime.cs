using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using Mirage.Configuration;
using Mirage.KopernicusMods;
using Mirage.Subdivision;
using Mirage.VirtualTexture;
using Mirage.WebIngest;
using Unity.Profiling;
using UnityEngine;

namespace Mirage.Runtime
{
	/// <summary>
	/// Mirage's flight/KSC-scene driver. Three responsibilities per frame:
	///
	/// 1. <b>Body lifecycle</b> — tracks <c>FlightGlobals.currentMainBody</c> and
	///    switches the active <see cref="T:Mirage.Runtime.MirageBody" /> when it changes. Looks the
	///    new body up in <see cref="T:Mirage.Configuration.MirageBodyRegistry" />; bodies without a
	///    registered VT config are simply ignored (Mirage isn't responsible for
	///    them).
	///
	/// 2. <b>Planet-frame globals</b> — pushes <c>_PlanetOrigin</c> and
	///    <c>_PlanetRadius</c> for the current main body so the shader helpers
	///    (<c>ApplyGPUHeightmapDisplacement</c>, <c>TrySampleVTWorldNormal</c>)
	///    have correct values. Set every frame because <c>currentMainBody.transform.position</c>
	///    follows the floating origin.
	///
	/// 3. <b>Streaming tick</b> — calls <see cref="M:Mirage.VirtualTexture.TileStreamingManager.Update(System.Int32)" />
	///    so the per-body streaming state advances even when the host mod (Parallax,
	///    etc.) doesn't tick it itself. Idempotent — duplicate ticks just process
	///    one extra batch per frame; no correctness issue.
	///
	/// Lives only in flight + KSC because that's where VT terrain renders; main
	/// menu / tracking station don't need it.
	/// </summary>
	// Token: 0x02000061 RID: 97
	[KSPAddon(-3, false)]
	public class MirageRuntime : MonoBehaviour
	{
		// Token: 0x060002CF RID: 719 RVA: 0x000177C8 File Offset: 0x000159C8
		private void Awake()
		{
			Shader.SetGlobalVector(MirageRuntime.s_MirageSunDirID, new Vector4(0f, 1f, 0f, 0f));
			Shader.SetGlobalVector(MirageRuntime.s_MirageSunColorID, Vector4.one);
			MirageSettings.PushShaderGlobals();
			Delegate onPreRender = Camera.onPreRender;
			Camera.CameraCallback b;
			if ((b = MirageRuntime.<>O.<0>__OnCameraPreRender) == null)
			{
				b = (MirageRuntime.<>O.<0>__OnCameraPreRender = new Camera.CameraCallback(MirageRuntime.OnCameraPreRender));
			}
			Camera.onPreRender = (Camera.CameraCallback)Delegate.Combine(onPreRender, b);
		}

		// Token: 0x060002D0 RID: 720 RVA: 0x00017840 File Offset: 0x00015A40
		private void Update()
		{
			CelestialBody mainBody = FlightGlobals.currentMainBody;
			using (MirageRuntime.s_PushGlobalsMarker.Auto())
			{
				MirageRuntime.PushPlanetGlobals(mainBody);
			}
			Shader.SetGlobalFloat("_ShadowCascadeFar", QualitySettings.shadowDistance);
			using (MirageRuntime.s_EnsureBodyMarker.Auto())
			{
				MirageRuntime.EnsureActiveBody(mainBody);
			}
			using (MirageRuntime.s_SubdivisionMarker.Auto())
			{
				SubdivisionRuntime.Update();
			}
			using (MirageRuntime.s_StreamingMarker.Auto())
			{
				TileStreamingManager.Update(Time.frameCount);
			}
			MirageRuntime.RefreshAtmosphere();
			MirageRuntime.MaintainAtmosphere();
		}

		// Token: 0x060002D1 RID: 721 RVA: 0x00017944 File Offset: 0x00015B44
		private static void OnCameraPreRender(Camera cam)
		{
			bool flag = MirageRuntime.s_SunLight == null;
			if (flag)
			{
				Light[] lights = Object.FindObjectsOfType<Light>();
				Light light;
				if ((light = lights.FirstOrDefault((Light x) => x.name == "SunLight")) == null)
				{
					light = (from l in lights
					where l.type == 1
					orderby l.intensity descending
					select l).FirstOrDefault<Light>();
				}
				MirageRuntime.s_SunLight = light;
			}
			bool flag2 = MirageRuntime.s_SunLight != null;
			if (flag2)
			{
				Shader.SetGlobalVector(MirageRuntime.s_MirageSunDirID, -MirageRuntime.s_SunLight.transform.forward);
				Shader.SetGlobalVector(MirageRuntime.s_MirageSunColorID, MirageRuntime.s_SunLight.color);
			}
		}

		// Token: 0x060002D2 RID: 722 RVA: 0x00017A37 File Offset: 0x00015C37
		private void OnDestroy()
		{
			Delegate onPreRender = Camera.onPreRender;
			Camera.CameraCallback value;
			if ((value = MirageRuntime.<>O.<0>__OnCameraPreRender) == null)
			{
				value = (MirageRuntime.<>O.<0>__OnCameraPreRender = new Camera.CameraCallback(MirageRuntime.OnCameraPreRender));
			}
			Camera.onPreRender = (Camera.CameraCallback)Delegate.Remove(onPreRender, value);
			MirageRuntime.DeactivateCurrent();
			SubdivisionRuntime.Clear();
		}

		// Token: 0x060002D3 RID: 723 RVA: 0x00017A78 File Offset: 0x00015C78
		private static void PushPlanetGlobals(CelestialBody body)
		{
			bool flag = body == null;
			if (!flag)
			{
				Vector3 origin = body.transform.position;
				Shader.SetGlobalVector(MirageRuntime.s_PlanetOriginID, origin);
				Shader.SetGlobalFloat(MirageRuntime.s_PlanetRadiusID, (float)body.Radius);
				Shader.SetGlobalMatrix(MirageRuntime.s_PlanetRotationID, Matrix4x4.Rotate(Quaternion.Inverse(body.transform.rotation)));
				Shader.SetGlobalVector(MirageRuntime.s_TerrainShaderOffsetID, FloatingOrigin.TerrainShaderOffset);
				double originAltitude = origin.magnitude - body.Radius;
				Shader.SetGlobalFloat(MirageRuntime.s_OriginAltitudeID, (float)originAltitude);
			}
		}

		// Token: 0x060002D4 RID: 724 RVA: 0x00017B24 File Offset: 0x00015D24
		private static void EnsureActiveBody(CelestialBody mainBody)
		{
			string targetName = (mainBody != null) ? mainBody.name : null;
			MirageBody mirageBody = MirageRuntime.s_ActiveBody;
			string activeName = (mirageBody != null) ? mirageBody.SphereName : null;
			bool flag = targetName == activeName;
			if (!flag)
			{
				MirageRuntime.DeactivateCurrent();
				bool flag2 = mainBody == null;
				if (!flag2)
				{
					VirtualTextureConfig cfg;
					bool flag3 = !MirageBodyRegistry.TryGetConfig(targetName, out cfg);
					if (!flag3)
					{
						bool flag4 = mainBody.pqsController == null;
						if (!flag4)
						{
							try
							{
								MirageRuntime.s_ActiveBody = new MirageBody(mainBody, cfg);
								TileStreamingManager.RegisterBody(targetName, MirageRuntime.s_ActiveBody);
								MirageRuntime.TryEnableWebIngest(targetName, cfg, mainBody);
								MirageRuntime.BindCachesToPqsMaterials(MirageRuntime.s_ActiveBody, mainBody.pqsController);
								MirageRuntime.ApplyTessellation(cfg, mainBody.pqsController);
								MirageDebug.Log("MirageRuntime: activated body '" + targetName + "'");
							}
							catch (Exception e)
							{
								MirageDebug.LogError("MirageRuntime: failed to activate '" + targetName + "': " + e.Message);
								MirageRuntime.s_ActiveBody = null;
							}
						}
					}
				}
			}
		}

		/// <summary>
		/// Turn on bake-as-you-fly for this body, if its config opted in (§8).
		///
		/// The height mapping is read off <see cref="T:Mirage.KopernicusMods.PQSMod_MirageTerrain" /> rather than configured separately,
		/// and that is the point: baked R16 must mean the same metres as canonical R16
		/// (<c>metres = offset + deformity·(R16/65535)</c>), because the GPU displacement and the CPU collision
		/// mesh both read tiles through the same key without knowing which tier served them. A second copy of
		/// those constants could drift, and the symptom would be craft sinking into hills — not a crash.
		/// Confirmed against the shipped archive by the packer's <c>--test-height</c> least-squares fit.
		///
		/// Failures here are never fatal: the body simply renders from canonical, exactly as before.
		/// </summary>
		// Token: 0x060002D5 RID: 725 RVA: 0x00017C3C File Offset: 0x00015E3C
		private static void TryEnableWebIngest(string name, VirtualTextureConfig cfg, CelestialBody body)
		{
			bool flag = !cfg.UseWebIngest;
			if (!flag)
			{
				PQSMod_MirageTerrain mod = body.pqsController.GetComponentsInChildren<PQSMod_MirageTerrain>(true).FirstOrDefault<PQSMod_MirageTerrain>();
				bool flag2 = mod == null;
				if (flag2)
				{
					MirageDebug.LogError("MirageRuntime: '" + name + "' asks for webIngest but has no MirageTerrain PQSMod, so the R16 height mapping (deformity/offset) is unknown. Baking would write terrain at wrong altitudes — ingest disabled for this body.");
				}
				else
				{
					try
					{
						MainThreadFetch.Install();
						ImageryProvider provider = ImageryProvider.ByName(cfg.imageryProvider);
						CubeTileBaker.GmrtFetchAsync gmrtFetch = null;
						bool bathymetry = cfg.bathymetry;
						if (bathymetry)
						{
							GmrtFetcher.Install();
							gmrtFetch = ((double w, double e, double s, double n, int res, CancellationToken ct) => GmrtFetcher.FetchAsync(GmrtElevation.BuildUrl(w, e, s, n, res), ct));
						}
						WorldCoverSource.RangeFetch worldCoverFetch = null;
						bool useWorldCover = MirageSettings.WorldCoverWater && cfg.waterMask && cfg.HasLayer(VTLayer.Color);
						bool flag3 = useWorldCover;
						if (flag3)
						{
							WorldCoverFetcher.Install();
							worldCoverFetch = ((string url, long from, long to, CancellationToken ct) => WorldCoverFetcher.FetchAsync(url, from, to, ct));
						}
						CubeTileBaker baker = new CubeTileBaker((ImageryProvider p, int z, int x, int y, CancellationToken ct) => MainThreadFetch.FetchAsync(p, name, z, x, y, ct), provider, cfg.tileSize, cfg.borderPx, body.Radius, mod.deformity, mod.offset, cfg.HasLayer(VTLayer.Color), cfg.HasLayer(VTLayer.Height), cfg.HasLayer(VTLayer.Normal), null, 0.0, gmrtFetch, cfg.heightDespike, cfg.ColorGradeParams, cfg.waterMask, worldCoverFetch, MirageSettings.WorldCoverUrl, MirageSettings.WorldCoverPrefix, MirageSettings.WorldCoverSeaSinkM, MirageSettings.WorldCoverSeaSinkMaxM, MirageSettings.SeaFlattenMin, MirageSettings.SeaFlattenMax, MirageSettings.SeaFlattenSlope, MirageSettings.WaterMaskBlurPx);
						TileStreamingManager.EnableIngest(name, baker, (long)MirageSettings.WebDiskCapMB * 1024L * 1024L, MirageSettings.WebIngestConcurrency);
						MirageDebug.Log(string.Concat(new string[]
						{
							"MirageRuntime: web ingest active for '",
							name,
							"' via ",
							provider.Name,
							string.Format(" (deformity={0} offset={1}, r={2:F0} m).\n", mod.deformity, mod.offset, body.Radius),
							"  Imagery: ",
							provider.Attribution,
							"\n  Elevation: ",
							ImageryProvider.TerrariumDem.Attribution,
							cfg.bathymetry ? "\n  Bathymetry: GMRT — Global Multi-Resolution Topography (GMRT) Synthesis, Ryan et al. 2009, Lamont-Doherty Earth Observatory / NSF. Licensed CC BY 4.0." : "",
							useWorldCover ? "\n  Water mask: © ESA WorldCover project 2021 / Contributors (ESA WorldCover 10 m 2021 v200). Licensed CC BY 4.0." : ""
						}));
					}
					catch (Exception e)
					{
						Exception e2;
						MirageDebug.LogError(string.Concat(new string[]
						{
							"MirageRuntime: could not enable web ingest for '",
							name,
							"': ",
							e2.Message,
							". Continuing with canonical tiles only."
						}));
					}
				}
			}
		}

		// Token: 0x060002D6 RID: 726 RVA: 0x00017F14 File Offset: 0x00016114
		private static void ApplyTessellation(VirtualTextureConfig cfg, PQS pqs)
		{
			float maxTess = cfg.ResolveMaxTessellation(2);
			float maxTessRange = cfg.ResolveMaxTessellationRange();
			float edgeLength = cfg.tessellationEdgeLength;
			MirageRuntime.SetTessellationOnMaterial(pqs.surfaceMaterial, maxTess, maxTessRange, edgeLength);
			MirageRuntime.SetTessellationOnMaterial(pqs.lowQualitySurfaceMaterial, maxTess, maxTessRange, edgeLength);
			string rangeDesc = (maxTessRange > 0f) ? string.Format("{0:0}m", maxTessRange) : "off";
			MirageDebug.Log(string.Format("  Tessellation: max={0} ({1}) ", maxTess, (cfg.maxTessellation > 0) ? "config" : "auto") + string.Format("edge={0:0.#}px cutoff={1}", edgeLength, rangeDesc));
		}

		// Token: 0x060002D7 RID: 727 RVA: 0x00017FB7 File Offset: 0x000161B7
		private static bool IsMirageTerrainMaterial(Material mat)
		{
			return ParallaxShaderLoader.UsesSameShader(mat) || MiragePqsShaderLoader.UsesSameShader(mat);
		}

		// Token: 0x060002D8 RID: 728 RVA: 0x00017FCC File Offset: 0x000161CC
		private static void SetTessellationOnMaterial(Material mat, float maxTess, float maxTessRange, float edgeLength)
		{
			bool flag = mat == null || !MirageRuntime.IsMirageTerrainMaterial(mat);
			if (!flag)
			{
				mat.SetFloat(MirageRuntime.s_MaxTessellationID, maxTess);
				mat.SetFloat(MirageRuntime.s_MaxTessellationRangeID, maxTessRange);
				bool flag2 = edgeLength > 0f;
				if (flag2)
				{
					mat.SetFloat(MirageRuntime.s_TessellationEdgeLengthID, edgeLength);
				}
			}
		}

		// Token: 0x060002D9 RID: 729 RVA: 0x00018028 File Offset: 0x00016228
		private static void BindCachesToPqsMaterials(MirageBody body, PQS pqs)
		{
			MirageRuntime.BindCachesToMaterial(body, pqs.surfaceMaterial);
			MirageRuntime.BindCachesToMaterial(body, pqs.lowQualitySurfaceMaterial);
		}

		// Token: 0x060002DA RID: 730 RVA: 0x00018048 File Offset: 0x00016248
		private static void BindCachesToMaterial(MirageBody body, Material mat)
		{
			bool flag = !MirageRuntime.IsMirageTerrainMaterial(mat);
			if (!flag)
			{
				TileCache cache = body.Cache;
				if (cache != null)
				{
					cache.BindToMaterial(mat);
				}
				MirageRuntime.BindAtmosphereToMaterial(body, mat);
			}
		}

		// Token: 0x060002DB RID: 731 RVA: 0x00018080 File Offset: 0x00016280
		private static void BindAtmosphereToMaterial(MirageBody body, Material mat)
		{
			bool flag = mat == null || !MirageRuntime.IsMirageTerrainMaterial(mat);
			if (!flag)
			{
				bool flag2 = MirageScattererBridge.TryBindAtmosphere(body.CelestialBody.name, mat);
				if (flag2)
				{
					mat.EnableKeyword("MIRAGE_ATMOSPHERE");
					MirageRuntime.s_AtmosphereActive = true;
					MirageRuntime.LogAtmosphereBindOnce(body, mat);
				}
				else
				{
					mat.DisableKeyword("MIRAGE_ATMOSPHERE");
					MirageRuntime.s_AtmosphereActive = false;
				}
			}
		}

		// Token: 0x060002DC RID: 732 RVA: 0x000180F0 File Offset: 0x000162F0
		private static void LogAtmosphereBindOnce(MirageBody body, Material mat)
		{
			bool flag = MirageRuntime.s_AtmosphereBindLogged;
			if (!flag)
			{
				MirageRuntime.s_AtmosphereBindLogged = true;
				Texture atlas = mat.GetTexture("AtmosphereAtlas");
				MirageDebug.Log(string.Concat(new string[]
				{
					"MirageAtmosphere[",
					body.CelestialBody.name,
					"] bound: atlas=",
					(atlas != null) ? string.Format("{0}x{1}", atlas.width, atlas.height) : "NULL",
					" ",
					string.Format("Rg={0} betaR={1} HR={2} ", mat.GetFloat("Rg"), mat.GetVector("betaR"), mat.GetFloat("HR")),
					string.Format("sunColor={0} exposure={1}", mat.GetColor("_sunColor"), mat.GetFloat("_AtmosphereExposure"))
				}));
			}
		}

		// Token: 0x060002DD RID: 733 RVA: 0x000181F4 File Offset: 0x000163F4
		private static void MaintainAtmosphere()
		{
			bool flag = !MirageRuntime.s_AtmosphereActive || MirageRuntime.s_ActiveBody == null;
			if (!flag)
			{
				PQS pqs = MirageRuntime.s_ActiveBody.CelestialBody.pqsController;
				bool flag2 = pqs == null;
				if (!flag2)
				{
					MirageRuntime.ReassertKeyword(pqs.surfaceMaterial);
					MirageRuntime.ReassertKeyword(pqs.lowQualitySurfaceMaterial);
				}
			}
		}

		// Token: 0x060002DE RID: 734 RVA: 0x00018250 File Offset: 0x00016450
		private static void ReassertKeyword(Material mat)
		{
			bool flag = mat != null && !mat.IsKeywordEnabled("MIRAGE_ATMOSPHERE");
			if (flag)
			{
				mat.EnableKeyword("MIRAGE_ATMOSPHERE");
			}
		}

		// Token: 0x060002DF RID: 735 RVA: 0x00018288 File Offset: 0x00016488
		private static void RefreshAtmosphere()
		{
			bool flag = MirageRuntime.s_ActiveBody == null || Time.frameCount % 120 != 0;
			if (!flag)
			{
				PQS pqs = MirageRuntime.s_ActiveBody.CelestialBody.pqsController;
				bool flag2 = pqs == null;
				if (!flag2)
				{
					MirageRuntime.BindAtmosphereToMaterial(MirageRuntime.s_ActiveBody, pqs.surfaceMaterial);
					MirageRuntime.BindAtmosphereToMaterial(MirageRuntime.s_ActiveBody, pqs.lowQualitySurfaceMaterial);
				}
			}
		}

		// Token: 0x060002E0 RID: 736 RVA: 0x000182F0 File Offset: 0x000164F0
		private static void DeactivateCurrent()
		{
			bool flag = MirageRuntime.s_ActiveBody == null;
			if (!flag)
			{
				string name = MirageRuntime.s_ActiveBody.SphereName;
				TileStreamingManager.UnregisterBody(name);
				MirageRuntime.s_ActiveBody.Dispose();
				MirageRuntime.s_ActiveBody = null;
				MirageRuntime.s_AtmosphereBindLogged = false;
				MirageRuntime.s_AtmosphereActive = false;
				MirageDebug.Log("MirageRuntime: deactivated body '" + name + "'");
			}
		}

		// Token: 0x040002A4 RID: 676
		private static readonly int s_PlanetOriginID = Shader.PropertyToID("_PlanetOrigin");

		// Token: 0x040002A5 RID: 677
		private static readonly int s_PlanetRadiusID = Shader.PropertyToID("_PlanetRadius");

		// Token: 0x040002A6 RID: 678
		private static readonly int s_PlanetRotationID = Shader.PropertyToID("_PlanetRotation");

		// Token: 0x040002A7 RID: 679
		private static readonly int s_OriginAltitudeID = Shader.PropertyToID("_OriginAltitude");

		// Token: 0x040002A8 RID: 680
		private static readonly int s_TerrainShaderOffsetID = Shader.PropertyToID("_TerrainShaderOffset");

		// Token: 0x040002A9 RID: 681
		private static readonly int s_MaxTessellationID = Shader.PropertyToID("_MaxTessellation");

		// Token: 0x040002AA RID: 682
		private static readonly int s_MaxTessellationRangeID = Shader.PropertyToID("_MaxTessellationRange");

		// Token: 0x040002AB RID: 683
		private static readonly int s_TessellationEdgeLengthID = Shader.PropertyToID("_TessellationEdgeLength");

		// Token: 0x040002AC RID: 684
		private static readonly int s_MirageSunDirID = Shader.PropertyToID("_MirageSunDir");

		// Token: 0x040002AD RID: 685
		private static readonly int s_MirageSunColorID = Shader.PropertyToID("_MirageSunColor");

		// Token: 0x040002AE RID: 686
		private static readonly ProfilerMarker s_PushGlobalsMarker = new ProfilerMarker("Mirage.PushPlanetGlobals");

		// Token: 0x040002AF RID: 687
		private static readonly ProfilerMarker s_EnsureBodyMarker = new ProfilerMarker("Mirage.EnsureActiveBody");

		// Token: 0x040002B0 RID: 688
		private static readonly ProfilerMarker s_SubdivisionMarker = new ProfilerMarker("Mirage.SubdivisionRuntime.Update");

		// Token: 0x040002B1 RID: 689
		private static readonly ProfilerMarker s_StreamingMarker = new ProfilerMarker("Mirage.TileStreamingManager.Update");

		// Token: 0x040002B2 RID: 690
		private static MirageBody s_ActiveBody;

		// Token: 0x040002B3 RID: 691
		private static Light s_SunLight;

		// Token: 0x040002B4 RID: 692
		private static bool s_AtmosphereActive;

		// Token: 0x040002B5 RID: 693
		private static bool s_AtmosphereBindLogged;

		// Token: 0x040002B6 RID: 694
		private const int AtmosphereRefreshInterval = 120;

		// Token: 0x020000C5 RID: 197
		[CompilerGenerated]
		private static class <>O
		{
			// Token: 0x04000534 RID: 1332
			public static Camera.CameraCallback <0>__OnCameraPreRender;
		}
	}
}
