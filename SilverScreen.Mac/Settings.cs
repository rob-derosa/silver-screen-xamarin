using System;
using Newtonsoft.Json;
using System.IO;
using System.Threading.Tasks;
using System.Diagnostics;
using SilverScreen.Shared;
using System.Collections.Generic;

namespace SilverScreen.Mac
{
	public class Settings
	{
		static Settings _instance;
		static readonly string _filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Library/SilverScreen/settings.json");

		public static Settings Instance
		{
			get
			{
				if(_instance == null)
					Load();

				return _instance;
			}
		}

		public bool PreferMp4
		{
			get;
			set;
		}

		public bool EnforceStagnation
		{
			get;
			set;
		} = true;

		public List<string> DeviceTokens
		{
			get;
			set;
		} = new List<string>();

		public Task Save()
		{
			return Task.Factory.StartNew(() => {
				try
				{
					var parentPath = Directory.GetParent(_filePath).FullName;

					if(!Directory.Exists(parentPath))
						Directory.CreateDirectory(parentPath);

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