using System;
using Kopernicus.ConfigParser.Attributes;
using Kopernicus.ConfigParser.BuiltinTypeParsers;
using Kopernicus.Configuration.ModLoader;

namespace Mirage.KopernicusMods
{
	/// <summary>Kopernicus loader for <see cref="T:Mirage.KopernicusMods.PQSMod_MirageSubdivision" />.</summary>
	// Token: 0x02000077 RID: 119
	public class MirageSubdivision : ModLoader<PQSMod_MirageSubdivision>
	{
		// Token: 0x17000088 RID: 136
		// (get) Token: 0x06000374 RID: 884 RVA: 0x0001A312 File Offset: 0x00018512
		// (set) Token: 0x06000375 RID: 885 RVA: 0x0001A324 File Offset: 0x00018524
		[ParserTarget("subdivisionLevel", Optional = true)]
		public NumericParser<int> SubdivisionLevel
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

		// Token: 0x17000089 RID: 137
		// (get) Token: 0x06000376 RID: 886 RVA: 0x0001A337 File Offset: 0x00018537
		// (set) Token: 0x06000377 RID: 887 RVA: 0x0001A349 File Offset: 0x00018549
		[ParserTarget("subdivisionRange", Optional = true)]
		public NumericParser<float> SubdivisionRange
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

		// Token: 0x06000378 RID: 888 RVA: 0x0001A35C File Offset: 0x0001855C
		public override void Create(PQS pqsVersion)
		{
			base.Create(pqsVersion);
			base.Mod.order = 2147483645;
		}
	}
}
