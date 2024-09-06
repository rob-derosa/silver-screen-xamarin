using System;
#if !__TODAY__
using SQLite.Net.Attributes;
#endif
namespace SilverScreen.Shared
{
	public partial class BaseModel
	{
		#if !__TODAY__
		[PrimaryKey, AutoIncrement]
		#endif
		public int ID
		{
			get;
			set;
		}

		public virtual bool IsEqual(BaseModel other)
		{
			return false;
		}
	}
}