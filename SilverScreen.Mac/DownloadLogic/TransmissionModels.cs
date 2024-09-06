using System.Collections.Generic;
using Newtonsoft.Json;

namespace SilverScreen.Mac
{
	public class AddRequestArgument : BaseRequestArgument
	{
		public AddRequestArgument() : base("torrent-add")
		{
		}

		[JsonProperty("paused")]
		public bool Paused
		{
			get;
			set;
		}

		[JsonProperty("download-dir")]
		public string DownloadDir
		{
			get;
			set;
		}

		[JsonProperty("filename")]
		public string Filename
		{
			get;
			set;
		}
	}

	public class AddResponseArgument
	{
		[JsonProperty("torrent-added")]
		public TorrentAddResponse TorrentAdded
		{
			get;
			set;
		}

		[JsonProperty("torrent-duplicate")]
		public TorrentAddResponse TorrentDuplicate
		{
			get;
			set;
		}
	}

	public class PingAllRequestArgument : BaseRequestArgument
	{
		public PingAllRequestArgument() : base("torrent-get")
		{
			Fields = new List<string>();

			Fields.Add("id");
			Fields.Add("name");
			Fields.Add("eta");
			Fields.Add("addedDate");
			Fields.Add("hashString");
			Fields.Add("percentDone");
			Fields.Add("isFinished");
			Fields.Add("sizeWhenDone");
			Fields.Add("error");
			Fields.Add("errorString");
			Fields.Add("isStalled");
			Fields.Add("downloadDir");
			Fields.Add("metadataPercentComplete");
			Fields.Add("queuePosition");
			Fields.Add("rateDownload");
			Fields.Add("files");
			Fields.Add("status");
			Fields.Add("leftUntilDone");
		}

		[JsonProperty("fields")]
		public List<string> Fields
		{
			get;
			set;
		}

	}

	public class PingRequestArgument : PingAllRequestArgument
	{
		[JsonProperty("ids")]
		public List<long> Ids
		{
			get;
			set;
		}
	}

	public class PingResponseArgument
	{
		[JsonProperty("removed")]
		public IList<Torrent> Removed
		{
			get;
			set;
		}

		[JsonProperty("torrents")]
		public IList<Torrent> Torrents
		{
			get;
			set;
		}
	}

	public class RemoveRequestArgument : BaseRequestArgument
	{
		public RemoveRequestArgument() : base("torrent-remove")
		{
			Ids = new List<long>();
		}

		[JsonProperty("ids")]
		public List<long> Ids
		{
			get;
			set;
		}
	}

	public class BaseRequestArgument
	{
		public BaseRequestArgument()
		{
		}

		public BaseRequestArgument(string methodName)
		{
			Method = methodName;
		}

		[JsonIgnore]
		public string Method
		{
			get;
			set;
		}
	}

	public class TransmissionRequest<T> where T : BaseRequestArgument, new()
	{
		public TransmissionRequest()
		{
			Arguments = new T();
		}

		[JsonProperty("method")]
		public string Method
		{
			get
			{
				return Arguments != null ? Arguments.Method : null;
			}
		}

		[JsonProperty("arguments")]
		public T Arguments
		{
			get;
			set;
		}
	}

	public class TransmissionResponse<T>
	{
		[JsonProperty("arguments")]
		public T Arguments
		{
			get;
			set;
		}

		[JsonProperty("result")]
		public string Result
		{
			get;
			set;
		}

		public bool IsSuccessful
		{
			get
			{
				return Result == "success";
			}
		}
	}

	public class TorrentAddResponse
	{

		[JsonProperty("hashString")]
		public string HashString
		{
			get;
			set;
		}

		[JsonProperty("id")]
		public long Id
		{
			get;
			set;
		}

		[JsonProperty("name")]
		public string Name
		{
			get;
			set;
		}
	}

	public class Torrent
	{
		[JsonProperty("addedDate")]
		public long AddedDate
		{
			get;
			set;
		}

		[JsonProperty("downloadDir")]
		public string DownloadDir
		{
			get;
			set;
		}

		[JsonProperty("error")]
		public long Error
		{
			get;
			set;
		}

		[JsonProperty("errorString")]
		public string ErrorString
		{
			get;
			set;
		}

		[JsonProperty("hashString")]
		public string HashString
		{
			get;
			set;
		}

		[JsonProperty("eta")]
		public long Eta
		{
			get;
			set;
		}

		[JsonProperty("files")]
		public IList<File> Files
		{
			get;
			set;
		}

		[JsonProperty("id")]
		public long Id
		{
			get;
			set;
		}

		[JsonProperty("isFinished")]
		public bool IsFinished
		{
			get;
			set;
		}

		[JsonProperty("isStalled")]
		public bool IsStalled
		{
			get;
			set;
		}

		[JsonProperty("leftUntilDone")]
		public long LeftUntilDone
		{
			get;
			set;
		}

		[JsonProperty("metadataPercentComplete")]
		public decimal MetadataPercentComplete
		{
			get;
			set;
		}

		[JsonProperty("name")]
		public string Name
		{
			get;
			set;
		}

		[JsonProperty("percentDone")]
		public decimal PercentDone
		{
			get;
			set;
		}

		[JsonProperty("queuePosition")]
		public long QueuePosition
		{
			get;
			set;
		}

		[JsonProperty("rateDownload")]
		public long RateDownload
		{
			get;
			set;
		}

		[JsonProperty("sizeWhenDone")]
		public long SizeWhenDone
		{
			get;
			set;
		}

		[JsonProperty("status")]
		public long Status
		{
			get;
			set;
		}
	}

	public class Tracker
	{
		[JsonProperty("announce")]
		public string Announce
		{
			get;
			set;
		}

		[JsonProperty("id")]
		public long Id
		{
			get;
			set;
		}

		[JsonProperty("scrape")]
		public string Scrape
		{
			get;
			set;
		}

		[JsonProperty("tier")]
		public long Tier
		{
			get;
			set;
		}
	}

	public class File
	{

		[JsonProperty("bytesCompleted")]
		public long BytesCompleted
		{
			get;
			set;
		}

		[JsonProperty("length")]
		public long Length
		{
			get;
			set;
		}

		[JsonProperty("name")]
		public string Name
		{
			get;
			set;
		}
	}
}