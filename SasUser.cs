using System;
using System.Net;
using Sas.Component;
using UniRx;
namespace Sas
{
	public class User
	{
		public Context context { private set; get; }
		public Sas.Util.WebsocketMgr socket_mgr { get; private set; }
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
			socket_mgr = new Sas.Util.WebsocketMgr ();
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

		public IObservable<Sas.Net.Websocket> MakeWS(string url)
		{
			var config = new Sas.Util.WebsocketMgr.Config ();
			config.url = new System.Uri (url);
			config.coockie.Add ("sas-accesstoken", context.token);
			return socket_mgr
				.Connection (config)
				.Select (socket => new Sas.Net.Websocket (socket, context.handler));
		}
	}
}

