using System;

namespace Sas
{
	namespace Data
	{
		public enum SNS
		{
			AUTO, SAS, 
		}

		public class AccountLoginParam
		{
			public SNS sns { get; set; } 

			public string extension { get; set; }

			public System.Security.SecureString email{ get; set; }

			public System.Security.SecureString password{ get; set; }

			public AccountLoginParam()
			{
				sns = SNS.SAS;
			}
		}

		public class AccountSignupParam : AccountLoginParam
		{
			//public System.Security.SecureString nick{ get; set; }
		}
	}
}

