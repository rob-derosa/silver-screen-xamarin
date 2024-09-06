using System;
using AppKit;
using Foundation;
using MultipeerConnectivity;
using SilverScreen.Shared;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using System.Diagnostics;

namespace SilverScreen.Mac
{
	public partial class AppDelegate : NSApplicationDelegate
	{
		NSStatusItem _statusItem;
		NSMenuItem _cancelItem;
		Dictionary<string, NSMenuItem> _downloadItems = new Dictionary<string, NSMenuItem>();
		NSMenu _menu = new NSMenu();

		async public override void DidFinishLaunching(NSNotification notification)
		{
			Instance = this;

			#region Menu Items

			_cancelItem = new NSMenuItem("Cancel All Downloads");
			_cancelItem.Activated += (sender, e) => {
				var msg = "Clicking 'Yes' will cancel the {0} download{1} you currently have running."
					.Fmt(DownloadCoordinator.Instance.TotalDownloads, DownloadCoordinator.Instance.TotalDownloads == 1 ? "" : "s");

				var alert = NSAlert.WithMessage("Are you sure you want to cancel all current downloads?", "Yes", "No", null, msg);
				if(alert.RunModal() == 1)
				{
					DownloadCoordinator.Instance.CancelAllRequests();
				}
			};

			_menu.AddItem(NSMenuItem.SeparatorItem);

			var clearHistoryItem = new NSMenuItem("Clear History");
			_menu.AddItem(clearHistoryItem);

			clearHistoryItem.Activated += async (sender, e) => {
				var msg = "Clicking 'Yes' will cancel the {0} download{1} you currently have running and wipe the database."
					.Fmt(DownloadCoordinator.Instance.TotalDownloads, DownloadCoordinator.Instance.TotalDownloads == 1 ? "" : "s");

				var alert = NSAlert.WithMessage("Are you sure you want to clear the existing history?", "Yes", "No", null, msg);
				if(alert.RunModal() == 1)
				{
					DownloadCoordinator.Instance.CancelAllRequests();
					DownloadCoordinator.Instance.History.Close();

					await Task.Delay(2000);

					DownloadManager.RecycleFile(DownloadHistory.DatabaseFilePath);
					DownloadCoordinator.Instance.History = new DownloadHistory();
				}
			};

			var quitItem = new NSMenuItem("Quit");
			_menu.AddItem(NSMenuItem.SeparatorItem);
			_menu.AddItem(quitItem);

			quitItem.Activated += (sender, e) => {
				NSApplication.SharedApplication.Terminate(this);
			};

			#endregion

			_statusItem = NSStatusBar.SystemStatusBar.CreateStatusItem(NSStatusItemLength.Variable);
			_statusItem.Title = string.Empty;
			_statusItem.HighlightMode = true;
			_statusItem.Menu = _menu;
			OnStateChanged(this, MCSessionState.NotConnected);

			DownloadCoordinator.Instance.DownloadActivityChanged += HandleDownloadActivityChanged;
			DownloadCoordinator.Instance.DownloadStatusChanged += HandleDownloadStatusChanged;
			ServiceHost.Instance.StateChanged += OnStateChanged;
			ServiceHost.Instance.Start();

			DownloadCoordinator.SendPushNotificationToAllDevices("Silver Screen Agent is up and running!");

			await Task.Delay(2000);
			DownloadCoordinator.Instance.KickoffPreviousDownloadQueue();
		}

		public override void WillTerminate(NSNotification notification)
		{
			ServiceHost.Instance.Stop();
		}

		public static AppDelegate Instance
		{
			get;
			private set;
		}

		void OnStateChanged(object sender, MCSessionState e)
		{
			UpdateApplicationState();
		}

		void HandleDownloadStatusChanged(object sender, EventArgs e)
		{
			UpdateDownloadActivityState();
		}

		void HandleDownloadActivityChanged(object sender, EventArgs e)
		{
			UpdateApplicationState();
			UpdateDownloadActivityState();
		}

		void UpdateDownloadActivityState()
		{
			BeginInvokeOnMainThread(() => {
				//Remove completed download items
				var downloads = DownloadCoordinator.Instance.CurrentDownloads;
				foreach(var kvp in _downloadItems.ToList())
				{
					if(downloads.FirstOrDefault(d => d.Response.Request.EpisodeTraktID == kvp.Key) == null)
					{
						_downloadItems.Remove(kvp.Key);
						_menu.RemoveItem(kvp.Value);
					}
				}
				if(downloads.Count > 0)
				{
					var i = _menu.ItemArray().ToList().IndexOf(_cancelItem);
					if(i < 0)
					{
						_menu.AddItem(NSMenuItem.SeparatorItem);
						_menu.InsertItem(_cancelItem, _downloadItems.Count);
					}
				}
				else
				{
					var i = _menu.ItemArray().ToList().IndexOf(_cancelItem);
					if(i >= 0)
						_menu.RemoveItem(_cancelItem);
				}

				foreach(var d in downloads.OrderByDescending(d => d.Response.PercentComplete))
				{
					NSMenuItem item;
					var req = d.Response.Request;

					if(_downloadItems.ContainsKey(d.Response.Request.EpisodeTraktID))
					{
						item = _downloadItems[d.Response.Request.EpisodeTraktID];
					}
					else
					{
						item = new NSMenuItem();
						_downloadItems.Add(d.Response.Request.EpisodeTraktID, item);
						_menu.InsertItem(item, 0);
					}

					var id = "{0}:{1} {2}".Fmt(req.ShowTitle.Max(20), req.S0E0, req.EpisodeTitle.Max(20));
					string title = string.Empty;
					switch(d.Response.State)
					{
						case DownloadState.Downloading:
							title = "{0} {2:P} {1}".Fmt(d.Response.State, id, d.Response.PercentComplete).Replace(" %", "%");
							break;

						case DownloadState.Converting:
							var progress = d.Response.Progress != null ? d.Response.Progress.Replace(" %", "%") : string.Empty;
							title = "{0} {2:P} {1}".Fmt(d.Response.State, id, progress).Trim();
							break;

						default:
							title = "{0} {1}".Fmt(d.Response.State.ToProperString(), id);
							break;
					}

					item.Title = title;
				}
			});
		}

		void UpdateApplicationState()
		{
			BeginInvokeOnMainThread(() =>
			{
				var connectionState = ServiceHost.Instance.IsConnected ? "connected" : "disconnected";
				var downloadState = DownloadCoordinator.Instance.IsDownloading ? "active" : "inactive";

				_statusItem.Image = NSImage.ImageNamed($"{connectionState}_{downloadState}_icon.png");
				_statusItem.Image.Template = true;

				_statusItem.AlternateImage = _statusItem.Image;
				_cancelItem.Enabled = DownloadCoordinator.Instance.IsDownloading;
			});
		}
	}
}