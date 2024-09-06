using System;
using Newtonsoft.Json;

namespace SilverScreen.iOS.Shared
{
	public class OAuthCode
	{
		[JsonProperty("code")]
		public string Code
		{
			get;
			set;
		}

		[JsonProperty("client_id")]
		public string ClientId
		{
			get;
			set;
		}

		[JsonProperty("client_secret")]
		public string ClientSecret
		{
			get;
			set;
		}

		[JsonProperty("redirect_uri")]
		public string RedirectUri
		{
			get;
			set;
		}

		[JsonProperty("grant_type")]
		public string GrantType
		{
			get;
		} = "authorization_code";
	}

	public class OAuthToken
	{

		[JsonProperty("access_token")]
		public string AccessToken
		{
			get;
			set;
		}

		[JsonProperty("token_type")]
		public string TokenType
		{
			get;
			set;
		}

		[JsonProperty("expires_in")]
		public int ExpiresIn
		{
			get;
			set;
		}

		[JsonProperty("refresh_token")]
		public string RefreshToken
		{
			get;
			set;
		}

		[JsonProperty("scope")]
		public string Scope
		{
			get;
			set;
		}

		[JsonProperty("created_at")]
		public int CreatedAt
		{
			get;
			set;
		}
	}


}