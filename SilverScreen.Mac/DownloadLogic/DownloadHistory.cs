using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using SQLite.Net;
using SQLiteNetExtensions.Extensions;
using SilverScreen.Shared;

namespace SilverScreen.Mac
{
	public class DownloadHistory
	{
		SQLiteConnection _connection;
		static object syncObject = new object();

		public DownloadHistory()
		{
			_connection = GetConnection();
			_connection.CreateTable<DownloadRequest>();
			_connection.CreateTable<DownloadResponse>();
		}

		public List<DownloadResponse> GetActiveDownloads()
		{
			lock(syncObject)
			{
				Debug.WriteLine("Getting active downloads");
				var list = _connection.GetAllWithChildren<DownloadResponse>().Where(r => r.Result == null && r.State != DownloadState.Complete).OrderBy(r => r.DateAdded).ToList();

				var enqueued = list.Where(r => r.State == DownloadState.Enqueued).ToList();
				enqueued.ForEach(r => list.Remove(r));
				enqueued.ForEach(list.Add);

				return list;
			}
		}

		public void Close()
		{
			_connection.Dispose();
			_connection = null;
		}

		public List<DownloadResponse> GetPreviousDownloads(int start, int length)
		{
			lock(syncObject)
			{
				Debug.WriteLine("Getting previous downloads");
				var list = _connection.GetAllWithChildren<DownloadResponse>().
					Where(r => (r.Result != null && r.Result != DownloadResult.None)).
					OrderByDescending(r => r.DateAdded).Skip(start).Take(length);

				return list.ToList();
			}
		}

		public DownloadResponse GetDownloadByEpisodeId(string episodeTraktID)
		{
			lock(syncObject)
			{
				Debug.WriteLine("GetDownloadByEpisodeId - " + episodeTraktID);
				var list = _connection.GetAllWithChildren<DownloadResponse>(r => r.Request.EpisodeTraktID == episodeTraktID).FirstOrDefault();
				return list;
			}
		}

		public DownloadResponse GetDownload(string responseId)
		{
			lock(syncObject)
			{
				Debug.WriteLine("GetDownloadByResponseId - " + responseId);

				try
				{
					return _connection.GetWithChildren<DownloadResponse>(responseId);
				}
				catch(Exception)
				{
					return null;
				}
			}
		}

		public void DeleteDownload(string responseId, bool commit = true)
		{
			var response = GetDownload(responseId);

			if(response != null)
				DeleteDownload(response, commit);
		}

		public void DeleteDownload(DownloadResponse response, bool commit = true)
		{
			lock(syncObject)
			{
				Debug.WriteLine("Deleting download: " + response);
				_connection.Delete(response, true);

				if(commit)
					_connection.Commit();
			}
		}

		public void UpdateDownload(DownloadResponse response, bool commit = false)
		{
			if(response == null || _connection == null)
				return;

			try
			{
				lock(syncObject)
				{
					Debug.WriteLine("Updating download: " + response);
					_connection.Update(response, typeof(DownloadResponse));

					if(commit)
						_connection.Commit();
				}
			}
			catch(Exception e)
			{
				Debug.WriteLine(e);
			}
		}

		public void AddDownload(DownloadResponse response, bool commit = true)
		{
			Debug.WriteLine("Adding download: " + response);
			lock(syncObject)
			{
				try
				{
					_connection.InsertWithChildren(response, true);

					if(commit)
						_connection.Commit();
				}
				catch(SQLiteException sq)
				{
					//Already added
					if(sq.Result == SQLite.Net.Interop.Result.Constraint)
						return;
				}
				catch(Exception e)
				{
					Debug.WriteLine(e);
				}
			}
		}

		public static string DatabaseFilePath
		{
			get
			{
				return Path.Combine(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SilverScreen"), "history.db");
			}
		}

		public SQLiteConnection GetConnection()
		{
			var dirPath = Path.GetDirectoryName(DatabaseFilePath);

			if(!Directory.Exists(dirPath))
				Directory.CreateDirectory(dirPath);

			var connection = new SQLiteConnection(new SQLite.Net.Platform.Generic.SQLitePlatformGeneric(), DatabaseFilePath);
			return connection;
		}

		public void Commit()
		{
			_connection.Commit();
		}

		public void Truncate()
		{
			lock(syncObject)
			{
				_connection.Execute("DELETE FROM DownloadRequest");
				_connection.Execute("DELETE FROM DownloadResponse");
				_connection.Commit();
			}
		}

		public void ClearActive()
		{
			lock(syncObject)
			{
				var list = _connection.GetAllWithChildren<DownloadResponse>(r => r.Result == null);
				_connection.DeleteAll(list, true);
				_connection.Commit();
			}
		}

		public void ClearHistory()
		{
			lock(syncObject)
			{
				var list = _connection.GetAllWithChildren<DownloadResponse>(r => r.Result != null);
				_connection.DeleteAll(list, true);
				_connection.Commit();
			}
		}
	}
}