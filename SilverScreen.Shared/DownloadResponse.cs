using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

#if !__IOS__
using SQLite.Net.Attributes;
using SQLiteNetExtensions.Attributes;
#endif

namespace SilverScreen.Shared
{
	public partial class DownloadResponse
	{
		#if !__IOS__
		[PrimaryKey]
		#endif
		public string ID
		{
			get;
			set;
		} = Guid.NewGuid().ToString();

		#if !__IOS__
		[ForeignKey(typeof(DownloadRequest))]
		#endif
		public string RequestId
		{
			get;
			set;
		}

		#if !__IOS__
		[OneToOne(CascadeOperations = CascadeOperation.All)]
		#endif
		public DownloadRequest Request
		{
			get;
			set;
		}

		public DateTime DateAdded
		{
			get;
			set;
		}

		public DateTime DateCompleted
		{
			get;
			set;
		}

		public DownloadState State
		{
			get;
			set;
		}

		public string FailureReason
		{
			get;
			set;
		}

		public DownloadResult? Result
		{
			get;
			set;
		}

		public long FileSize
		{
			get;
			set;
		}

		public string Progress
		{
			get;
			set;
		}

		public long CurrentFileSize
		{
			get;
			set;
		}

		public string TransmissionHash
		{
			get;
			set;
		}

		public int TransmissionID
		{
			get;
			set;
		}

		List<string> _searchResults;

		#if !__IOS__
		[Ignore]
		#endif
		public List<string> SearchResults
		{
			get
			{
				return _searchResults;
			}
			set
			{
				_searchResults = value;

				if(_searchResults == null || _searchResults.Count == 0)
					_searchResultString = null;
				else
					_searchResultString = string.Join("|", _searchResults);
			}
		}

		string _searchResultString;

		[JsonIgnore]
		public string SearchResultString
		{
			get
			{
				if(_searchResults == null || _searchResults.Count == 0)
					return null;

				return  string.Join("|", _searchResults);
			}
			set
			{
				_searchResultString = value;

				if(string.IsNullOrWhiteSpace(_searchResultString))
					_searchResults = null;
				else
					_searchResults = _searchResultString.Split('|').ToList();
			}
		}

		public string SelectedSearchResult
		{
			get;
			set;
		}

		public string DownloadPath
		{
			get;
			set;
		}

		public string FinalPath
		{
			get;
			set;
		}

		public string TorrentName
		{
			get;
			set;
		}

		public string ParentPath
		{
			get;
			set;
		}

		#if !__IOS__
		[Ignore]
		#endif
		public string Extension
		{
			get
			{
				return DownloadPath == null ? null : Path.GetExtension(DownloadPath);
			}
		}

		#if !__IOS__
		[Ignore]
		#endif
		public double PercentComplete
		{
			get
			{
				if(FileSize == 0)
					return 0;

				double d = (double)CurrentFileSize / (double)FileSize;
				return d;
			}
		}

		#if !__IOS__
		[Ignore]
		#endif
		public bool IsSuccessful
		{
			get
			{
				return Result != null && Result == DownloadResult.Success;
			}
		}

		#if !__IOS__
		[Ignore]
		#endif
		public bool IsComplete
		{
			get
			{
				return Result != null && State == DownloadState.Complete;
			}
		}

		#if !__IOS__
		[Ignore]
		#endif
		public bool IsRunning
		{
			get
			{
				return Result == null && (State != DownloadState.None && State != DownloadState.Complete);
			}
		}

		public override string ToString()
		{
			return string.Format("[ID={0}, Show={1}, SeasonEp={2}, DateAdded={3}]", ID, Request?.ShowTitle, Request?.S0E0, DateAdded);
		}
	}

	public enum DownloadResult
	{
		None = 0,
		Success = 1,
		Cancelled = 2,
		Failed = 3
	}

	public enum DownloadState
	{
		None,
		//0
		Enqueued,
		//1
		Searching,
		//2
		Validating,
		//3
		Downloading,
		//4
		Converting,
		//5
		AddingToiTunes,
		//6
		Skipping,
		//7
		Complete
		//8
	}

	public static class FailureReason
	{
		public static readonly string None = null;
		public static readonly string NoSearchResults = "no search results found";
		public static readonly string NoValidFilesFoundByName = "no files with the right name found";
		public static readonly string NoValidFilesFoundBySize = "no files with the right file size found";
		public static readonly string NoValidFilesFoundByExtension = "no files with the right extension found";
		public static readonly string TransmissionPingFailed = "couldn't communicate with Transmission";
		public static readonly string UnableToAddTorrentToTransmission = "can't add the torrent to Transmission";
		public static readonly string UnableToConvertWithHandbrake = "unable to convert with Handbrake";
		public static readonly string UnableToDownload = "unable to download";
		public static readonly string UnableToAddToiTunes = "unable to add to iTunes";
		public static readonly string DownloadStagnant = "download was stagnant";
		public static readonly string Unknown = "unknown error";
	}
}