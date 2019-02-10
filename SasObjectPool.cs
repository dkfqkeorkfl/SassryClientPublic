using UnityEngine;
using System.Collections.Generic;
using SPStudios.Tools;
using System;

namespace Sas
{
	public interface IPool<T>
	{
		T Alloc();
		T[] Alloc (int size);

		void Dealloc(T item);
		int  capacity { get; set; }
		int  inactive { get;}
		bool empty    { get; }
	}

	public class SasPool<T> :Singleton< SasPool<T> > where T : class, new()
	{
		private class Defulat : IPool<T>
		{
			const int DEF_DEFAULT_CAPACUTY = 20;

			private readonly Stack<T> mInactive = new Stack<T>();

			public int  capacity { get; set; }
			public int  inactive { get { return mInactive.Count;      } }
			public bool empty    { get { return mInactive.Count == 0; } }

			public Defulat()
			{
				capacity = DEF_DEFAULT_CAPACUTY;
			}

			public T Alloc()
			{
				return empty ? new T() : mInactive.Pop ();
			}

			public T[] Alloc(int size)
			{
				T[] items = new T[size];
				for (int i = 0; i < size; ++i)
					items [i] = Alloc ();

				return items;
			}

			public void Dealloc(T item)
			{
				mInactive.Push (item);
			}
		}

		readonly Defulat DEFAULT_ADAPTER = new Defulat();
		IPool<T> mAdapter = null;
		IPool<T> adapter 
		{ 
			get { return mAdapter != null ? mAdapter : DEFAULT_ADAPTER; }
			set { mAdapter = value; }
		}

		public static event Action<T> reset, release;
		public static int capacity { get { return SasPool<T>.Instance.adapter.capacity; } set { SasPool<T>.Instance.adapter.capacity = value; } }
		public static int inactive { get { return SasPool<T>.Instance.adapter.inactive; } }

		public static T Alloc()
		{
			return SasPool<T>.Instance.AllocImpl ();
		}

		public static T[] Alloc(int size)
		{
			return SasPool<T>.Instance.AllocImpl (size);
		}

		public static void Dealloc(T item)
		{
			SasPool<T>.Instance.DeallocImpl (item);
		}

		public static void Dealloc(T[] items)
		{
			SasPool<T>.Instance.DeallocImpl (items);
		}

		T AllocImpl()
		{
			T item = adapter.Alloc ();

			Reset (item);
			return item;
		}

		T[] AllocImpl(int size)
		{
			T[] items = adapter.Alloc (size);
			Reset (items, size);
			return items;
		}

		void DeallocImpl(T item)
		{
			IPool<T> a = adapter;
			Release(item);

			if (a.inactive < a.capacity == false)
				return;

			a.Dealloc (item);
		}

		void DeallocImpl(T[] items)
		{
			foreach (var item in items)
				Dealloc (item);
		}

		void Reset(T item)   
		{ 
			if (reset != null) reset (item); 
		}
		void Reset(T[] items, int size) 
		{ 
			if (reset == null)
				return;

			for (int i = 0; i < size; ++i)
				reset (items [i]);
		}
		void Release(T item) 
		{ 
			if( release != null ) release (item); 
		}
	}
}


//public class SasPoolObject  : IPool<GameObject>
//{
//	private readonly List<BetterObjectPool> mPools = new List<BetterObjectPool>();
//	private BetterObjectPool mCached = null;
//
//	public int capacity { get { return mCached.initialMax; } set{ mCached.initialMax = value; } }
//	public int inactive { get { return mCached.inactive;} }
//
//	public GameObject Alloc ()
//	{
//		if (mCached == null)
//			return null;
//
//		return mCached.GetInstanceFromPool ();
//	}
//
//	public GameObject Alloc(GameObject obj, Transform parent, Vector3 pos, Quaternion rotation)
//	{
//		if (null == obj)
//			return null;
//		BetterObjectPool pool = GetPool(obj);
//		if ( pool == null )
//			return null;
//
//		return pool.GetPoolObject (parent, pos, rotation);
//	}
//
//	public GameObject[] Alloc (int size)
//	{
//		GameObject[] objs = new GameObject[size];
//		for (int i = 0; i < size; ++i)
//			objs [i] = Alloc ();
//		return objs;
//	}
//		
//	public void Dealloc (GameObject item)
//	{
//		BetterObjectPool pool = GetPool(item);
//		if ( pool == null )
//			return;
//
//		pool.RemoveInstanceFromPool (item);
//	}
//
//	public bool Contain(GameObject obj)
//	{
//		return GetPool (obj) != null;
//	}
//		
//	public BetterObjectPool GetPool(GameObject instance)
//	{
//		var pbType = UnityEditor.PrefabUtility.GetPrefabParent (instance);
//		pbType = pbType == null ? UnityEditor.PrefabUtility.GetPrefabObject (instance) : pbType;
//		if (mCached != null && pbType == mCached.PrefabType )
//			return mCached;
//		 
//		int index = mPools.FindIndex ((val) => val.PrefabType == pbType);
//		if ( index < 0 ) {
//			var obj = new GameObject (pbType.name + "Pool");
//			var pool = obj.AddComponent<BetterObjectPool> ();
//			pool.ObjectPrefab = instance;
//			mPools.Add (pool);
//			index = mPools.Count - 1;
//		} 
//
//		mCached = mPools [index];
//		return mCached;
//	}
//		
//}
//	
//public static class SasPoolExtension
//{
//	private static readonly SasPoolObject mPoolObject = new SasPoolObject();
//	static SasPoolExtension()
//	{
//		SasPool<GameObject>.Instance.adapter = mPoolObject;
//		SasPool<GameObject>.Instance.release += (obj) => mPoolObject.GetPool (obj);
//		SasPool<GameObject>.Instance.reset += (obj) => {
//			obj.SendMessage ("OnAlloc",SendMessageOptions.DontRequireReceiver);
//		};
//		SasPool<GameObject>.Instance.release += (obj) => {
//			obj.SendMessage ("OnDealloc",SendMessageOptions.DontRequireReceiver);
//		};
//	}
//
//	public static BetterObjectPool Ready(this SasPool<GameObject> pool, GameObject obj)
//	{
//		return mPoolObject.GetPool (obj);
//	}
//
//	public static bool Contain(this SasPool<GameObject> pool, GameObject obj)
//	{
//		return mPoolObject.Contain (obj);
//	}
//	public static GameObject Alloc(this SasPool<GameObject> pool, GameObject obj, Transform parent)
//	{
//		return Alloc (pool, obj, parent, Vector3.zero, Quaternion.identity);
//	}
//	public static GameObject Alloc(this SasPool<GameObject> pool, GameObject obj, Transform parent, Vector3 pos)
//	{
//		return Alloc (obj, parent, pos, Quaternion.identity);
//	}
//	public static GameObject Alloc(this SasPool<GameObject> pool, GameObject obj, Transform parent, Quaternion rotation)
//	{
//		return Alloc (pool, obj, parent, Vector3.zero, rotation);
//	}
//	public static GameObject Alloc(this SasPool<GameObject> pool, GameObject obj, Transform parent, Vector3 pos, Quaternion rotation)
//	{
//		return Alloc(obj, parent, pos, rotation);
//	}
//	private static GameObject Alloc(GameObject obj, Transform parent, Vector3 pos, Quaternion rotation)
//	{
//		GameObject pooled = mPoolObject.Alloc (obj, parent, pos, rotation);
//		SasPool<GameObject>.Instance.Reset(pooled);
//		return pooled;
//	}
//}