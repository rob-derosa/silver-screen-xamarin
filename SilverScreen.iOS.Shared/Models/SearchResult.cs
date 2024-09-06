using System;
using Newtonsoft.Json;

namespace SilverScreen.iOS.Shared
{
	public class SearchResult
	{
		[JsonProperty("type")]
		public string Type
		{
			get;
			set;
		}

		[JsonProperty("score")]
		public double Score
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
}

