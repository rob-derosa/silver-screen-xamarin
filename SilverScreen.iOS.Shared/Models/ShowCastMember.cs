using System.Collections.Generic;
using Newtonsoft.Json;
using SQLiteNetExtensions.Attributes;
using SilverScreen.Shared;

namespace SilverScreen.iOS.Shared
{
	public class Person : BaseModel
	{
		[JsonProperty("name")]
		public string Name
		{
			get;
			set;
		}

		[JsonProperty("ids")]
		[OneToOne(CascadeOperations = CascadeOperation.All)]
		public IdentifierSet Identifiers
		{
			get; set;
		}

		[JsonProperty("images")]
		[OneToOne(CascadeOperations = CascadeOperation.All)]
		public PersonImages Images
		{
			get;
			set;
		}
	}

	public class PersonImages : BaseModel
	{
		[JsonProperty("headshot")]
		[OneToOne(CascadeOperations = CascadeOperation.All)]
		public ImageSet Headshot
		{
			get;
			set;
		}

		[JsonProperty("fanart")]
		[OneToOne(CascadeOperations = CascadeOperation.All)]
		public ImageSet Fanart
		{
			get;set;
		}
	}

	public class CastMember : BaseModel
	{
		[OneToOne(CascadeOperations = CascadeOperation.All)]
		[JsonProperty("character")]
		public string Character
		{
			get; set;
		}

		[OneToOne(CascadeOperations = CascadeOperation.All)]
		[JsonProperty("person")]
		public Person Person
		{
			get; set;
		}
	}

	public class Production : BaseModel
	{
		[JsonProperty("job")]
		public string Job
		{
			get; set;
		}

		[JsonProperty("person")]
		[OneToOne(CascadeOperations = CascadeOperation.All)]
		public Person Person
		{
			get; set;
		}
	}

	public class CrewMember : BaseModel
	{
		[JsonProperty("production")]
		[OneToMany(CascadeOperations = CascadeOperation.All)]
		public IList<Production> Production
		{
			get; set;
		} = new List<Production>();
	}

	public class ShowCast : BaseModel
	{
		[JsonProperty("cast")]
		[OneToMany(CascadeOperations = CascadeOperation.All)]
		public IList<CastMember> Cast
		{
			get; set;
		} = new List<CastMember>();

		[JsonProperty("crew")]
		[OneToOne(CascadeOperations = CascadeOperation.All)]
		public CrewMember Crew
		{
			get; set;
		}
	}
}