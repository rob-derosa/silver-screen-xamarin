using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using SQLite.Net;
using SQLite.Net.Async;
using SQLiteNetExtensionsAsync.Extensions;
using System.Linq;

namespace SilverScreen.iOS.Shared
{
	public class DataService
	{
		SQLiteAsyncConnection _connection;
		static DataService _instance;

		public static DataService Instance
		{
			get
			{
				return _instance ?? (_instance = new DataService());
			}
		}

		public SQLiteAsyncConnection Connection
		{
			get
			{
				return _connection ?? (_connection = GetConnection());
			}
		}

		async public Task Initialize()
		{
			await Connection.CreateTableAsync<Episode>();
			await Connection.CreateTableAsync<Season>();
			await Connection.CreateTableAsync<Show>();
		}

		public void Close()
		{
			_connection = null;
		}

		async public Task<List<Show>> GetAllShowsAsync()
		{
			var list = await Connection.GetAllWithChildrenAsync<Show>(null, true);
			return list.Where(s => s?.Identifiers?.Trakt != null).ToList();
		}

		async public Task<List<Season>> GetSeasonsForShowAsync(int showId)
		{
			return await Connection.GetAllWithChildrenAsync<Season>(s => s.ShowID == showId);
		}

		async public Task Delete(object item)
		{
			await Connection.DeleteAsync(item, recursive: true);
		}

		async public Task LoadChildren(object item)
		{
			await Connection.GetChildrenAsync(item, true);	
		}

		async public Task Save(object item)
		{
			await Connection.InsertOrReplaceWithChildrenAsync(item, true);
		}

		public static string DatabaseFilePath
		{
			get;
			set;
		}

		public SQLiteAsyncConnection GetConnection()
		{
			var dirPath = Path.GetDirectoryName(DatabaseFilePath);

			if(!Directory.Exists(dirPath))
				Directory.CreateDirectory(dirPath);

			Debug.WriteLine(dirPath);
			var connectionString = new SQLiteConnectionString(DatabaseFilePath, false);
			var connectionWithLock = new SQLiteConnectionWithLock(new SQLite.Net.Platform.XamarinIOS.SQLitePlatformIOS(), connectionString);
			var connection = new SQLiteAsyncConnection(() => connectionWithLock);
			return connection;
		}

		async public Task Truncate()
		{
			await Connection.ExecuteAsync("DELETE FROM Show");
			await Connection.ExecuteAsync("DELETE FROM Season");
			await Connection.ExecuteAsync("DELETE FROM Episode");
			await Connection.ExecuteAsync("VACUUM Show");
			await Connection.ExecuteAsync("VACUUM Season");
			await Connection.ExecuteAsync("VACUUM Episode");
		}
	}
}