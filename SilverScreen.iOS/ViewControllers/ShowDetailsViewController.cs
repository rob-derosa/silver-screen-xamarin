using System;
using System.Threading.Tasks;
using Foundation;
using SDWebImage;
using UIKit;
using SilverScreen.iOS.Shared;
using SilverScreen.Shared;

namespace SilverScreen.iOS
{
	public class ShowDetailsViewController : BaseViewController<ShowDetailsViewModel>
	{
		#region Fields

		UILabel _titleLabel;
		UILabel _metaLabel;
		UITextView _descriptionLabel;
		UIImageView _backgroundImage;
		ShowSeasonsHorizontalViewController _seasonsView;
		ShowCastHorizontalViewController _castView;
		UIButton _settingsButton;
		UIButton _doneButton;
		UIView _blurView;
		UISegmentedControl _segment;

		#endregion

		public Action<Show> ShowRemoved
		{
			get;
			set;
		}

		protected override async void LayoutInterface()
		{
			View.BackgroundColor = UIColor.FromRGB(33, 33, 33);
			var insets = new UIEdgeInsets(20, 20, 20, 20);

			_titleLabel = new UILabel() {
				Font = UIFont.FromName("HelveticaNeue-UltraLight", 64f),
				TextColor = UIColor.FromRGBA(255, 255, 255, 225),
				AdjustsFontSizeToFitWidth = true,
			};

			_metaLabel = new UILabel
			{
				Font = UIFont.FromName("HelveticaNeue-Light", 11f),
				TextColor = UIColor.FromRGBA(255, 255, 255, 225),
				AdjustsFontSizeToFitWidth = true,
			};

			_descriptionLabel = new UITextView() {
				Font = UIFont.FromName("HelveticaNeue-Light", 14f),
				TextColor = UIColor.FromRGBA(255, 255, 255, 200),
				BackgroundColor = UIColor.Clear,
				Selectable = false,
				UserInteractionEnabled = false,
			};

			_descriptionLabel.TextContainer.MaximumNumberOfLines = 3;
			_descriptionLabel.TextContainer.LineBreakMode = UILineBreakMode.TailTruncation;

			_settingsButton = new UIButton(UIButtonType.Custom);
			_settingsButton.SetImage(UIImage.FromBundle("Images/settings_icon"), UIControlState.Normal);
			_settingsButton.ImageEdgeInsets = insets;
			_settingsButton.Alpha = .75f;
			_settingsButton.TouchUpInside += (sender, e) =>
			{
				DisplaySettingsSheet();
			};

			_segment = new UISegmentedControl();
			_segment.TintColor = UIColor.FromRGBA(255, 255, 255, 50);
			_segment.InsertSegment("Epsiodes", 0, true);
			_segment.InsertSegment("Cast", 1, true);
			_segment.SelectedSegment = 0;
			_segment.SetTitleTextAttributes(new UITextAttributes
			{
				TextColor = UIColor.FromRGBA(255, 255, 255, 255),
			}, UIControlState.Normal);

			_segment.ValueChanged += (sender, e) => {
				UpdateSelectedView();
			};

			_doneButton = new UIButton(UIButtonType.Custom);
			_doneButton.SetImage(UIImage.FromBundle("Images/close_icon"), UIControlState.Normal);
			_doneButton.ImageEdgeInsets = insets;
			_doneButton.Alpha = _settingsButton.Alpha;
			_doneButton.TouchUpInside += (sender, e) =>
			{
				ViewModel.CancelTasks();
				DismissViewControllerAsync(true);
			};

			_blurView = new UIVisualEffectView(UIBlurEffect.FromStyle(UIBlurEffectStyle.Dark)) {
				Frame = View.Bounds,
				Alpha = .90f,
				Hidden = true,
			};

			_backgroundImage = new UIImageView(View.Bounds);
			_backgroundImage.Alpha = .8f;
			_backgroundImage.ContentMode = UIViewContentMode.ScaleAspectFill;

			_seasonsView = new ShowSeasonsHorizontalViewController();
			_castView = new ShowCastHorizontalViewController();

			Add(_backgroundImage);
			Add(_blurView);
			AddWithKey(_doneButton, "done");
			AddWithKey(_settingsButton, "settings");
			AddWithKey(_titleLabel, "title");
			AddWithKey(_metaLabel, "meta");
			AddWithKey(_descriptionLabel, "desc");
			AddWithKey(_seasonsView.View, "seasons");
			AddWithKey(_castView.View, "cast");
			AddWithKey(_segment, "segment");

			AddConstraint("H:|-(46)-[title(>=100)]-(40)-[settings(70)]-(0)-[done(==settings)]-(10)-|");
			AddConstraint("H:|-(46)-[meta(>=100)]-(50)-|");
			AddConstraint("H:|-(42)-[desc(>=100)]-(50)-|");
			AddConstraint("H:|-(>=10)-[segment(160)]-(>=10)-|");
			AddConstraint("H:|-(0)-[seasons(>=100)]-(0)-|");
			AddConstraint("H:|-(0)-[cast(>=100)]-(0)-|");
			AddConstraint("V:|-(10)-[settings(70)]-(>=1)-|");
			AddConstraint("V:|-(10)-[done(==settings)]-(>=1)-|");
			AddConstraint("V:|-(20)-[title(80)]-(0)-[meta(16)]-(0)-[desc(68)]-[segment]-(0)-[seasons(>=100)]-(0)-|");

			AddConstraint("V:|-(10)-[done(==settings)]-(>=1)-|");

			View.AddConstraint(NSLayoutConstraint.Create(_castView.View, NSLayoutAttribute.Top, NSLayoutRelation.Equal, _seasonsView.View, NSLayoutAttribute.Top, 1, 0));
			View.AddConstraint(NSLayoutConstraint.Create(_castView.View, NSLayoutAttribute.Height, NSLayoutRelation.Equal, _seasonsView.View, NSLayoutAttribute.Height, 1, 0));
			View.AddConstraint(NSLayoutConstraint.Create(_segment, NSLayoutAttribute.CenterX, NSLayoutRelation.Equal, _descriptionLabel, NSLayoutAttribute.CenterX, 1f, 0));

			base.LayoutInterface();
			UpdateSelectedView();

			await UpdateView();
		}

		void UpdateSelectedView()
		{
			_seasonsView.View.Hidden = _segment.SelectedSegment == 1;
			_castView.View.Hidden = _segment.SelectedSegment == 0;
		}

		public override async void ViewWillAppear(bool animated)
		{
			base.ViewWillAppear(animated);
			ViewModel.ResetCancellationToken();
			await UpdateView();
		}

		async Task RefreshShow()
		{
			var title = ViewModel.Show.Title;
			_titleLabel.Text = "Updating {0}...".Fmt(title);

			TraktService.Instance.ClearHistory(ViewModel.Show.Identifiers?.Trakt.ToString());
			await UpdateView(true);
		}

		public async Task UpdateView(bool forceRefresh = false)
		{
			if(!IsLayoutInitialized || ViewModel.Show == null)
				return;

			_settingsButton.Enabled = false;
			_titleLabel.Text = ViewModel.Show.Title;
			_metaLabel.Text = ViewModel.Show.Metadata;
			_descriptionLabel.Text = ViewModel.Show.Overview;
			_backgroundImage.Image = null;
			_seasonsView.UpdateView();
			_castView.UpdateView();
			_blurView.Hidden = false;
			_segment.Hidden = true;

			if(ViewModel.Show.Images.FanArt?.Full != null)
			{
				App.NetworkInUse(true);
				_backgroundImage.SetImage(new NSUrl(ViewModel.Show.Images.FanArt.Full), null, SDWebImageOptions.HighPriority, (image,  error, cachetype, imageUrl) => {
					App.NetworkInUse(false);
				});
			}
			else
			{
				_backgroundImage.Image = UIImage.FromBundle("Images/missing_screen.png");
			}

			await _castView.RefreshData(forceRefresh);
			_segment.Hidden = ViewModel.Show.CastAndCrew?.Cast == null || ViewModel.Show.CastAndCrew.Cast.Count == 0;
			_castView.UpdateView();

			if(App.IsNetworkReachable)
				await _seasonsView.RefreshData(forceRefresh);

			_settingsButton.Enabled = true;
			_seasonsView.UpdateView();
		}

		void DisplaySettingsSheet()
		{
			var actionSheet = UIAlertController.Create("Some options for you, sire", null, UIAlertControllerStyle.ActionSheet);

			#region Refresh & Delete

			var refreshAction = UIAlertAction.Create("Refresh Show", UIAlertActionStyle.Default, async(action) =>
			{
				await RefreshShow();
			});

			var deleteAction = UIAlertAction.Create("Remove Show", UIAlertActionStyle.Default, (action) =>
			{
				var alert = new UIAlertView("Are you sure you want to remove {0}?".Fmt(ViewModel.Show.Title), null, null, "No", "Yes");
				alert.Clicked += (sender2, e2) =>
				{
					if(e2.ButtonIndex == 1)
					{
						TraktService.Instance.ClearHistory(ViewModel.Show.Identifiers?.Trakt.ToString());
						ViewModel.CancelTasks();
						if(ShowRemoved != null)
							ShowRemoved(ViewModel.Show);
					}
				};

				alert.Show();
			});

			#endregion

			#region Alternate Title

			var alternateAction = UIAlertAction.Create("Set Alternate Title", UIAlertActionStyle.Default, async(action) =>
			{
				UITextField field = null;
				var altSheet = UIAlertController.Create("Alternate Title", null, UIAlertControllerStyle.Alert);
				altSheet.AddTextField((txt) =>
				{
					field = txt;
					field.Text = ViewModel.Show.AlternateTitle;
					field.Placeholder = ViewModel.Show.Title;
				});

				var cancelAction = UIAlertAction.Create("Cancel", UIAlertActionStyle.Cancel, async(action2) =>
				{
					await altSheet.DismissViewControllerAsync(true);
				});

				var saveAction = UIAlertAction.Create("Save", UIAlertActionStyle.Default, async(action2) =>
				{
					ViewModel.Show.AlternateTitle = field.Text.Trim();
					await ViewModel.Show.Save();
					await altSheet.DismissViewControllerAsync(true);
				});

				altSheet.AddAction(cancelAction);
				altSheet.AddAction(saveAction);
				await PresentViewControllerAsync(altSheet, true);
			});

			#endregion

			actionSheet.AddAction(refreshAction);
			actionSheet.AddAction(deleteAction);

			if(AppDelegate.Instance.Client.IsConnected)
			{
				actionSheet.AddAction(alternateAction);
			}

			actionSheet.ModalPresentationStyle = UIModalPresentationStyle.Popover;
			actionSheet.PopoverPresentationController.SourceView = _settingsButton;
			actionSheet.PopoverPresentationController.SourceRect = _settingsButton.Bounds;

			PresentViewControllerAsync(actionSheet, true);
		}
	}
}