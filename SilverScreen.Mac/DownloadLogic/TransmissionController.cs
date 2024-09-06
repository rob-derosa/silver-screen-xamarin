using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using AppKit;
using Newtonsoft.Json;
using System.Diagnostics;

namespace SilverScreen.Mac
{
	public class TransmissionController
	{
		object _syncObject = new object();

		public event EventHandler<PingResponseArgument> StatusUpdated;

		static TransmissionController _instance;

		public bool IsCheckingStatus
		{
			get;
			private set;
		}

		public static TransmissionController Instance
		{
			get
			{
				return _instance ?? (_instance = new TransmissionController());
			}
		}

		string _transmissionSessionId;
		string _defaultDownloadDir;
		readonly string _transmissionSessionIdKey = "X-Transmission-Session-Id";
		readonly string _downloadDirectoryKey = "incomplete-dir";
		readonly Uri _transmissionHostUri = new Uri("http://localhost:9091/transmission/rpc");
		HttpClient _client;
		int _maxTryCount = 2;

		async Task<TransmissionResponse<T1>> SendRequest<T2, T1>(T2 payload, int tryCount = 0) where T2 : BaseRequestArgument, new()
		{
			if(_client == null)
			{
				_client = new HttpClient();
			}

			var request = new HttpRequestMessage();

			request.Method = HttpMethod.Post;
			request.RequestUri = _transmissionHostUri;

			var transmissionRequest = new TransmissionRequest<T2>();
			transmissionRequest.Arguments = payload;
			var json = JsonConvert.SerializeObject(transmissionRequest);
			request.Content = new StringContent(json);
			if(_transmissionSessionId != null)
			{
				request.Headers.Add(_transmissionSessionIdKey, _transmissionSessionId);
			}

			HttpResponseMessage response = null;
			try
			{
				//Debug.WriteLine("Request: " + json);
				response = await _client.SendAsync(request);
			}
			catch(WebException we)
			{
				Debug.WriteLine(we);
				if(we.Status == WebExceptionStatus.ConnectFailure)
				{
					await EnsureTransmissionIsRunning();
					return await SendRequest<T2, T1>(payload);
				}
				else
				{
					//Debug.WriteLine(we.Status);
				}
			}
			catch(Exception e)
			{
				//Debug.WriteLine(e);
			}

			if(response != null)
			{
				var content = await response.Content.ReadAsStringAsync();
				if(response.Headers.Contains(_transmissionSessionIdKey) && _transmissionSessionId == null)
				{
					_transmissionSessionId = response.Headers.GetValues(_transmissionSessionIdKey).FirstOrDefault();
					return await SendRequest<T2, T1>(payload);
				}

				if(response.StatusCode == (HttpStatusCode)409)
				{
					await Task.Delay(5000);
					_client = null;
					_transmissionSessionId = null;
					return await SendRequest<T2, T1>(payload);
				}

				//Debug.WriteLine("Response: " + content);
				var contentObject = JsonConvert.DeserializeObject<TransmissionResponse<T1>>(content);

				return contentObject;
			}
			else
			{
				Debug.WriteLine("!!!NULL RESPONSE!!!");
				if(tryCount < _maxTryCount)
				{
					tryCount++;
					return await SendRequest<T2, T1>(payload, tryCount);
				}

				return null;
			}
		}

		class GetSessionRequest : BaseRequestArgument
		{
			public GetSessionRequest() : base("session-get")
			{
			}
		}

		async Task GetMetadata()
		{
			var request = new GetSessionRequest();
			var response = await SendRequest<GetSessionRequest, Dictionary<string, object>>(request);

			_defaultDownloadDir = (string)response.Arguments[_downloadDirectoryKey];
		}

		public async Task<Tuple<string, int>> Add(string url, string hashString = null)
		{
			try
			{
				if(_defaultDownloadDir == null)
				{
					await GetMetadata();
				}

				if(hashString != null)
				{
					//Due to a Transmission bug, we first need to ensure this torrent does not exist
					var allResponse = await GetAllStatus();
					var existingTorrent = allResponse.Torrents.FirstOrDefault(t => t.HashString == hashString);

					if(existingTorrent != null)
					{
						return new Tuple<string, int>(hashString, (int)existingTorrent.Id);
					}
				}

				var request = new AddRequestArgument();
				request.DownloadDir = _defaultDownloadDir;
				request.Filename = url;

				var response = await SendRequest<AddRequestArgument, AddResponseArgument>(request);

				if(response == null || response.Arguments == null || response.Arguments.TorrentAdded == null || response.Arguments.TorrentAdded.HashString == null)
				{
					return null;
				}

				return new Tuple<string, int>(response.Arguments.TorrentAdded.HashString, (int)response.Arguments.TorrentAdded.Id);
			}
			catch(Exception e)
			{
				Debug.WriteLine(e);
				return null;
			}
		}

		public async Task<bool> EnsureTransmissionIsRunning()
		{
			bool wasRunning = false;
			bool isRunning = wasRunning;
			NSApplication.SharedApplication.InvokeOnMainThread(() =>
			{
				Debug.WriteLine("Checking for running applications");
				wasRunning = NSWorkspace.SharedWorkspace.RunningApplications.Any(app => app.LocalizedName.Equals("Transmission"));

				if(!wasRunning)
				{
					isRunning = NSWorkspace.SharedWorkspace.LaunchApplication("/Applications/Transmission.app");
				}
			});

			if(!wasRunning)
				await Task.Delay(500);

			return isRunning;
		}

		public bool Remove(int transmissionDownloadId)
		{
			var request = new RemoveRequestArgument();
			request.Ids.Add(transmissionDownloadId);

			#pragma warning disable 4014
			SendRequest<RemoveRequestArgument, object>(request);
			#pragma warning restore 4014
			return true;
		}

		async public Task<PingResponseArgument> GetStatus(int transmissionDownloadId)
		{
			var request = new PingRequestArgument();
			request.Ids = new List<long>();
			request.Ids.Add(transmissionDownloadId);
			var response = await SendRequest<PingRequestArgument, PingResponseArgument>(request);
			return response?.Arguments;
		}

		async public Task<PingResponseArgument> GetAllStatus()
		{
			var request = new PingAllRequestArgument();
			var response = await SendRequest<PingAllRequestArgument, PingResponseArgument>(request);
			return response.Arguments;
		}

		public void StartStatusCheck()
		{
			CheckStatus();
		}

		async void CheckStatus()
		{
			lock(_syncObject)
			{
				if(IsCheckingStatus)
					return;

				IsCheckingStatus = true;				
				Debug.WriteLine("STARTING STATUS CHECK");
			}

			while(StatusUpdated != null)
			{
				var response = await GetAllStatus();
				StatusUpdated?.Invoke(this, response);
				await Task.Delay(DownloadCoordinator.UpdateInterval);
			}

			IsCheckingStatus = false;
			Debug.WriteLine("STOPPING STATUS CHECK");
		}
	}

	public enum TransmissionPingResponse
	{
		None,
		Good,
		InvalidFileSize,
		InvalidName,
		CannotConnect,
		Stagnant,
		AvoidingConversion,
		Unknown
	}
}