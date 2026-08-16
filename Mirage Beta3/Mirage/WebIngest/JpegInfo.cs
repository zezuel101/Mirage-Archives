using System;

namespace Mirage.WebIngest
{
	/// <summary>Result of reading a JPEG's headers without decoding a single pixel.</summary>
	// Token: 0x0200001B RID: 27
	public readonly struct JpegInfo
	{
		// Token: 0x06000093 RID: 147 RVA: 0x000057E4 File Offset: 0x000039E4
		public JpegInfo(bool valid, JpegFrameKind kind, int width, int height, int components, int restartInterval, int maxH, int maxV, string error)
		{
			this.Valid = valid;
			this.Kind = kind;
			this.Width = width;
			this.Height = height;
			this.Components = components;
			this.RestartInterval = restartInterval;
			this.MaxH = maxH;
			this.MaxV = maxV;
			this.Error = error;
		}

		// Token: 0x06000094 RID: 148 RVA: 0x00005838 File Offset: 0x00003A38
		public static JpegInfo Fail(string error)
		{
			return new JpegInfo(false, JpegFrameKind.Unknown, 0, 0, 0, 0, 0, 0, error);
		}

		/// <summary>Conventional subsampling name, from the luma sampling factors.</summary>
		// Token: 0x17000021 RID: 33
		// (get) Token: 0x06000095 RID: 149 RVA: 0x00005854 File Offset: 0x00003A54
		public string Subsampling
		{
			get
			{
				return (this.Components == 1) ? "grayscale" : ((this.MaxH == 1 && this.MaxV == 1) ? "4:4:4" : ((this.MaxH == 2 && this.MaxV == 2) ? "4:2:0" : ((this.MaxH == 2 && this.MaxV == 1) ? "4:2:2" : string.Format("{0}x{1}", this.MaxH, this.MaxV))));
			}
		}

		// Token: 0x06000096 RID: 150 RVA: 0x000058DC File Offset: 0x00003ADC
		public override string ToString()
		{
			return this.Valid ? string.Format("{0}x{1} {2} {3}ch {4} restart={5}", new object[]
			{
				this.Width,
				this.Height,
				this.Kind,
				this.Components,
				this.Subsampling,
				this.RestartInterval
			}) : ("invalid (" + this.Error + ")");
		}

		// Token: 0x04000081 RID: 129
		public readonly bool Valid;

		// Token: 0x04000082 RID: 130
		public readonly JpegFrameKind Kind;

		// Token: 0x04000083 RID: 131
		public readonly int Width;

		// Token: 0x04000084 RID: 132
		public readonly int Height;

		// Token: 0x04000085 RID: 133
		public readonly int Components;

		/// <summary>Restart interval from the DRI marker (0 = none). This is the decoder's ONLY parallelism seam
		/// (§6): restart markers chop the entropy-coded scan into independently-decodable intervals, so Huffman —
		/// which is inherently serial and branchy — can at least be split across workers. Zero means the whole
		/// scan is one serial run.</summary>
		// Token: 0x04000086 RID: 134
		public readonly int RestartInterval;

		/// <summary>Luma sampling factors — the max H/V across components. (1,1) means 4:4:4 (no chroma
		/// subsampling, so the decoder's upsample path is a no-op); (2,2) means 4:2:0. Worth knowing because it
		/// determines whether chroma upsampling is exercised at all.</summary>
		// Token: 0x04000087 RID: 135
		public readonly int MaxH;

		// Token: 0x04000088 RID: 136
		public readonly int MaxV;

		// Token: 0x04000089 RID: 137
		public readonly string Error;
	}
}
