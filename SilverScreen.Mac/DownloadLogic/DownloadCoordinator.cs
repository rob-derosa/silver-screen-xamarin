using System;
using System.Collections.Generic;
using SilverScreen.Shared;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using System.Diagnostics;
using System.Reflection;

namespace SilverScreen.Mac
{
	public class DownloadCoordinator
	{
		#region Properties and Fields

		bool _alreadyUpdating;

		public event EventHandler DownloadActivityChanged;
		public event EventHandler DownloadStatusChanged;

		public const int UpdateInterval = 1000;
		static DownloadCoordinator _instance;

		public DownloadHistory History
		{
			get;
			set;
		} = new DownloadHistory();

		public int TotalDownloads
		{
			get
			{
				return CurrentDownloads.Count + EnqueuedDownloads.Count;
			}
		}

		public int MaxDownloads
		{
			get;
			set;
		} = 10;

		public List<DownloadManager> CurrentDownloads
		{
			get;
			set;
		}

		public Action<List<DownloadRequest>> DownloadStatusUpdated
		{
			get;
			set;
		}

		public Queue<DownloadManager> EnqueuedDownloads
		{
			get;
			set;
		}

		public bool IsDownloading
		{
			get
			{
				return CurrentDownloads.Count > 0;
			}
		}

		public static DownloadCoordinator Instance
		{
			get
			{
				return _instance ?? (_instance = new DownloadCoordinator());
			}
		}

		#endregion

		DownloadCoordinator()
		{
			CurrentDownloads = new List<DownloadManager>();
			EnqueuedDownloads = new Queue<DownloadManager>();
		}

		public void KickoffPreviousDownloadQueue()
		{
			var list = GetActiveDownloads();

			foreach(var item in list)
			{
				var manager = new DownloadManager(item);
				EnqueuedDownloads.Enqueue(manager);
			}

			StartQueue();
		}

		public bool EnqueueEpisodeForDownload(DownloadRequest request)
		{
			if(EnqueuedDownloads.Any(r => r.Response.Request.EpisodeTraktID == request.EpisodeTraktID))
				return true;

			if(CurrentDownloads.Any(m => m.Response.Request.EpisodeTraktID == request.EpisodeTraktID))
				return true;

			var manager = new DownloadManager(request);

			manager.Response.DateAdded = DateTime.UtcNow;
			History.AddDownload(manager.Response);
			EnqueuedDownloads.Enqueue(manager);
			manager.BroadcastStatus(false);

			StartQueue();
			return true;
		}

		public void CancelAllRequests()
		{
			Debug.WriteLine("Cancel all requested");

			foreach(var enqueued in EnqueuedDownloads)
				enqueued.Cancel();

			foreach(var manager in CurrentDownloads.ToList())
			{
				manager.Cancel();
				CurrentDownloads.Remove(manager);
			}

			History.ClearActive();

			Debug.WriteLine("Cancel all completed");
			UpdateDownloadCollections();
			DownloadActivityChanged(this, new EventArgs());
		}

		public bool RemoveDownloadFromHistory(string responseId)
		{
			var response = History.GetDownload(responseId);
			History.DeleteDownload(response);
			return true;
		}

		public bool CancelDownloadRequest(string responseId)
		{
			var manager = CurrentDownloads.FirstOrDefault(r => r.Response.ID == responseId);

			if(manager == null)
				manager = EnqueuedDownloads.FirstOrDefault(r => r.Response.ID == responseId);

			if(manager != null)
			{
				manager.Cancel();
				CurrentDownloads.Remove(manager);
			}

			UpdateDownloadCollections();
			StartQueue();
			return true;
		}

		public List<DownloadResponse> GetPreviousDownloads()
		{
			return History.GetPreviousDownloads(0, 20);
		}

		public List<DownloadResponse> GetActiveDownloads()
		{
			return History.GetActiveDownloads();
		}

		public List<DownloadRequest> GetMissingDownloads(List<DownloadRequest> requests)
		{
			var toReturn = requests.ToList();
			foreach(var req in requests)
			{
				if(HasEpisodeOnDisk(req))
					toReturn.Remove(req);
			}

			return toReturn.OrderByDescending(r => r.BroadcastDate).ToList();
		}

		public DownloadResponse GetDownloadUpdate(string episodeTraktID)
		{
			var manager = CurrentDownloads.FirstOrDefault(r => r.Response.Request.EpisodeTraktID == episodeTraktID);

			if(manager == null)
			{
				manager = EnqueuedDownloads.FirstOrDefault(r => r.Response.Request.EpisodeTraktID == episodeTraktID);
				if(manager == null)
				{
					return new DownloadResponse
					{
						Request = new DownloadRequest
						{
							EpisodeTraktID = episodeTraktID
						},
						State = DownloadState.None
					};
				}
			}

			return manager.Response;
		}

		/// <summary>
		/// Kicks off X number of downloads simultaneously and will not await the completion of the download
		/// </summary>
		void StartQueue()
		{
			DownloadActivityChanged(this, new EventArgs());

			while(CurrentDownloads.Count < MaxDownloads && EnqueuedDownloads.Count > 0)
			{
				var next = EnqueuedDownloads.Dequeue();

				if(next.CancellationToken.IsCancellationRequested)
					continue;

				DownloadEpisodeToDisk(next);
				History.Commit();
			}

			DownloadActivityChanged(this, new EventArgs());

			#pragma warning disable CS4014
			UpdateStatusChanged();
			#pragma warning restore CS4014
		}

		/// <summary>
		/// Downloads an episode to disk and kicks off the queue after completion
		/// </summary>
		/// <param name="manager">Manager.</param>
		async void DownloadEpisodeToDisk(DownloadManager manager)
		{
			Debug.WriteLine("Download starting: " + manager.Response.Request.ShowTitle + " " + manager.Response.Request.S0E0);
			CurrentDownloads.Add(manager);

			var task = manager.DownloadEpisodeToDisk();
			await TaskRunner.RunSafe(task);

			CurrentDownloads.Remove(manager);
			Debug.WriteLine("Download ending: " + manager.Response.Request.ShowTitle + " " + manager.Response.Request.S0E0);
			ServiceHost.Instance.SendMessage(MessageAction.DownloadUpdateInquiry, manager.Response);
			UpdateDownloadCollections();

			if(manager.Response.State == DownloadState.Complete)
			{
				SendDownloadCompletePushNotification(manager.Response);
			}

			StartQueue();
		}

		public bool HasEpisodeOnDisk(DownloadRequest request)
		{
			var path = request.GetiTunesPath();
			Debug.WriteLine("Checking file existence: " + path);
			return System.IO.File.Exists(path);
		}

		internal void UpdateDownloadCollections()
		{
			ServiceHost.Instance.GetActiveDownloads();
			ServiceHost.Instance.GetPreviousDownloads();
		}

		async Task UpdateStatusChanged()
		{
			if(_alreadyUpdating)
				return;

			_alreadyUpdating = true;

			while(CurrentDownloads.Count > 0)
			{
				DownloadStatusChanged?.Invoke(this, new EventArgs());
				await Task.Delay(UpdateInterval);
			}

			//Invoke once more now that there are no more downloads
			DownloadStatusChanged?.Invoke(this, new EventArgs());
			_alreadyUpdating = false;
		}

		void SendDownloadCompletePushNotification(DownloadResponse response)
		{
			var sub = response.IsSuccessful ? "is available to watch" : $"failed because {response.FailureReason}";
			SendPushNotificationToAllDevices($"{response.Request.ShowTitle}: {response.Request.S0E0} {sub}.");
		}

		internal static void SendPushNotificationToAllDevices(string message)
		{
			foreach(var token in Settings.Instance.DeviceTokens)
			{
				SendPushNotificationToDevice(token, message);
			}
		}

		internal static void SendPushNotificationToDevice(string token, string message)
		{
			try
			{
				var exeDir = Path.GetDirectoryName(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location));
				var certPath = Path.Combine(exeDir, "Resources/com.silverscreen.ios-APNS_dev.pem");
				var args = $"push {token} -c \"{certPath}\" -m \"{message}\"".Fmt(token);
				var exePath = Path.Combine(exeDir, "Resources/apn");

				var info = new ProcessStartInfo(exePath, args);
				info.UseShellExecute = false;
				info.CreateNoWindow = true;

				var proc = new Process {
					StartInfo = info
				};

				proc.Start();
			}
			catch(Exception e)
			{
				Debug.WriteLine("Error sending push notification: " + e);
			}
		}
	}			
}