using System;

namespace Sas
{
	namespace Net
	{
		public class ProtocolHandler
		{
			System.Collections.Generic.Dictionary<string, System.Action<Newtonsoft.Json.Linq.JToken>> handlers { get; set; }

			public ProtocolHandler()
			{
				handlers = new System.Collections.Generic.Dictionary<string, System.Action<Newtonsoft.Json.Linq.JToken>>();
			}

			public void Add(string key, System.Action<Newtonsoft.Json.Linq.JToken> action)
			{
				if (handlers.ContainsKey (key))
					throw new Exception (ERRNO.LOCAL_HANDLER_DUMPLICATED.ToErrCode ());

				handlers.Add (key, action);
			}

			public void Del(string key)
			{
				handlers.Remove(key);
			}

			public void Del(System.Collections.Generic.IList<string> keys)
			{
				foreach(var key in keys)
					Del(key);
			}

			public bool Has(string key)
			{
				return handlers.ContainsKey(key);
			}

			public void Process(Newtonsoft.Json.Linq.JObject token)
			{
				var parents = token as Newtonsoft.Json.Linq.JObject;
				foreach(var key in parents.Properties())
				{
					var param = parents [key.Name];
					System.Action<Newtonsoft.Json.Linq.JToken> ret;
					if (handlers.TryGetValue (key.Name, out ret))
						ret (param);
				}
			}
		}
	}
}
