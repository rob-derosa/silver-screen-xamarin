using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using SilverScreen.Shared;

namespace SilverScreen.iOS.Shared
{
	public class ShowRating : BaseModel
	{
		[JsonProperty("rated_at")]
		public string RatedAt
		{
			get;
			set;
		}

		[JsonProperty("rating")]
		public int Rating
		{
			get;
			set;
		}

		public Show Show
		{
			get;
			set;
		}
	}

	public class RatedShowRoot
	{
		[JsonProperty("shows")]
		public List<RatedShow> Shows
		{
			get;
			set;
		} = new List<RatedShow>();
	}

	public class RatedShow
	{
		[JsonProperty("rated_at")]
		public string RatedAt
		{
			get;
			set;
		}

		[JsonProperty("rating")]
		public int Rating
		{
			get;
			set;
		}

		[JsonProperty("year")]
		public int Year
		{
			get;
			set;
		}

		[JsonProperty("title")]
		public string Title
		{
			get;
			set;
		}

		[JsonProperty("ids")]
		public IdentifierSet Identifiers
		{
			get;
			set;
		}
	}

	internal class Ids
    {
        [JsonProperty("trakt")]
        public int Trakt { get; set; }
    }

	internal class FavoriteShow
    {
        [JsonProperty("ids")]
        public Ids Ids { get; set; }
	}

    internal class FavoriteList
    {
		[JsonProperty("shows")]
		public List<FavoriteShow> Shows { get; set; } = new List<FavoriteShow>();
	}
}