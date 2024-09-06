using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SilverScreen.Shared;

namespace SilverScreen.iOS.Shared
{
	public class TraktService
	{
		readonly string _traktClientID = "76e54446be89af2fe22ba0cebb9d1a0548c59e59c98f4cbe74a07b8b6c964906";
		readonly string _traktClientSecret = "3081ae1038d59747ed364ca6374acd2f781b20ca4b7865dcddf234cb766f261a";
		HttpClient _client;

		Dictionary<string, DateTime> _history = new Dictionary<string, DateTime>();

		public bool ForceRequests
		{
			get;
			set;
		}

		public string RedirectUrl
		{
			get;
			private set;
		}

		public string TraktAuthUrl
		{
			get;
			private set;
		}

		static TraktService _instance;

		public static TraktService Instance
		{
			get
			{
				if(_instance == null)
				{
					_instance = new TraktService {
						ForceRequests = false,
					};
					_instance.RedirectUrl = "https://www.google.com/";
					_instance.TraktAuthUrl = "https://trakt.tv/oauth/authorize?response_type=code&client_id={0}&redirect_uri={1}".Fmt(_instance._traktClientID, _instance.RedirectUrl);
				}
				return _instance;
			}
		}

		public void Reset()
		{
			_client = null;
		}

		async Task<T> GetContent<T>(string path, bool cache = true, bool firstAttempt = true, Dictionary<string, object> keys = null)
		{
			//if(!ForceRequests && _history.ContainsKey(path))
			//{
			//	var dtm = _history[path];
			//	if(DateTime.Now.Subtract(dtm).TotalHours < 1)
			//		return default(T);
			//}

			if(_client == null)
			{
				_client = new HttpClient();
				_client.DefaultRequestHeaders.TryAddWithoutValidation("trakt-api-version", "2");
				_client.DefaultRequestHeaders.TryAddWithoutValidation("trakt-api-key", _traktClientID);
				_client.DefaultRequestHeaders.TryAddWithoutValidation("authorization", "Bearer {0}".Fmt(Settings.Instance.AuthToken));

				if(keys != null)
				{
					foreach(var kvp in keys)
						_client.DefaultRequestHeaders.TryAddWithoutValidation(kvp.Key, kvp.Value.ToString());
				}
			}

			if(!path.Contains("?"))
				path += "?a=b";

			var url = "https://api-v2launch.trakt.tv/{0}".Fmt(path);// + "&t=" + DateTime.Now.Ticks.ToString();
			Debug.WriteLine("Requesting JSON from {0}".Fmt(url));

			string json;
			using(var response = await _client.GetAsync(url))
			{
				json = await response.Content.ReadAsStringAsync();

				if(response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
				{
					Settings.Instance.AuthToken = null;
					await Settings.Instance.Save();

					if(Settings.Instance.RefreshToken != null)
					{
						//Token has expired, need to refresh
						//TODO Refresh here
					}

					if(Settings.Instance.AuthToken == null && firstAttempt)
					{
						_client = null;						
						var auth = ServiceContainer.Resolve<IAuthenticationHandler>();
						if(auth != null)
						{
							await auth.AuthenticateUser();

							if(Settings.Instance.AuthToken != null)
							{
								return await GetContent<T>(path, cache, false);
							}
						}
					}
				}

				if(response.StatusCode != System.Net.HttpStatusCode.OK || string.IsNullOrWhiteSpace(json))
				{
					Debug.WriteLine("Invalid response from server:\n{0}", json);
					throw new HttpRequestException("Unable to get valid JSON from Trakt server");
				}
			}

			if(!_history.ContainsKey(path) && cache)
				_history.Add(path, DateTime.Now);

			Debug.WriteLine(json);
			return JsonConvert.DeserializeObject<T>(json);
		}

		public Task<OAuthToken> GetTokenForCode(string code)
		{
			return new Task<OAuthToken>(() =>
			{
				//var handler = new NativeMessageHandler() {
				//	DisableCaching = true,
				//	Proxy = CoreFoundation.CFNetwork.GetDefaultProxy(),
				//	UseProxy = true,
				//};

				using(var client = new HttpClient())
				{
					client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
					var authCode = new OAuthCode {
						Code = code,
						ClientId = _traktClientID,
						ClientSecret = _traktClientSecret,
						RedirectUri = TraktService.Instance.RedirectUrl,
					};
					var codeJson = JsonConvert.SerializeObject(authCode);
					var content = new StringContent(codeJson, Encoding.UTF8, "application/json");
					var response = client.PostAsync("https://api-v2launch.trakt.tv/oauth/token", content).Result;
					var body = response.Content.ReadAsStringAsync().Result;

					var token = JsonConvert.DeserializeObject<OAuthToken>(body);
					return token;
				}
			});
		}


		public void ClearHistory(string keyFilter = null)
		{
			if(keyFilter != null)
			{
				_history.Keys.Where(k => k.ContainsNoCase(keyFilter)).ToList().ForEach(k => _history.Remove(k));
			}
			else
			{
				_history.Clear();
			}
		}

		public Task<Show> GetShow(int traktId)
		{
			return new Task<Show>(() => {
				var show = GetContent<Show>($"shows/{traktId}?extended=full,images").Result;
				return show;
			});
		}

		public Task<List<SearchResult>> SearchShows(string query)
		{
			return new Task<List<SearchResult>>(() =>
			{
				var keys = new Dictionary<string, object>();
				keys.Add("X-Pagination-Page:", 1);
				keys.Add("X-Pagination-Limit", 100);
				var list = GetContent<List<SearchResult>>("search?query={0}&type=show".Fmt(query), false, true, keys).Result;
				return list.OrderByDescending(r => r.Score).ToList();
			});
		}

		public Task<List<Season>> GetSeasonsForShow(Show show)
		{
			return new Task<List<Season>>(() =>
			{
				var list = GetContent<List<Season>>("shows/{0}/seasons?extended=full,images".Fmt(show.Identifiers.Trakt)).Result;

				if(list == null)
					return null;

				list = list.OrderBy(s => s.Number).ToList();
				return list;
			});
		}

		public Task<List<Episode>> GetEpisodesForSeason(Show show, Season season)
		{
			return new Task<List<Episode>>(() =>
			{
				var list = GetContent<List<Episode>>("shows/{0}/seasons/{1}?extended=full,images".Fmt(show.Identifiers.Trakt, season.Number)).Result;

				if(list == null)
					return null;

				list = list.OrderBy(e => e.InitialBroadcastDate).ToList();
				return list;
			});
		}

		public Task<ShowCast> GetCastAndCrew(Show show)
		{
			return new Task<ShowCast>(() => {
				var cast = GetContent<ShowCast>($"shows/{show.Identifiers.Trakt}/people?extended=images").Result;
				return cast;
			});
		}

		public Task<List<Show>> GetUpdatedShowsSince(DateTime time)
		{
			return new Task<List<Show>>(() =>
			{
				var list = GetContent<List<ShowUpdate>>("shows/updates/{0}/?limit=1000000".Fmt(time.ToString("O"))).Result;

				if(list == null)
					return null;

				var shows = list.OrderBy(s => s.Show.SortTitle).Select(s => s.Show).ToList();

				Debug.WriteLine("Count: " + list.Count);
				return shows;
			});
		}

		public Task<UserProfile> GetUserProfile()
		{
			return new Task<UserProfile>(() => {
				var profile = GetContent<UserProfile>("users/me").Result;
				return profile;
			});
		}

		public Task<List<ShowRating>> GetFavoriteShows()
		{
			return new Task<List<ShowRating>>(() => {
				var list = GetContent<List<ShowRating>>("sync/watchlist/shows?extended=full,images".Fmt(Settings.Instance.TraktUsername)).Result;
				return list;
			});
		}

		public Task AddShowToFavorites(Show show)
		{
			return new Task(() => {
				var client = new HttpClient();
				client.DefaultRequestHeaders.TryAddWithoutValidation("trakt-api-version", "2");
				client.DefaultRequestHeaders.TryAddWithoutValidation("trakt-api-key", _traktClientID);
				client.DefaultRequestHeaders.TryAddWithoutValidation("authorization", "Bearer {0}".Fmt(Settings.Instance.AuthToken));

				var favorite = new FavoriteShow
				{
					Ids = new Ids { Trakt = show.Identifiers.Trakt }
				};

				var root = new FavoriteList();
				root.Shows.Add(favorite);

				var codeJson = JsonConvert.SerializeObject(root);
				var content = new StringContent(codeJson, Encoding.UTF8, "application/json");
				var response = client.PostAsync($"https://api-v2launch.trakt.tv/sync/watchlist", content).Result;
				var body = response.Content.ReadAsStringAsync().Result;

				Console.WriteLine(body);
			});
		}

		public Task RemoveShowFromFavorites(Show show)
		{
			return new Task(() => {
				var client = new HttpClient();
				client.DefaultRequestHeaders.TryAddWithoutValidation("trakt-api-version", "2");
				client.DefaultRequestHeaders.TryAddWithoutValidation("trakt-api-key", _traktClientID);
				client.DefaultRequestHeaders.TryAddWithoutValidation("authorization", "Bearer {0}".Fmt(Settings.Instance.AuthToken));

				var favorite = new FavoriteShow
				{
					Ids = new Ids { Trakt = show.Identifiers.Trakt }
				};

				var root = new FavoriteList();
				root.Shows.Add(favorite);

				var codeJson = JsonConvert.SerializeObject(root);
				var content = new StringContent(codeJson, Encoding.UTF8, "application/json");
				var response = client.PostAsync($"https://api-v2launch.trakt.tv/sync/watchlist/remove", content).Result;
				var body = response.Content.ReadAsStringAsync().Result;

				Console.WriteLine(body);
			});
		}

		public Task RateShow(Show show, int rating)
		{
			return new Task(() => {
				var client = new HttpClient();
				client.DefaultRequestHeaders.TryAddWithoutValidation("trakt-api-version", "2");
				client.DefaultRequestHeaders.TryAddWithoutValidation("trakt-api-key", _traktClientID);
				client.DefaultRequestHeaders.TryAddWithoutValidation("authorization", "Bearer {0}".Fmt(Settings.Instance.AuthToken));

				var showRating = new RatedShow
				{
					Title = show.Title,
					Year = show.Year.Value,
					Identifiers = show.Identifiers,
					Rating = rating,
				};

				var root = new RatedShowRoot();
				root.Shows.Add(showRating);

				var codeJson = JsonConvert.SerializeObject(root);
				var content = new StringContent(codeJson, Encoding.UTF8, "application/json");
				var response = client.PostAsync("https://api-v2launch.trakt.tv/sync/ratings", content).Result;
				var body = response.Content.ReadAsStringAsync().Result;

				Console.WriteLine(body);
			});
		}
	}
}