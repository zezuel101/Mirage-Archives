using System;

namespace Mirage.WebIngest
{
	/// <summary>
	/// The colour post-process that makes fetched WEB imagery match the CANONICAL tier's look. Dialled in by eye
	/// with the packer's <c>--color-match</c> helper; this is the C# port of that page's <c>applyPixel()</c>, and
	/// the ORDER of operations here is the contract shared with it:
	///   exposure → temperature/tint → brightness → contrast → saturation → gamma.
	///
	/// <b>RGB only.</b> The colour tile's alpha is a water mask (white = water), set separately in the bake from
	/// the height layer — the grade must never touch it. Unity-free (System only) so it links into the offline
	/// packer and is gated by <c>--test-colorgrade</c>, which pins this maths against the helper's.
	/// </summary>
	// Token: 0x02000012 RID: 18
	public readonly struct ColorGrade
	{
		// Token: 0x0600007A RID: 122 RVA: 0x00004FFC File Offset: 0x000031FC
		public ColorGrade(float exposure, float brightness, float contrast, float saturation, float gamma, float temperature, float tint)
		{
			this.Exposure = exposure;
			this.Brightness = brightness;
			this.Contrast = contrast;
			this.Saturation = saturation;
			this.Gamma = gamma;
			this.Temperature = temperature;
			this.Tint = tint;
		}

		/// <summary>The shipping default — neutral (identity). Per-body config supplies any actual grade.</summary>
		// Token: 0x17000016 RID: 22
		// (get) Token: 0x0600007B RID: 123 RVA: 0x00005034 File Offset: 0x00003234
		public static ColorGrade Default
		{
			get
			{
				return new ColorGrade(0f, 0f, 1f, 1f, 1f, 0f, 0f);
			}
		}

		/// <summary>The no-op grade (used when grading is turned off in config).</summary>
		// Token: 0x17000017 RID: 23
		// (get) Token: 0x0600007C RID: 124 RVA: 0x0000505E File Offset: 0x0000325E
		public static ColorGrade Identity
		{
			get
			{
				return new ColorGrade(0f, 0f, 1f, 1f, 1f, 0f, 0f);
			}
		}

		/// <summary>True when this grade leaves every pixel unchanged, so the bake can skip the per-texel work.</summary>
		// Token: 0x17000018 RID: 24
		// (get) Token: 0x0600007D RID: 125 RVA: 0x00005088 File Offset: 0x00003288
		public bool IsIdentity
		{
			get
			{
				return this.Exposure == 0f && this.Brightness == 0f && this.Contrast == 1f && this.Saturation == 1f && this.Gamma == 1f && this.Temperature == 0f && this.Tint == 0f;
			}
		}

		// Token: 0x0600007E RID: 126 RVA: 0x000050F3 File Offset: 0x000032F3
		private static float Clamp01(float v)
		{
			return (v < 0f) ? 0f : ((v > 1f) ? 1f : v);
		}

		/// <summary>
		/// Grade one pixel. Inputs and outputs are in 0..255 (the range the reprojector produces and BC7 encode
		/// consumes); the maths runs in 0..1 to match the helper page byte-for-byte. Output is NOT clamped to
		/// [0,255] — the caller's existing <c>Clamp255</c> does that, same as an ungraded pixel.
		/// </summary>
		// Token: 0x0600007F RID: 127 RVA: 0x00005114 File Offset: 0x00003314
		public void Apply(float r255, float g255, float b255, out float rOut, out float gOut, out float bOut)
		{
			float r256 = r255 / 255f;
			float g256 = g255 / 255f;
			float b256 = b255 / 255f;
			float i = (float)Math.Pow(2.0, (double)this.Exposure);
			r256 *= i;
			g256 *= i;
			b256 *= i;
			r256 *= 1f + this.Temperature / 100f;
			b256 *= 1f - this.Temperature / 100f;
			g256 *= 1f + this.Tint / 100f;
			float add = this.Brightness / 100f;
			r256 += add;
			g256 += add;
			b256 += add;
			r256 = (r256 - 0.5f) * this.Contrast + 0.5f;
			g256 = (g256 - 0.5f) * this.Contrast + 0.5f;
			b256 = (b256 - 0.5f) * this.Contrast + 0.5f;
			float luma = 0.2126f * r256 + 0.7152f * g256 + 0.0722f * b256;
			r256 = luma + (r256 - luma) * this.Saturation;
			g256 = luma + (g256 - luma) * this.Saturation;
			b256 = luma + (b256 - luma) * this.Saturation;
			float ig = 1f / this.Gamma;
			r256 = (float)Math.Pow((double)ColorGrade.Clamp01(r256), (double)ig);
			g256 = (float)Math.Pow((double)ColorGrade.Clamp01(g256), (double)ig);
			b256 = (float)Math.Pow((double)ColorGrade.Clamp01(b256), (double)ig);
			rOut = r256 * 255f;
			gOut = g256 * 255f;
			bOut = b256 * 255f;
		}

		// Token: 0x04000053 RID: 83
		public const float DefaultExposure = 0f;

		// Token: 0x04000054 RID: 84
		public const float DefaultBrightness = 0f;

		// Token: 0x04000055 RID: 85
		public const float DefaultContrast = 1f;

		// Token: 0x04000056 RID: 86
		public const float DefaultSaturation = 1f;

		// Token: 0x04000057 RID: 87
		public const float DefaultGamma = 1f;

		// Token: 0x04000058 RID: 88
		public const float DefaultTemperature = 0f;

		// Token: 0x04000059 RID: 89
		public const float DefaultTint = 0f;

		// Token: 0x0400005A RID: 90
		public readonly float Exposure;

		// Token: 0x0400005B RID: 91
		public readonly float Brightness;

		// Token: 0x0400005C RID: 92
		public readonly float Contrast;

		// Token: 0x0400005D RID: 93
		public readonly float Saturation;

		// Token: 0x0400005E RID: 94
		public readonly float Gamma;

		// Token: 0x0400005F RID: 95
		public readonly float Temperature;

		// Token: 0x04000060 RID: 96
		public readonly float Tint;
	}
}
