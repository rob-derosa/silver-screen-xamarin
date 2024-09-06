using System;

namespace SilverScreen.iOS.Shared
{
	public class DependencyContainer
	{
		public static void Initialize()
		{
			ServiceContainer.Register<ShowListViewModel>();
			ServiceContainer.Register<ShowDetailsViewModel>();
			ServiceContainer.Register<EpisodeDetailsViewModel>();
			ServiceContainer.Register<DownloadListViewModel>();
			ServiceContainer.Register<AddShowSearchViewModel>();
			ServiceContainer.Register<SettingsViewModel>();
			ServiceContainer.Register<AuthenticationViewModel>();
			ServiceContainer.Register<BaseViewModel>();
		}
	}
}