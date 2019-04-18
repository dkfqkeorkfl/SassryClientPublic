using System;
using Newtonsoft.Json.Linq;
namespace Sas
{
	public class Emapper<T> where T : IComparable 
	{
		public JObject root { get; private set; }
		JObject dir_root { get { return (JObject)root ["dir"]; }}
		JObject hash_root { get { return (JObject)root ["hash"]; }}

		public Emapper()
		{
			root = new JObject ();
			var dir = root ["dir"] = new JObject ();
			var hash = root ["hash"] = new JObject ();

			foreach (var name in Enum.GetNames(typeof(T))) {
				var sub = new JObject ();
				sub ["order"] = new JValue(Enum.Parse (typeof(T), name));
				sub ["hash"] = new JValue((int)Sas.SecureHelper.Crc32 (name));

				dir [name] = sub;
				hash [sub ["hash"].Value<string>()] = name;
			}
		}

		public long ToHash(T key)
		{
			return ToHash (key.ToString ());
		}

		public long ToHash(string key)
		{
			var ret = dir_root [key];
			if (ret == null)
				return 0;

			var obj = (JObject)ret;
			return obj ["hash"].Value<long> ();
		}

		public T ToEnum(long hash)
		{
			var ret = hash_root [hash.ToString ()];
			if (ret == null)
				return default(T);

			return (T) Enum.Parse (typeof(T), ret.Value<string> ());
		}
	}
}

