using System;

namespace Mirage.WebIngest
{
	/// <summary>Result of reading a JPEG's headers without decoding a single pixel.</summary>
	// Token: 0x0200001D RID: 29
	public readonly struct JpegInfo
	{
		// Token: 0x060000A8 RID: 168 RVA: 0x00006C80 File Offset: 0x00004E80
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

		// Token: 0x060000A9 RID: 169 RVA: 0x00006CD4 File Offset: 0x00004ED4
		public static JpegInfo Fail(string error)
		{
			return new JpegInfo(false, JpegFrameKind.Unknown, 0, 0, 0, 0, 0, 0, error);
		}

		/// <summary>Conventional subsampling name, from the luma sampling factors.</summary>
		// Token: 0x17000021 RID: 33
		// (get) Token: 0x060000AA RID: 170 RVA: 0x00006CF0 File Offset: 0x00004EF0
		public string Subsampling
		{
			get
			{
				return (this.Components == 1) ? "grayscale" : ((this.MaxH == 1 && this.MaxV == 1) ? "4:4:4" : ((this.MaxH == 2 && this.MaxV == 2) ? "4:2:0" : ((this.MaxH == 2 && this.MaxV == 1) ? "4:2:2" : string.Format("{0}x{1}", this.MaxH, this.MaxV))));
			}
		}

		// Token: 0x060000AB RID: 171 RVA: 0x00006D78 File Offset: 0x00004F78
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

		// Token: 0x0400009B RID: 155
		public readonly bool Valid;

		// Token: 0x0400009C RID: 156
		public readonly JpegFrameKind Kind;

		// Token: 0x0400009D RID: 157
		public readonly int Width;

		// Token: 0x0400009E RID: 158
		public readonly int Height;

		// Token: 0x0400009F RID: 159
		public readonly int Components;

		/// <summary>Restart interval from the DRI marker (0 = none). This is the decoder's ONLY parallelism seam
		/// (§6): restart markers chop the entropy-coded scan into independently-decodable intervals, so Huffman —
		/// which is inherently serial and branchy — can at least be split across workers. Zero means the whole
		/// scan is one serial run.</summary>
		// Token: 0x040000A0 RID: 160
		public readonly int RestartInterval;

		/// <summary>Luma sampling factors — the max H/V across components. (1,1) means 4:4:4 (no chroma
		/// subsampling, so the decoder's upsample path is a no-op); (2,2) means 4:2:0. Worth knowing because it
		/// determines whether chroma upsampling is exercised at all.</summary>
		// Token: 0x040000A1 RID: 161
		public readonly int MaxH;

		// Token: 0x040000A2 RID: 162
		public readonly int MaxV;

		// Token: 0x040000A3 RID: 163
		public readonly string Error;
	}
}
