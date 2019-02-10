using System;
using System.Collections.Generic;
using Sas.Net;
using UnityEngine;

namespace Sas
{
	public enum ERRNO : uint
	{
		UNKNOWN,
		MESSAGE,
		SYNTAX_ERROR,
		DB_ERROR,
		INVALID_AUTHENTICATION,
		INVALID_ID,
		INVALID_PASSWORD,
		INVALID_NICK,
		INVALID_EMAIL,
		INVALID_PRIVATE_KEY,
		ACCOUNT_DUMPLICATED_ID,
		ACCOUNT_DUMPLICATED_NICK,
		ACCOUNT_DUMPLICATED_EMAIL,

		ACCOUNT_NOT_FIND,
		ACCOUNT_BLOCKED, 
		INVALID_UID,
		INVALID_OTP,
		INVALID_EQUIP,
		INVALID_TOKEN,
		INVALID_SIGNATURE,
		SYNTAX_ERROR_SQL,
		INVALID_ACCOUNT,
		ACCESS_FUll,
		INVALID_TARGET,

		LOCAL_HOST_NOT_FIND_EXIST,
		LOCAL_HOST_SIGNATURE_ERR,

		REQUET_ALREADY_CONNECTING
	}

	[System.Serializable]
	public class Error
	{
		public int errno { get; set; }
		public string what { get; set; }

		public ERRNO serial { get { return ErrnoToEnum (errno); } }

		static Dictionary<int, ERRNO> sErrnoTable;

		public static ERRNO ErrnoToEnum (int id)
		{
			if (sErrnoTable == null) {

				sErrnoTable = new Dictionary<int, ERRNO> ();
				foreach (var val in Enum.GetValues (typeof(ERRNO))) {
					var value = (uint)val;
					var name = Enum.GetName (typeof(ERRNO), value);
					sErrnoTable.Add ((int)SecureHelper.Crc32 (name), (ERRNO)value);
				}
			}

			ERRNO ret;
			if (!sErrnoTable.TryGetValue (id, out ret)) {
				Debug.LogWarning (string.Format ("cannot find type of errno{0}", id));
				return ERRNO.UNKNOWN;
			}

			return ret;

		}

	}

	public class Exception : System.Exception
	{
		ERRNO mErrno = ERRNO.UNKNOWN;

		public Exception(System.Exception inner) : base("", inner) {}
		public Exception (ERRNO errno, string what = "") : base (what)
		{
			this.errno = errno;
		}

		public Exception (Error error) : base (error.what == null ? "" : error.what)
		{
			this.errno = Error.ErrnoToEnum (error.errno);
		}
			
		public ERRNO errno { 
			get { return this.InnerException != null ? this.InnerException.GetErrno () : mErrno; } 
			private set { mErrno = value; } 
		}

		public override string Message {
			get {
				return this.InnerException != null ? this.InnerException.Message : base.Message;
			}
		}
	}

	public static class ExceptionExt
	{
		public static ERRNO GetErrno (this System.Exception e)
		{
			var exception = e as Sas.Exception;
			return exception != null ? exception.errno : Sas.ERRNO.MESSAGE;
		}

		public static string GetErrstr (this System.Exception e)
		{
			var exception = e as Sas.Exception;
			return Enum.GetName (typeof(Sas.ERRNO), exception != null ? exception.errno : Sas.ERRNO.MESSAGE);

		}
	}
}