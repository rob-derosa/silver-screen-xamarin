using Newtonsoft.Json;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using System;
using System.Threading;
using SilverScreen.Shared;
using System.Diagnostics;
using SQLiteNetExtensions.Attributes;
using SQLite.Net.Attributes;
using SQLiteNetExtensionsAsync.Extensions;

namespace SilverScreen.iOS.Shared
{
	public partial class Show : BaseModel
	{
		Episode _nextEpisode;
		Episode _previousEpisode;
		string _sortTitle;

		#region Custom Properties

		[JsonIgnore]
		[Ignore]
		public string Metadata
		{
			get
			{
				if(Airs == null)
					return string.Empty;
				
				var zone = TimeZoneInfo.FindSystemTimeZoneById(Airs.TimeZone);
				var date = DateTime.Parse(DateTime.Today.ToString("d") + " " + Airs.Time);
				date = TimeZoneInfo.ConvertTimeToUtc(date, zone);
				return "airs {0}s on {1} at {2}".Fmt(Airs.Day, Network, date.ToLocalTime().ToString("t"));
			}
		}

		[JsonIgnore]
		[Ignore]
		public Episode NextEpisode
		{
			get
			{
				return _nextEpisode;
			}
			set
			{
				_nextEpisode = value;
			}
		}

		[JsonIgnore]
		[Ignore]
		public Episode PreviousEpisode
		{
			get
			{
				return _previousEpisode;
			}
			set
			{
				_previousEpisode = value;
			}
		}

		[JsonIgnore]
		[Ignore]
		public Season LatestSeason
		{
			get
			{
				return Seasons.LastOrDefault();
			}
		}

		[JsonIgnore]
		[Ignore]
		public string SortTitle
		{
			get
			{
				if(_sortTitle == null)
				{
					if(Title == null)
						return null;

					_sortTitle = Title.Sterilize();
//					var parts = Title.Split(' ');
//					if(parts.Length > 0 && parts[0].EndsWith("'s"))
//						_sortTitle = _sortTitle.TrimStart(parts[0]).Trim();
				}

				return _sortTitle;
			}
		}

		DateTime? _lastUpdateDate;

		[JsonIgnore]
		[Ignore]
		public DateTime? LastUpdateDate
		{
			get
			{
				if(_lastUpdateDate == null)
				{
					DateTime dtm;
					if(DateTime.TryParse(LastUpdateDateString, out dtm))
					{
						_lastUpdateDate = dtm;
					}
					else
					{
						_lastUpdateDate = null;
					}
				}

				return _lastUpdateDate;
			}
		}

		#endregion

		#region Properties

		string _title;

		[JsonProperty("title")]
		public string Title
		{
			get
			{
				return _title;
			}
			set
			{
				_title = value;

				if(_title != null)
					_title = _title.Trim();

				_sortTitle = null;
			}
		}

		[JsonProperty("year")]
		public int? Year
		{
			get;
			set;
		}

		[JsonIgnore]
		public string IdentifiersBlob
		{
			get; set;
		}

		[TextBlob("IdentifiersBlob")]
		[JsonProperty("ids")]
		public IdentifierSet Identifiers
		{
			get;
			set;
		}

		[JsonProperty("alternate_title")]
		public string AlternateTitle
		{
			get;
			set;
		}

		List<Season> _seasons;

		[OneToMany(CascadeOperations = CascadeOperation.All)]
		[JsonProperty("seasons")]
		public List<Season> Seasons
		{
			get
			{
				return _seasons;
			}
			set
			{
				_seasons = value;

				if(_seasons != null)
				{
					_seasons = _seasons.OrderBy(s => s.Number).ToList();
				}
			}
		}

		[JsonIgnore]
		[Ignore]
		public List<Season> SeasonsReversed
		{
			get
			{
				if(Seasons == null)
					return null;

				return Seasons.OrderByDescending(s => s.Number).ToList(); 
			}
		}

		[JsonProperty("overview")]
		public string Overview
		{
			get;
			set;
		}

		[JsonProperty("first_aired")]
		public string FirstAired
		{
			get;
			set;
		}

		[JsonIgnore]
		public string AirsBlob
		{
			get; set;
		}

		[TextBlob("AirsBlob")]
		[JsonProperty("airs")]
		public Airs Airs
		{
			get;
			set;
		}

		[JsonProperty("runtime")]
		public int? Runtime
		{
			get;
			set;
		}

		[JsonProperty("certification")]
		public string Certification
		{
			get;
			set;
		}

		[JsonProperty("network")]
		public string Network
		{
			get;
			set;
		}

		[JsonProperty("country")]
		public string Country
		{
			get;
			set;
		}

		[JsonProperty("trailer")]
		public string Trailer
		{
			get;
			set;
		}

		[JsonProperty("status")]
		public string Status
		{
			get;
			set;
		}

		[JsonProperty("rating")]
		public double Rating
		{
			get;
			set;
		}

		[JsonProperty("votes")]
		public int Votes
		{
			get;
			set;
		}

		[JsonProperty("updated_at")]
		public string LastUpdateDateString
		{
			get;
			set;
		}

		[JsonProperty("language")]
		public string Language
		{
			get;
			set;
		}

		[JsonIgnore]
		public string GenresBlob
		{
			get; set;
		}

		[TextBlob("GenresBlob")]
		[JsonProperty("genres")]
		public List<string> Genres
		{
			get;
			set;
		}

		[JsonProperty("aired_episodes")]
		public int AiredEpisodes
		{
			get;
			set;
		}

		[JsonIgnore]
		public string ImagesBlob
		{
			get; set;
		}

		[TextBlob("ImagesBlob")]
		[JsonProperty("images")]
		public ImageTypes Images
		{
			get;
			set;
		}

		[JsonIgnore]
		public string CastCrewBlob
		{
			get; set;
		}

		[TextBlob("CastCrewBlob")]
		[JsonProperty("cast_crew")]
		public ShowCast CastAndCrew
		{
			get;
			set;
		}

		#endregion

		#region Previous/Next Episode

		public Season GetPreviousSeason(Season season)
		{
			return Seasons.Where(s => s.Number < season.Number).OrderByDescending(s => s.Number).FirstOrDefault();
		}

		public Season GetNextSeason(Season season)
		{
			return Seasons.Where(s => s.Number > season.Number).OrderByDescending(s => s.Number).FirstOrDefault();
		}

		async public Task GetNextEpisodeFromCache()
		{
			if(_nextEpisode != null)
				return;

			Season currentSeason = LatestSeason;
			while(_nextEpisode == null && currentSeason != null)
			{
				if(currentSeason != null)
				{
					await currentSeason.EnsureEpisodesLoadedFromCache();

					if(currentSeason.Episodes != null)
					{
						_nextEpisode = currentSeason.Episodes.Where(e => e.InitialBroadcastDate != null &&
						                            e.InitialBroadcastDate.Value > DateTime.Now)
						                            .OrderBy(e => e.InitialBroadcastDate)
						                            .OrderBy(e => e.Number).FirstOrDefault();
					}

					if(currentSeason.Episodes.All(e => e.InitialBroadcastDate != null && e.InitialBroadcastDate.Value < DateTime.Today.AddHours(24)))
						return;
				}

				currentSeason = GetPreviousSeason(currentSeason);
			}
		}

		async public Task GetPreviousEpisodeFromCache()
		{
			if(_previousEpisode != null)
				return;

			Season currentSeason = LatestSeason;
			while(_previousEpisode == null && currentSeason != null)
			{
				await currentSeason.EnsureEpisodesLoadedFromCache();

				if(currentSeason.Episodes != null)
				{
					_previousEpisode = currentSeason.Episodes.Where(e => e.InitialBroadcastDate != null &&
					                                e.InitialBroadcastDate.Value < DateTime.Now)
					                                .OrderByDescending(e => e.InitialBroadcastDate)
					                                .OrderByDescending(e => e.Number).FirstOrDefault();
				}

				currentSeason = GetPreviousSeason(currentSeason);
			}
		}



		public override string ToString()
		{
			return Title;
		}

		#endregion

		#region Helpers

		//		public void SetSeasonForNumber(Season newSeason)
		//		{
		//			var oldSeason = SeasonForNumber(newSeason.Number);
		//
		//			if(oldSeason != null)
		//				Seasons.Remove(oldSeason);
		//
		//			Seasons.Add(newSeason);
		//		}

		#endregion

		internal async override Task Save()
		{
			Debug.WriteLine($"Saving {Title}");
			await base.Save();
		}

		internal string GetBestScreenUrlForEpisode(Episode ep)
		{
			var screenUrl = ep.Images.Screenshot.Full;
			screenUrl = screenUrl ?? ep.Images.Screenshot.Medium;
			screenUrl = screenUrl ?? ep.Images.Screenshot.Thumb;
			return screenUrl;
		}

		internal string GetBestFanArt()
		{
			var screenUrl = Images.FanArt.Medium;
			screenUrl = screenUrl ?? Images.FanArt.Full;
			screenUrl = screenUrl ?? Images.FanArt.Thumb;
			return screenUrl;
		}

		public Season SeasonByNumber(int number)
		{
			return Seasons.FirstOrDefault(s => s.Number == number);
		}
	}

	#region Airs

	public class Airs : BaseModel
	{
		[JsonProperty("day")]
		public string Day
		{
			get;
			set;
		}

		[JsonProperty("time")]
		public string Time
		{
			get;
			set;
		}

		[JsonProperty("timezone")]
		public string TimeZone
		{
			get;
			set;
		}
	}

	#endregion

	#region ImageSet

	public partial class ImageSet : BaseModel
	{
		[JsonProperty("full")]
		public string Full
		{
			get;
			set;
		}

		[JsonProperty("medium")]
		public string Medium
		{
			get;
			set;
		}

		[JsonProperty("thumb")]
		public string Thumb
		{
			get;
			set;
		}

		public override bool IsEqual(BaseModel other)
		{
			if(other == null)
				return false;

			var img = other as ImageSet;

			if(img == null)
				return false;
			
			return Full == img.Full && Medium == img.Medium && Thumb == img.Thumb;
		}
	}

	#endregion

	#region ImageTypes

	public class ImageTypes : BaseModel
	{
		[JsonProperty("fanart")]
		[Ignore]
		public ImageSet FanArt
		{
			get;
			set;
		}

		[JsonIgnore]
		[Ignore]
		public int FanArtId
		{
			get;set;
		}

		[JsonProperty("poster")]
		[Ignore]
		public ImageSet Poster
		{
			get;
			set;
		}

		[JsonIgnore]
		[Ignore]
		public int PosterId
		{
			get;
			set;
		}

		[JsonProperty("logo")]
		[Ignore]
		public ImageSet Logo
		{
			get;
			set;
		}

		[JsonIgnore]
		[Ignore]
		public int LogoId
		{
			get; set;
		}

		[JsonProperty("clearart")]
		[Ignore]
		public ImageSet ClearArt
		{
			get;
			set;
		}

		[JsonIgnore]
		[Ignore]
		public int ClearArtId
		{
			get; set;
		}

		[JsonProperty("banner")]
		[Ignore]
		public ImageSet Banner
		{
			get;
			set;
		}

		[JsonIgnore]
		[Ignore]
		public int BannerId
		{
			get; set;
		}

		[JsonProperty("thumb")]
		public ImageSet Thumb
		{
			get;
			set;
		}


		[JsonProperty("screenshot")]
		[Ignore]
		public ImageSet Screenshot
		{
			get;
			set;
		}

		[JsonIgnore]
		[Ignore]
		public int ScreenshotId
		{
			get; set;
		}

		public override bool IsEqual(BaseModel other)
		{
			if(other == null)
				return false;

			var img = other as ImageTypes;

			if(img == null)
				return false;

			return FanArt.IsEqual(img.FanArt) && Poster.IsEqual(img.Poster) && Logo.IsEqual(img.Logo) && ClearArt.IsEqual(img.ClearArt)
			&& Banner.IsEqual(img.Banner) && Thumb.IsEqual(img.Thumb) && Screenshot.IsEqual(img.Screenshot);
		}
	}

	#endregion

	#region ShowUpdate

	public class ShowUpdate
	{
		DateTime? _lastUpdateDate;

		[JsonIgnore]
		public DateTime? LastUpdateDate
		{
			get
			{
				if(_lastUpdateDate == null)
				{
					DateTime dtm;
					if(DateTime.TryParse(LastUpdateDateString, out dtm))
					{
						_lastUpdateDate = dtm;
					}
					else
					{
						_lastUpdateDate = null;
					}
				}

				return _lastUpdateDate;
			}
		}

		[JsonProperty("updated_at")]
		public string LastUpdateDateString
		{
			get;
			set;
		}

		[JsonProperty("show")]
		public Show Show
		{
			get;
			set;
		}
	}

	#endregion
}