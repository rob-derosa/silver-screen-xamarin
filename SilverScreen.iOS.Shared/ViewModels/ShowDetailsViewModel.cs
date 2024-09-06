using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using SQLiteNetExtensionsAsync.Extensions;

namespace SilverScreen.iOS.Shared
{
	public class ShowDetailsViewModel : BaseViewModel
	{
		public Show Show
		{
			get;
			set;
		}

		#region Loading Seasons

		async public Task Refresh(bool fullRefresh)
		{
			Show.PreviousEpisode = null;
			Show.NextEpisode = null;

			if(WasCancelledAndReset)
				return;

			await EnsureSeasonsLoadedFromCloud(fullRefresh);

			if(WasCancelledAndReset)
				return;

			await EnsureNextEpisodeLoaded(false, fullRefresh);

			if(WasCancelledAndReset)
				return;

			await EnsurePreviousEpisodeLoaded(false, fullRefresh);

			if(fullRefresh)
			{
				//await EnsureAllSeasonsLoadedd();
				//var cloneJson = JsonConvert.SerializeObject(this);
				//for(int i = 0; i < 10; i++)
				//{
				//	var clone = JsonConvert.DeserializeObject<Show>(cloneJson);
				//	clone.Save();
				//}
				await Show.Save();
			}
		}

		async public Task EnsureCastAndCrewLoaded(bool force = false)
		{
			if(force || Show.CastAndCrew == null)
			{
				var castCrew = await RunSafe(TraktService.Instance.GetCastAndCrew(Show));
				Show.CastAndCrew = castCrew;
			}
		}

		/// <summary>
		/// Ensures that all the episodes for this season are loaded
		/// </summary>
		async public Task<bool> EnsureSeasonLoaded(int seasonNumber, bool forceRefresh = false)
		{
			var season = Show.SeasonByNumber(seasonNumber);
			bool save = false;

			//Season not available or episodes already loaded
			if((season == null || (season.Episodes != null && season.Episodes.Count > 0)) && !forceRefresh)
				return save;

			if(!forceRefresh)
				await season.EnsureEpisodesLoadedFromCache();

			if(season.Episodes == null || season.Episodes.Count == 0 || forceRefresh)
			{
				try
				{
					var episodes = await RunSafe(TraktService.Instance.GetEpisodesForSeason(Show, season));

					if(episodes == null)
					{
						await season.EnsureEpisodesLoadedFromCache();
						return save;
					}

					if(season.Episodes == null)
						season.Episodes = new List<Episode>();

					await DataService.Instance.Connection.DeleteAllAsync(season.Episodes);
					season.Episodes.Clear();

					//No valid episodes found
					if(episodes.All(e => string.IsNullOrWhiteSpace(e.Title)))
					{
						Show.Seasons.Remove(season);
						await DataService.Instance.Connection.DeleteAsync(season);
						return false;
					}

					foreach(var ep in episodes)
					{
						//Remove invalid and duplicate episodes
						if(string.IsNullOrWhiteSpace(ep.Title) || season.Episodes.Any(e => e.S0E0 == ep.S0E0))
							continue;

						ep.Show = Show;
						season.Episodes.Add(ep);
						save = true;
					}

					foreach(var ep in season.Episodes)
					{
						ep.SeasonID = season.ID;
					}
				}
				catch(Exception e)
				{
					Debug.WriteLine("Error ensuring seasons are loaded: " + e);
				}
			}

			return save;
		}

		async public Task EnsureSeasonsLoadedFromCloud(bool forceRefresh = false)
		{
			var save = false;
			if(forceRefresh || Show.Seasons == null || Show.Seasons.Count == 0)
			{
				Show.PreviousEpisode = null;
				Show.NextEpisode = null;
				var freshSeasons = await RunSafe(TraktService.Instance.GetSeasonsForShow(Show));

				if(freshSeasons == null)
					return;

				if(Show.Seasons != null)
				{
					foreach(var freshSeason in freshSeasons)
					{
						var currentSeason = Show.Seasons.SingleOrDefault(s => s.Number == freshSeason.Number);

						//Add any new seasons
						if(currentSeason == null)
						{
							Show.Seasons.Add(freshSeason);
						}
						else
						{
							//Season is identical
							if(currentSeason.IsEqual(freshSeason))
								continue;

							//Replace current season w/ new one but delete the old
							var index = Show.Seasons.IndexOf(currentSeason);
							var old = Show.Seasons[index];
							await DataService.Instance.Delete(old);
							Show.Seasons[index] = freshSeason;
							save = true;
						}
					}
				}
				else
				{
					Show.Seasons = freshSeasons;
				}
			}

			if(save)
				await Show.Save();
		}

		#endregion

		async public Task EnsureNextEpisodeLoaded(bool save = false, bool forceRefresh = false)
		{
			if(Show.NextEpisode != null && !forceRefresh)
				return;

			if(Show.LatestSeason != null)
			{
				var currentSeason = Show.LatestSeason;
				int count = 0;

				while(Show.NextEpisode == null && currentSeason != null)
				{
					await EnsureSeasonLoaded(currentSeason.Number, forceRefresh);
					await Show.GetNextEpisodeFromCache();

					if(currentSeason.Episodes != null && currentSeason.Episodes.All(e => e.InitialBroadcastDate != null && e.InitialBroadcastDate < DateTime.Today))
						return;

					currentSeason = Show.GetPreviousSeason(currentSeason);
					count++;
				}

				if(Show.NextEpisode != null && save)
					await Show.Save();
			}
		}

		async public Task EnsurePreviousEpisodeLoaded(bool save = false, bool forceRefresh = false)
		{
			if(Show.PreviousEpisode != null && !forceRefresh)
				return;

			if(Show.LatestSeason != null)
			{
				int count = 0;
				var currentSeason = Show.LatestSeason;
				while(Show.PreviousEpisode == null && currentSeason != null)
				{
					await EnsureSeasonLoaded(currentSeason.Number, forceRefresh);
					await Show.GetPreviousEpisodeFromCache();

					currentSeason = Show.GetPreviousSeason(currentSeason);
					count++;
				}

				if(Show.PreviousEpisode != null && save)
					await Show.Save();
			}
		}

		async public Task EnsureAllSeasonsLoaded(bool force = false, Action<Season> onSeasonLoaded = null)
		{
			await EnsureSeasonsLoadedFromCloud(force);
			foreach(var s in Show.SeasonsReversed.ToList())
			{
				if(WasCancelledAndReset)
					return;

				var doSave = await EnsureSeasonLoaded(s.Number, force);

				if(doSave)
					await s.Save();

				onSeasonLoaded?.Invoke(s);
			}
		}
	}
}