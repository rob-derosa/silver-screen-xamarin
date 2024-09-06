using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Xml;
using System.Threading.Tasks;
using SilverScreen.Shared;
using System.IO.Compression;
using System.IO;
using System.Threading;
using System.Diagnostics;

namespace SilverScreen.Mac
{
	public class KickassSearchHost : ISearchHost
	{
		readonly string _rssUrl = "https://kat.cr/usearch/{0} category:tv/?rss=1&field=seeders&sorder=desc";

		public async Task<List<SearchResult>> SearchForEpisode(DownloadRequest request, CancellationToken token)
		{
			var encoded = Uri.EscapeDataString("{0} {1}".Fmt(request.SearchPhrase, request.S0E0));
			var searchUrl = _rssUrl.Fmt(encoded) + "&t=" + DateTime.Now.Ticks.ToString();
			var list = await SearchByUrl(searchUrl, token);

			if(list.Count == 0)
			{
				searchUrl = _rssUrl.Fmt("{0} {1}".Fmt(request.SearchPhrase, request.BroadcastDate.ToString("yyyy MM dd")));
				var result = await SearchByUrl(searchUrl, token);
				list.AddRange(result);
			}

			if(list.Count == 0)
			{
				searchUrl = _rssUrl.Fmt("{0} {1}".Fmt(request.ShowTitle, request.EpisodeTitle));
				var result = await SearchByUrl(searchUrl, token);
				list.AddRange(result);
			}
				
			return list;
		}

		async Task<List<SearchResult>> SearchByUrl(string url, CancellationToken token)
		{
			var list = new List<SearchResult>();
			if(token.IsCancellationRequested)
				return list;

			Debug.WriteLine(url);
			try
			{
				var client = new HttpClient();
				client.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Encoding", "gzip, deflate");

				string content;
				var response = await client.GetAsync(new Uri(url), token);

				if(response.StatusCode == System.Net.HttpStatusCode.NotFound)
					return list;
				
				using(var responseStream = await response.Content.ReadAsStreamAsync())
				using(var decompressedStream = new GZipStream(responseStream, CompressionMode.Decompress))
				using(var streamReader = new StreamReader(decompressedStream))
				{
					content = streamReader.ReadToEnd();
				}

				var xmlDoc = new XmlDocument();

				content = content.Replace("torrent:", "torrent-");
				xmlDoc.LoadXml(content);

				//foreach(XmlNode node in xmlDoc.SelectNodes("//*[name()='torrent:magnetURI']"))
				foreach(XmlNode node in xmlDoc.DocumentElement.SelectNodes("channel/item"))
				{
					var magnetUrl = node.SelectSingleNode("torrent-magnetURI").InnerText;
					Debug.WriteLine(magnetUrl);
					var seeds = long.Parse(node.SelectSingleNode("torrent-seeds").InnerText);	
					var date = DateTime.Parse(node.SelectSingleNode("pubDate").InnerText);

					var result = new SearchResult {
						Url = magnetUrl,
						Seeds = seeds,
						PublishDate = date
					};

					list.Add(result);
				}
			}
			catch(Exception e)
			{
				Debug.WriteLine("Error searching KA engine: {0}", e);
			}

			return list;
		}
	}
}