using System;
using Newtonsoft.Json;
using System.IO;
using System.Threading.Tasks;
using System.Diagnostics;
using SilverScreen.Shared;

namespace SilverScreen.iOS.Shared
{
	public class Settings
	{
		static Settings _instance;
		static readonly string _filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "settings.json");

		public Settings()
		{
			LastRefreshDate = DateTime.Now.AddMonths(-1);
		}

		public static Settings Instance
		{
			get
			{
				if(_instance == null)
					Settings.Load();

				return _instance;
			}
		}

		public DateTime LastUpcomingEpisodesWriteTime
		{
			get;
			set;
		}

		public string AuthToken
		{
			get;
			set;
		}

		public string RefreshToken
		{
			get;
			set;
		}

		public string TraktUsername
		{
			get;
			set;
		}

		public DateTime LastRefreshDate
		{
			get;
			set;
		}

		public Task Save()
		{
			return Task.Factory.StartNew(() => {
				try
				{
					Debug.WriteLine(string.Format("Saving settings: {0}", _filePath));
					var json = JsonConvert.SerializeObject(this);
					using(var sw = new StreamWriter(_filePath, false))
					{
						sw.Write(json);
					}
				}
				catch(Exception e)
				{
					Debug.WriteLine(e);
				}
			});
		}

		public static void Load()
		{
			Debug.WriteLine(string.Format("Loading settings: {0}", _filePath));
			_instance = Helpers.LoadFromFile<Settings>(_filePath);

			if(_instance == null)
				_instance = new Settings();
		}
	}
}