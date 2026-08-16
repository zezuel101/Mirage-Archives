using System;

namespace Mirage.VirtualTexture
{
	/// <summary>Mitchell-Netravali bicubic kernel (B/C parameterized). Mirage uses B = C = 1/3.</summary>
	// Token: 0x0200004D RID: 77
	public readonly struct MitchellNetravali
	{
		// Token: 0x060001E0 RID: 480 RVA: 0x0000DE34 File Offset: 0x0000C034
		public MitchellNetravali(double b, double c)
		{
			this.cube0 = -0.16666666666666666 * b - c;
			this.cube1 = -1.5 * b - c + 2.0;
			this.cube2 = -this.cube1;
			this.cube3 = -this.cube0;
			this.sq0 = 0.5 * b + 2.0 * c;
			this.sq1 = 2.0 * b + c - 3.0;
			this.sq2 = -2.5 * b - 2.0 * c + 3.0;
			this.sq3 = -c;
			this.lin0 = -0.5 * b - c;
			this.lin2 = -this.lin0;
			this.flat02 = 0.16666666666666666 * b;
			this.flat1 = -0.3333333333333333 * b + 1.0;
		}

		/// <summary>Filter four consecutive samples at fractional position d (0 at p1, 1 at p2).</summary>
		// Token: 0x060001E1 RID: 481 RVA: 0x0000DF44 File Offset: 0x0000C144
		public double Evaluate(double p0, double p1, double p2, double p3, double d)
		{
			return (this.cube0 * p0 + this.cube1 * p1 + this.cube2 * p2 + this.cube3 * p3) * d * d * d + (this.sq0 * p0 + this.sq1 * p1 + this.sq2 * p2 + this.sq3 * p3) * d * d + (this.lin0 * p0 + this.lin2 * p2) * d + this.flat02 * p0 + this.flat1 * p1 + this.flat02 * p2;
		}

		// Token: 0x04000177 RID: 375
		private readonly double cube0;

		// Token: 0x04000178 RID: 376
		private readonly double cube1;

		// Token: 0x04000179 RID: 377
		private readonly double cube2;

		// Token: 0x0400017A RID: 378
		private readonly double cube3;

		// Token: 0x0400017B RID: 379
		private readonly double sq0;

		// Token: 0x0400017C RID: 380
		private readonly double sq1;

		// Token: 0x0400017D RID: 381
		private readonly double sq2;

		// Token: 0x0400017E RID: 382
		private readonly double sq3;

		// Token: 0x0400017F RID: 383
		private readonly double lin0;

		// Token: 0x04000180 RID: 384
		private readonly double lin2;

		// Token: 0x04000181 RID: 385
		private readonly double flat02;

		// Token: 0x04000182 RID: 386
		private readonly double flat1;
	}
}
