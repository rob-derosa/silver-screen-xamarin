using MultipeerConnectivity;
using SilverScreen.Shared;
using System;
using Foundation;
using System.Threading.Tasks;
using System.Linq;
using UIKit;
using System.Diagnostics;

namespace SilverScreen.iOS
{
	public class ServiceClient : BasePeer
	{
		MCNearbyServiceBrowser _browser;
		BrowserDelegate _browserDelegate;
		bool _isRunning;

		public MCSession ConnectedHostSession
		{
			get
			{
				if(Sessions.Count > 0)
					return Sessions.Values.First();

				return null;
			}
		}

		public MCPeerID ConnectedHost
		{
			get
			{
				return ConnectedHostSession?.ConnectedPeers.FirstOrDefault();
			}
		}

		async public void Start()
		{
			if(_isRunning)
				return;
			
			if(Debugger.IsAttached)
			{
				//This delay is needed because the host will disconnect if the session wires get crossed
				await Task.Delay(6000);
			}

			//PeerID = new MCPeerID("Client-{0}".Fmt(Guid.NewGuid().ToString()));
			PeerID = new MCPeerID(UIDevice.CurrentDevice.Name);
			//Debug.WriteLine("My peer ID: " + PeerID.DisplayName);

			_browserDelegate = new BrowserDelegate {
				Client = this
			};

			_browser = new MCNearbyServiceBrowser(PeerID, ServiceType); 
			_browser.Delegate = _browserDelegate;
			_browser.StartBrowsingForPeers();
			_isRunning = true;
			Debug.WriteLine("Client started");
		}

		public void Stop()
		{
			if(_browser != null)
			{
				_browser.StopBrowsingForPeers();
				_browser.Delegate = null;
				DisconnectPeer(ConnectedHost);
			}
	
			_isRunning = false;
			Debug.WriteLine("Client stopped");
		}

		public void OnFoundPeer(MCPeerID peerId)
		{
			if(ConnectedHost != null || !_isRunning)
				return;
			
			if(peerId.DisplayName != null && peerId.DisplayName.StartsWith("Host-", StringComparison.Ordinal))
			{
				//Debug.WriteLine("Found host: " + peerId.DisplayName);

				ConnectPeer(peerId);
				_browser.InvitePeer(peerId, ConnectedHostSession, null, 5);
			}
			else
			{
				//Debug.WriteLine("Ignoring peer: " + peerId.DisplayName);
			}
		}

		//		public void OnLostPeer(MCPeerID peerId)
		//		{
		//			if(!_isRunning)
		//				return;
		//			Debug.WriteLine("LOST PEER!!! " + peerId.DisplayName);
		//			if(ConnectedHost != null && ConnectedHost.DisplayName == peerId.DisplayName && Sessions.ContainsKey)
		//			{
		//				var session = Sessions
		//
		//				SetStateChanged(peerId, MCSessionState.NotConnected);
		//			}
		//		}

		public void OnCannotBrowse(NSError error)
		{
			if(!_isRunning)
				return;
			
			Debug.WriteLine("Error starting browser " + error.DebugDescription);
		}

		class BrowserDelegate : MCNearbyServiceBrowserDelegate
		{
			internal ServiceClient Client
			{
				get;
				set;
			}

			public override void FoundPeer(MCNearbyServiceBrowser browser, MCPeerID peerId, NSDictionary info)
			{
				Client.OnFoundPeer(peerId);
			}

			public override void LostPeer(MCNearbyServiceBrowser browser, MCPeerID peerId)
			{
				//Client.OnLostPeer(peerId);
			}

			public override void DidNotStartBrowsingForPeers(MCNearbyServiceBrowser browser, NSError error)
			{
				Client.OnCannotBrowse(error);
			}
		}
	}
}