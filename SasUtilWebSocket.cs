using System;

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Sas;
using UniRx;
using UnityEngine;

namespace Sas
{
	namespace Util
	{
		public class Websocket
		{
			public class SharedData
			{
				public Dictionary<int, Websocket> connected { get; private set; }

				public SharedData ()
				{
					this.connected = new Dictionary<int, Websocket> ();
				}
			}

			WebSocketSharp.WebSocket socket { get; set; }

			UniRx.Subject<byte[]> subscribe { get; set; }

			public SharedData shared_data { get; private set; }

			public Websocket (WebSocketSharp.WebSocket socket, SharedData shared_data)
			{
				this.socket = socket;
				this.shared_data = shared_data;
				this.subscribe = new UniRx.Subject<byte[]> ();

				socket.OnMessage += (sender, e) => {
					this.subscribe.OnNext (e.RawData);
				};
				socket.OnClose += (sender, e) => { 
					shared_data.connected.Remove (socket.GetHashCode ());
					this.subscribe.OnCompleted ();
				};
				socket.OnError += (sender, e) => {
					this.subscribe.OnError (new System.Exception (e.Message));
				};
			}

			public UniRx.IObservable<byte[]> Recv ()
			{
				return subscribe.AsObservable ();
			}

			public UniRx.IObservable<bool> Send (byte[] bytes)
			{
				return UniRx.Observable.Create<bool> (observer => {
					socket.SendAsync (bytes, success => {
						observer.OnNext (success);
						observer.OnCompleted ();
					});

					return new Sas.DisposeAction (() => {});
				});
			}

			public UniRx.IObservable<bool> Send (string data)
			{
				return UniRx.Observable.Create<bool> (observer => {
					socket.SendAsync (data, success => {
						observer.OnNext (success);
						observer.OnCompleted ();
					});

					return new Sas.DisposeAction (() => {});
				});
			}
		}

		public class WebsocketMgr
		{
			public class Config
			{
				public System.Uri url;
				public string[] protocols;
				public Dictionary<string, string> coockie = new Dictionary<string, string> ();
			}

			public Websocket.SharedData shared_data { get; private set; }

			public int size { get { return shared_data.connected.Count; } }

			public WebsocketMgr ()
			{
				this.shared_data = new Websocket.SharedData ();
			}

			public UniRx.IObservable<Websocket> Connection (Config config)
			{
				return UniRx.Observable.Create<Websocket> (observer => {
					var impl = new WebSocketSharp.WebSocket (config.url.ToString (), config.protocols);
					System.EventHandler<WebSocketSharp.ErrorEventArgs> onError = (obj, e) => {
						observer.OnError (new System.Exception (e.Message));
					};

					impl.OnError += onError;

					foreach (var pr in config.coockie)
						impl.SetCookie (new WebSocketSharp.Net.Cookie (pr.Key, pr.Value));
					
					var socket = new Websocket (impl, shared_data);
					impl.OnOpen += (sender, e) => {
						impl.OnError -= onError;
						this.shared_data.connected.Add (socket.GetHashCode (), socket);

						observer.OnNext (socket);
						observer.OnCompleted ();
					};
					impl.ConnectAsync ();

					return new Sas.DisposeAction (() => {
					});
				});
			}
		}
	}
}

