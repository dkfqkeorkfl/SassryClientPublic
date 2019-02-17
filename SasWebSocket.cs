using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Sas;
using UniRx;
using UnityEngine;

namespace Sas
{
	namespace Net
	{
		public class ClientSocket
		{
			WebSocket socket { get; set; }

			public bool is_conn { get { return connection != null; } }

			PROTOCOL mLastSend = new PROTOCOL (), mLastRecv = new PROTOCOL ();

			System.IDisposable connection { set; get; }

			UniRx.Subject<PROTOCOL> packethole { get; set; }

			public int handle {
				get {
					return socket.GetHashCode ();
				}
			}

			public ClientSocket (WebSocket socket)
			{
				this.socket = socket;
			}

			public static IObservable<ClientSocket> Connect (string url)
			{
				return Observable.FromCoroutine<ClientSocket> (observe => ConnectImpl (url, observe))
                    .Do (socket => {
					socket.packethole = new UniRx.Subject<PROTOCOL>();
					socket.connection = 
							Observable.FromCoroutine<PROTOCOL> (
						(observer) => RecvImpl (socket, observer))
                            .Subscribe (
						protocol => {
							socket.packethole.OnNext (protocol);
							socket.mLastRecv = protocol;
						},
						exception => {
							socket.packethole.OnError (exception);
							socket.CloseImpl ();
						});
				});
			}

			public bool Send (long command, Newtonsoft.Json.Linq.JToken payload)
			{
				if (!is_conn) {
					return false;
				}
				var packet = new PROTOCOL ();
				packet.ver = PROTOCOL.VERSION;
				packet.serial = mLastSend.serial + 1;
				packet.utc = UnixDateTime.ToUnixTimeMilliseconds (System.DateTime.UtcNow);
				packet.command = command;
				packet.payload = payload;

				var json = Newtonsoft.Json.JsonConvert.SerializeObject (packet);
				socket.SendString (json);
				mLastSend = packet;
				return true;
			}

			public void Close ()
			{
				packethole.OnCompleted ();
				socket.Close ();
				CloseImpl ();
			}

			public void CloseImpl ()
			{
				connection.Dispose ();
				connection = null;

			}

			public IObservable<PROTOCOL> Recv()
			{
				return packethole.AsObservable ();
			}
			static IEnumerator RecvImpl (ClientSocket client, IObserver<PROTOCOL> observer)
			{
				while (true) {
					var recved = client.socket.RecvString ();
					if (!string.IsNullOrEmpty (client.socket.error)) {
						observer.OnError (new System.Exception (client.socket.error));
						break;
					}

					if (!string.IsNullOrEmpty (recved)) {
						var protocol = Newtonsoft.Json.JsonConvert.DeserializeObject<PROTOCOL> (recved);
						observer.OnNext (protocol);
					}
					yield return null;
				}
			}

			static IEnumerator ConnectImpl (string url, IObserver<ClientSocket> observer)
			{
				System.Uri url_info = new System.Uri (url);
				var socket = new WebSocket (url_info);
				yield return socket.Connect ().ToYieldInstruction (false);

				if (!string.IsNullOrEmpty (socket.error)) {
					observer.OnError (new System.Exception (socket.error));
					yield break;
				}

				observer.OnNext (new ClientSocket (socket));
				observer.OnCompleted ();
			}
		}
	}

}