using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Diagnostics;
using SilverScreen.Shared;

namespace SilverScreen.iOS.Shared
{
	public class AddShowSearchViewModel : BaseViewModel
	{
		string _previousQuery = string.Empty;

		public Show SelectedShow
		{
			get;
			set;
		}

		public string SearchQuery
		{
			get;
			set;
		}

		public List<int> ExistingShowIds
		{
			get;
			set;
		}

		public List<Show> SearchResults
		{
			get;
			set;
		}

		public AddShowSearchViewModel()
		{
			SearchResults = new List<Show>();
		}

		public async Task SearchShows()
		{
			if(SearchQuery == null)
				return;
			
			//if(SearchQuery.Trim() == _previousQuery.Trim())
			//	return;

			if(string.IsNullOrWhiteSpace(SearchQuery))
				return;

			_previousQuery = SearchQuery;
			var results = await RunSafe(TraktService.Instance.SearchShows(SearchQuery));

			if(_previousQuery != SearchQuery || results == null)
				return;
		
			foreach(var s in results.ToList())
			{
				if(ExistingShowIds.Contains(s.Show.Identifiers.Trakt))
					results.Remove(s);
			}

			//var withoutImgs = new List<SearchResult>();
			//foreach(var r in results.ToList())
			//{
			//	if(string.IsNullOrWhiteSpace(r.Show.Images?.Poster?.Thumb))
			//	{
			//		withoutImgs.Add(r);
			//		results.Remove(r);
			//	}
			//}

			//results.AddRange(withoutImgs);

			_previousQuery = SearchQuery;
			SearchResults = results.Select(r => r.Show).ToList();
		}
	}
}

