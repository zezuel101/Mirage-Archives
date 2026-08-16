using System;

namespace Mirage.WebIngest
{
	/// <summary>
	/// The color post-process that makes fetched WEB imagery match the CANONICAL tier's look. Dialled in by eye
	/// with the packer's <c>--color-match</c> helper; this is the C# port of that page's <c>applyPixel()</c>, and
	/// the ORDER of operations here is the contract shared with it:
	///   exposure → temperature/tint → brightness → contrast → saturation → gamma.
	///
	/// <b>RGB only.</b> The color tile's alpha is a water mask (white = water), set separately in the bake from
	/// the height layer — the grade must never touch it. Unity-free (System only) so it links into the offline
	/// packer and is gated by <c>--test-colorgrade</c>, which pins this maths against the helper's.
	/// </summary>
	// Token: 0x0200000A RID: 10
	public readonly struct ColorGrade
	{
		// Token: 0x06000053 RID: 83 RVA: 0x00002D3D File Offset: 0x00000F3D
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
		// Token: 0x17000015 RID: 21
		// (get) Token: 0x06000054 RID: 84 RVA: 0x00002D75 File Offset: 0x00000F75
		public static ColorGrade Default
		{
			get
			{
				return new ColorGrade(0f, 0f, 1f, 1f, 1f, 0f, 0f);
			}
		}

		/// <summary>The no-op grade (used when grading is turned off in config).</summary>
		// Token: 0x17000016 RID: 22
		// (get) Token: 0x06000055 RID: 85 RVA: 0x00002D9F File Offset: 0x00000F9F
		public static ColorGrade Identity
		{
			get
			{
				return new ColorGrade(0f, 0f, 1f, 1f, 1f, 0f, 0f);
			}
		}

		/// <summary>True when this grade leaves every pixel unchanged, so the bake can skip the per-texel work.</summary>
		// Token: 0x17000017 RID: 23
		// (get) Token: 0x06000056 RID: 86 RVA: 0x00002DCC File Offset: 0x00000FCC
		public bool IsIdentity
		{
			get
			{
				return this.Exposure == 0f && this.Brightness == 0f && this.Contrast == 1f && this.Saturation == 1f && this.Gamma == 1f && this.Temperature == 0f && this.Tint == 0f;
			}
		}

		// Token: 0x06000057 RID: 87 RVA: 0x00002E37 File Offset: 0x00001037
		private static float Clamp01(float v)
		{
			return (v < 0f) ? 0f : ((v > 1f) ? 1f : v);
		}

		/// <summary>
		/// Grade one pixel. Inputs and outputs are in 0..255 (the range the reprojector produces and BC7 encode
		/// consumes); the maths runs in 0..1 to match the helper page byte-for-byte. Output is NOT clamped to
		/// [0,255] — the caller's existing <c>Clamp255</c> does that, same as an ungraded pixel.
		/// </summary>
		// Token: 0x06000058 RID: 88 RVA: 0x00002E58 File Offset: 0x00001058
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

		// Token: 0x0400002F RID: 47
		public const float DefaultExposure = 0f;

		// Token: 0x04000030 RID: 48
		public const float DefaultBrightness = 0f;

		// Token: 0x04000031 RID: 49
		public const float DefaultContrast = 1f;

		// Token: 0x04000032 RID: 50
		public const float DefaultSaturation = 1f;

		// Token: 0x04000033 RID: 51
		public const float DefaultGamma = 1f;

		// Token: 0x04000034 RID: 52
		public const float DefaultTemperature = 0f;

		// Token: 0x04000035 RID: 53
		public const float DefaultTint = 0f;

		// Token: 0x04000036 RID: 54
		public readonly float Exposure;

		// Token: 0x04000037 RID: 55
		public readonly float Brightness;

		// Token: 0x04000038 RID: 56
		public readonly float Contrast;

		// Token: 0x04000039 RID: 57
		public readonly float Saturation;

		// Token: 0x0400003A RID: 58
		public readonly float Gamma;

		// Token: 0x0400003B RID: 59
		public readonly float Temperature;

		// Token: 0x0400003C RID: 60
		public readonly float Tint;
	}
}
