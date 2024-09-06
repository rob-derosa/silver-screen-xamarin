using System;
using MultipeerConnectivity;
using Foundation;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Threading.Tasks;

namespace SilverScreen.Shared
{
	/// <summary>
	/// Contains logic that is shared by both the Advertiser/Host and Browser(s)/Client(s)
	/// </summary>
	public class BasePeer
	{
		//Dictionary<string, MCSessionState> _lastState = new Dictionary<string, MCSessionState>();
		protected Dictionary<string, MCSession> Sessions = new Dictionary<string, MCSession>();

		protected MCPeerID PeerID;
		protected static readonly string ServiceType = "SilverScreen";

		public event EventHandler<MessagePayload> DataReceived;
		public event EventHandler<MCSessionState> StateChanged;
		DateTime? _lastConnectedSession;


		public bool IsConnected
		{
			get
			{
				return Sessions.Values.ToList().Any(s => s.ConnectedPeers.Length > 0);
			}
		}

		public MCSession ConnectPeer(MCPeerID peer)
		{
			if(string.IsNullOrEmpty(peer.DisplayName))
				return null;

			var session = new MCSession(PeerID);
			session.Delegate = new SessionDelegate {
				Service = this
			};

			if(Sessions.ContainsKey(peer.DisplayName))
			{
				DisconnectPeer(peer);
			}

			Sessions.Add(peer.DisplayName, session);
			_lastConnectedSession = DateTime.Now;
			return session;
		}

		public void DisconnectPeer(MCPeerID peer)
		{	
			if(peer == null || !Sessions.ContainsKey(peer.DisplayName))
				return;

			var session = Sessions[peer.DisplayName];
			session.Disconnect();
			Sessions.Remove(peer.DisplayName);
			//session.Dispose();
		}

		protected virtual void OnInternalDataReceived(MCPeerID peerId, string payloadString)
		{
			//Debug.WriteLine("Incoming data from " + peerId.DisplayName);
			var msg = JsonConvert.DeserializeObject<MessagePayload>(payloadString);
			DataReceived?.Invoke(this, msg);
		}

		protected virtual void OnInternalStateChanged(MCSession session, MCPeerID peerId, MCSessionState state)
		{
			SetStateChanged(session, peerId, state);
		}

		protected virtual void SetStateChanged(MCSession session, MCPeerID peerId, MCSessionState state)
		{
			switch(state)
			{
				case MCSessionState.Connected:
					Debug.WriteLine("Connected: {0}".Fmt(peerId.DisplayName));
					break;
				case MCSessionState.Connecting:
					Debug.WriteLine("Connecting: {0}".Fmt(peerId.DisplayName));
					break;
				case MCSessionState.NotConnected:
					Debug.WriteLine("Not Connected: {0}".Fmt(peerId.DisplayName));
					break;
			}

			StateChanged?.Invoke(this, state);

			if(peerId.DisplayName == null || !Sessions.ContainsKey(peerId.DisplayName))
				return;

			var existingSession = Sessions[peerId.DisplayName];

			if(session.GetHashCode() != existingSession.GetHashCode())
			{
				Debug.WriteLine("Wrong session: {0}", peerId.DisplayName);
				return;
			}

			switch(state)
			{
				case MCSessionState.NotConnected:
					Sessions.Remove(peerId.DisplayName);
					break;
			}

		}

		async public void SendMessage(MessageAction action, object payload = null, MCPeerID peerId = null)
		{
			if(!IsConnected)
				return;

			if(peerId != null && !Sessions.ContainsKey(peerId.DisplayName))
				return;

			if(_lastConnectedSession.HasValue && DateTime.Now.Subtract(_lastConnectedSession.Value).TotalSeconds < 2.5)
			{
				await Task.Delay(1000);
			}

			NSError error;
			var msg = new MessagePayload {
				Action = action,
				PayloadString = JsonConvert.SerializeObject(payload)
			};

			var json = JsonConvert.SerializeObject(msg);

			if(peerId == null)
			{
				foreach(var session in Sessions.Values)
				{
					session.SendData(json, session.ConnectedPeers, MCSessionSendDataMode.Reliable, out error);
				}
			}
			else
			{
				var session = Sessions[peerId.DisplayName];
				session.SendData(json, session.ConnectedPeers, MCSessionSendDataMode.Reliable, out error);
			}
		}

		protected class SessionDelegate : MCSessionDelegate
		{
			internal BasePeer Service
			{
				get;
				set;
			}

			public override void DidChangeState(MCSession session, MCPeerID peerId, MCSessionState state)
			{
				Service?.OnInternalStateChanged(session, peerId, state);
			}

			public override void DidReceiveData(MCSession session, NSData data, MCPeerID peerId)
			{
				Service?.OnInternalDataReceived(peerId, data.ToString());
			}

			public override void DidStartReceivingResource(MCSession session, string resourceName, MCPeerID fromPeer, NSProgress progress)
			{
			}

			public override void DidFinishReceivingResource(MCSession session, string resourceName, MCPeerID fromPeer, NSUrl localUrl, NSError error)
			{
				error = null;
			}

			public override void DidReceiveStream(MCSession session, NSInputStream stream, string streamName, MCPeerID peerId)
			{
			}
		}
	}

	public class MessagePayload
	{
		public MessageAction Action
		{
			get;
			set;
		}

		public string PayloadString
		{
			get;
			set;
		}

		public T GetPayload<T>()
		{
			return JsonConvert.DeserializeObject<T>(PayloadString);
		}
	}

	public enum MessageAction
	{
		DownloadEpisode,
		CancelDownload,
		CancelAllDownloads,
		DownloadUpdateInquiry,
		PreviousDownloads,
		ActiveDownloads,
		MissingDownloads,
		HasEpisodeOnDisk,
		ClearHistory,
		ClearHistoricDownload,
		RegisterDevice,
	}
}