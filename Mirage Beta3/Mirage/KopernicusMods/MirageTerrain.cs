using System;
using Kopernicus.ConfigParser.Attributes;
using Kopernicus.ConfigParser.BuiltinTypeParsers;
using Kopernicus.Configuration.ModLoader;

namespace Mirage.KopernicusMods
{
	/// <summary>Kopernicus loader for <see cref="T:Mirage.KopernicusMods.PQSMod_MirageTerrain" />.</summary>
	// Token: 0x02000079 RID: 121
	public class MirageTerrain : ModLoader<PQSMod_MirageTerrain>
	{
		// Token: 0x1700008B RID: 139
		// (get) Token: 0x06000383 RID: 899 RVA: 0x0001A6FB File Offset: 0x000188FB
		// (set) Token: 0x06000384 RID: 900 RVA: 0x0001A70D File Offset: 0x0001890D
		[ParserTarget("deformity", Optional = true)]
		public NumericParser<double> Deformity
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

		// Token: 0x1700008C RID: 140
		// (get) Token: 0x06000385 RID: 901 RVA: 0x0001A720 File Offset: 0x00018920
		// (set) Token: 0x06000386 RID: 902 RVA: 0x0001A732 File Offset: 0x00018932
		[ParserTarget("offset", Optional = true)]
		public NumericParser<double> Offset
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
