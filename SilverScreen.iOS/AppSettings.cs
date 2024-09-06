using System;
using System.IO;
using Foundation;

namespace SilverScreen.iOS.Shared
{
	public class AppSettings
	{
		public static bool IsNetworkReachable
		{
			get;
			set;
		}

		public static readonly string SharedGroupId = "group.SilverScreen.SharedData";

		static string _sharedGroupDirectory;
		public static string SharedGroupDirectory
		{
			get
			{
				if(_sharedGroupDirectory == null)
					_sharedGroupDirectory = NSFileManager.DefaultManager.GetContainerUrl(SharedGroupId).Path;

				return _sharedGroupDirectory;
			}
		}

		public static string SharedDatabasePath
		{
			get
			{
				return Path.Combine(SharedGroupDirectory, "shows.db");
			}
		}

		public static string UpcomingShowsPath
		{
			get
			{
				return Path.Combine(SharedGroupDirectory, "upcoming_shows.json");
			}
		}
	}
}