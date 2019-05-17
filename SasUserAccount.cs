using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;
using Sas;
using Sas.Data;
using UniRx;
using UnityEngine;

namespace Sas
{
	namespace Component
	{
		public class AutoLoginFile
		{
			public uint version { get; set; }

			public string token { get; set; }

			public string password { get; set; }

			public string signature { get; set; }

			public AutoLoginFile ()
			{
				version = 1;
			}
		}

		public class AccessDelReqParam
		{
			public IList<ulong> targets { get; set; }
		}

		public class AccountLoginReqParam
		{
			public int sns { get; set; }

			public long sid { get; set; }

			public byte[] email { get; set; }

			public byte[] token { get; set; }

			public int platform { get; set; }

			public string equip { get; set; }

			public string app { get; set; }

			public string ver { get; set; }

			public string os { get; set; }

			public string model { get; set; }

			public AccountLoginReqParam ()
			{
				sns = SNS.SAS.GetHashCode ();
				sid = 0;

				platform = Application.platform.GetHashCode ();
				equip = SystemInfo.deviceUniqueIdentifier;
				app = Application.identifier;
				ver = Application.version;
				os = SystemInfo.operatingSystem;
				model = SystemInfo.deviceModel;
			}
		}

		public class AccountSignUpReqParam
		{
			public int sns { get; set; }

			public long sid { get; set; }

			public byte[] email { get; set; }

			public byte[] token { get; set; }

			public byte[] public_key { get; set; }

			public byte[] private_key { get; set; }

			public byte[] authentication { get; set; }

			public byte[] test { get; set; }

			public AccountSignUpReqParam ()
			{
				sns = SNS.SAS.GetHashCode ();
				sid = 0;
			}
		}

		public class Account
		{
			Context context { get; set; }

			byte[] authentication { get; set; }

			public Account (Context ctx)
			{
				Debug.Assert (ctx != null);
				context = ctx;
				context.handler.Add ("/Account/Login", token => context.login_result = token.ToObject<AccountLoginResult> ());
				context.handler.Add ("/Account/AccessOpen", token => {
					var equip = token.ToObject<AccountAccessEquipResult> ();
					context.login_result.equips = context.login_result.equips
						.Where (e => e.id == equip.id)
						.DefaultIfEmpty (null)
						.Select (e => e != null ? e : equip)
						.ToArray ();
				});
				context.handler.Add ("/Account/AccessDel", token => {
					var targets = token.ToObject<ulong[]> ();
					context.login_result.equips = context.login_result.equips
						.Where (e => Array.Find (targets, t => e.id == t) == null)
						.ToArray ();
				});
			}

			~Account ()
			{
				context.handler.Del (new string[] {
					"/Account/Login",
					"/Account/Login",
					"/Account/AccessDel"
				});
			}

			static byte[] MakeAuthentication (JToken token)
			{
				var cipher = token.ToObject<byte[]> ();
				using (var aes = new System.Security.Cryptography.AesManaged ()) {
					using (var key = Sas.SecureHelper.APP_KEY)
						aes.Key = key.GetByte (Convert.FromBase64String);
					using (var iv = Sas.SecureHelper.APP_IV)
						aes.IV = iv.GetByte (Convert.FromBase64String);

					using (var question = aes.Decrypt (cipher.ToArray ())) {
						var val = SasUtil.Evaluate (question.GetByte (b => b));
						question.AppendChar ('=');
						foreach (var ch in val.ToString ()) {
							question.AppendChar (ch);
						}

						return question.GetByte (aes.Encrypt);
					}

				}
			}

			public UniRx.IObservable<string> Authentication ()
			{
				// 추후 런쳐 사
				return context.Get ("/Account/Authentication")
                    .Select (result => {
					authentication = MakeAuthentication (result.protocol.payload ["/Account/Authentication"]);
					return result.protocol.command;
				});
			}

			public UniRx.IObservable<Sas.Net.HTTPResult> SignUp (AccountSignupParam param)
			{
				return Observable.Range (0, 1)
                    .SelectMany (_ => {
					var req = new AccountSignUpReqParam ();
					using (var aes = new System.Security.Cryptography.AesManaged ()) {
						using (var iv = SecureHelper.APP_IV) {
							aes.IV = iv.GetByte (Convert.FromBase64String);
						}

						using (var key = SecureHelper.APP_KEY) {
							aes.Key = key.GetByte (Convert.FromBase64String);
						}

						req.authentication = authentication;
						req.token = param.password.GetByte (aes.Encrypt);
						req.email = param.email.GetByte (aes.Encrypt);
						using (var rsa = new System.Security.Cryptography.RSACryptoServiceProvider ()) {
							req.public_key = aes.Encrypt (rsa.ToXmlString (false));
							req.private_key = rsa.ExportPrivateKey ().GetByte (aes.Encrypt);
							req.test = param.email.GetByte (rsa.Encrypt);
						}
					}

					var json = JObject.FromObject (req);
					return context.Post ("/Account/SignUp", json);
				});
			}

			UniRx.IObservable<Net.HTTPResult> LoginAsSas (AccountLoginParam param)
			{
				return Observable.Range (0, 1)
                    .SelectMany (_ => {
					var req = new AccountLoginReqParam ();
					using (var aes = new System.Security.Cryptography.AesManaged ()) {
						using (var iv = SecureHelper.APP_IV) {
							aes.IV = iv.GetByte (Convert.FromBase64String);
						}

						using (var key = SecureHelper.APP_KEY) {
							aes.Key = key.GetByte (Convert.FromBase64String);
						}

						req.email = param.email.GetByte (aes.Encrypt);
						req.token = param.password.GetByte (aes.Encrypt);
					}

					var json = JObject.FromObject (req);
					return context.Post ("/Account/Login", json);
				});
			}

			UniRx.IObservable<Net.HTTPResult> LoginAsAuto (AccountLoginParam argc)
			{
				return Observable.Range (0, 1)
                    .SelectMany (_ => {
					JsonDB db = new JsonDB ("account");
					var obj = db.Get (argc.extension);
					if (obj.Type == JTokenType.Null)
						throw new Sas.Exception (Sas.ERRNO.LOCAL_HOST_NOT_FIND_EXIST.ToErrCode ());

					AutoLoginFile body = obj.ToObject<AutoLoginFile> ();
					;
					var hashed = body.signature;
					body.signature = null;

					var json = Newtonsoft.Json.JsonConvert.SerializeObject (body);
					var pbkdf = new Rfc2898DeriveBytes (json, SecureHelper.APP_SALT.GetByte (Convert.FromBase64String), 3483);
					var signature = Convert.ToBase64String (pbkdf.GetBytes (256));
					if (!string.Equals (signature, hashed))
						throw new Sas.Exception (Sas.ERRNO.LOCAL_HOST_SIGNATURE_ERR.ToErrCode ());

					var param = new AccountLoginReqParam ();
					param.sns = SNS.AUTO.GetHashCode ();
					param.token = Convert.FromBase64String (body.password);
					var inst = context.requester.Post (context.server + "/Account/LoginAuto");
					inst.mReq.Headers.Add ("sas-accesstoken", body.token);
					return inst.Invoke (JObject.FromObject (param));
				});
			}

			public UniRx.IObservable<Net.HTTPResult> Login (AccountLoginParam param)
			{
				return Observable.Range (0, 1)
                    .SelectMany (_ => {
					switch (param.sns) {
					case Data.SNS.AUTO:
						return LoginAsAuto (param);
					default:
						break;
					}

					return LoginAsSas (param);
				});
			}

			public UniRx.IObservable<Net.HTTPResult> AccessDel (IList<ulong> targets)
			{
				return Observable.Range (0, 1)
                    .SelectMany (_ => {
					if (context.token == null)
						throw new System.MissingFieldException ("context", "otp");

					var param = new AccessDelReqParam { targets = targets };
					return context.Post ("/Account/AccessDel", JObject.FromObject (param));
				});
			}

			public UniRx.IObservable<Net.HTTPResult> AccessOpen ()
			{
				return Observable.Range (0, 1)
                    .SelectMany (_ => {
					if (context.token == null)
						throw new System.MissingFieldException ("context", "otp");

					return context.Post ("/Account/AccessOpen", null);
				});
			}

			public UniRx.IObservable<bool> DumpAutoLogin (string filename)
			{
				return Observable.Range (0, 1)
                    .Select (_ => {
					if (context.accept_equip == null)
						throw new System.MissingFieldException ("context", "accept_equip");

					var body = new AutoLoginFile ();
					body.token = context.token;

					using (var rsa = new System.Security.Cryptography.RSACryptoServiceProvider ()) {
						rsa.FromXmlString (context.public_key);
						var encrypted = rsa.Encrypt (context.token_time.ToString ());
						body.password = Convert.ToBase64String (encrypted);
					}
					var json = Newtonsoft.Json.JsonConvert.SerializeObject (body);
					var pbkdf = new Rfc2898DeriveBytes (json, SecureHelper.APP_SALT.GetByte (Convert.FromBase64String), 3483);
					body.signature = Convert.ToBase64String (pbkdf.GetBytes (256));
					JsonDB db = new JsonDB ("account");
					var obj = JToken.FromObject (body);
					db.Put (filename, obj);
					return true;
				});
			}
		}

	}
}