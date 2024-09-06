using System;
using System.Threading.Tasks;

namespace SilverScreen.iOS.Shared
{
	public interface IAuthenticationHandler
	{
		Task AuthenticateUser();
	}
}