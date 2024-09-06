using System;
using System.Threading.Tasks;
using SilverScreen.iOS.Shared;

namespace SilverScreen.Shared
{
	public partial class BaseModel
	{
		internal async virtual Task Save()
		{
			await DataService.Instance.Save(this);
		}

		internal async virtual Task LoadChildren()
		{
			await DataService.Instance.LoadChildren(this);
		}
	}
}