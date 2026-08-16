using System;

namespace Mirage.VirtualTexture
{
	/// <summary>
	/// Mitchell-Netravali bicubic kernel (B/C parametrised; Mirage uses B=C=1/3 to match
	/// <c>MirageVT.cginc:MitchellNetravali1D</c>). Ported from BurstPQS's evaluator so it can be
	/// shared by the stock and Burst CPU sampling paths. Plain <c>double</c> arithmetic — Burst safe.
	///
	/// Lives in its own Unity-free file so the offline packer and the web-ingest reprojection can link it:
	/// the resample kernel is a correctness-relevant constant shared by the shader, the CPU sampler and the
	/// baker, and a second copy of it is how those silently drift apart.
	/// </summary>
	// Token: 0x0200004A RID: 74
	public readonly struct MitchellNetravali
	{
		// Token: 0x060001C0 RID: 448 RVA: 0x0000D530 File Offset: 0x0000B730
		public MitchellNetravali(double b, double c)
		{
			this.C = c;
			this._n6BnC = -0.16666666666666666 * b - c;
			this._n32BnC2 = -1.5 * b - c + 2.0;
			this._32BCn2 = -this._n32BnC2;
			this._6BC = -this._n6BnC;
			this._2B2C = 0.5 * b + 2.0 * c;
			this._2BCn3 = 2.0 * b + c - 3.0;
			this._n52Bn2C3 = -2.5 * b - 2.0 * c + 3.0;
			this._n2BnC = -0.5 * b - c;
			this._2BC = -this._n2BnC;
			this._6B = 0.16666666666666666 * b;
			this._n3B1 = -0.3333333333333333 * b + 1.0;
		}

		// Token: 0x060001C1 RID: 449 RVA: 0x0000D63C File Offset: 0x0000B83C
		public double Evaluate(double p0, double p1, double p2, double p3, double d)
		{
			return (this._n6BnC * p0 + this._n32BnC2 * p1 + this._32BCn2 * p2 + this._6BC * p3) * d * d * d + (this._2B2C * p0 + this._2BCn3 * p1 + this._n52Bn2C3 * p2 - this.C * p3) * d * d + (this._n2BnC * p0 + this._2BC * p2) * d + this._6B * p0 + this._n3B1 * p1 + this._6B * p2;
		}

		// Token: 0x0400018F RID: 399
		private readonly double C;

		// Token: 0x04000190 RID: 400
		private readonly double _n6BnC;

		// Token: 0x04000191 RID: 401
		private readonly double _n32BnC2;

		// Token: 0x04000192 RID: 402
		private readonly double _32BCn2;

		// Token: 0x04000193 RID: 403
		private readonly double _6BC;

		// Token: 0x04000194 RID: 404
		private readonly double _2B2C;

		// Token: 0x04000195 RID: 405
		private readonly double _2BCn3;

		// Token: 0x04000196 RID: 406
		private readonly double _n52Bn2C3;

		// Token: 0x04000197 RID: 407
		private readonly double _n2BnC;

		// Token: 0x04000198 RID: 408
		private readonly double _2BC;

		// Token: 0x04000199 RID: 409
		private readonly double _6B;

		// Token: 0x0400019A RID: 410
		private readonly double _n3B1;
	}
}
