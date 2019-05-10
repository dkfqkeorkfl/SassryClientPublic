using System;
using System.Collections.Generic;
using Sas.Net;
using UnityEngine;

namespace Sas
{
	public enum ERRNO
	{
		UNKNOWN,
		MESSAGE,
		SYNTAX_ERROR,
		SYNTAX_ERROR_SQL,
		DB_ERROR,

		INVALID_AUTHENTICATION,
		INVALID_ID,
		INVALID_PASSWORD,
		INVALID_NICK,
		INVALID_EMAIL,
		INVALID_PRIVATE_KEY,
		INVALID_PROTOCOL,
		INVALID_UID,
		INVALID_OTP,
		INVALID_EQUIP,
		INVALID_TOKEN,
		INVALID_SIGNATURE,
		INVALID_ACCOUNT,
		INVALID_TARGET,

		ACCOUNT_DUMPLICATED_ID,
		ACCOUNT_DUMPLICATED_NICK,
		ACCOUNT_DUMPLICATED_EMAIL,
		ACCOUNT_NOT_FIND,
		ACCOUNT_BLOCKED, 


		ACCESS_FUll,

		DISSCONNECT_CONNECTION,
		LOCAL_HOST_NOT_FIND_EXIST,
		LOCAL_HOST_SIGNATURE_ERR,
		LOCAL_HANDLER_DUMPLICATED,

		REQUET_ALREADY_CONNECTING
	}

	[System.Serializable]
	public class Error
	{
		public long code { get; set; }
		public string what { get; set; }
	}

	public class Exception : System.Exception
	{
		public long code { get; private set; }

		public Exception(System.Exception inner) : base("", inner) {
			this.code = inner.ToErrnoOfSas ().ToErrCode ();
		}

		public Exception (long code, string what = "") : base (what)
		{
			this.code = code;
		}

		public Exception (Error error) : base (error.what == null ? "" : error.what)
		{
			this.code = error.code;
		}

		public override string Message {
			get {
				return this.InnerException != null ? this.InnerException.Message : base.Message;
			}
		}
	}

	public static class ExceptionExt
	{
		static Emapper<ERRNO> mapper = new Emapper<ERRNO>();
		public static ERRNO ToErrnoOfSas(this System.Exception e)
		{
			var exception = e as Sas.Exception;
			return exception != null ? mapper.ToEnum(exception.code) : Sas.ERRNO.MESSAGE;
		}

		public static string ToErrstrOfSas (this System.Exception e)
		{
			var exception = e as Sas.Exception;
			return Enum.GetName (typeof(Sas.ERRNO), exception != null ? exception.ToErrnoOfSas() : Sas.ERRNO.MESSAGE);

		}

		public static long ToErrCode(this ERRNO e)
		{
			return mapper.ToHash (e);
		}
	}
}