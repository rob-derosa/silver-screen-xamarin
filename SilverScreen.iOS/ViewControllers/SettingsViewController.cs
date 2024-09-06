using System;
using UIKit;
using SilverScreen.iOS.Shared;
using MonoTouch.Dialog;

namespace SilverScreen.iOS
{
	public class SettingsViewController : DialogViewController
	{
		public SettingsViewController() : base(null)
		{
		}

		public Action OnSettingsSaved
		{
			get;
			set;
		}

		public Action OnSyncClicked
		{
			get;
			set;
		}

		public Action OnRefreshClicked
		{
			get;
			set;
		}

		public Action OnLogOutClicked
		{
			get;
			set;
		}

		public override void ViewDidLoad()
		{
			var syncTrakt = new StringElement("Sync with Trakt", () =>
			{
				if(OnSyncClicked != null)
					OnSyncClicked();
			});
			syncTrakt.Alignment = UITextAlignment.Center;

			var clearAllShows = new StringElement("Log Out of Trakt", () =>
			{
				var alert = new UIAlertView("Are you sure?", "This will clear your library on this device - are you sure?\n\n(does not affect your watchlist?", null, "No", "Yes");
				alert.Clicked += (sender, e) =>
				{
					if(e.ButtonIndex == 1)
					{
						if(OnLogOutClicked != null)
							OnLogOutClicked();
					}
				};
				alert.Show();
			});
			clearAllShows.Alignment = UITextAlignment.Center;

			var refreshShows = new StringElement("Refresh Library", () =>
			{
				if(OnRefreshClicked != null)
					OnRefreshClicked();
			});
			refreshShows.Alignment = UITextAlignment.Center;


			var seriesSection = new Section("Series Library") {
				syncTrakt,
			};

			if(Settings.Instance.TraktUsername != null)
			{
				seriesSection.Insert(1, new Element[] {
					clearAllShows
				});
			}

			var root = new RootElement("Settings") {
					seriesSection
			};

			this.Root = root;

			NavigationItem.SetRightBarButtonItem(new UIBarButtonItem(UIBarButtonSystemItem.Done, (s, e) =>
			{
				ParentViewController.DismissViewControllerAsync(true);
			}), false);
		}

	}
}

