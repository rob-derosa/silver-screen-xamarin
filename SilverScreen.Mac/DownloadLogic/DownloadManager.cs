using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using AppKit;
using Foundation;
using SilverScreen.Shared;
using System.Configuration;

namespace SilverScreen.Mac
{
	public class DownloadManager
	{
		#region Properties and Fields

		CancellationTokenSource _tokenSource = new CancellationTokenSource();
		TaskCompletionSource<DownloadResponse> _taskCompletionSource;
		string _downloadDirectory;

		const long _minimumFileSize = 80 * 1024 * 1024;
		const int _maxStagnantSizeCount = 100;

		long _lastFileSize = 0;
		long _stagnantSizeCount = 0;
		bool _hasTriedReconverting;

		//private bool _skipToNextSearchResult;

		public CancellationToken CancellationToken
		{
			get
			{
				return _tokenSource.Token;
			}
		}

		string[] ValidExtensions
		{
			get;
			set;
		//} = new[] { ".mp4" };
		} = new[] {".mp4", ".avi", ".mkv", ".m4v"};

		public DownloadResponse Response
		{
			get;
			private set;
		}

		int? FileIndex
		{
			get;
			set;
		}

		#endregion

		//Existing download
		public DownloadManager(DownloadResponse response)
		{
			Response = response;
		}

		//New download
		public DownloadManager(DownloadRequest request)
		{
			Response = new DownloadResponse {
				Request = request,
				RequestId = request.ID
			};

			Response.State = DownloadState.Enqueued;
		}

		/// <summary>
		/// Searches for episode and iterates through all possible options
		/// </summary>
		public Task<DownloadResponse> DownloadEpisodeToDisk()
		{
			var task = new Task<DownloadResponse>(() =>
			{
				_taskCompletionSource = new TaskCompletionSource<DownloadResponse>();

				if(CancellationToken.IsCancellationRequested)
				{
					CompleteDownload(DownloadResult.Cancelled);
					return _taskCompletionSource.Task.Result;
				}

				SearchForEpisodeAndProcessResults();
				return _taskCompletionSource.Task.Result;
			});

			return task;
		}

		void SearchForEpisodeAndProcessResults()
		{
			if(Response.SearchResults == null || Response.SearchResults.Count == 0)
			{
				Response.State = DownloadState.Searching;
				BroadcastStatus(true);

				Response.SearchResults = SearchForEpisode().Result.Select(sr => sr.Url).ToList();
			}

			ProcessSearchResults();
		}

		void ProcessSearchResults()
		{
			if(Response.SearchResults.Count == 0)
			{
				CompleteDownload(DownloadResult.Failed, FailureReason.NoSearchResults);
			}
			else
			{
				int count = 0;
				//Loop through all the search results to find a successfull lead
				while(Response.SearchResults.Count > 0 && Response.State != DownloadState.Complete)
				{
					var magnetUrl = Response.SearchResults.First();
					var success = ProcessDownloadRequest(magnetUrl);
					Response.SearchResults.Remove(magnetUrl);
					BroadcastStatus(true);

					if(!success)
					{
						Debug.WriteLine(Response.FailureReason);

						//Erred out somewhere - this shouldn't happen tho since we would just move onto the next option
						if(Response.Result != null)
							break;

						Task.Delay(3000).Wait();
						count++;
					}
					else
					{
						break;	
					}
				}

				if(Response.Result == null)
				{
					if(Response.State != DownloadState.Complete)
					{
						CompleteDownload(DownloadResult.Failed, Response.FailureReason ?? "{0} unsuccessul attempt{1} made".Fmt(count, count == 1 ? "" : "s"));
					}
					else
					{
						CompleteDownload(Response.FailureReason == null ? DownloadResult.Success : DownloadResult.Failed);
					}
				} 
			}
		}

		/// <summary>
		/// Downloads the file, converts and adds to iTunes
		/// </summary>
		bool ProcessDownloadRequest(string torrentUrl)
		{
			Debug.WriteLine("Processing torrent " + torrentUrl);
			if(Response.DownloadPath == null || !System.IO.File.Exists(Response.DownloadPath))
			{
				DownloadTorrentUsingTransmission(torrentUrl);
			}

			if(Response.DownloadPath != null && System.IO.File.Exists(Response.DownloadPath))
			{
				OnFileDownloadCompleted();
			}
			else
			{
				CleanUp();

				if(!CancellationToken.IsCancellationRequested && Response.FailureReason == FailureReason.None)
					Response.FailureReason = FailureReason.UnableToDownload;
			}

			return Response.FailureReason == FailureReason.None && Response.Result == null;
		}

		/// <summary>
		/// Adds, tracks and removes the torrent from Transmission
		/// </summary>
		void DownloadTorrentUsingTransmission(string torrentUrl)
		{
			Response.FailureReason = FailureReason.None;
			var tuple = TransmissionController.Instance.Add(torrentUrl, Response.TransmissionHash).Result;

			if(tuple == null)
			{
				Response.State = DownloadState.Skipping;
				Response.FailureReason = FailureReason.UnableToAddTorrentToTransmission;
				return;
			}

			Response.State = DownloadState.Validating;
			Response.TransmissionHash = tuple.Item1;
			Response.TransmissionID = tuple.Item2;

			Response.Progress = null;
			Response.FinalPath = null;
			Response.DownloadPath = null;
			Response.TorrentName = null;

			BroadcastStatus(true);

			//Give Transmission a second before we start pinging it
			Task.Delay(1000).Wait();

			TransmissionController.Instance.StatusUpdated += OnTransmissionStatusUpdated;
			TransmissionController.Instance.StartStatusCheck();

			while(Response.FailureReason == null && Response.PercentComplete < 1)
			{
				Task.Delay(DownloadCoordinator.UpdateInterval).Wait();
			}
		}

		void OnTransmissionStatusUpdated(object sender, PingResponseArgument e)
		{
			var tor = e?.Torrents.FirstOrDefault(t => t.Id == Response.TransmissionID);

			if(tor == null && Response.Result == null)
			{
				//Ping failed - do something here - move to next torrent
				CompleteTransmission();
				Response.FailureReason = FailureReason.TransmissionPingFailed;
				return;
			}

			_downloadDirectory = tor.DownloadDir;
			Response.FileSize = tor.SizeWhenDone;
			Response.CurrentFileSize = tor.SizeWhenDone - tor.LeftUntilDone;
			Response.Progress = TimeSpan.FromSeconds(tor.Eta).ToTime();
			Response.ParentPath = Path.Combine(_downloadDirectory, tor.Name);

			if(Settings.Instance.EnforceStagnation)
			{
				if(_lastFileSize == Response.CurrentFileSize)
				{
					_stagnantSizeCount++;

					if(_stagnantSizeCount >= _maxStagnantSizeCount)
					{
						//Stag too long, move onto next option
						CompleteTransmission();
						Response.FailureReason = FailureReason.DownloadStagnant;
						return;
					}
				}
				else
				{
					_stagnantSizeCount = 0;
				}
			}

			_lastFileSize = Response.CurrentFileSize;

			if(Response.DownloadPath == null)
			{
				//Look for valid files
				if(tor.Files != null && tor.Files.Count > 0)
				{
					//No valid files found in torrent
					var fileReason = SetDownloadPath(tor);

					if(fileReason != FailureReason.None)
					{
						CompleteTransmission();
						Response.FailureReason = fileReason;
						return;
					}
				}
			}

			if(Response.DownloadPath != null && Response.State != DownloadState.Downloading)
			{
				Response.State = DownloadState.Downloading;
				BroadcastStatus(true);
			}
			else
			{
				BroadcastStatus(false);
			}

			if(Response.PercentComplete == 1.0)
			{
				//Download complete
				CompleteTransmission();
			}
		}

		void CompleteTransmission()
		{
			_stagnantSizeCount = 0;

			if(Response.TransmissionID != 0)
				TransmissionController.Instance.Remove(Response.TransmissionID);

			TransmissionController.Instance.StatusUpdated -= OnTransmissionStatusUpdated;
		}

		void OnFileDownloadCompleted()
		{
			//Called here in case recovering from a crash and download finished
			CompleteTransmission();

			Response.State = DownloadState.Converting;
			BroadcastStatus(true);

			if(HandleConversion())
			{
				if(CancellationToken.IsCancellationRequested)
				{
					CompleteDownload(DownloadResult.Cancelled);
					return;
				}						

				if(Response.FinalPath != null && System.IO.File.Exists(Response.FinalPath) &&
				   Response.FinalPath.ToLower().EndsWith(".mp4", StringComparison.Ordinal))
				{
					Response.State = DownloadState.AddingToiTunes;
					BroadcastStatus(true);

					if(iTunesController.AddMediaToiTunes(Response))
					{
						Response.State = DownloadState.Complete;
						Response.FailureReason = FailureReason.None;
					}
					else
					{
						if(Response.FinalPath == Response.DownloadPath)
						{
							RecycleFile(Response.DownloadPath);
							Response.FailureReason = FailureReason.UnableToAddToiTunes;
						}
						else
						{
							RecycleFile(Response.FinalPath);
							Response.FinalPath = null;
						
							if(!_hasTriedReconverting)
							{
								_hasTriedReconverting = true;
								OnFileDownloadCompleted();
								return;
							}
							else
							{
								Response.FailureReason = FailureReason.UnableToAddToiTunes;
							}
						}
					}
				}
				else
				{
					//Handbrake conversion succeeded but FinalPath is missing - wha?
					Response.FailureReason = FailureReason.Unknown;
				}
			}
			else
			{
				Response.FailureReason = FailureReason.UnableToConvertWithHandbrake;
			}
		}

		/// <summary>
		/// Searches using all engines for leads
		/// </summary>
		/// <returns>The for episode.</returns>
		async Task<List<SearchResult>> SearchForEpisode()
		{
			var list = new List<SearchResult>();
			try
			{
				var pbSearchHost = new PirateBaySearchHost();
				var pb = await pbSearchHost.SearchForEpisode(Response.Request, CancellationToken);
				list.AddRange(pb);

				//var kaSearchHost = new KickassSearchHost();
				//var ka = await kaSearchHost.SearchForEpisode(Response.Request, CancellationToken);
				//list.AddRange(ka);

				//list = list.OrderByDescending(s => s.Seeds).ToList();

				Debug.WriteLine("");
				Debug.WriteLine("");
				Debug.WriteLine("");
				list.ForEach(s => Debug.WriteLine(s.Seeds + " : " + s.Url));
			}
			catch(Exception e)
			{
				Debug.WriteLine(e);
			}

			return list;
		}

		bool HandleConversion()
		{
			//No need to convert, file is already compatible
			if(System.IO.File.Exists(Response.DownloadPath) && Response.DownloadPath.ToLower().EndsWith(".mp4", StringComparison.Ordinal))
			{
				Response.FinalPath = Response.DownloadPath;
				return true;
			}

			if(Response.FinalPath != null)
			{
				if(System.IO.File.Exists(Response.FinalPath))
				{
					if(System.IO.File.Exists(Response.DownloadPath))
					{
						var fileInfo = new FileInfo(Response.FinalPath);
						var origFileInfo = new FileInfo(Response.DownloadPath);

						//Converted file needs to be at least 2/3 the size of the original
						if(fileInfo.Length >= origFileInfo.Length * .66)
						{
							return true;
						}
					}
				}
			}

			ConvertFileToCompatibleFormat(Response);
			return System.IO.File.Exists(Response.FinalPath);
		}

		bool ConvertFileToCompatibleFormat(DownloadResponse response)
		{
			try
			{
				var downloadPath = response.DownloadPath;
				var exeDir = Path.GetDirectoryName(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location));
				var exePath = Path.Combine(exeDir, "Resources/HandBrakeCLI");

				var parentDir = Path.GetDirectoryName(downloadPath);
				var tempPath = Path.Combine(parentDir, Path.GetFileNameWithoutExtension(downloadPath) + ".mp4");
				var outputPath = tempPath;
				var finalPath = tempPath;

				response.FinalPath = finalPath;
				DownloadCoordinator.Instance.History.UpdateDownload(Response, true);


				if(System.IO.File.Exists(outputPath))
				{
					return true;
				}
				//Remove any existing partial conversions
				//RecycleFile(outputPath);

				const string presets = "-e x264 -q 20.0 -a 1,1 -E faac,copy:ac3 -B 160,160 -6 dpl2,auto -R Auto,Auto -D 0.0,0.0 -f mp4 -X 720 --loose-anamorphic -m -x cabac=0:ref=2:me=umh:bframes=0:weightp=0:8x8dct=0:trellis=0:subme=6";
				var args = "-i \"{0}\" -o \"{1}\" {2}".Fmt(downloadPath, outputPath, presets);
				Debug.WriteLine(args);
				var info = new ProcessStartInfo(exePath, args);

				info.RedirectStandardOutput = true;
				info.RedirectStandardError = true;
				info.UseShellExecute = false;
				info.CreateNoWindow = true;

				var proc = new Process {
					StartInfo = info	
				};

				proc.Start();
				while(!proc.StandardOutput.EndOfStream && !proc.HasExited)
				{
					if(CancellationToken.IsCancellationRequested)
					{
						proc.Kill();
						break;	
					}

					var line = proc.StandardOutput.ReadLine();
					Debug.WriteLine(line);

					var parts = line.Split(' ');

					if(parts.Length < 6)
						continue;

					var percentage = parts[5];
					response.Progress = percentage + " %";
					BroadcastStatus(false);

					Thread.Sleep(1000);
				}

				while(!proc.StandardError.EndOfStream && !proc.HasExited)
				{
					Debug.WriteLine("HANDBRAKE ERROR");
					var line = proc.StandardOutput.ReadLine();
					Debug.WriteLine(line);
				}

				if(proc.ExitCode == 0 && System.IO.File.Exists(outputPath))
				{
					System.IO.File.Move(outputPath, finalPath);
					return true;
				}

				return false;
			}
			catch(Exception e)
			{
				Debug.WriteLine("Error converting with Handbrake: " + e);
				return false;
			}
		}

		public virtual void Cancel()
		{
			if(!_tokenSource.IsCancellationRequested && CancellationToken.CanBeCanceled)
			{
				_tokenSource.Cancel();
			}

			switch(Response.State)
			{
				case DownloadState.Searching:
					break;
				case DownloadState.Downloading:
				case DownloadState.Validating:
				case DownloadState.Skipping:
					CompleteTransmission();
					break;
			}

			CompleteDownload(DownloadResult.Cancelled);
		}

		void CompleteDownload(DownloadResult result, string failureReason = null)
		{
			Response.State = DownloadState.Complete;
			Response.Result = result;
			Response.FailureReason = failureReason;
			Response.DateCompleted = DateTime.UtcNow;
			CleanUp();

			BroadcastStatus(true);

			if(_taskCompletionSource != null)
				_taskCompletionSource.TrySetResult(Response);
		}

		internal void BroadcastStatus(bool saveHistory)
		{
			if(saveHistory)
				DownloadCoordinator.Instance.History.UpdateDownload(Response, true);
	
			ServiceHost.Instance.SendMessage(MessageAction.DownloadUpdateInquiry, Response);
		}


		int _loopForPrefCount;
		string SetDownloadPath(Torrent torrent)
		{
			//int i = 0;
			var reason = FailureReason.NoValidFilesFoundByName;

			foreach(var f in torrent.Files)
			{
				var ext = Path.GetExtension(f.Name).ToLower();

				if(!ValidExtensions.Contains(ext))
				{
					reason = FailureReason.NoValidFilesFoundByExtension;
					continue;
				}

				if(f.Length >= _minimumFileSize)
				{
					//Only consider this conditional the first loop thru
					if(_loopForPrefCount < 1 && Settings.Instance.PreferMp4 && ext.ToLower() != ".mp4")
					{
						reason = FailureReason.NoValidFilesFoundByExtension;
						continue;
					}

					//FileIndex = i;
					Response.DownloadPath = Path.Combine(_downloadDirectory, f.Name);
					Response.FileSize = f.Length;
					return FailureReason.None;
				}
				else
				{
					reason = FailureReason.NoValidFilesFoundBySize;
					continue;
				}
			}

			if(_loopForPrefCount >= 1)
				return reason;

			_loopForPrefCount++;

			if(Settings.Instance.PreferMp4 && _loopForPrefCount <= 1)
			{
				return SetDownloadPath(torrent);
			}

			return reason;
		}

		void CleanUp()
		{
			RecycleFile(Response.FinalPath);
			RecycleFile(Response.ParentPath);
		}

		public static void RecycleFile(string path)
		{
			if(path == null)
				return;
			
			NSRunLoop.Main.BeginInvokeOnMainThread(() =>
			{
				nint tag;
				var dir = Path.GetDirectoryName(path);
				var file = Path.GetFileName(path);
				NSWorkspace.SharedWorkspace.PerformFileOperation(NSWorkspace.OperationRecycle, dir, string.Empty, new [] {
					file
				}, out tag);
			});
		}
	}
}