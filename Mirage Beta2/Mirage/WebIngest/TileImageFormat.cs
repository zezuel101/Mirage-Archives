using System;

namespace Mirage.WebIngest
{
	/// <summary>
	/// A Web-Mercator XYZ imagery source. Lifted from GeoStream's provider table together with its ToS research,
	/// which is the load-bearing part: the set of providers below is what remains after several were ruled out
	/// on licensing grounds, and that conclusion must not be silently re-litigated by adding a "just one more"
	/// endpoint later.
	///
	/// RULED OUT — do not re-add without new legal grounds:
	///   - Esri World Imagery: Esri's own terms state it "is not free and can only be used with an ArcGIS Online
	///     or ArcGIS Enterprise license", and separately "is not available for commercial use". Hitting
	///     server.arcgisonline.com's raw tile endpoint without such a license was never authorized.
	///   - Google Maps Tile API / Bing Maps: both explicitly prohibit exactly this "raw tile URL outside our SDK"
	///     access pattern.
	/// </summary>
	/// <summary>The container a provider serves. Load-bearing rather than descriptive: EOX signals "no imagery
	/// here" by returning a PNG from a <c>.jpg</c> endpoint, so "is this a coverage gap?" is the question
	/// "does the container disagree with what we asked for?" — which is unanswerable without knowing what we
	/// asked for. A DEM provider serving PNG legitimately is not a gap, and reading it as one declines the
	/// entire planet.</summary>
	// Token: 0x0200001A RID: 26
	public enum TileImageFormat
	{
		// Token: 0x04000089 RID: 137
		Jpeg,
		// Token: 0x0400008A RID: 138
		Png
	}
}
