using System;

namespace Mirage.Configuration
{
	/// <summary>An inclusive range of floats, and the clamp onto it.</summary>
	// Token: 0x02000080 RID: 128
	internal readonly struct FloatRange
	{
		/// <summary>An inclusive range of floats, and the clamp onto it.</summary>
		// Token: 0x0600039D RID: 925 RVA: 0x0001B6E3 File Offset: 0x000198E3
		public FloatRange(float min, float max)
		{
			this.Min = min;
			this.Max = max;
		}

		// Token: 0x0600039E RID: 926 RVA: 0x0001B6F3 File Offset: 0x000198F3
		public float Clamp(float value)
		{
			return Math.Min(Math.Max(value, this.Min), this.Max);
		}

		// Token: 0x0600039F RID: 927 RVA: 0x0001B70C File Offset: 0x0001990C
		public override string ToString()
		{
			return string.Format("[{0}, {1}]", this.Min, this.Max);
		}

		// Token: 0x0400034F RID: 847
		public readonly float Min;

		// Token: 0x04000350 RID: 848
		public readonly float Max;
	}
}
