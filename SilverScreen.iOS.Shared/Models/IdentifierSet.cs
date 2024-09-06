using System;
using Newtonsoft.Json;
using SilverScreen.Shared;

namespace SilverScreen.iOS.Shared
{
	public class IdentifierSet : BaseModel
	{
		[JsonProperty("trakt")]
		public int Trakt
		{
			get;
			set;
		}

		[JsonProperty("slug")]
		public string Slug
		{
			get;
			set;
		}

		[JsonProperty("tvdb")]
		public int? TVDB
		{
			get;
			set;
		}

		[JsonProperty("imdb")]
		public string IMDB
		{
			get;
			set;
		}

		[JsonProperty("tmdb")]
		public int? TMDB
		{
			get;
			set;
		}

		[JsonProperty("tvrage")]
		public int? TVRage
		{
			get;
			set;
		}
	}
}