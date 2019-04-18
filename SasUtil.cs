using System;
using UnityEngine;
using System.Collections.Generic;

namespace Sas
{
	public class DisposeAction : System.IDisposable
	{
		System.Action action;
		public DisposeAction(System.Action action)
		{
			this.action = action;
		}
		public void Dispose ()
		{
			this.action ();
		}
	}

	static class SasUtil
	{
		

		public static long nowUtc { get { return UnixDateTime.ToUnixTimeMilliseconds (System.DateTime.UtcNow); } }

		public static double Evaluate (string expression)
		{
			var xsltExpression =
				string.Format ("number({0})",
					new System.Text.RegularExpressions.Regex (@"([\+\-\*])").Replace (expression, " ${1} ")
					.Replace ("/", " div ")
					.Replace ("%", " mod "));

			// ReSharper disable PossibleNullReferenceException
			return (double)new System.Xml.XPath.XPathDocument (new System.IO.StringReader ("<r/>"))
					.CreateNavigator ()
					.Evaluate (xsltExpression);
			// ReSharper restore PossibleNullReferenceException
		}

		public static void ForeachChild (this Transform tm, Action<Transform> a)
		{
			int count = tm.childCount;
			for (int i = 0; i < count; ++i) {
				Transform child = tm.GetChild (i);
				a (child);
				child.ForeachChild (a);
			}
		}

		public static UniRx.IObservable<Transform> OnChildrenAsObserable (this Transform tm)
		{
			return UniRx.Observable.Create<Transform> ((observer) => {
				tm.ForeachChild (child => observer.OnNext (child));
				observer.OnCompleted ();
				return UniRx.Disposable.Create (() => {
				});
			});
		}

		/// <summary>
		/// Search depth-first Traversal
		/// </summary>
		public static Transform FindDST (this Transform tm, Predicate<Transform> pred)
		{
			Transform rtn = null;
			int count = tm.childCount;
			for (int i = 0; i < count && rtn == null; ++i) {
				Transform child = tm.GetChild (i);

				if (pred (child))
					rtn = child;
				else
					rtn = child.FindDST (pred);
			}
			return rtn;
		}
			
		public static int CountChildTotal (this Transform tm)
		{
			int total = 0;
			CountChildTotal (tm, ref total);
			return total;
		}

		public static void CountChildTotal (this Transform tm, ref int total)
		{
			int count = tm.childCount;
			total += count;
			for (int i = 0; i < count; ++i) {
				CountChildTotal (tm.GetChild (i), ref total);
			}
		}

		public static KeyValuePair<int,int> EqualRange<T>(this IList<T> values, T target, Func<T,T, bool> less)
		{
			int lowerBound = LowerBound(values, 0, values.Count, target, less);
			int upperBound = UpperBound(values, lowerBound, values.Count, target, less);

			return new KeyValuePair<int, int>(lowerBound, upperBound);
		}

		public static int LowerBound<T, U>(this IList<T> values, int first, int last, U target, Func<T,U, bool> less)
		{
			int left  = first;
			int right = last;

			while (left < right)
			{
				int mid = left + (right - left)/2;
				var middle = values[mid];

				if (less(middle, target))
					left = mid + 1;
				else
					right = mid;
			}

			return left;
		}

		public static int UpperBound<T, U>(this IList<T> values, int first, int last, U target, Func<T,U, bool> less)
		{
			int left  = first;
			int right = last;

			while (left < right)
			{
				int mid = left + (right - left) / 2;
				var middle = values[mid];

				if (less(middle, target))
					right = mid;
				else
					left = mid + 1;
			}

			return left;
		}
	}
}

