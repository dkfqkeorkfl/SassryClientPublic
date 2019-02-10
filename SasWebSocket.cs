using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UniRx;

using System.Linq;

namespace Sas
{
	namespace Net
	{
		public class ClientSocket
		{
			
			WebSocket mSocket = null;

			System.IDisposable mExcuter = null;
			PROTOCOL mLastSend = new PROTOCOL (), mLastRecv = new PROTOCOL ();

			public event System.Action<PROTOCOL> on_recv;
			public event System.Action<bool> on_connect;
			public event System.Action on_close;

			public string error { get; private set; }


			public bool is_connecting {
				get {
					return mExcuter != null && mSocket == null;
				}
			}

			public bool is_connected {
				get {
					return mSocket != null; 
				}
			}

			public bool is_disconnected {
				get {
					return mExcuter == null && mSocket == null;  
				}
			}

			public int handle {
				get {
					return is_connected ? mSocket.GetHashCode () : 0;
				}
			}

			public ClientSocket ()
			{
				on_connect += (ret) => {
					if (ret == false) {
						mExcuter = null;
						return;
					}

				mExcuter = Observable.FromCoroutine<PROTOCOL> (
					(observer, cancellation_token) => RecvImpl (mSocket, observer, cancellation_token))
						.Subscribe (
					
					protocol => {
								if(mLastRecv.serial >= protocol.serial)
							{
								error = string.Format ("[{0}] protocol is invaild. socket is closed.", 
									System.Reflection.MethodBase.GetCurrentMethod ().Name);
								this.Close();
							}
						
						on_recv (protocol);
						mLastRecv = protocol;
					},
								
					exception => {
						error = exception.Message;
						on_close ();
					});
				};

				on_close += () => {
					if (mSocket != null)
						mSocket.Close ();

					mExcuter = null;
					mSocket = null;
				};
			}

			public bool Connect (string url)
			{
				if (!is_disconnected) {
					error = string.Format ("[{0}] socket is already connecting.", System.Reflection.MethodBase.GetCurrentMethod ().Name);
					return false;
				}

				//Observable.FromCoroutine<byte[]>(mSocket.Connect()).Subscribe(
				mExcuter = Observable.FromCoroutine<WebSocket> (
					(overserve, cancellation_token) =>
					ConnectImpl (url, overserve, cancellation_token))
					.Subscribe (
					socket => {
						mSocket = socket;
						on_connect (true);
					},
					expcetion => {
						error = expcetion.Message;
						on_connect (false);
					}
				);

				return true;
			}

			public bool Send (long command, byte[] payload)
			{
				if (!is_connected) {
					error = string.Format ("[{0}] socket isn't connecting.", System.Reflection.MethodBase.GetCurrentMethod ().Name);
					return false;
				}
				
				var packet = new PROTOCOL();
				packet.ver = PROTOCOL.VERSION;
				packet.serial = mLastSend.serial + 1;
				packet.utc = UnixDateTime.ToUnixTimeMilliseconds (System.DateTime.UtcNow);
				packet.command = command;
				packet.payload = payload;
					
				var pretty_json = JsonUtility.ToJson (packet);
				mSocket.SendString (pretty_json);
				mLastSend = packet;
				return true;
			}

			public void Close ()
			{
				if (mExcuter != null) {
					mExcuter.Dispose ();
					on_close ();
				}
			}

			static IEnumerator RecvImpl (WebSocket websocket, IObserver<PROTOCOL> observer, CancellationToken cancellation_token)
			{
				Debug.Assert (websocket != null);

				while (!cancellation_token.IsCancellationRequested) {
					var recved_str = websocket.RecvString ();
					if (!string.IsNullOrEmpty (websocket.error)) {
						observer.OnError (new System.Exception (websocket.error));
						break;
					}

					if (!string.IsNullOrEmpty (recved_str)) {
						var recved = JsonUtility.FromJson<PROTOCOL> (recved_str);
						observer.OnNext (recved);
					}
					yield return null;
				}
			}

			static IEnumerator ConnectImpl (string url, IObserver<WebSocket> observer, CancellationToken cancellationToken)
			{
				System.Uri url_info = new System.Uri (url);
				var socket = new WebSocket (url_info);
				yield return socket.Connect ().ToYieldInstruction (false, cancellationToken);

				if (cancellationToken.IsCancellationRequested) {
					if (string.IsNullOrEmpty (socket.error))
						socket.Close ();
					yield break;
				}

				if (!string.IsNullOrEmpty (socket.error)) {
					observer.OnError (new System.Exception (socket.error));
					yield break;
				}

				observer.OnNext (socket);
				observer.OnCompleted ();
			}
		}
	}

}