using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Newtonsoft.Json.Linq;
using UniRx;
using UnityEngine;

namespace Sas {
    namespace Net {
        public class ExceptionRes : Sas.Exception {
            public ExceptionRes (System.Net.HttpWebResponse res, Error error) : base (error) {
                this.res = res;
            }

            public System.Net.HttpWebResponse res { get; private set; }
        }

        public class ExceptionReq : Sas.Exception {
            public ExceptionReq (System.Net.HttpWebRequest req, JToken param, System.Exception inner) : base (inner) {
                this.req = req;
                this.param = param;
            }

            public System.Net.HttpWebRequest req { get; private set; }

            public JToken param { get; private set; }

            public System.Net.HttpWebResponse res {
                get {
                    var e = this.InnerException as ExceptionRes;
                    return e != null ? e.res : null;
                }
            }
        }

        public class HTTPResult {
            public HttpWebRequest req;
            public HttpWebResponse res;
            public PROTOCOL protocol;
            //			public Newtonsoft.Json.Linq.JObject protocol;
            public JToken param;
        }

        public class HTTPSRequest {
            public class SharedState {
                System.Collections.Generic.LinkedList<HttpWebRequest> mProgress = new System.Collections.Generic.LinkedList<HttpWebRequest> ();

                public int send_cnt { get; private set; }

                public event Action<HttpWebRequest, HttpWebResponse, System.Exception> post_handler;

                public uint index { get; set; }

                public SharedState () {
                    index = 0;
                    send_cnt = 0;
                }

                public void Add (HttpWebRequest req) {
                    ++send_cnt;
                    mProgress.AddLast (req);
                }

                public void Del (HttpWebRequest req, HttpWebResponse res, System.Exception exception = null) {

                    var ret = mProgress.Remove (req);
                    if (ret == false)
                        Debug.Log (string.Format ("[{0}] Error that connot find request : {1}",
                            System.Reflection.MethodBase.GetCurrentMethod ().Name, req.Address.ToString ()));
                    post_handler (req, res, exception);
                }

                public bool Has (HttpWebRequest req) {
                    return mProgress.Where (data => {
                            return data == req;
                        })
                        .Take (1)
                        .Count () == 1;
                }

                public int Count (Predicate<HttpWebRequest> pred) {
                    return mProgress.Where (data => {
                            return pred (data);
                        })
                        .Count ();
                }

                public int Count (System.Text.RegularExpressions.Regex rx) {
                    Predicate<HttpWebRequest> pred = (data) => {
                        return rx.IsMatch (data.Connection);
                    };
                    return Count (pred);
                }
            }

            public class Inst {
                public HttpWebRequest mReq { get; private set; }

                SharedState mShared = null;

                public bool is_connecting { get { return mShared.Has (mReq); } }

                public Inst (SharedState shared, HttpWebRequest req) {
                    mReq = req;
                    mShared = shared;
                }

                public UniRx.IObservable<HTTPResult> Invoke (JToken payload = null) {
                    return UniRx.Observable.Range (0, 1)
                        .Select (_ => {
                            if (is_connecting)
                                throw new Sas.Exception (Sas.ERRNO.REQUET_ALREADY_CONNECTING);
                            return _;
                        })
                        .SelectMany (_ => {
                            mShared.Add (mReq);
                            if (payload == null)
                                return UniRx.Observable.Range (0, 1);
                            return mReq.GetRequestStreamAsObservable ()
                                .Select (stream => {

                                    mReq.ContentType = "application/json";
                                    var packet = new PROTOCOL ();
                                    packet.ver = PROTOCOL.VERSION;
                                    packet.serial = mShared.send_cnt + 1;
                                    packet.utc = UnixDateTime.ToUnixTimeMilliseconds (System.DateTime.UtcNow);
                                    packet.command = COMMAND.POST.GetHashCode ();
                                    packet.payload = payload;
                                    // new JArray(payload.ToList());
                                    var pretty_json = Newtonsoft.Json.JsonConvert.SerializeObject (packet);

                                    var bytes = System.Text.Encoding.UTF8.GetBytes (pretty_json);
                                    stream.BeginWrite (bytes, 0, bytes.Length, (handle) => {
                                        stream.EndWrite (handle);
                                        stream.Dispose ();
                                    }, null);
                                    return _;
                                });
                        })
                        .SelectMany (_ => {
                            return UniRx.Observable.Create<HTTPResult> (observer => {
                                var handle = mReq.GetResponseAsObservable ()
                                    .Select (response => {
                                        using (var ostream = response.GetResponseStream ()) {
                                            using (System.IO.StreamReader reader = new System.IO.StreamReader (ostream)) {
                                                ostream.Flush ();
                                                var data = reader.ReadToEnd ();

                                                var ret = new HTTPResult ();
                                                ret.req = mReq;
                                                ret.res = response;
                                                ret.param = payload;
                                                var root = JObject.Parse (data);
                                                ret.protocol = new PROTOCOL ();
                                                ret.protocol.ver = (long) root["ver"];
                                                ret.protocol.utc = (long) root["utc"];
                                                ret.protocol.serial = (long) root["serial"];
                                                ret.protocol.command = (long) root["command"];
                                                ret.protocol.payload = root["payload"];
                                                if (ret.protocol.ecommand == COMMAND.ERR) {
                                                    var jError = ret.protocol.GetPayload (token => {
                                                        var obj = (JObject) token;
                                                        var err = new Error {
                                                            errno = (int) obj["errno"],
                                                            what = (string) obj["what"],
                                                        };

                                                        return err;
                                                    });
                                                    throw new Sas.Net.ExceptionRes (response, jError);
                                                }

                                                return ret;
                                            }
                                        }
                                    })
                                    .Delay (TimeSpan.FromMilliseconds (1), UniRx.Scheduler.MainThread)
                                    .Do (protocol => mShared.Del (protocol.req, protocol.res))
                                    .Subscribe (data => observer.OnNext (data),
                                        err => {
                                            var exception = new Net.ExceptionReq (mReq, payload, err);
                                            UniRx.Observable.Range (0, 1)
                                                .SubscribeOnMainThread ()
                                                .Do (__ => {
                                                    mShared.Del (mReq, null, exception);
                                                    throw exception;
                                                })
                                                .SubscribeOnMainThread ()
                                                .Subscribe (__ => { },
                                                    inner => observer.OnError (inner));
                                        },
                                        () => observer.OnCompleted ());

                                return UniRx.Disposable.Create (() => {
                                    handle.Dispose ();
                                });
                            });
                        });
                }
            }

            public string cert_filename { get; private set; }

            SharedState mShared = new SharedState ();

            public event Action<HttpWebRequest, HttpWebResponse, System.Exception> post_handler {
                add {
                    mShared.post_handler += value;
                }
                remove {
                    mShared.post_handler -= value;
                }
            }

            System.Security.Cryptography.X509Certificates.X509Certificate2 cert { set; get; }

            static HTTPSRequest () {
                ServicePointManager.ServerCertificateValidationCallback += (o, certificate, chain, errors) => true;
            }

            public bool LoadCert (string filename) {
                var cert_text = Resources.Load<UnityEngine.TextAsset> (filename);
                if (!cert_text) {
                    return false;
                }

                cert_filename = filename;
                cert = new System.Security.Cryptography.X509Certificates.X509Certificate2 (cert_text.bytes);
                return true;
            }

            public Sas.Net.ClientSocket MakeSocket (string url) {
                var client = new Sas.Net.ClientSocket ();
                client.Connect (url);
                return client;
            }

            public Inst Get (string url) {
                Debug.Log (string.Format ("[{0}] GET : {1}", System.Reflection.MethodBase.GetCurrentMethod ().Name, url.ToString ()));
                var req = (System.Net.WebRequest.Create (url)) as HttpWebRequest;
                if (cert != null)
                    req.ClientCertificates.Add (cert);
                req.Method = "GET";
                req.Headers.Add (HttpRequestHeader.ContentLanguage, PreciseLocale.GetLanguage ());
                req.Headers.Add (HttpRequestHeader.ContentLocation, PreciseLocale.GetRegion ());

                return new Inst (mShared, req);
            }

            public Inst Post (Uri url) {
                Debug.Log (string.Format ("[{0}] POST : {1}", System.Reflection.MethodBase.GetCurrentMethod ().Name, url.ToString ()));
                var req = (System.Net.WebRequest.Create (url)) as HttpWebRequest;

                if (cert != null)
                    req.ClientCertificates.Add (cert);
                req.Method = "POST";
                req.Headers.Add (HttpRequestHeader.ContentLanguage, PreciseLocale.GetLanguage ());
                req.Headers.Add (HttpRequestHeader.ContentLocation, PreciseLocale.GetRegion ());

                return new Inst (mShared, req);
            }

            public Inst Post (string url) {
                var converted_url = new Uri (url);
                return Post (converted_url);
            }
        }
    }
}