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
			public PROTOCOL last_send { get; private set; }
			public PROTOCOL last_recv { get; private set; }
			public ProtocolHandler handler { get; private set; }
			Util.Websocket socket { get; set; }

			public Websocket(Util.Websocket socket, ProtocolHandler handler)
			{
				this.socket = socket;
				this.handler = handler;

				this.last_send = new PROTOCOL ();
				this.last_recv = new PROTOCOL ();
			}

			public UniRx.IObservable<PROTOCOL> Recv()
			{
				return this.socket.Recv()
					.Select(bytes=> {
						var text = System.Text.Encoding.UTF8.GetString (bytes);
						var protocol = Newtonsoft.Json.JsonConvert.DeserializeObject<PROTOCOL> (text);
						handler.Process(protocol.payload as Newtonsoft.Json.Linq.JObject);
						last_recv = protocol;
						return protocol;
					});
			}

			public UniRx.IObservable<bool> Send(string command, Newtonsoft.Json.Linq.JToken payload)
			{
				var packet = new PROTOCOL ();
				packet.ver = PROTOCOL.VERSION;
				packet.serial = last_send.serial + 1;
				packet.utc = UnixDateTime.ToUnixTimeMilliseconds (System.DateTime.UtcNow);
				packet.command = command;
				packet.payload = payload;
				last_send = packet;
				var json = Newtonsoft.Json.JsonConvert.SerializeObject (packet);
				return socket.Send (json);
			}
		}
	}

}