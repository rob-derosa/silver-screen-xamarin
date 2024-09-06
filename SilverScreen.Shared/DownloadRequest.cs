using System;
using Newtonsoft.Json;
using System.Linq;
using System.IO;
#if !__IOS__
using SQLite.Net.Attributes;
#endif

namespace SilverScreen.Shared
{
	public partial class DownloadRequest
	{
		public DownloadRequest()
		{
		}

		#if !__IOS__
		[PrimaryKey]
		#endif
		public string ID
		{
			get;
			set;
		} = Guid.NewGuid().ToString();

		string _showTitle;

		public string ShowTitle
		{
			get
			{
				return _showTitle;
			}
			set
			{
				_showTitle = value;

				if(_showTitle != null)
					_showTitle = _showTitle.Trim();
			}
		}

		string _episodeTitle;

		public string EpisodeTitle
		{
			get
			{
				return _episodeTitle;
			}
			set
			{
				_episodeTitle = value;

				if(_episodeTitle != null)
					_episodeTitle = _episodeTitle.Trim();
			}
		}

		public string EpisodeSummary
		{
			get;
			set;
		}

		public DateTime BroadcastDate
		{
			get;
			set;
		}

		public int SeasonNumber
		{
			get;
			set;
		}

		public int EpisodeNumber
		{
			get;
			set;
		}

		public string EpisodeTraktID
		{
			get;
			set;
		}

		public string ShowTraktID
		{
			get;
			set;
		}

		public string AlternateShowTitle
		{
			get;
			set;
		}

		#if !__IOS__
		[Ignore]
		#endif
		[JsonIgnore]
		public string S0E0
		{
			get
			{
				return "S{0}E{1}".Fmt(SeasonNumber.ToString("00"), EpisodeNumber.ToString("00"));
			}
		}

		#if !__IOS__
		[Ignore]
		#endif
		[JsonIgnore]
		public string SearchPhrase
		{
			get
			{
				var title = string.IsNullOrEmpty(AlternateShowTitle) ? ShowTitle : AlternateShowTitle;
				var s = title.Sterilize().Replace("'", "").Replace(":", "").Replace("&", "");
				return s;
			}
		}

		public string GetiTunesPath()
		{
			var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyMusic), "iTunes/iTunes Media/TV Shows");

			const string illegal = "/\\?%*|\"”“<>:’";
			string safeSeriesTitle = illegal.ToCharArray().Aggregate(ShowTitle, (c1, c2) => c1.Replace(c2, '_'));
			string safeEpisodeTitle = illegal.ToCharArray().Aggregate(EpisodeTitle, (c1, c2) => c1.Replace(c2, '_'));

			var parentDir = "{0}/{1}/Season {2}".Fmt(path, safeSeriesTitle, SeasonNumber);
			var filePath = Path.Combine(parentDir, safeEpisodeTitle + ".mp4");
			return filePath;
		}

		public DownloadRequest Clone()
		{
			return new DownloadRequest {
				SeasonNumber = SeasonNumber,
				EpisodeNumber = EpisodeNumber,
				AlternateShowTitle = AlternateShowTitle,
				ShowTitle = ShowTitle,
				ShowTraktID = ShowTraktID,
				EpisodeTraktID = EpisodeTraktID,
				EpisodeSummary = EpisodeSummary,
				EpisodeTitle = EpisodeTitle,
				BroadcastDate = BroadcastDate,
			};
		}
	}

	public class EpisodeDownloadedCheck
	{
		public EpisodeDownloadedCheck()
		{
		}

		public EpisodeDownloadedCheck(DownloadRequest request, bool existsLocally)
		{
			Request = request;
			ExistsLocally = existsLocally;
		}

		public DownloadRequest Request
		{
			get;
			set;
		}

		public bool ExistsLocally
		{
			get;
			set;
		}
	}
}