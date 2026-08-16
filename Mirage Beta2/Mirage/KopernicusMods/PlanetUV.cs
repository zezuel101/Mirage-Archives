using System;
using Kopernicus.ConfigParser.Attributes;
using Kopernicus.ConfigParser.BuiltinTypeParsers;
using Kopernicus.Configuration.ModLoader;

namespace Mirage.KopernicusMods
{
	// Token: 0x0200006B RID: 107
	public class PlanetUV : ModLoader<PQSMod_PlanetUV>
	{
		// Token: 0x17000085 RID: 133
		// (get) Token: 0x06000308 RID: 776 RVA: 0x00018DC4 File Offset: 0x00016FC4
		// (set) Token: 0x06000309 RID: 777 RVA: 0x00018DE6 File Offset: 0x00016FE6
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
