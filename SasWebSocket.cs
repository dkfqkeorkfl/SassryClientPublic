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
		public class Websocket
		{
			public class SharedData
			{
				public Dictionary<int, Websocket> connected { get; private set; }

				public SharedData()
				{
					connected = new Dictionary<int, Websocket>();
				}
			}

			PROTOCOL mLastSend = new PROTOCOL (), mLastRecv = new PROTOCOL ();
			WebSocketSharp.WebSocket socket { get; set; }
			UniRx.Subject<PROTOCOL> subscribe { get; set; }
			public SharedData shared_data { get; private set; }

			public Websocket(WebSocketSharp.WebSocket socket, SharedData shared_data)
			{
				this.socket = socket;
				this.shared_data = shared_data;
				this.subscribe = new UniRx.Subject<PROTOCOL> ();

				socket.OnMessage += (sender, e) => {
					var text = System.Text.Encoding.UTF8.GetString (e.RawData);
					var protocol = Newtonsoft.Json.JsonConvert.DeserializeObject<PROTOCOL> (text);
					this.subscribe.OnNext(protocol);
					mLastRecv = protocol;
				};
				socket.OnClose += (sender, e) => { 
					shared_data.connected.Remove(socket.GetHashCode());
					this.subscribe.OnCompleted();
				};
				socket.OnError += (sender, e) => {
					this.subscribe.OnError(new System.Exception(e.Message));
				};
			}

			public UniRx.IObservable<PROTOCOL> Recv()
			{
				return subscribe.AsObservable ();
			}

			public UniRx.IObservable<bool> Send(long command, Newtonsoft.Json.Linq.JToken payload)
			{
				return UniRx.Observable.Create<bool> (observer => {
					var packet = new PROTOCOL ();
					packet.ver = PROTOCOL.VERSION;
					packet.serial = mLastSend.serial + 1;
					packet.utc = UnixDateTime.ToUnixTimeMilliseconds (System.DateTime.UtcNow);
					packet.command = command;
					packet.payload = payload;
					mLastSend = packet;
					var json = Newtonsoft.Json.JsonConvert.SerializeObject (packet);
					socket.SendAsync(json, success => {
						observer.OnNext(success);
						observer.OnCompleted();
					});

					return new Sas.DisposeAction(()=> {});
				});
			}
		}

		public class WebsocketMgr
		{
			public class Config
			{
				public System.Uri url;
				public string[] protocols;
				public Dictionary<string, string> coockie = new Dictionary<string, string>();
			}

			public Websocket.SharedData shared_data { get; private set; }
			public int size { get { return shared_data.connected.Count; }}

			public WebsocketMgr()
			{
				this.shared_data = new Websocket.SharedData ();
			}


			public UniRx.IObservable<Websocket> Connection(Config config)
			{
				return UniRx.Observable.Create<Websocket> (observer => {
					var impl = new WebSocketSharp.WebSocket(config.url.ToString(), config.protocols);
					System.EventHandler<WebSocketSharp.ErrorEventArgs> onError = (obj, e)=> {
						observer.OnError(new System.Exception(e.Message));
					};

					impl.OnError += onError;

					foreach(var pr in config.coockie)
						impl.SetCookie(new WebSocketSharp.Net.Cookie(pr.Key, pr.Value));
					var socket = new Websocket(impl, shared_data);
					impl.OnOpen += (sender, e) => {
						impl.OnError -= onError;
						this.shared_data.connected.Add(socket.GetHashCode(), socket);

						observer.OnNext(socket);
						observer.OnCompleted();
					};
					impl.ConnectAsync();

					return new Sas.DisposeAction(()=> {});
				});
			}
		}

//		public class ClientSocket
//		{
//			WebSocketSharp.WebSocket ws { get; set; }
//			WebSocket socket { get; set; }
//
//			public bool is_conn { get { return connection != null; } }
//
//			PROTOCOL mLastSend = new PROTOCOL (), mLastRecv = new PROTOCOL ();
//
//			System.IDisposable connection { set; get; }
//
//			UniRx.Subject<PROTOCOL> packethole { get; set; }
//
//			public int handle {
//				get {
//					return socket.GetHashCode ();
//				}
//			}
//
//			public ClientSocket (WebSocket socket)
//			{
////				new WebSocketSharp.WebSocket(
//				this.socket = socket;
//			}
//
//			public static IObservable<ClientSocket> Connect (string url)
//			{
//				return Observable.FromCoroutine<ClientSocket> (observe => ConnectImpl (url, observe))
//                    .Do (socket => {
//					socket.packethole = new UniRx.Subject<PROTOCOL>();
//					socket.connection = 
//							Observable.FromCoroutine<PROTOCOL> (
//						(observer) => RecvImpl (socket, observer))
//                            .Subscribe (
//						protocol => {
//							socket.packethole.OnNext (protocol);
//							socket.mLastRecv = protocol;
//						},
//						exception => {
//							socket.packethole.OnError (exception);
//							socket.CloseImpl ();
//						});
//				});
//			}
//
//			public IObservable<int> Send (long command, Newtonsoft.Json.Linq.JToken payload)
//			{
//				if (!is_conn) {
//					throw new Exception(ERRNO.DISSCONNECT_CONNECTION.ToErrCode());
//				}
//				var packet = new PROTOCOL ();
//				packet.ver = PROTOCOL.VERSION;
//				packet.serial = mLastSend.serial + 1;
//				packet.utc = UnixDateTime.ToUnixTimeMilliseconds (System.DateTime.UtcNow);
//				packet.command = command;
//				packet.payload = payload;
//
//				var json = Newtonsoft.Json.JsonConvert.SerializeObject (packet);
//				socket.SendString (json);
//				mLastSend = packet;
//				return Observable.Range (0, 1).Select (_ => json.Length);
//			}
//
//			public void Close ()
//			{
//				packethole.OnCompleted ();
//				socket.Close ();
//				CloseImpl ();
//			}
//
//			void CloseImpl ()
//			{
//				connection.Dispose ();
//				connection = null;
//
//			}
//
//			public IObservable<PROTOCOL> Recv()
//			{
//				return packethole.AsObservable ();
//			}
//			static IEnumerator RecvImpl (ClientSocket client, IObserver<PROTOCOL> observer)
//			{
//				while (true) {
//					var recved = client.socket.RecvString ();
//					if (!string.IsNullOrEmpty (client.socket.error)) {
//						observer.OnError (new System.Exception (client.socket.error));
//						break;
//					}
//
//					if (!string.IsNullOrEmpty (recved)) {
//						var protocol = Newtonsoft.Json.JsonConvert.DeserializeObject<PROTOCOL> (recved);
//						observer.OnNext (protocol);
//					}
//					yield return null;
//				}
//			}
//
//			static IEnumerator ConnectImpl (string url, IObserver<ClientSocket> observer)
//			{
//				System.Uri url_info = new System.Uri (url);
//				var socket = new WebSocket (url_info);
//				yield return socket.Connect ().ToYieldInstruction (false);
//
//				if (!string.IsNullOrEmpty (socket.error)) {
//					observer.OnError (new System.Exception (socket.error));
//					yield break;
//				}
//
//				observer.OnNext (new ClientSocket (socket));
//				observer.OnCompleted ();
//			}
//		}
	}

}