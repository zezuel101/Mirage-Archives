using System;

namespace Mirage.WebIngest
{
	// Token: 0x0200001B RID: 27
	public sealed class ImageryProvider
	{
		/// <summary>Stable identity, used as a cache namespace. Names change when the underlying layer changes
		/// (see s2cloudless2024) precisely so old and new imagery can never mix in one cache and put a seam at
		/// every boundary between them.</summary>
		// Token: 0x1700001A RID: 26
		// (get) Token: 0x0600009D RID: 157 RVA: 0x00006AF0 File Offset: 0x00004CF0
		public string Name { get; }

		/// <summary>URL template — {0} = z, {1} = y, {2} = x. Note the y/x ordering, matching WMTS row/col.</summary>
		// Token: 0x1700001B RID: 27
		// (get) Token: 0x0600009E RID: 158 RVA: 0x00006AF8 File Offset: 0x00004CF8
		public string UrlTemplate { get; }

		/// <summary>Deepest zoom the provider actually serves. Past this, <see cref="M:Mirage.WebIngest.MercatorTileMath.LevelToZoom(System.Int32,System.Int32)" />
		/// clamps and the source is upsampled rather than sharper.</summary>
		// Token: 0x1700001C RID: 28
		// (get) Token: 0x0600009F RID: 159 RVA: 0x00006B00 File Offset: 0x00004D00
		public int MaxZoom { get; }

		/// <summary>Attribution string that must be surfaced to the user when this provider's imagery is shown.
		/// For CC BY-NC-SA sources this is a licence condition, not a courtesy.</summary>
		// Token: 0x1700001D RID: 29
		// (get) Token: 0x060000A0 RID: 160 RVA: 0x00006B08 File Offset: 0x00004D08
		public string Attribution { get; }

		/// <summary>True if the licence forbids redistributing baked tiles. Mirage bakes client-side and never
		/// ships baked imagery (§11 decision 5: "client-generate only"), which is what keeps the share-alike and
		/// non-commercial terms of EOX's CC BY-NC-SA out of Sol's distribution entirely.</summary>
		// Token: 0x1700001E RID: 30
		// (get) Token: 0x060000A1 RID: 161 RVA: 0x00006B10 File Offset: 0x00004D10
		public bool RestrictsRedistribution { get; }

		/// <summary>The container this endpoint serves when it HAS data. Anything else coming back is a
		/// container mismatch — see <see cref="T:Mirage.WebIngest.TileImageFormat" />.</summary>
		// Token: 0x1700001F RID: 31
		// (get) Token: 0x060000A2 RID: 162 RVA: 0x00006B18 File Offset: 0x00004D18
		public TileImageFormat Format { get; }

		// Token: 0x060000A3 RID: 163 RVA: 0x00006B20 File Offset: 0x00004D20
		private ImageryProvider(string name, string urlTemplate, int maxZoom, string attribution, bool restrictsRedistribution, TileImageFormat format = TileImageFormat.Jpeg)
		{
			this.Name = name;
			this.UrlTemplate = urlTemplate;
			this.MaxZoom = maxZoom;
			this.Attribution = attribution;
			this.RestrictsRedistribution = restrictsRedistribution;
			this.Format = format;
		}

		// Token: 0x060000A4 RID: 164 RVA: 0x00006B57 File Offset: 0x00004D57
		public string BuildUrl(int z, int x, int y)
		{
			return string.Format(this.UrlTemplate, z, y, x);
		}

		// Token: 0x17000020 RID: 32
		// (get) Token: 0x060000A5 RID: 165 RVA: 0x00006B76 File Offset: 0x00004D76
		public static ImageryProvider Default
		{
			get
			{
				return ImageryProvider.S2Cloudless2024;
			}
		}

		/// <summary>Resolve by name (the stable identity — array indices reshuffle as providers are retired).
		/// An unknown name yields the default rather than throwing, so a stale config can't break startup.</summary>
		// Token: 0x060000A6 RID: 166 RVA: 0x00006B80 File Offset: 0x00004D80
		public static ImageryProvider ByName(string name)
		{
			foreach (ImageryProvider p in ImageryProvider.All)
			{
				bool flag = p.Name == name;
				if (flag)
				{
					return p;
				}
			}
			return ImageryProvider.Default;
		}

		// Token: 0x04000091 RID: 145
		private static readonly string GibsYesterday = DateTime.UtcNow.AddDays(-1.0).ToString("yyyy-MM-dd");

		/// <summary>
		/// EOX Sentinel-2 cloudless, 2024 layer. A single self-consistent global mosaic from one constellation
		/// with uniform colour processing — no patchwork anywhere — at the cost of a hard ~10 m/px ceiling (z14).
		/// That ceiling lands exactly on Mirage's L12 target (see MercatorTileMath.CubeLevelToZoomOffset).
		///
		/// The 2024 layer (not 2020) because EOX rewrote the BRDF correction, removing residual striping over
		/// tropics/deserts. Licence: CC BY-NC-SA — free for a non-commercial mod WITH attribution, and
		/// share-alike, which is why baked tiles are client-generated and never redistributed.
		/// </summary>
		// Token: 0x04000092 RID: 146
		public static readonly ImageryProvider S2Cloudless2024 = new ImageryProvider("s2cloudless2024", "https://tiles.maps.eox.at/wmts/1.0.0/s2cloudless-2024_3857/default/g/{0}/{1}/{2}.jpg", 14, "Sentinel-2 cloudless (https://s2maps.eu) by EOX IT Services GmbH — Contains modified Copernicus Sentinel data 2024", true, TileImageFormat.Jpeg);

		/// <summary>
		/// NASA GIBS MODIS Terra true colour. U.S. federal government work — public domain, no usage restriction
		/// beyond a courtesy attribution, and its WMTS is purpose-built for this small-tile/high-frequency access
		/// pattern. Caps at z9 (~300 m/px), i.e. cube L7 — useful as a legally-unencumbered low-bandwidth option
		/// but not viable for near-ground detail.
		///
		/// Not the default: MODIS true colour is a raw DAILY snapshot, not a cloud-filtered composite, so any
		/// given area can easily be mostly cloud. EOX's "cloudless" is a genuine multi-date composite.
		/// </summary>
		// Token: 0x04000093 RID: 147
		public static readonly ImageryProvider GibsModis = new ImageryProvider("gibs_modis", "https://gibs.earthdata.nasa.gov/wmts/epsg3857/best/MODIS_Terra_CorrectedReflectance_TrueColor/default/" + ImageryProvider.GibsYesterday + "/GoogleMapsCompatible_Level9/{0}/{1}/{2}.jpeg", 9, "MODIS Terra true colour courtesy of NASA EOSDIS GIBS", false, TileImageFormat.Jpeg);

		/// <summary>
		/// Terrarium terrain-RGB — the ELEVATION source, not imagery. AWS "elevation-tiles-prod" (the Mapzen
		/// dataset), aggregating SRTM, GMTED, ETOPO1, NED/3DEP and various national DEMs; global, including
		/// ocean bathymetry, served to z15. Elevation is packed as <c>R·256 + G + B/256 − 32768</c> metres.
		///
		/// <b>PNG, not JPEG, and that is not a detail.</b> The encoding puts one metre in the G channel's LSB,
		/// so lossy compression is catastrophic here: a single unit of chroma error is a 25 cm step, and JPEG's
		/// 4:2:0 chroma subsampling would smear elevation across neighbouring texels. Hence
		/// <see cref="T:Mirage.WebIngest.PngDecoder" /> exists.
		///
		/// Licensing: the constituent datasets are predominantly public domain or CC-BY. Mirage's posture is the
		/// same as for EOX — tiles are baked on the user's machine and never redistributed (§11 decision 5), so
		/// the share-alike/attribution questions stay out of Sol's distribution entirely.
		///
		/// Why height matters at all: Mirage's cache is LOCKSTEP — one slot map, one page table, three atlases,
		/// and a tile is resident only when EVERY present layer landed. Baking colour past canonical's finest
		/// height level would fail the whole tile group, so "stream colour from the web" is not actually an
		/// option on a body with a height layer. Height has to come too.
		/// </summary>
		// Token: 0x04000094 RID: 148
		public static readonly ImageryProvider TerrariumDem = new ImageryProvider("terrarium", "https://s3.amazonaws.com/elevation-tiles-prod/terrarium/{0}/{2}/{1}.png", 15, "Elevation data from Mapzen / AWS Open Data (elevation-tiles-prod); sources include SRTM, GMTED2010, ETOPO1 and 3DEP", false, TileImageFormat.Png);

		/// <summary>Colour imagery sources (the ones a user picks between).</summary>
		// Token: 0x04000095 RID: 149
		public static readonly ImageryProvider[] All = new ImageryProvider[]
		{
			ImageryProvider.S2Cloudless2024,
			ImageryProvider.GibsModis
		};
	}
}
