using System;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Collections.Generic;
using Foundation;
using System.Text.RegularExpressions;
using CoreText;

namespace SilverScreen.Shared
{
	public static partial class Extensions
	{
		public static string ToProperString(this Enum e)
		{
			return string.Join(" ", Regex.Split(e.ToString(), @"(?<!^)(?=[A-Z])"));
		}

		public static string Max(this string s, int maxLength)
		{
			if(s.Length <= maxLength)
				return s;

			return s.Substring(0, maxLength) + "...";
		}

		public static string ToTime(this TimeSpan span)
		{
			int seconds = (int)(span.TotalSeconds % 60);
			int minutes = (seconds / 60) % 60;
			int hours = (seconds / 3600); 

			if(hours > 0)
				return "{0:00}:{1:00}:{2:00}".Fmt(hours, minutes, seconds);

			if(minutes > 0)
				return "{0:00}:{1:00}".Fmt(minutes, seconds);

			if(seconds > 0)
				return "{0:00}".Fmt(seconds);

			return null;
		}

		public static string ToMicroString(this DateTime date, bool withTime = false)
		{
			if(withTime)
				return date.ToString("M/d/yy h:mm tt");
			
			return date.ToString("MM/dd/yyyy");
		}

		public static bool ContainsNoCase(this string s, string contains)
		{
			if(s == null || contains == null)
				return false;

			return s.IndexOf(contains, StringComparison.OrdinalIgnoreCase) >= 0;
		}

		public static string TrimStart(this string s, string toTrim)
		{
			if(s.StartsWith(toTrim, true, Thread.CurrentThread.CurrentCulture))
				return s.Substring(toTrim.Length);

			return s;
		}

		public static string TrimEnd(this string s, string toTrim)
		{
			if(s.EndsWith(toTrim, true, Thread.CurrentThread.CurrentCulture))
				return s.Substring(0, s.Length - toTrim.Length);

			return s;
		}

		public static string Fmt(this string s, params object[] args)
		{
			return string.Format(s, args);
		}

		//		public static Task Enqueue(this Task task, CancellationToken token, bool startNextTask = true)
		//		{
		//			TaskManager.Instance.EnqueueTask(task, null, token, startNextTask, false);
		//			return task;
		//		}
		//
		//		public static Task Enqueue(this Task task, string intentKey, CancellationToken token, bool startNextTask = true, bool withPriority = false)
		//		{
		//			TaskManager.Instance.EnqueueTask(task, intentKey, token, startNextTask, withPriority);
		//			return task;
		//		}

		public static string Sterilize(this string s)
		{
			return s.TrimStart("the ").TrimStart("a ");
		}

		public static NSString ToNSString(this string s)
		{
			return new NSString(s);
		}

		public static NSDate ToNSDate(this DateTime date)
		{
			DateTime reference = TimeZone.CurrentTimeZone.ToLocalTime(new DateTime(2001, 1, 1, 0, 0, 0));
			return NSDate.FromTimeIntervalSinceReferenceDate((date - reference).TotalSeconds);
		}

		//public static DateTime FromNSDate(this NSDate date)
		//{
		//	DateTime reference = TimeZone.CurrentTimeZone.ToLocalTime(new DateTime(2001, 1, 1, 0, 0, 0));
		//	return NSDate.FromTimeIntervalSinceReferenceDate((date - reference).TotalSeconds);
		//}
	}
}