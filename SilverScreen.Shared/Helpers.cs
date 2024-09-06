using System;
using Newtonsoft.Json;
using System.IO;
using System.Net;

namespace SilverScreen.Shared
{
	public class Helpers
	{
		public static T LoadFromFile<T>(string path)
		{
			string json = null;
			if(File.Exists(path))
			{
				using(var sr = new StreamReader(path))
				{
					json = sr.ReadToEnd();
				}

				if(json != null)
				{
					try
					{
						return JsonConvert.DeserializeObject<T>(json);
					}
					catch(Exception)
					{
					}
				}
			}

			return default(T);
		}
	}
}