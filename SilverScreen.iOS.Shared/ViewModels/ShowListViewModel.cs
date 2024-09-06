using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using System.Threading;
using SilverScreen.Shared;
using System.Diagnostics;
using Xamarin;
using SQLiteNetExtensionsAsync.Extensions;
using Newtonsoft.Json;
using System.IO;
using System.Collections;

namespace SilverScreen.iOS.Shared
{
	public class ShowListViewModel : BaseViewModel
	{
		List<Show> _searchResults;
		List<Show> _allShows;

		public ShowListViewModel()
		{
			Shows = new List<Show>();
		}

		public List<Show> Shows
		{
			get
			{
				if(!SearchMode)
				{
					return _allShows;
				}

				return _searchResults;
			}
			set
			{
				_allShows = value;
			}
		}

		public bool SearchMode
		{
			get;
			set;
		}

		public void SearchFor(string text)
		{
			var showsByTitle = _allShows.Where(show => show.Title.ContainsNoCase(text)).ToList();
			var showsByDesc = _allShows.Where(show => !showsByTitle.Contains(show) && show.Overview.ContainsNoCase(text)
	            || show.CastAndCrew != null && (show.CastAndCrew.Cast.Any(c => c.Character.ContainsNoCase(text) || c.Person.Name.ContainsNoCase(text)))).ToList();

			showsByTitle.AddRange(showsByDesc);
			_searchResults = showsByTitle; 
		}

		async public Task LoadCachedShows()
		{
			IsBusy = true;

			var dtm = DateTime.Now;
			var list = await DataService.Instance.GetAllShowsAsync();
			Shows = list.OrderBy(s => s.SortTitle).ToList();

			if(Shows == null)
				Shows = new List<Show>();

			Shows = list.OrderBy(s => s.SortTitle).ToList();
			var diff = DateTime.Now.Subtract(dtm).TotalSeconds;

			if(Shows == null)
				Shows = new List<Show>();

			IsBusy = false;

			//Don't await
			WriteUpcomingShowsToSharedDisk(AppSettings.UpcomingShowsPath);
		}

		async public Task WriteUpcomingShowsToSharedDisk(string path)
		{
			var spanOut = DateTime.Today.ToUniversalTime().AddDays(7);
			var yesterday = DateTime.Today.ToUniversalTime();
			var episodes = await DataService.Instance.Connection.GetAllWithChildrenAsync<Episode>(e => e.InitialBroadcastDate > yesterday && e.InitialBroadcastDate < spanOut);
			episodes = episodes.OrderBy(e => e.InitialBroadcastDate).ToList();

			var showIds = episodes.Select(e => e.ShowID).Distinct();
			var shows = Shows.Where(s => showIds.Contains(s.ID));
			var upcomingEpisodes = new List<UpcomingEpisode>();

			foreach(var ep in episodes)
			{
				var show = shows.SingleOrDefault(s => s.ID == ep.ShowID);
				var upcoming = new UpcomingEpisode
				{
					ShowTitle = show.Title,
					ShowImageUrl = show.Images?.Poster?.Thumb,
					EpisodeTitle = ep.Title,
					BroadcastDate = ep.InitialBroadcastDate.Value,
					EpisodeDescription = ep.Overview,
					S0E0 = ep.S0E0,
					EpisodeImageUrl = show.GetBestScreenUrlForEpisode(ep)
				};

				upcomingEpisodes.Add(upcoming);
			}

			await Task.Run(() => {
				try
				{
					var json = JsonConvert.SerializeObject(upcomingEpisodes);
					if(File.Exists(path))
						File.Delete(path);
					
					using(var sw = new StreamWriter(path, false))
					{
						sw.Write(json);
					}

					Settings.Instance.LastUpcomingEpisodesWriteTime = DateTime.Now;
					Settings.Instance.Save();
				}
				catch(Exception e)
				{
					Debug.WriteLine(e);
				}
			});
		}


		async public Task RefreshShows(Action<Show> onRefreshShow = null)
		{
			try
			{
				IsBusy = true;
				TraktService.Instance.ClearHistory();
				var updated = await RunSafe(TraktService.Instance.GetUpdatedShowsSince(Settings.Instance.LastRefreshDate));

				if(updated == null)
					return;

				var hash = new Dictionary<int, Show>();

				foreach(var s in _allShows)
					hash.Add(s.Identifiers.Trakt, s);

				Settings.Instance.LastRefreshDate = DateTime.UtcNow;
				await Settings.Instance.Save();
				var showViewModel = new ShowDetailsViewModel();

				foreach(var s in updated)
				{
					if(WasCancelledAndReset)
					{
						Debug.WriteLine("Refreshing shows cancelled");
						return;
					}

					if(s == null)
						continue;

					if(!hash.ContainsKey(s.Identifiers.Trakt) || !_allShows.Exists(show => show.Identifiers.Trakt == s.Identifiers.Trakt))
					{
						//Debug.WriteLine("Skipping {0} - not located in shows list".Fmt(s.Title));
				    	continue;
					}

					var match = hash[s.Identifiers.Trakt];

					if(onRefreshShow != null)
						onRefreshShow(match);

					showViewModel.Show = match;
					await showViewModel.Refresh(true);
				}

				WriteUpcomingShowsToSharedDisk(AppSettings.UpcomingShowsPath);
			}
			catch(Exception e)
			{
				Debug.WriteLine("Error refreshing shows: " + e);
				Insights.Report(e);
			}
			finally
			{
				Debug.WriteLine("Refreshing shows complete");
				IsBusy = false;
			}
		}

		public async Task RemoveShow(Show show)
		{
			await DataService.Instance.Delete(show).ConfigureAwait(false);
			await RunSafe(TraktService.Instance.RemoveShowFromFavorites(show));
			Shows.Remove(show);
		}

		public async Task ClearAllShows()
		{
			TraktService.Instance.ClearHistory();
			await DataService.Instance.Truncate();
			Shows.Clear();
		}

		async public Task SyncFavoriteShows(Action<Show, int> onShowAdded, Action<Show, int> onShowRemoved)
		{
			IsBusy = true;

			try
			{
				if(Settings.Instance.TraktUsername == null)
				{
					var user = await RunSafe(TraktService.Instance.GetUserProfile()).ConfigureAwait(false);
					if(user == null)
						return;
					
					Settings.Instance.TraktUsername = user.Username;
				}

				await Settings.Instance.Save().ConfigureAwait(false);
				var favorites = await RunSafe(TraktService.Instance.GetFavoriteShows()).ConfigureAwait(false);

				if(favorites == null)
					return;

				var existingTraktIds = Shows.Select(s => s.Identifiers.Trakt).ToList();
				var favoriteTraktIds = favorites.OrderBy(s => s.Show.SortTitle).Select(s => s.Show.Identifiers.Trakt);
				var toAdd = favoriteTraktIds.Where(id => !existingTraktIds.Contains(id));
				var toRemove = existingTraktIds.Where(id => !favoriteTraktIds.Contains(id));

				Settings.Instance.LastRefreshDate = DateTime.UtcNow;
				await Settings.Instance.Save();

				var comp = new ShowComparer();
				var showViewModel = new ShowDetailsViewModel();

				foreach(var traktId in toRemove)
				{
					var show = Shows.SingleOrDefault(s => s.Identifiers.Trakt == traktId);

					if(show == null)
						continue;

					var index = Shows.IndexOf(show);

					if(index < 0)
						continue;

					Debug.WriteLine("Removed {0}", show);
					Shows.Remove(show);
					onShowRemoved?.Invoke(show, index);
				}

				foreach(var traktId in toAdd)
				{
					try
					{
						var show = favorites.SingleOrDefault(s => s.Show.Identifiers.Trakt == traktId).Show;
						if(WasCancelledAndReset)
						{
							Debug.WriteLine("Sync cancelled");
							return;
						}

						showViewModel.Show = show;
						var index = Shows.BinarySearch(show, comp);

						if(index < 0)
							index = ~index;

						Shows.Insert(index, show);
						onShowAdded(show, index);
						await showViewModel.Refresh(true);

						if(show.PreviousEpisode == null)
						{
							//This shouldn't happen too often unless it's a net new show so let's log to keep an eye on it over time
							var kvp = new Dictionary<string, string> { { "Title", show.Title }, { "TraktID", show.Identifiers?.Trakt.ToString() } };
							Insights.Track("ShowWithNoPreviousEpisode", kvp);
						}

						Debug.WriteLine("Added {0}", show);
					}
					catch(Exception e)
					{
						Debug.WriteLine("Error adding show: " + e);
						Insights.Report(e);
					}
				}

				WriteUpcomingShowsToSharedDisk(AppSettings.UpcomingShowsPath);
			}
			catch(Exception e)
			{
				Debug.WriteLine("Error syncing shows: " + e);
				Insights.Report(e);
			}
			finally
			{
				Debug.WriteLine("Syncing rated shows complete");
				IsBusy = false;
			}
		}

		async public Task<int> AddShow(Show show, SortMode sortMode)
		{
			show = await RunSafe(TraktService.Instance.GetShow(show.Identifiers.Trakt));

			TraktService.Instance.ClearHistory(show.Identifiers?.Trakt.ToString());

			var showViewModel = new ShowDetailsViewModel { Show = show };
			await showViewModel.Refresh(true);

			if(!string.IsNullOrEmpty(Settings.Instance.TraktUsername))
			{
				await RunSafe(TraktService.Instance.AddShowToFavorites(show));
			}

			Shows.Add(show);
			int index = 0;
			Show nextShow;

			if(Shows.Count > 0)
			{
				switch(sortMode)
				{
					case SortMode.Title:
						{
							nextShow = Shows.FirstOrDefault(s => string.Compare(s.SortTitle, show.SortTitle, StringComparison.InvariantCultureIgnoreCase) == 1);
							index = Shows.IndexOf(nextShow);
							break;
						}

					case SortMode.RecentEpisodes:
						{
							var temp = Shows.ToList();
							temp.Add(show);

							temp = SortListByRecentEpisodes(temp);
							index = temp.IndexOf(show);
							break;
						}
					case SortMode.UpcomingEpisodes:
						{
							var temp = Shows.ToList();
							temp.Add(show);

							temp = SortListByUpcomgingEpisodes(temp);
							index = temp.IndexOf(show);

							break;
						}
				}
			}

			index = index < 0 ? Shows.Count - 1 : index;
			index = index < 0 ? 0 : index;

			Shows.Remove(show);
			Shows.Insert(index, show);

			return index;
		}

		public List<Show> SortListByEpisodeTitle(List<Show> shows)
		{
			return shows.OrderBy(s => s.SortTitle).ToList();
		}

		public List<Show> SortListByRecentEpisodes(List<Show> shows)
		{
			var have = shows.Where(s => s.PreviousEpisode != null && s.PreviousEpisode.InitialBroadcastDate != null);
			var dontHave = shows.Where(s => s.PreviousEpisode == null || s.PreviousEpisode.InitialBroadcastDate == null);
			var list = have.OrderByDescending(s => s.PreviousEpisode.InitialBroadcastDate).ToList();
			list.AddRange(dontHave.OrderBy(s => s.SortTitle));

			return list;
		}

		public List<Show> SortListByUpcomgingEpisodes(List<Show> shows)
		{
			var have = shows.Where(s => s.NextEpisode != null && s.NextEpisode.InitialBroadcastDate != null);
			var dontHave = shows.Where(s => s.NextEpisode == null || s.NextEpisode.InitialBroadcastDate == null);
			var list = have.OrderBy(s => s.NextEpisode.InitialBroadcastDate).ToList();
			list.AddRange(dontHave.OrderBy(s => s.SortTitle));

			return list;
		}

		class ShowComparer : IComparer<Show>
		{
			public int Compare(Show x, Show y)
			{
				if(x == y)
					return 0;

				if(x == null)
					return -1;

				if(y == null)
					return 1;

				return String.Compare(x.SortTitle, y.SortTitle, true);
			}
		}
	}
}