using System;
using Kopernicus.ConfigParser.Attributes;
using Kopernicus.ConfigParser.BuiltinTypeParsers;
using Kopernicus.Configuration.ModLoader;

namespace Mirage.KopernicusMods
{
	// Token: 0x02000067 RID: 103
	[RequireConfigType(1)]
	public class MirageSubdivision : ModLoader<PQSMod_MirageSubdivision>
	{
		// Token: 0x1700007E RID: 126
		// (get) Token: 0x060002F1 RID: 753 RVA: 0x000188B2 File Offset: 0x00016AB2
		// (set) Token: 0x060002F2 RID: 754 RVA: 0x000188C4 File Offset: 0x00016AC4
		[ParserTarget("subdivisionLevel", Optional = false)]
		public NumericParser<int> subdivisionLevel
		{
			get
			{
				return base.Mod.subdivisionLevel;
			}
			set
			{
				base.Mod.subdivisionLevel = value;
			}
		}

		// Token: 0x1700007F RID: 127
		// (get) Token: 0x060002F3 RID: 755 RVA: 0x000188D7 File Offset: 0x00016AD7
		// (set) Token: 0x060002F4 RID: 756 RVA: 0x000188E9 File Offset: 0x00016AE9
		[ParserTarget("subdivisionRange", Optional = false)]
		public NumericParser<float> subdivisionRange
		{
			get
			{
				return base.Mod.subdivisionRange;
			}
			set
			{
				base.Mod.subdivisionRange = value;
			}
		}

		// Token: 0x17000080 RID: 128
		// (get) Token: 0x060002F5 RID: 757 RVA: 0x000188FC File Offset: 0x00016AFC
		// (set) Token: 0x060002F6 RID: 758 RVA: 0x0001890E File Offset: 0x00016B0E
		[ParserTarget("order", Optional = false)]
		public NumericParser<int> order
		{
			get
			{
				return base.Mod.order;
			}
			set
			{
				base.Mod.order = 2147483645;
			}
		}
	}
}
