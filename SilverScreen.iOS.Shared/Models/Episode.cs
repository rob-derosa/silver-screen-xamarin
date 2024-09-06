using System;
using Newtonsoft.Json;
using SilverScreen.Shared;
using SQLiteNetExtensions.Attributes;
using SQLite.Net.Attributes;

namespace SilverScreen.iOS.Shared
{
	public partial class Episode : BaseModel
	{
		#region Properties

		[ForeignKey(typeof(Show))]
		public int ShowID
		{
			get;set;
		}

		[ManyToOne]
		public Show Show
		{
			get;set;
		}

		DateTime? _initialBroadcastDate;

		[JsonIgnore]
		public DateTime? InitialBroadcastDate
		{
			get
			{
				if(_initialBroadcastDate == null)
				{
					DateTime dtm;
					if(DateTime.TryParse(InitialBroadcastDateString, out dtm))
					{
						_initialBroadcastDate = dtm;
					}
					else
					{
						_initialBroadcastDate = null;
					}
				}

				return _initialBroadcastDate;
			}
			set
			{
				_initialBroadcastDate = value;
			}
		}

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
			set
			{
				_lastUpdateDate = value;
			}
		}

		[Ignore]
		[JsonIgnore]
		public string FormattedBroadcastDate
		{
			get
			{
				if(!InitialBroadcastDate.HasValue)
					return string.Empty;

				return InitialBroadcastDate.Value.ToString("dddd, MMMM d, yyyy \\a\\t h:mm tt");
			}
		}

		[JsonIgnore]
		[Ignore]
		public string S0E0
		{
			get
			{
				return "S{0}E{1}".Fmt(SeasonNumber.ToString("00"), Number.ToString("00"));
			}
		}

		[JsonProperty("seasonID")]
		public string SeasonTraktID
		{
			get;
			set;
		}

		[ForeignKey(typeof(Season))]
		public int SeasonID
		{
			get;
			set;
		}

		[ManyToOne]
		[JsonIgnore]
		public Season Season
		{
			get;set;
		}

		[JsonProperty("season")]
		public int SeasonNumber
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
			}
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

		[JsonProperty("overview")]
		public string Overview
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

		[JsonProperty("first_aired")]
		[Ignore]
		public string InitialBroadcastDateString
		{
			get;
			set;
		}

		[JsonProperty("updated_at")]
		[Ignore]
		public string LastUpdateDateString
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

		[JsonProperty("ignore_not_local")]
		public bool IgnoreNotLocal
		{
			get;
			set;
		}

		#endregion

		public override bool IsEqual(BaseModel other)
		{
			if(other == null)
				return false;

			var ep = other as Episode;

			if(ep == null)
				return false;

			return Title == ep.Title && S0E0 == ep.S0E0 && InitialBroadcastDate == ep.InitialBroadcastDate
			&& Number == ep.Number && Overview == ep.Overview && ep.Images.IsEqual(ep.Images);
		}

		public override string ToString()
		{
			return "{0} : {1}".Fmt(Title, S0E0);
		}
	}
}