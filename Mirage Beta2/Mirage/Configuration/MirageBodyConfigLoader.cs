using System;
using Kopernicus.ConfigParser.Attributes;

namespace Mirage.Configuration
{
	/// <summary>
	/// Kopernicus ConfigParser target for a <c>Body { name = …; VirtualTexture { … } }</c>
	/// block inside a top-level <c>MirageTerrain</c> node.
	/// </summary>
	// Token: 0x02000070 RID: 112
	[RequireConfigType(1)]
	public class MirageBodyConfigLoader
	{
		// Token: 0x170000A3 RID: 163
		// (get) Token: 0x06000353 RID: 851 RVA: 0x00019E3C File Offset: 0x0001803C
		// (set) Token: 0x06000354 RID: 852 RVA: 0x00019E44 File Offset: 0x00018044
		[ParserTarget("name", Optional = false)]
		public string Name { get; set; }

		// Token: 0x170000A4 RID: 164
		// (get) Token: 0x06000355 RID: 853 RVA: 0x00019E4D File Offset: 0x0001804D
		// (set) Token: 0x06000356 RID: 854 RVA: 0x00019E55 File Offset: 0x00018055
		[ParserTarget("VirtualTexture", Optional = true)]
		public VirtualTextureConfigLoader VirtualTexture { get; set; }
	}
}
