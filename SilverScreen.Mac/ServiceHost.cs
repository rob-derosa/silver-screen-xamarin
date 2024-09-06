using MultipeerConnectivity;
using SilverScreen.Shared;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Diagnostics;

namespace SilverScreen.Mac
{
	public class ServiceHost : BasePeer
	{
		AssistantDelegate _assistantDelegate;
		MCNearbyServiceAdvertiser _assistant;

		static ServiceHost _instance;

		public static ServiceHost Instance
		{
			get
			{
				return _instance ?? (_instance = new ServiceHost());
			}
		}

		public ServiceHost()
		{
			DataReceived += OnDataReceived;
		}

		#region Connection

		public void Start()
		{
			PeerID = new MCPeerID("Host-{0}".Fmt(Guid.NewGuid().ToString()));

			_assistantDelegate = new AssistantDelegate {
				Host = this
			};

			_assistant = new MCNearbyServiceAdvertiser(PeerID, null, ServiceType);
			_assistant.Delegate = _assistantDelegate;
			_assistant.StartAdvertisingPeer();
		}

		public void Stop()
		{
			_assistant.StopAdvertisingPeer();

			foreach(var session in Sessions.Values)
				session.Disconnect();
		}

		class AssistantDelegate : MCNearbyServiceAdvertiserDelegate
		{
			internal ServiceHost Host
			{
				get;
				set;
			}

			public override void DidNotStartAdvertisingPeer(MCNearbyServiceAdvertiser advertiser, Foundation.NSError error)
			{
			}

			public override void DidReceiveInvitationFromPeer(MCNearbyServiceAdvertiser advertiser, MCPeerID peerId, Foundation.NSData context, MCNearbyServiceAdvertiserInvitationHandler invitationHandler)
			{
				var session = Host.ConnectPeer(peerId);
				invitationHandler(true, session);
			}
		}

		#endregion

		void OnDataReceived(object sender, MessagePayload payload)
		{
			switch(payload.Action)
			{
				case MessageAction.DownloadEpisode:
					{
						var request = payload.GetPayload<DownloadRequest>();
						EnqueueDownloadRequest(request);
						break;
					}
				case MessageAction.CancelDownload:
					{
						var responseId = payload.GetPayload<string>();
						CancelDownloadRequest(responseId);
						break;
					}
				case MessageAction.CancelAllDownloads:
					{
						CancelAllDownloadRequests();
						break;
					}
				case MessageAction.DownloadUpdateInquiry:
					{
						var episodeId = payload.GetPayload<string>();
						GetDownloadUpdate(episodeId);
						break;
					}
				case MessageAction.PreviousDownloads:
					{
						GetPreviousDownloads();
						break;
					}
				case MessageAction.ActiveDownloads:
					{
						GetActiveDownloads();
						break;
					}
				case MessageAction.MissingDownloads:
					{
						var requests = payload.GetPayload<List<DownloadRequest>>();
						GetMissingDownloads(requests);
						break;
					}
				case MessageAction.ClearHistory:
					{
						ClearHistory();
						break;
					}
				case MessageAction.RegisterDevice:
					{
						RegisterDevice(payload.GetPayload<string>());
						break;
					}
				case MessageAction.HasEpisodeOnDisk:
					{
						var request = payload.GetPayload<DownloadRequest>();
						GetHasEpisodeOnDisk(request);
						break;
					}
				case MessageAction.ClearHistoricDownload:
					{
						var responseId = payload.GetPayload<string>();
						RemoveDownloadFromHistory(responseId);
						break;
					}
			}
		}

		void RegisterDevice(string token)
		{
			Debug.WriteLine(token);
			if(!Settings.Instance.DeviceTokens.Contains(token))
			{
				if(Settings.Instance.DeviceTokens.Count > 3)
					Settings.Instance.DeviceTokens.Clear();

				Settings.Instance.DeviceTokens.Add(token);
				Settings.Instance.Save();
			}
		}

		void EnqueueDownloadRequest(DownloadRequest request)
		{
			DownloadCoordinator.Instance.EnqueueEpisodeForDownload(request);
			GetActiveDownloads();
		}

		void CancelAllDownloadRequests()
		{
			DownloadCoordinator.Instance.CancelAllRequests();
			Thread.Sleep(500); //?
			GetPreviousDownloads();
			GetActiveDownloads();
		}

		void CancelDownloadRequest(string responseId)
		{
			DownloadCoordinator.Instance.CancelDownloadRequest(responseId);
			GetPreviousDownloads();
			GetActiveDownloads();
		}

		void GetDownloadUpdate(string episodeId)
		{
			var response = DownloadCoordinator.Instance.GetDownloadUpdate(episodeId);
			Instance.SendMessage(MessageAction.DownloadUpdateInquiry, response);
		}

		void GetHasEpisodeOnDisk(DownloadRequest request)
		{
			var response = DownloadCoordinator.Instance.HasEpisodeOnDisk(request);
			Instance.SendMessage(MessageAction.HasEpisodeOnDisk, new EpisodeDownloadedCheck(request, response));
		}

		internal void GetActiveDownloads()
		{
			var list = DownloadCoordinator.Instance.GetActiveDownloads();
			Instance.SendMessage(MessageAction.ActiveDownloads, list);
		}

		internal void GetPreviousDownloads()
		{
			var list = DownloadCoordinator.Instance.GetPreviousDownloads();
			Instance.SendMessage(MessageAction.PreviousDownloads, list);
		}

		internal void GetMissingDownloads(List<DownloadRequest> requests)
		{
			var list = DownloadCoordinator.Instance.GetMissingDownloads(requests);
			Instance.SendMessage(MessageAction.MissingDownloads, list);
		}

		void ClearHistory()
		{
			DownloadCoordinator.Instance.History.ClearHistory();
			GetPreviousDownloads();
		}

		void RemoveDownloadFromHistory(string responseId)
		{
			DownloadCoordinator.Instance.RemoveDownloadFromHistory(responseId);
			GetPreviousDownloads();
		}
	}
}