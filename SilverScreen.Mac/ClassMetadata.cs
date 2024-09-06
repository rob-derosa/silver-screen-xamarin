using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using SQLite.Net.Attributes;
using SQLiteNetExtensions.Attributes;

namespace SilverScreen.Shared
{
	[MetadataType(typeof(DownloadRequestMetadata))]
	public partial class DownloadRequest
	{
		public class DownloadRequestMetadata
		{
			[PrimaryKey]
			public string ID
			{
				get;
				set;
			}

			[Ignore]
			public string S0E0
			{
				get;
			}

			[Ignore]
			public string SearchPhrase
			{
				get;
			}
		}
	}

	[MetadataType(typeof(DownloadResponseMetadata))]
	public partial class DownloadResponse
	{
		public class DownloadResponseMetadata
		{
			[PrimaryKey]
			public string ID
			{
				get;
				set;
			}

			[ForeignKey(typeof(DownloadRequest))]
			public string RequestId
			{
				get;
				set;
			}

			[OneToOne(CascadeOperations = CascadeOperation.All)]
			public DownloadRequest Request
			{
				get;
				set;
			}

			[Ignore]
			public List<string> SearchResults
			{
				get;
				set;
			}

			[Ignore]
			public string Extension
			{
				get;
			}

			[Ignore]
			public double PercentComplete
			{
				get;
			}

			[Ignore]
			public bool IsSuccessful
			{
				get;
			}

			[Ignore]
			public bool IsComplete
			{
				get;
			}

			[Ignore]
			public bool IsRunning
			{
				get;
			}
		}
	}
}