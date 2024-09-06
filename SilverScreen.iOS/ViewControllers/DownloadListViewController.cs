using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using CoreAnimation;
using CoreGraphics;
using Foundation;
using MultipeerConnectivity;
using SDWebImage;
using UIKit;
using SilverScreen.iOS.Shared;
using SilverScreen.Shared;

namespace SilverScreen.iOS
{
	public class DownloadListViewController : BaseViewController<DownloadListViewModel>
	{
		#region Properties and Fields

		internal static float HeaderBarHeight = (float)UIApplication.SharedApplication.StatusBarFrame.Height + 36f;
		bool _doSwitchIfNotActive;

		DownloadTableViewController _activeList;
		DownloadTableViewController _previousList;
		DownloadTableViewController _missingList;
		SortMode _sortMode;
		UIButton _sortButton;
		UIButton _cancelButton;
		UIButton _clearButton;
		UIButton _ignoreButton;
		UIButton _doneButton;
		UISwitch _historySwitch;
		UILabel _switchLabelOn;
		UILabel _switchLabelOff;
		UILabel _titleLabel;
		UIView _backgroundBlurView;
		UIImageView _backgroundImage;
		UISegmentedControl _segment;
		List<DownloadResponse> _lastHistory;

		bool ShowFailures
		{
			get
			{
				return _historySwitch.On;
			}
		}

		ViewOptions SelectedView
		{
			get
			{
				if(_segment == null)
					return ViewOptions.None;
				
				return (ViewOptions)(int)_segment.SelectedSegment;
			}
		}

		public DownloadListViewController()
		{
			_sortMode = SortMode.Title;
		}

		#endregion

		#region Update UI

		protected override void LayoutInterface()
		{
			base.LayoutInterface();

			View.BackgroundColor = UIColor.FromRGB(66, 66, 66);

			_backgroundBlurView = new UIVisualEffectView(UIBlurEffect.FromStyle(UIBlurEffectStyle.Dark)) {
				Frame = View.Bounds,
				Alpha = .85f,
				Hidden = true,
			};

			var titleBlurView = new UIVisualEffectView(UIBlurEffect.FromStyle(UIBlurEffectStyle.Light)) {
				Frame = new CGRect(0, 0, View.Frame.Width, HeaderBarHeight),
				BackgroundColor = UIColor.Clear
			};

			_backgroundImage = new UIImageView(View.Bounds);
			_backgroundImage.Alpha = .8f;

			var atts = new UITextAttributes {
				TextColor = UIColor.FromRGBA(255, 255, 255, 255),
			};

			_segment = new UISegmentedControl();
			_segment.TintColor = UIColor.FromRGBA(255, 255, 255, 50);
			_segment.InsertSegment("Active", 0, true);
			_segment.InsertSegment("Previous", 1, true);
			_segment.InsertSegment("Missing", 2, true);
			_segment.SetTitleTextAttributes(atts, UIControlState.Normal);

			_segment.ValueChanged += (sender, e) =>
			{
				_doSwitchIfNotActive = false;
				switch((ViewOptions)(int)_segment.SelectedSegment)
				{
					case ViewOptions.ActiveDownloads:
						DisplayActiveDownloads();
						break;

					case ViewOptions.PreviousDownloads:
						DisplayPreviousDownloads();
						break;

					case ViewOptions.MissingDownloads:
						DisplayMissingDownloads();
						break;
				}
			};

			_activeList = new DownloadTableViewController(new CGRect(0, 0, View.Bounds.Width, View.Bounds.Height));
			_activeList.Table.ContentInset = new UIEdgeInsets(titleBlurView.Bounds.Height, 0, 0, 0);
			_activeList.Table.DestructionText = "Cancel";
			_activeList.Table.RowSelected += OnRowSelected;
			_activeList.Table.RowDeleted += OnActiveRowDeleted;

			_previousList = new DownloadTableViewController(_activeList.View.Frame);
			_previousList.Table.ContentInset = _activeList.Table.ContentInset;
			_previousList.Table.DestructionText = "Hide";
			_previousList.Table.RowSelected += OnRowSelected;
			_previousList.Table.RowDeleted += OnPastRowDeleted;

			_missingList = new DownloadTableViewController(new CGRect(0, 0, View.Bounds.Width, View.Bounds.Height));
			_missingList.Table.ContentInset = _activeList.Table.ContentInset;
			_missingList.Table.DestructionText = "Hide";
			_missingList.Table.RowSelected += OnRowSelected;
			_missingList.Table.RowDeleted += OnMissingRowIgnored;
			_missingList.Table.RowDownloadClicked += OnMissingRowDownloadClicked;

			_sortButton = new UIButton(UIButtonType.RoundedRect) {
				Alpha = .75f,
			};

			_sortButton.SetTitle("Sort", UIControlState.Normal);
			_sortButton.SetTitleColor(UIColor.White, UIControlState.Normal);
			_sortButton.TouchUpInside += (sender, e) =>
			{
				DisplaySortActionSheet();
			};

			_cancelButton = new UIButton(UIButtonType.RoundedRect) {
				Alpha = .75f,
			};

			_cancelButton.SetTitle("Cancel All", UIControlState.Normal);
			_cancelButton.SetTitleColor(UIColor.White, UIControlState.Normal);
			_cancelButton.TouchUpInside += (sender, e) =>
			{
				var alert = new UIAlertView("Cancel All Downloads", "Are you sure you want to cancel all downloads?", null, "No", "Yes");
				alert.Clicked += (senderr, ee) =>
				{
					if(ee.ButtonIndex == 1)
					{
						CancelAllDownloads();
					}
				};
				alert.Show();
			};

			_historySwitch = new UISwitch
			{
				OnTintColor = UIColor.FromRGBA(255, 255, 255, 155),
				TintColor = UIColor.FromRGBA(255, 255, 255, 50),
				Transform = CGAffineTransform.MakeScale(.75f, .75f),
			};

			_historySwitch.ValueChanged += (sender, e) => {
				PreviousDownloadsUpdated();
			};

			_switchLabelOn = new UILabel
			{
				Font = UIFont.FromName("HelveticaNeue-Light", 11f),
				TextColor = UIColor.FromRGBA(255, 255, 255, 75),
				TextAlignment = UITextAlignment.Right,
				Text = "Good",
			};

			_switchLabelOff = new UILabel
			{
				Font = UIFont.FromName("HelveticaNeue-Light", 11f),
				TextColor = UIColor.FromRGBA(255, 255, 255, 75),
				TextAlignment = UITextAlignment.Left,
				Text = "All",
			};

			_clearButton = new UIButton(UIButtonType.RoundedRect) {
				Alpha = _cancelButton.Alpha,
			};

			_clearButton.SetTitle("Clear History", UIControlState.Normal);
			_clearButton.SetTitleColor(UIColor.White, UIControlState.Normal);
			_clearButton.TouchUpInside += (sender, e) =>
			{
				ClearHistory();
			};

			_ignoreButton = new UIButton(UIButtonType.RoundedRect) {
				Alpha = _cancelButton.Alpha,
			};

			_ignoreButton.SetTitle("Ignore All", UIControlState.Normal);
			_ignoreButton.SetTitleColor(UIColor.White, UIControlState.Normal);
			_ignoreButton.TouchUpInside += (sender, e) =>
			{
				IgnoreAllMissing();
			};

			_titleLabel = new UILabel() {
				Font = UIFont.FromName("HelveticaNeue-Light", 24f),
				TextColor = UIColor.FromRGBA(255, 255, 255, 225),
				TextAlignment = UITextAlignment.Center,
				AdjustsFontSizeToFitWidth = true,
				Text = "Downloads",
			};

			_doneButton = new UIButton(UIButtonType.Custom) {
				Alpha = .75f,
				ImageEdgeInsets = new UIEdgeInsets(26, 26, 26, 26),
			};

			_doneButton.SetImage(UIImage.FromBundle("Images/close_icon"), UIControlState.Normal);
			_doneButton.SetTitle("Done", UIControlState.Normal);
			_doneButton.TouchUpInside += (sender, e) =>
			{
				DismissViewControllerAsync(true);
			};

			Add(_backgroundImage);
			Add(_backgroundBlurView);
			AddWithKey(_cancelButton, "cancelAll", titleBlurView);
			AddWithKey(_clearButton, "clearHistory", titleBlurView);
			AddWithKey(_ignoreButton, "ignoreAll", titleBlurView);
			AddWithKey(_switchLabelOn, "switchLabelOn", titleBlurView);
			AddWithKey(_switchLabelOff, "switchLabelOff", titleBlurView);
			AddWithKey(_historySwitch, "switch", titleBlurView);
			AddWithKey(_segment, "segment", titleBlurView);
			AddWithKey(_doneButton, "done", titleBlurView);

			Add(_activeList.View);
			Add(_previousList.View);
			Add(_missingList.View);
			Add(titleBlurView);

			AddConstraint("H:|-(0)-[clearHistory(120)]-(>=20)-[switchLabelOn(40)]-(2)-[switch]-(4)-[switchLabelOff]-(74)-|", titleBlurView);
			AddConstraint("H:|-(0)-[ignoreAll(120)]-(>=20)-|", titleBlurView);
			AddConstraint("H:|-(0)-[cancelAll(120)]-(>=20)-[done(70)]-(-10)-|", titleBlurView);
			titleBlurView.AddConstraint(NSLayoutConstraint.Create(_cancelButton, NSLayoutAttribute.CenterY, NSLayoutRelation.Equal, titleBlurView, NSLayoutAttribute.CenterY, 1, 0));
			titleBlurView.AddConstraint(NSLayoutConstraint.Create(_historySwitch, NSLayoutAttribute.CenterY, NSLayoutRelation.Equal, titleBlurView, NSLayoutAttribute.CenterY, 1, 0));
			titleBlurView.AddConstraint(NSLayoutConstraint.Create(_clearButton, NSLayoutAttribute.CenterY, NSLayoutRelation.Equal, titleBlurView, NSLayoutAttribute.CenterY, 1, 0));
			titleBlurView.AddConstraint(NSLayoutConstraint.Create(_ignoreButton, NSLayoutAttribute.CenterY, NSLayoutRelation.Equal, titleBlurView, NSLayoutAttribute.CenterY, 1, 0));
			titleBlurView.AddConstraint(NSLayoutConstraint.Create(_doneButton, NSLayoutAttribute.CenterY, NSLayoutRelation.Equal, titleBlurView, NSLayoutAttribute.CenterY, 1, 0));
			titleBlurView.AddConstraint(NSLayoutConstraint.Create(_doneButton, NSLayoutAttribute.Height, NSLayoutRelation.Equal, null, NSLayoutAttribute.NoAttribute, 1, 70));
			titleBlurView.AddConstraint(NSLayoutConstraint.Create(_switchLabelOn, NSLayoutAttribute.CenterY, NSLayoutRelation.Equal, _historySwitch, NSLayoutAttribute.CenterY, 1, 0));
			titleBlurView.AddConstraint(NSLayoutConstraint.Create(_switchLabelOff, NSLayoutAttribute.CenterY, NSLayoutRelation.Equal, _historySwitch, NSLayoutAttribute.CenterY, 1, 0));

			titleBlurView.AddConstraint(NSLayoutConstraint.Create(_segment, NSLayoutAttribute.Width, NSLayoutRelation.Equal, null, NSLayoutAttribute.NoAttribute, 1, 400));
			titleBlurView.AddConstraint(NSLayoutConstraint.Create(_segment, NSLayoutAttribute.CenterY, NSLayoutRelation.Equal, titleBlurView, NSLayoutAttribute.CenterY, 1, 0));
			titleBlurView.AddConstraint(NSLayoutConstraint.Create(_segment, NSLayoutAttribute.CenterX, NSLayoutRelation.Equal, titleBlurView, NSLayoutAttribute.CenterX, 1, 0));

			_segment.SelectedSegment = 0;
			DisplayActiveDownloads();
		}

		#endregion

		public override void ViewWillAppear(bool animated)
		{
			base.ViewWillAppear(animated);

			_doSwitchIfNotActive = true;
			AppDelegate.Instance.Client.DataReceived += HandleDataReceived;
		}

		public override void ViewDidDisappear(bool animated)
		{
			AppDelegate.Instance.Client.DataReceived -= HandleDataReceived;

			base.ViewDidDisappear(animated);
		}

		#region Host Connection

		void HandleDataReceived(object sender, MessagePayload e)
		{
			BeginInvokeOnMainThread(() =>
			{
				switch(e.Action)
				{
					case MessageAction.PreviousDownloads:
						_lastHistory = e.GetPayload<List<DownloadResponse>>();
						PreviousDownloadsUpdated();
						break;
					case MessageAction.ActiveDownloads:
						ActiveDownloadsUpdated(e.GetPayload<List<DownloadResponse>>().Where(d => d.Request?.Show != null && d.Request?.Episode != null).ToList());
						break;
					case MessageAction.MissingDownloads:
						MissingDownloadsUpdated(e.GetPayload<List<DownloadRequest>>().Where(d => d.Show != null && d.Episode != null).ToList());
						break;
					case MessageAction.DownloadUpdateInquiry:
						HandleDownloadUpdate(e.GetPayload<DownloadResponse>());
						break;
				}
			});
		}

		#endregion

		void HandleDownloadUpdate(DownloadResponse response)
		{
			//Debug.WriteLine("Incoming response: " + response.Request.ShowTitle + " " + response.State);
			_activeList.Table.UpdateDownload(response);
			_missingList.Table.UpdateDownload(response);
		}

		#region ViewState

		void UpdateSelectedView()
		{
			_backgroundImage.Image = null;

			_activeList.View.Hidden = SelectedView != ViewOptions.ActiveDownloads;
			_previousList.View.Hidden = SelectedView != ViewOptions.PreviousDownloads;
			_missingList.View.Hidden = SelectedView != ViewOptions.MissingDownloads;

			//TODO move to UIButton collection for DownloadsListView
			_clearButton.Hidden = true;
			_cancelButton.Hidden = true;
			_ignoreButton.Hidden = true;
			_historySwitch.Hidden = true;
			_switchLabelOn.Hidden = true;
			_switchLabelOff.Hidden = true;
			DownloadTableViewController table = null;

			switch(SelectedView)
			{
				case ViewOptions.ActiveDownloads:
					_cancelButton.Hidden = !_activeList.HasDownloads;
					table = _activeList;
					break;

				case ViewOptions.PreviousDownloads:
					_clearButton.Hidden = _lastHistory == null || !_lastHistory.Any();
					_historySwitch.Hidden = _clearButton.Hidden;
					_switchLabelOn.Hidden = _clearButton.Hidden;
					_switchLabelOff.Hidden = _clearButton.Hidden;
					table = _previousList;
					break;

				case ViewOptions.MissingDownloads:
					_ignoreButton.Hidden = !_missingList.HasDownloads;
					table = _missingList;
					break;
			}

			DownloadResponse download;

			if(table.Table.IndexPathForSelectedRow != null && table.Table.Downloads != null)
			{
				download = table.Table.Downloads[(int)table.Table.IndexPathForSelectedRow.Item];
			}
			else
			{
				download = table.Table.Downloads?.FirstOrDefault();
			}

			SetBackgroundImage(download);
		}

		#endregion

		#region Previous Downloads

		void GetPreviousDownloads()
		{
			Debug.WriteLine("Getting previous downloads");
			_previousList.StartLoading("Loading previous downloads...");
			AppDelegate.Instance.Client.SendMessage(MessageAction.PreviousDownloads);
		}

		void PreviousDownloadsUpdated()
		{
			if(_lastHistory == null)
				return;
			
			var list = _lastHistory.Where(d => d.Request?.Show != null && d.Request?.Episode != null && (ShowFailures || d.Result == DownloadResult.Success)).ToList();

			_previousList.Table.Downloads = list;
			_previousList.StopLoading(_previousList.Table.Downloads.Count == 0 ? "You have no previous downloads" : null);
			_previousList.Table.ReloadData();
			_clearButton.Enabled = true;
			UpdateSelectedView();
		}

		void DisplayPreviousDownloads()
		{
			if(_previousList == null)
				return;

			UpdateSelectedView();

			if(_previousList.Table.Downloads == null && SelectedView == ViewOptions.PreviousDownloads && !_previousList.IsLoading)
				GetPreviousDownloads();
		}

		#endregion

		#region Active Downloads

		void GetActiveDownloads()
		{
			Debug.WriteLine("Getting active downloads");
			_activeList.StartLoading("Loading active downloads...");
			AppDelegate.Instance.Client.SendMessage(MessageAction.ActiveDownloads);
		}

		void ActiveDownloadsUpdated(List<DownloadResponse> list)
		{
			_activeList.Table.Downloads = list;
			_activeList.StopLoading(_activeList.Table.Downloads.Count == 0 ? "You have no active downloads" : null);
			_activeList.Table.ReloadData();
			_cancelButton.Enabled = true;
			UpdateSelectedView();

			if(_doSwitchIfNotActive && list.Count == 0)
			{
				BeginInvokeOnMainThread(() => {
					_doSwitchIfNotActive = false;
					_segment.SelectedSegment = 2;
					DisplayMissingDownloads();
				});
			}
		}

		void DisplayActiveDownloads()
		{
			if(_activeList == null)
				return;

			UpdateSelectedView();

			if(_activeList.Table.Downloads == null && SelectedView == ViewOptions.ActiveDownloads && !_activeList.IsLoading)
				GetActiveDownloads();
		}

		#endregion

		#region Missing Downloads

		void GetMissingDownloads()
		{
			int maxDaysOld = 21;

			Debug.WriteLine("Getting missing downloads");
			_missingList.StartLoading("Loading missing downloads...");

			var vm = ServiceContainer.Resolve<ShowListViewModel>();
			var list = new List<DownloadRequest>();

			foreach(var show in vm.Shows)
			{
				if(show.PreviousEpisode == null)
					continue;

				var season = show.SeasonByNumber(show.PreviousEpisode.SeasonNumber);
				var ep = show.PreviousEpisode;

				while(ep != null && ep.InitialBroadcastDate != null
				      && DateTime.UtcNow.Subtract(ep.InitialBroadcastDate.Value).TotalDays < maxDaysOld + 1 && ep.InitialBroadcastDate.Value < DateTime.Now)
				{
					list.Add(new DownloadRequest(show, ep));
					ep = season.EpisodeByNumber(ep.Number - 1);
				}
			}

			AppDelegate.Instance.Client.SendMessage(MessageAction.MissingDownloads, list);
		}

		void MissingDownloadsUpdated(List<DownloadRequest> list)
		{
			var responses = list.Select(r => new DownloadResponse{ Request = r }).ToList();
			_missingList.Table.Downloads = responses.Where(r => !r.Request.Episode.IgnoreNotLocal).ToList();
			_missingList.StopLoading(_missingList.Table.Downloads.Count == 0 ? "You have no missing episodes" : null);

			//TODO compare lists before reload
			_missingList.Table.ReloadData();
			_ignoreButton.Enabled = true;
			UpdateSelectedView();
		}

		void DisplayMissingDownloads()
		{
			if(_missingList == null)
				return;

			UpdateSelectedView();

			if(_missingList.Table.Downloads == null && SelectedView == ViewOptions.MissingDownloads && !_missingList.IsLoading)
				GetMissingDownloads();
		}

		#endregion

		void OnActiveRowDeleted(object sender, DownloadResponse response)
		{
			AppDelegate.Instance.Client.SendMessage(MessageAction.CancelDownload, response.ID);
		}

		void OnPastRowDeleted(object sender, DownloadResponse response)
		{
			AppDelegate.Instance.Client.SendMessage(MessageAction.ClearHistoricDownload, response.ID);

			var index = _previousList.Table.Downloads.IndexOf(response);
			_previousList.Table.Downloads.Remove(response);
			_previousList.Table.DeleteRows(new [] { NSIndexPath.FromRowSection(index, 0) }, UITableViewRowAnimation.Automatic);
		}

		void OnMissingRowDownloadClicked(object sender, DownloadResponse response)
		{
			AppDelegate.Instance.Client.SendMessage(MessageAction.DownloadEpisode, response.Request.Clone());
		}

		async void OnMissingRowIgnored(object sender, DownloadResponse response)
		{
			if(response.Request.Episode == null)
				return;

			var index = _missingList.Table.Downloads.IndexOf(response);
			response.Request.Episode.IgnoreNotLocal = true;
			await response.Request.Show.SeasonByNumber(response.Request.SeasonNumber).Save();

			_missingList.Table.Downloads.Remove(response);
			_missingList.Table.DeleteRows(new [] { NSIndexPath.FromRowSection(index, 0) }, UITableViewRowAnimation.Automatic);
		}

		void OnRowSelected(object sender, DownloadResponse response)
		{
			SetBackgroundImage(response);
		}

		void SetBackgroundImage(DownloadResponse download)
		{
			if(download == null)
				return;

			string url;

			var vm = ServiceContainer.Resolve<ShowListViewModel>();
			var show = vm.Shows.FirstOrDefault(s => s.Identifiers?.Trakt.ToString() == download.Request.ShowTraktID);
			if(show != null)
			{
				url = show.Images.FanArt.Full;

				if(string.IsNullOrWhiteSpace(url))
				{
					var season = show.SeasonByNumber(download.Request.SeasonNumber);

					if(season != null)
					{
						season.EnsureEpisodesLoadedFromCache();
						var episode = season.Episodes.FirstOrDefault(e => e.Identifiers?.Trakt.ToString() == download.Request.EpisodeTraktID);
						url = episode.Images.Screenshot.Full;
					}
				}

				if(!string.IsNullOrWhiteSpace(url))
				{
					_backgroundImage.SetImage(new NSUrl(url));
					var transition = CATransition.CreateAnimation();
					transition.Duration = 0.25f;
					transition.TimingFunction = CAMediaTimingFunction.FromName(CAMediaTimingFunction.EaseIn);

					_backgroundBlurView.Hidden = false;
					_backgroundImage.Layer.AddAnimation(transition, null);
				}
			}
		}

		async void IgnoreAllMissing()
		{
			foreach(var response in _missingList.Table.Downloads)
			{
				if(response.Request.Episode == null)
					continue;

				response.Request.Episode.IgnoreNotLocal = true;
				await response.Request.Show.SeasonByNumber(response.Request.SeasonNumber).Save();
			}
		}

		void ClearHistory()
		{
			_clearButton.Enabled = false;
			AppDelegate.Instance.Client.SendMessage(MessageAction.ClearHistory);
		}

		void CancelAllDownloads()
		{
			_cancelButton.Enabled = false;
			AppDelegate.Instance.Client.SendMessage(MessageAction.CancelAllDownloads);
		}

		#region Sort

		void DisplaySortActionSheet()
		{
			var alphaAction = UIAlertAction.Create("TV Show Title", UIAlertActionStyle.Default, (action) =>
			{
				if(_sortMode == SortMode.Title)
					return;

				_sortMode = SortMode.Title;
				SortByTitle();
			});

			var upcomingAction = UIAlertAction.Create("Upcoming Episodes", UIAlertActionStyle.Default, (action) =>
			{
				if(_sortMode == SortMode.UpcomingEpisodes)
					return;

				_sortMode = SortMode.UpcomingEpisodes;
				SortByUpcoming();
			});

			var recentAction = UIAlertAction.Create("Recent Episodes", UIAlertActionStyle.Default, (action) =>
			{
				if(_sortMode == SortMode.RecentEpisodes)
					return;

				_sortMode = SortMode.RecentEpisodes;
				SortByRecent();
			});

			var actionSheet = UIAlertController.Create("Sort TV Shows by", null, UIAlertControllerStyle.ActionSheet);
			actionSheet.AddAction(alphaAction);
			actionSheet.AddAction(upcomingAction);
			actionSheet.AddAction(recentAction);
			actionSheet.ModalPresentationStyle = UIModalPresentationStyle.Popover;

			actionSheet.PopoverPresentationController.SourceView = _sortButton;
			actionSheet.PopoverPresentationController.SourceRect = _sortButton.Bounds;

			PresentViewControllerAsync(actionSheet, true);
		}

		void SortByTitle()
		{
			_titleLabel.Text = "Downloads by Title";
		}

		void SortByRecent()
		{
			_titleLabel.Text = "Downloads by Most Recent";
		}

		void SortByUpcoming()
		{
			_titleLabel.Text = "Downloads by Speed";
		}

		List<Show> SortListByEpisodeTitle(List<Show> shows)
		{
			return shows.OrderBy(s => s.SortTitle).ToList();
		}

		List<Show> SortListByRecentEpisodes(List<Show> shows)
		{
			var have = shows.Where(s => s.PreviousEpisode != null && s.PreviousEpisode.InitialBroadcastDate != null);
			var dontHave = shows.Where(s => s.PreviousEpisode == null || s.PreviousEpisode.InitialBroadcastDate == null);
			var list = have.OrderByDescending(s => s.PreviousEpisode.InitialBroadcastDate).ToList();
			list.AddRange(dontHave.OrderBy(s => s.SortTitle));

			return list;
		}

		List<Show> SortListByUpcomgingEpisodes(List<Show> shows)
		{
			var have = shows.Where(s => s.NextEpisode != null && s.NextEpisode.InitialBroadcastDate != null);
			var dontHave = shows.Where(s => s.NextEpisode == null || s.NextEpisode.InitialBroadcastDate == null);
			var list = have.OrderBy(s => s.NextEpisode.InitialBroadcastDate).ToList();
			list.AddRange(dontHave.OrderBy(s => s.SortTitle));

			return list;
		}

		public void UpdateSortedShows()
		{
			switch(_sortMode)
			{
				case SortMode.Title:
					SortByTitle();
					break;
				case SortMode.UpcomingEpisodes:
					SortByUpcoming();
					break;
				case SortMode.RecentEpisodes:
					SortByRecent();
					break;
			}
		}

		void ReorderShowsList(Action reorder)
		{
//			var currentList = ViewModel.ActiveDownloads.ToList();
//			reorder();
//
//			_activeList.ScrollRectToVisible(new CGRect(0, 0, 10, 10), false);
//			var indexSet = new List<NSIndexPath>();
//			_activeList.BeginUpdates();
//			int newIndex = 0;
//
//			foreach(var s in ViewModel.ActiveDownloads)
//			{
//				var currentIndex = currentList.IndexOf(currentList.FirstOrDefault(sh => sh.ID == s.ID));
//				_activeList.MoveRow(currentIndex.ToIndexPath(), newIndex.ToIndexPath());
//
//				if(_activeList.IndexPathsForVisibleRows.Any(i => i.Row == newIndex))
//				{
//					indexSet.Add(newIndex.ToIndexPath());
//				}
//				newIndex++;
//			}
//
//			_activeList.EndUpdates();
//
//			_activeList.BeginUpdates();
//			_activeList.ReloadRows(indexSet.ToArray(), UITableViewRowAnimation.Automatic);
//			_activeList.EndUpdates();
		}

		#endregion
	}
}