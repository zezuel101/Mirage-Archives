using System;
using System.Linq;
using System.Threading;
using Mirage.KopernicusMods;
using Mirage.VirtualTexture;
using Mirage.WebIngest;

namespace Mirage.Runtime
{
	/// <summary>Wires bake-as-you-fly onto opted-in bodies. Failure falls back to canonical tiles.</summary>
	// Token: 0x02000072 RID: 114
	internal static class WebIngestActivator
	{
		// Token: 0x06000364 RID: 868 RVA: 0x00019D60 File Offset: 0x00017F60
		public static void TryEnable(string name, VirtualTextureConfig cfg, CelestialBody body)
		{
			bool flag = !cfg.UseWebIngest;
			if (!flag)
			{
				PQSMod_MirageTerrain mod = body.pqsController.GetComponentsInChildren<PQSMod_MirageTerrain>(true).FirstOrDefault<PQSMod_MirageTerrain>();
				bool flag2 = mod == null;
				if (flag2)
				{
					MirageDebug.LogError("WebIngestActivator: '" + name + "' asks for webIngest but has no MirageTerrain PQSMod, so the R16 height mapping (deformity/offset) is unknown. Baking would write terrain at wrong altitudes — ingest disabled for this body.");
				}
				else
				{
					try
					{
						MainThreadFetch.Install();
						ImageryProvider provider = ImageryProvider.ByName(cfg.imageryProvider);
						CubeTileBaker.GmrtFetchAsync gmrtFetch = WebIngestActivator.CreateGmrtFetch(cfg);
						WorldCoverSource.RangeFetch worldCoverFetch = WebIngestActivator.CreateWorldCoverFetch(cfg);
						CubeTileBaker baker = WebIngestActivator.BuildBaker(name, cfg, body, mod, provider, gmrtFetch, worldCoverFetch);
						TileStreamingManager.EnableIngest(name, baker, (long)MirageSettings.WebDiskCapMB * 1024L * 1024L, MirageSettings.WebIngestConcurrency);
						WebIngestActivator.LogActivation(name, body, mod, provider, cfg, worldCoverFetch != null);
					}
					catch (Exception e)
					{
						MirageDebug.LogError(string.Concat(new string[]
						{
							"MirageRuntime: could not enable web ingest for '",
							name,
							"': ",
							e.Message,
							". Continuing with canonical tiles only."
						}));
					}
				}
			}
		}

		// Token: 0x06000365 RID: 869 RVA: 0x00019E70 File Offset: 0x00018070
		private static CubeTileBaker.GmrtFetchAsync CreateGmrtFetch(VirtualTextureConfig cfg)
		{
			bool flag = !cfg.bathymetry;
			CubeTileBaker.GmrtFetchAsync result;
			if (flag)
			{
				result = null;
			}
			else
			{
				GmrtFetcher.Install();
				result = ((double w, double e, double s, double n, int res, CancellationToken ct) => GmrtFetcher.FetchAsync(GmrtElevation.BuildUrl(w, e, s, n, res), ct));
			}
			return result;
		}

		// Token: 0x06000366 RID: 870 RVA: 0x00019EB8 File Offset: 0x000180B8
		private static WorldCoverSource.RangeFetch CreateWorldCoverFetch(VirtualTextureConfig cfg)
		{
			bool useWorldCover = MirageSettings.WorldCoverWater && cfg.waterMask && cfg.HasLayer(VTLayer.Color);
			bool flag = !useWorldCover;
			WorldCoverSource.RangeFetch result;
			if (flag)
			{
				result = null;
			}
			else
			{
				WorldCoverFetcher.Install();
				result = ((string url, long from, long to, CancellationToken ct) => WorldCoverFetcher.FetchAsync(url, from, to, ct));
			}
			return result;
		}

		// Token: 0x06000367 RID: 871 RVA: 0x00019F18 File Offset: 0x00018118
		private static CubeTileBaker BuildBaker(string name, VirtualTextureConfig cfg, CelestialBody body, PQSMod_MirageTerrain mod, ImageryProvider provider, CubeTileBaker.GmrtFetchAsync gmrtFetch, WorldCoverSource.RangeFetch worldCoverFetch)
		{
			return new CubeTileBaker((ImageryProvider p, int z, int x, int y, CancellationToken ct) => MainThreadFetch.FetchAsync(p, name, z, x, y, ct), provider, cfg.tileSize, cfg.borderPx, body.Radius, mod.deformity, mod.offset, cfg.HasLayer(VTLayer.Color), cfg.HasLayer(VTLayer.Height), cfg.HasLayer(VTLayer.Normal), null, 0.0, gmrtFetch, cfg.heightDespike, cfg.ColorGradeParams, cfg.waterMask, worldCoverFetch, MirageSettings.WorldCoverUrl, MirageSettings.WorldCoverPrefix, MirageSettings.WorldCoverSeaSinkM, MirageSettings.WorldCoverSeaSinkMaxM, MirageSettings.SeaFlattenMin, MirageSettings.SeaFlattenMax, MirageSettings.SeaFlattenSlope, MirageSettings.WaterMaskBlurPx);
		}

		// Token: 0x06000368 RID: 872 RVA: 0x00019FC0 File Offset: 0x000181C0
		private static void LogActivation(string name, CelestialBody body, PQSMod_MirageTerrain mod, ImageryProvider provider, VirtualTextureConfig cfg, bool usingWorldCover)
		{
			string bathymetry = cfg.bathymetry ? "\n  Bathymetry: GMRT — Global Multi-Resolution Topography (GMRT) Synthesis, Ryan et al. 2009, Lamont-Doherty Earth Observatory / NSF. Licensed CC BY 4.0." : "";
			string waterMask = usingWorldCover ? "\n  Water mask: © ESA WorldCover project 2021 / Contributors (ESA WorldCover 10 m 2021 v200). Licensed CC BY 4.0." : "";
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
				bathymetry,
				waterMask
			}));
		}
	}
}
