using System;
using Kopernicus.ConfigParser.Attributes;

namespace Mirage.Configuration
{
	/// <summary>ConfigParser target for a <c>Body { name = …; VirtualTexture { } }</c> block.</summary>
	// Token: 0x02000088 RID: 136
	[RequireConfigType(1)]
	public class MirageBodyConfigLoader
	{
		// Token: 0x170000AF RID: 175
		// (get) Token: 0x060003FF RID: 1023 RVA: 0x0001C8AB File Offset: 0x0001AAAB
		// (set) Token: 0x06000400 RID: 1024 RVA: 0x0001C8B3 File Offset: 0x0001AAB3
		[ParserTarget("name", Optional = false)]
		public string Name { get; set; }

		// Token: 0x170000B0 RID: 176
		// (get) Token: 0x06000401 RID: 1025 RVA: 0x0001C8BC File Offset: 0x0001AABC
		// (set) Token: 0x06000402 RID: 1026 RVA: 0x0001C8C4 File Offset: 0x0001AAC4
		[ParserTarget("VirtualTexture", Optional = true)]
		public VirtualTextureConfigLoader VirtualTexture { get; set; }
	}
}
