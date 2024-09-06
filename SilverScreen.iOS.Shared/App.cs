using System;
using Foundation;
using System.IO;
using System.Runtime.CompilerServices;

namespace SilverScreen.iOS.Shared
{
	public class App
	{
		public static event EventHandler<bool> NetworkInUseChanged;
		public static event EventHandler<Exception> TaskExceptionOccurred;

		public static bool IsNetworkReachable
		{
			get;
			set;
		}

		public static bool IsNetworkInUse
		{
			get;
			set;
		}

		static object _networkSync = new object();
		static int _networkUsageCount;
		internal static void NetworkInUse(bool inUse)
		{
			lock(_networkSync)
			{
				if(inUse)
				{
					if(_networkUsageCount == 0)
					{
						NetworkInUseChanged(null, true);
					}
					_networkUsageCount += 1;
				}
				else
				{
					if(_networkUsageCount == 1)
					{
						NetworkInUseChanged(null, false);
					}
					_networkUsageCount -= 1;
				}
			}
		}

		public static void OnTaskException(Exception e)
		{
			TaskExceptionOccurred?.Invoke(null, e);
		}
	}
}

