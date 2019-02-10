using System;
using System.Collections.Generic;

namespace Sas
{
	namespace Data
	{
		public class AccountAccessEquipResult
		{
			public ulong id { get; set;}
			public string name { get; set; }
			public string desc { get; set; }
			public string equip { get; set; }
		}
			
		public class AccountLoginResult
		{
			public ulong uid { get; set; }
			public string otp { get; set; }
			public string public_key { get; set; }
			public long token_time { get; set; }
			public long regist_time { get; set; }
			public int auth{ get; set; }
			public IList<AccountAccessEquipResult> equips { get; set; }
		}
	}
}

