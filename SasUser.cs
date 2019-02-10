using System;
using System.Net;
using Sas.Component;
namespace Sas
{
	public class User
	{
		public Context context { private set; get; }
		public Account account { private set; get; }
		public string server { private set; get; }
		public string cert { private set; get; }
		public event Action<HttpWebRequest, HttpWebResponse, System.Exception> requester_post_handler {
			add {
				context.requester.post_handler += value;
			}
			remove {
				context.requester.post_handler -= value;
			}
		}

		public User (string server, string cert = null)
		{
			this.server = server;
			this.cert = cert;
			Reset ();
		}

		public void Reset()
		{
			context = new Context (server, new Net.HTTPSRequest ());
			if (cert != null)
				context.requester.LoadCert (cert);
			account = new Account (context);
		}
	}
}

