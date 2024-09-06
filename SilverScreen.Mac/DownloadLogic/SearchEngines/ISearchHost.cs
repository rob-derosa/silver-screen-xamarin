using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SilverScreen.Shared;
using System.Threading;

namespace SilverScreen.Mac
{
	public interface ISearchHost
	{
		Task<List<SearchResult>> SearchForEpisode(DownloadRequest download, CancellationToken token);
	}

	public class SearchResult
	{
		public string Url
		{
			get;
			set;
		}

		public long Seeds
		{
			get;
			set;
		}

		public DateTime PublishDate
		{
			get;
			set;
		}
	}
}