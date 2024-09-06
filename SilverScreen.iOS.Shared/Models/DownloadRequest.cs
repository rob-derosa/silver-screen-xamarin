using Newtonsoft.Json;
using SilverScreen.iOS.Shared;
using System.Linq;
using System;

namespace SilverScreen.Shared
{
	public partial class DownloadRequest
	{
		public DownloadRequest(Show show, Episode ep)
		{
			ShowTraktID = show.Identifiers?.Trakt.ToString();
			EpisodeTraktID = ep.Identifiers?.Trakt.ToString();
			ShowTitle = show.Title;
			EpisodeTitle = ep.Title;
			EpisodeNumber = ep.Number;
			SeasonNumber = ep.SeasonNumber;
			BroadcastDate = ep.InitialBroadcastDate == null ? DateTime.MinValue : ep.InitialBroadcastDate.Value;
			AlternateShowTitle = show.AlternateTitle;
		}

		Episode _episode;

		[JsonIgnore]
		public Episode Episode
		{
			get
			{
				if(_episode == null && Show != null && EpisodeTraktID != null)
				{
					var season = Show.SeasonByNumber(SeasonNumber);
					if(season != null)
					{
						season.EnsureEpisodesLoadedFromCache();
						_episode = season.Episodes.FirstOrDefault(e => e.Identifiers?.Trakt.ToString() == EpisodeTraktID);
					}
				}
				return _episode;
			}
		}

		Show _show;

		[JsonIgnore]
		public Show Show
		{
			get
			{
				if(_show == null)
				{
					var vm = ServiceContainer.Resolve<ShowListViewModel>();
					_show = vm.Shows.FirstOrDefault(s => s?.Identifiers?.Trakt.ToString() == ShowTraktID);
				}

				return _show;
			}
		}
	}
}