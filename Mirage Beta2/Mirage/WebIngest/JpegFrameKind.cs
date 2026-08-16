using System;

namespace Mirage.WebIngest
{
	/// <summary>JPEG frame kind, from the SOF marker. Decides how hard the decoder (§6) has to be.</summary>
	// Token: 0x0200001C RID: 28
	public enum JpegFrameKind
	{
		// Token: 0x04000097 RID: 151
		Unknown,
		/// <summary>SOF0/SOF1 — baseline or extended sequential. One scan, decodable in a single pass.</summary>
		// Token: 0x04000098 RID: 152
		Baseline,
		/// <summary>SOF2 — progressive. Multi-scan with spectral selection and successive approximation. A
		/// materially harder decoder; a naive baseline port fed this emits garbage rather than failing loudly.</summary>
		// Token: 0x04000099 RID: 153
		Progressive,
		/// <summary>SOF3/5/6/7/9/… — lossless, arithmetic-coded, or hierarchical. Out of scope.</summary>
		// Token: 0x0400009A RID: 154
		Unsupported
	}
}
