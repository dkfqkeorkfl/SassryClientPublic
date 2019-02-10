using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Sas
{
	public static class Singletons {

		static readonly Dictionary<System.Type, System.Object> mObjs = new Dictionary<System.Type, System.Object>();
		static readonly GameObject mRoot;
		static readonly System.Type mMonoType = typeof(MonoBehaviour);

		static Singletons()
		{
			mRoot = new GameObject ("__singletons");
		}

		public static T Get<T>() where T : new()
		{
			var type = typeof(T);
			System.Object obj;
			if (mObjs.TryGetValue (type, out obj)) {
				return (T)obj;
			}

			if (mMonoType.IsAssignableFrom (type)) {
				obj = CreateMono (type);

			} else {
				obj = Create (type);
			}
			mObjs.Add (type, obj);
			return (T)obj;
		}

		public static System.Object CreateMono(System.Type type)
		{
			return mRoot.AddComponent (type);
		}
		public static System.Object Create(System.Type type)
		{
			return Activator.CreateInstance(type);
		}
	}

}
