using System;
using Kopernicus.ConfigParser.Attributes;
using Kopernicus.ConfigParser.BuiltinTypeParsers;
using Kopernicus.Configuration.ModLoader;

namespace Mirage.KopernicusMods
{
	/// <summary>
	/// Kopernicus config loader for <see cref="T:Mirage.KopernicusMods.PQSMod_MirageTerrain" />. Node name is <c>MirageTerrain</c>
	/// inside a PQS <c>Mods { }</c> block. Tile paths/dimensions come from the body's VirtualTexture
	/// config; only the height mapping and ordering are configured here.
	/// </summary>
	// Token: 0x02000069 RID: 105
	public class MirageTerrain : ModLoader<PQSMod_MirageTerrain>
	{
		// Token: 0x17000082 RID: 130
		// (get) Token: 0x060002FF RID: 767 RVA: 0x00018C63 File Offset: 0x00016E63
		// (set) Token: 0x06000300 RID: 768 RVA: 0x00018C75 File Offset: 0x00016E75
		[ParserTarget("order", Optional = true)]
		public NumericParser<int> order
		{
			get
			{
				return base.Mod.order;
			}
			set
			{
				base.Mod.order = value;
			}
		}

		// Token: 0x17000083 RID: 131
		// (get) Token: 0x06000301 RID: 769 RVA: 0x00018C88 File Offset: 0x00016E88
		// (set) Token: 0x06000302 RID: 770 RVA: 0x00018C9A File Offset: 0x00016E9A
		[ParserTarget("deformity", Optional = true)]
		public NumericParser<double> deformity
		{
			get
			{
				return base.Mod.deformity;
			}
			set
			{
				base.Mod.deformity = value;
			}
		}

		// Token: 0x17000084 RID: 132
		// (get) Token: 0x06000303 RID: 771 RVA: 0x00018CAD File Offset: 0x00016EAD
		// (set) Token: 0x06000304 RID: 772 RVA: 0x00018CBF File Offset: 0x00016EBF
		[ParserTarget("offset", Optional = true)]
		public NumericParser<double> offset
		{
			get
			{
				return base.Mod.offset;
			}
			set
			{
				base.Mod.offset = value;
			}
		}
	}
}
