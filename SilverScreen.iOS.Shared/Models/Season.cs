using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Linq;
using SilverScreen.Shared;
using System.Threading.Tasks;
using SQLite.Net.Attributes;
using SQLiteNetExtensions.Attributes;

namespace SilverScreen.iOS.Shared
{
	public class Season : BaseModel
	{
		[JsonIgnore]
		[Ignore]
		public List<Episode> EpisodesReversed
		{
			get
			{
				if(Episodes == null)
					return null;

				return Episodes.OrderByDescending(s => s.Number).ToList();
			}
		}

		//[JsonIgnore]
		[OneToMany(CascadeOperations = CascadeOperation.All)]
		public List<Episode> Episodes
		{
			get;
			set;
		}

		[ForeignKey(typeof(Show))]
		public int ShowID
		{
			get;
			set;
		}

		[JsonProperty("number")]
		public int Number
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

		[JsonProperty("episode_count")]
		public int EpisodeCount
		{
			get;
			set;
		}

		[JsonProperty("overview")]
		public string Overview
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

		async public Task EnsureEpisodesLoadedFromCache()
		{
			if(Episodes != null && Episodes.Count > 0)
				return;

			if(Episodes == null || Episodes.Count == 0)
				await LoadChildren();

			if(Episodes == null)
				Episodes = new List<Episode>();
		}

		public override string ToString()
		{
			return Number.ToString();
		}

		public override bool IsEqual(BaseModel other)
		{
			if(other == null)
				return false;

			var s = other as Season;

			if(s == null || Number != s.Number || Episodes == null || Episodes.Count == 0 ||
			   EpisodeCount != s.EpisodeCount || Episodes.Count != s.EpisodeCount)
				return false;

			//At this point, everything seems to match - let's check the broadcast date to make sure it's old enough to ignore
			var reversed = Episodes.ToList();
			reversed.Reverse();

			var latestReportedEp = reversed.LastOrDefault();
			if(latestReportedEp == null || latestReportedEp.InitialBroadcastDate == null)
				return false;

			var diff = DateTime.UtcNow.Subtract(latestReportedEp.InitialBroadcastDate.Value);
			return diff.TotalDays > 30;
		}

		public Episode EpisodeByNumber(int number)
		{
			return Episodes.FirstOrDefault(e => e.Number == number);
		}
	}
}