using System;
using Couchbase.Lite;
using SilverScreen.Shared;

namespace SilverScreen.Mac
{
	public class DataService : BaseDataService<DataService>
	{
		Database _downloadDatabase;

		public DataService() : base()
		{
		}

		protected override Database GetDatabaseForType(Type type)
		{
			if(type == typeof(DownloadResponse))
				return _downloadDatabase;

			return null;
		}

		public override void CreateDatabases()
		{
			_downloadDatabase = Manager.SharedInstance.GetDatabase("downloads");  
			Databases.Add(_downloadDatabase);
		}
	}
}