using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Foundation;
using HtmlAgilityPack;
using SilverScreen.Shared;

namespace SilverScreen.Mac
{
	public class PirateBaySearchHost : ISearchHost
	{
		const string _searchUrl = "https://pirateproxy.one/search/{0}/0/{1}/0";

		public async Task<List<SearchResult>> SearchForEpisode(DownloadRequest request, CancellationToken token)
		{
			var date = request.BroadcastDate.ToString("yyyy.MM.dd");
			var searchPhrase = "{0} {1}".Fmt(request.SearchPhrase, request.S0E0);
			var searchUrl = _searchUrl.Fmt(searchPhrase, "7") + "&t=" + DateTime.Now.Ticks.ToString();

			var list = await SearchByUrl(searchUrl, token);

			if(list.Count == 0)
			{
				searchPhrase = "{0} {1}".Fmt(request.SearchPhrase, date);
				searchUrl = _searchUrl.Fmt(searchPhrase, "7");
				var inner = await SearchByUrl(searchUrl, token);
				list.AddRange(inner);
			}

			if(list.Count == 0)
			{
				searchPhrase = "{0} {1}".Fmt(request.ShowTitle, request.EpisodeTitle);
				searchUrl = _searchUrl.Fmt(searchPhrase, "7");
				var inner = await SearchByUrl(searchUrl, token);
				list.AddRange(inner);
			}

			return list;
		}

		async Task<List<SearchResult>> SearchByUrl(string url, CancellationToken token)
		{
			var list = new List<SearchResult>();
			url = url.Replace(" ", "%20");

			if(token.IsCancellationRequested)
				return list;

			Debug.WriteLine(url);

			try
			{
				//Using NSUrl* because using HttpClient, even w/ ModernHttpClient, throws encryption error
				var req = new NSMutableUrlRequest(new NSUrl(url));
				var response = await NSUrlConnection.SendRequestAsync(req, NSOperationQueue.MainQueue);
				var content = response.Data.ToString();

				/*
				var client = new HttpClient(new NativeMessageHandler());

				string content;
				var response = await client.GetAsync(new Uri(url), token);

				if(response.StatusCode == System.Net.HttpStatusCode.NotFound)
					return list;

				using(var responseStream = await response.Content.ReadAsStreamAsync())
				using(var streamReader = new StreamReader(responseStream))
				{
					content = streamReader.ReadToEnd();
				}
				*/

				if(string.IsNullOrEmpty(content))
					return list;

				var htmlDoc = new HtmlDocument();
				htmlDoc.LoadHtml(content);
				var nodes = htmlDoc.DocumentNode.SelectNodes("//a[@title='Download this torrent using magnet']");

				if(nodes != null)
				{
					foreach(var node in nodes)
					{
						var seeds = long.Parse(node.ParentNode.NextSibling.NextSibling.InnerText);
						var magnetUrl = node.Attributes["href"].Value;
						var result = new SearchResult
						{
							Url = magnetUrl,
							Seeds = seeds,
						};

						list.Add(result);
					}
				}
			}
			catch(Exception e)
			{
				Debug.WriteLine("Error searching TPB engine: {0}", e);
			}

			return list;
		}
	}
}