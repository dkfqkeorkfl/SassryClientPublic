using System;
using System.Linq;
using UnityEngine;
using Sas.Data;
using Newtonsoft.Json.Linq;
using UniRx;
namespace Sas
{
	namespace Component
	{
		public class Context
		{
			// 불필요 한건 다 로컬 DB
			public string server { get; private set; }

			public Net.HTTPSRequest requester { get; private set; }

			public AccountLoginResult login_result { get; set; }

			public AccountAccessEquipResult accept_equip { get { 
					if (login_result == null) return null;
					var ret = login_result.equips.Where (e => e.equip == SystemInfo.deviceUniqueIdentifier).FirstOrDefault (); 
					return ret != null ? ret : null;
				} }

			public ulong uid{ get { return login_result != null ? login_result.uid : 0; } }

			public string otp { get { return login_result != null ? login_result.otp : null; } }

			public string public_key { get { return login_result.public_key; } }

			public long token_time { get { return login_result.token_time; } }

			public event Action<JObject> paylod_handler;

			public string token {
				get { 
					if (string.IsNullOrEmpty(otp))
						return null;
					
					var serialize = new Sas.Net.AccessToken ();
					serialize.uid = uid;
					serialize.otp = otp;
					var json = JsonUtility.ToJson (serialize);
					using (var aes = new System.Security.Cryptography.AesManaged ()) {
						using (var key = Sas.SecureHelper.APP_KEY)
							aes.Key = key.GetByte (Convert.FromBase64String);
						using (var iv = Sas.SecureHelper.APP_IV)
							aes.IV = iv.GetByte (Convert.FromBase64String);
						return Convert.ToBase64String (aes.Encrypt (json));
					}
				}
			}

			public Context (string server, Net.HTTPSRequest other)
			{
				this.requester = other;
				this.server = server;
			}

			public UniRx.IObservable<Net.HTTPResult> Get (string page)
			{
				var inst = requester.Get (server + page);
				var token = this.token;
				if (!string.IsNullOrEmpty (token))
					inst.mReq.Headers.Add ("sas-accesstoken", token);
					
				return inst.Invoke ().Do (ret => {
					paylod_handler((JObject)ret.protocol.payload);
				});
			}

			public UniRx.IObservable<Net.HTTPResult> Post (string page, JToken payload)
			{
				var inst = requester.Post (server + page);
				var token = this.token;
				if (!string.IsNullOrEmpty (token))
					inst.mReq.Headers.Add ("sas-accesstoken", token);

				return inst.Invoke (payload)
					.Do (ret => {
						paylod_handler((JObject)ret.protocol.payload);
					});
			}
		}
	}
}

