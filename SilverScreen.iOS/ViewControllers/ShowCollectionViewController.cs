using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using CoreGraphics;
using Foundation;
using MultipeerConnectivity;
using ObjCRuntime;
using SDWebImage;
using SilverScreen.iOS.Shared;
using SilverScreen.Shared;
using UIKit;
using Xamarin;

namespace SilverScreen.iOS
{
	public class ShowCollectionViewController : BaseViewController<ShowListViewModel>, IUICollectionViewDataSource, IUICollectionViewDelegate
	{
		#region Fields

		static readonly nfloat _posterImageScale = .4f;
		static readonly CGSize _posterImageSize = new CGSize(300, 450);
		static string _title;
		static NSString showCellId = new NSString("ShowGridCell");
		bool _isRefreshing;
		bool _isSyncing;
		List<int> _initializedCells = new List<int>();

		SortMode _sortMode;
		UICollectionView _showList;
		UIView _searchBlurView;
		UISearchBar _searchBar;
		UIButton _sortButton;
		UIButton _searchButton;
		UIButton _addButton;
		UIButton _settingsButton;
		UIButton _cancelButton;
		UILabel _titleLabel;
		UIButton _downloadButton;
		UILabel _emptyMessage;
		UIView _statusView;
		UIActivityIndicatorView _activity;
		JSBadgeView _badge;
		UIRefreshControl _refreshControl;

		ShowDetailsViewController _detailsViewController;
		DownloadListViewController _downloadListViewController;

		#endregion

		NSLayoutConstraint[] _horizontalConstraints;

		[DllImport(Constants.ObjectiveCLibrary, EntryPoint = "objc_msgSendSuper")]
		static extern void void_objc_msgSendSuper_IntPtr(IntPtr deviceHandle, IntPtr setterHandle, IntPtr handle);

		public ShowCollectionViewController()
		{
			AppDelegate.Instance.Client.StateChanged += HandleClientStateChanged;
			AppDelegate.Instance.Client.DataReceived += HandleClientDataReceived;
			_sortMode = SortMode.Title;
		}

		#region Update UI

		protected async override void LayoutInterface()
		{
			base.LayoutInterface();

			var statusHeight = UIApplication.SharedApplication.StatusBarFrame.Height;
			View.BackgroundColor = UIColor.FromRGB(33, 33, 33);

			var backgroundBlurView = new UIVisualEffectView(UIBlurEffect.FromStyle(UIBlurEffectStyle.Dark)) {
				Frame = new CGRect(0, 0, View.Frame.Width, 56 + statusHeight),
				Alpha = .9f,
			};

			var blurView = new UIView {
				Frame = backgroundBlurView.Frame,
			};
			blurView.Add(backgroundBlurView);

			var layout = new UICollectionViewFlowLayout();
			layout.SectionInset = new UIEdgeInsets(15 + blurView.Bounds.Height, 20, 20, 20);
			layout.ItemSize = new CGSize(_posterImageSize.Width * _posterImageScale, _posterImageSize.Height * _posterImageScale + 35);

			layout.MinimumLineSpacing = 20;
			_showList = new UICollectionView(new CGRect(0, 0, View.Bounds.Width, View.Bounds.Height), layout);
			_showList.BackgroundColor = UIColor.Clear;
			_showList.RegisterClassForCell(typeof(ShowGridCell), showCellId);
			_showList.Delegate = this;
			_showList.DataSource = this;
			_showList.AlwaysBounceVertical = true;

			_statusView = new UIVisualEffectView(UIBlurEffect.FromStyle(UIBlurEffectStyle.Dark)) {
				Frame = new CGRect(0, View.Frame.Height / 2 - 75, View.Frame.Width, 150)
			};
			_statusView.Hidden = true;

			_activity = new UIActivityIndicatorView(UIActivityIndicatorViewStyle.WhiteLarge);
			_activity.HidesWhenStopped = false;
			_activity.Frame = new CGRect(blurView.Frame.Width / 2 - 25, _statusView.Frame.Height / 2 - 25 - 20, 50, 50);

			_emptyMessage = new UILabel(new CGRect(View.Frame.Width / 2 - 350, View.Frame.Height / 2 - 100 - statusHeight, 700, 200));
			_emptyMessage.Font = UIFont.FromName("HelveticaNeue-UltraLight", 36f);
			_emptyMessage.TextColor = UIColor.FromRGBA(255, 255, 255, 50);
			_emptyMessage.TextAlignment = UITextAlignment.Center;
			_emptyMessage.Lines = 3;
			_emptyMessage.Text = "loading shows";

			_sortButton = new UIButton(UIButtonType.RoundedRect);
			_sortButton.SetTitle("Sort", UIControlState.Normal);
			_sortButton.TouchUpInside += (sender, e) =>
			{
				DisplaySortActionSheet();
			};

			_searchButton = new UIButton(UIButtonType.RoundedRect);
			_searchButton.SetTitle("Search", UIControlState.Normal);
			_searchButton.TouchUpInside += (sender, e) =>
			{
				InitiateSearchForShow();
			};

			_cancelButton = new UIButton(UIButtonType.RoundedRect);
			_cancelButton.SetTitle("Cancel", UIControlState.Normal);
			_cancelButton.Hidden = true;
			_cancelButton.TouchUpInside += (sender, e) =>
			{
				if(_isSyncing)
					CancelSyncFromTrakt();

				if(_isRefreshing)
					CancelRefresh();
			};

			_titleLabel = new UILabel();
			_titleLabel.Font = UIFont.FromName("HelveticaNeue-Light", 24f);
			_titleLabel.TextColor = UIColor.FromRGBA(255, 255, 255, 225);
			_titleLabel.TextAlignment = UITextAlignment.Center;
			_titleLabel.AdjustsFontSizeToFitWidth = true;
			_searchBlurView = new UIVisualEffectView(UIBlurEffect.FromStyle(UIBlurEffectStyle.Dark)) {
				Frame = blurView.Bounds,
				Hidden = true,
			};
			UpdateTitle("TV Shows");

			_settingsButton = new UIButton(UIButtonType.System);
			_settingsButton.SetImage(UIImage.FromBundle("Images/settings_icon").ImageWithRenderingMode(UIImageRenderingMode.AlwaysTemplate), UIControlState.Normal);
			_settingsButton.ImageEdgeInsets = new UIEdgeInsets(20, 20, 20, 20);
			_settingsButton.TintColor = UIApplication.SharedApplication.KeyWindow.TintColor;
			_settingsButton.TouchUpInside += (sender, e) =>
			{
				DisplaySettingsView();
			};

			_addButton = new UIButton(UIButtonType.System);
			_addButton.SetImage(UIImage.FromBundle("Images/plus_icon").ImageWithRenderingMode(UIImageRenderingMode.AlwaysTemplate), UIControlState.Normal);
			_addButton.ImageEdgeInsets = new UIEdgeInsets(20, 20, 20, 20);
			_addButton.TintColor = UIApplication.SharedApplication.KeyWindow.TintColor;
			_addButton.TouchUpInside += (sender, e) =>
			{
				DisplayAddShowView();
			};

			_downloadButton = new UIButton(UIButtonType.RoundedRect) {
				Alpha = .0f,
			};

			_downloadButton.SetTitle("Downloads", UIControlState.Normal);
			//_downloadButton.Layer.BorderWidth = 1f;
			//_downloadButton.Layer.BorderColor = View.TintColor.CGColor;
			//_downloadButton.Layer.CornerRadius = 3f;
			_downloadButton.TouchUpInside += (sender, e) =>
			{
				DisplayDownloadsList();
			};

			_refreshControl = new UIRefreshControl();
			_refreshControl.TintColor = UIColor.White;
			_refreshControl.PrimaryActionTriggered += HandleRefreshControlActivated;

			_showList.AddSubview(_refreshControl);

			_searchBar = new UISearchBar();
			_searchBar.ShowsCancelButton = true;
			_searchBar.SearchBarStyle = UISearchBarStyle.Minimal;

			var a = UITextField.AppearanceWhenContainedIn(typeof(UISearchBar));
			void_objc_msgSendSuper_IntPtr(a.SuperHandle, Selector.GetHandle("setTextColor:"), UIColor.FromRGB(150, 150, 150).Handle);
			void_objc_msgSendSuper_IntPtr(a.SuperHandle, Selector.GetHandle("setFont:"), UIFont.FromName("HelveticaNeue-Light", 16f).Handle);

			_searchBar.TextChanged += (sender, e) =>
			{
				UpdateSearchResults();
			};

			_searchBar.OnEditingStopped += async(sender, e) =>
			{
				await CancelSearch();
			};

			_searchBar.CancelButtonClicked += async(sender, e) =>
			{
				await CancelSearch();
				_searchBar.ResignFirstResponder();
			};

			_searchBlurView.AddSubview(_searchBar);

			AddWithKey(_sortButton, "sort", blurView);
			AddWithKey(_searchButton, "search", blurView);
			AddWithKey(_cancelButton, "cancel", blurView);
			AddWithKey(_titleLabel, "title", blurView);
			AddWithKey(_downloadButton, "download", blurView);
			AddWithKey(_addButton, "add", blurView);
			AddWithKey(_settingsButton, "settings", blurView);

			Add(_showList);
			Add(blurView);
			Add(_statusView);
			Add(_emptyMessage);
			Add(_searchBlurView);

			var y = 10 + statusHeight;
			_horizontalConstraints = AddConstraint("H:|-(0)-[settings(60)]-(-10)-[cancel(0)]-[search(80)]-(>=10)-[download(100)]-(4)-[sort(80)]-(-10)-[add(60)]-(0)-|", blurView);
			AddConstraint("V:|-({0})-[search(28)]-(>=10)-|".Fmt(y), blurView);
			AddConstraint("V:|-({0})-[cancel(==search)]-(>=10)-|".Fmt(y), blurView);
			AddConstraint("V:|-({0})-[title(==search)]-(>=10)-|".Fmt(y), blurView);
			AddConstraint("V:|-({0})-[sort(==search)]-(>=10)-|".Fmt(y), blurView);

			blurView.AddConstraint(NSLayoutConstraint.Create(_titleLabel, NSLayoutAttribute.CenterX, NSLayoutRelation.Equal, blurView, NSLayoutAttribute.CenterX, 1, 0));
			blurView.AddConstraint(NSLayoutConstraint.Create(_downloadButton, NSLayoutAttribute.CenterY, NSLayoutRelation.Equal, _addButton, NSLayoutAttribute.CenterY, 1, 0));
			blurView.AddConstraint(NSLayoutConstraint.Create(_downloadButton, NSLayoutAttribute.Height, NSLayoutRelation.Equal, _sortButton, NSLayoutAttribute.Height, 1, 0));

			blurView.AddConstraint(NSLayoutConstraint.Create(_addButton, NSLayoutAttribute.CenterY, NSLayoutRelation.Equal, _sortButton, NSLayoutAttribute.CenterY, 1, 0));
			blurView.AddConstraint(NSLayoutConstraint.Create(_addButton, NSLayoutAttribute.Height, NSLayoutRelation.Equal, _addButton, NSLayoutAttribute.Width, 1, 0));

			blurView.AddConstraint(NSLayoutConstraint.Create(_settingsButton, NSLayoutAttribute.CenterY, NSLayoutRelation.Equal, _searchButton, NSLayoutAttribute.CenterY, 1, 0));
			blurView.AddConstraint(NSLayoutConstraint.Create(_settingsButton, NSLayoutAttribute.Height, NSLayoutRelation.Equal, _settingsButton, NSLayoutAttribute.Width, 1, 0));

			_refreshControl.Subviews[0].Transform = CGAffineTransform.MakeTranslation(0, blurView.Bounds.Height + 4);

			await InitializeDatabase();
			UpdateViewState();

			if(AppDelegate.Instance.Client.IsConnected)
				HandleClientStateChanged(null, MCSessionState.Connected);

			//Load the latest episodes for sorting
			foreach(var s in ViewModel.Shows)
			{
				await s.GetNextEpisodeFromCache();
				await s.GetPreviousEpisodeFromCache();
			}

			IsBusyChanged();
			AppDelegate.Instance.Client.SendMessage(MessageAction.ActiveDownloads);

			AppDelegate.Instance.EnsureShowsRefreshed();
		}

		protected override void ViewDidEnterBackground()
		{
			base.ViewDidEnterBackground();

			#pragma warning disable CS4014
			UpdateConnectionState(false);
			#pragma warning restore CS4014
		}

		public override void ViewWillAppear(bool animated)
		{
			AppDelegate.Instance.Client.SendMessage(MessageAction.ActiveDownloads);
			base.ViewWillAppear(animated);
		}

		#endregion

		#region Host Connection

		void HandleClientDataReceived(object sender, MessagePayload e)
		{
			BeginInvokeOnMainThread(() =>
			{
				switch(e.Action)
				{
					case MessageAction.ActiveDownloads:
						var list = e.GetPayload<List<DownloadResponse>>();
						UIApplication.SharedApplication.ApplicationIconBadgeNumber = 0;
						_downloadButton.SetTitle("Downloads", UIControlState.Normal);

					if(_badge == null)
					{
						_badge = new JSBadgeView()
						{
							BadgeBackgroundColor = View.TintColor,
							BadgeStrokeColor = View.TintColor,
							BadgeTextColor = UIColor.White,
							BadgeTextFont = UIFont.FromName("AvenirNext-Regular", 11),
							BadgeAlignment = JSBadgeView.Alignment.TopRight,
							BadgePositionAdjustment = new CGPoint(-6, 6),
						};

					}

					//View.Add(_badge);
					//_badge.BadgePositionAdjustment = new CGPoint(_downloadButton.Frame.Right - 6, _downloadButton.Frame.Top + 6);
					_downloadButton.Add(_badge);
					_badge.BadgeText = list.Count.ToString();
					_badge.Hidden = list.Count == 0;

					break;
				}
			});
		}

		void HandleClientStateChanged(object sender, MCSessionState e)
		{
			InvokeOnMainThread(async() => {
				await UpdateConnectionState(e == MCSessionState.Connected);
			});

			if(e == MCSessionState.Connected)
			{
				AppDelegate.Instance.Client.SendMessage(MessageAction.ActiveDownloads);
			}
		}

		async Task UpdateConnectionState(bool isConnected)
		{
			const float offAlpha = 0f;
			const float onAlpha = .95f;

			#if DEBUG
			const float alphaDuration = 0.5f;
			#else
			const float alphaDuration = 2.5f;
			#endif

			if(isConnected)
			{
				Console.WriteLine("Connected to host");
				Console.WriteLine("Sending");
				AppDelegate.Instance.Client.SendMessage(MessageAction.ActiveDownloads);
				Console.WriteLine("Done");
				await UIView.AnimateAsync(alphaDuration, () => {
					_downloadButton.Alpha = onAlpha;
				});

				_downloadButton.Alpha = onAlpha;
			}
			else
			{
				Console.WriteLine("Disconnected from host");
				await UIView.AnimateAsync(alphaDuration, () => {
					_downloadButton.Alpha = offAlpha;
				});

				_downloadButton.Alpha = offAlpha;
			}

			if(_downloadListViewController != null)
			{
				_downloadListViewController.DismissViewController(false, null);
			}
		}

		#endregion

		async void HandleRefreshControlActivated(object sender, EventArgs e)
		{
			await RefreshShows();
			_refreshControl.EndRefreshing();
		}

		async void ToggleCancelControls(bool switchh)
		{
			_horizontalConstraints[3].Constant = switchh ? 80 : 0;
			await UIView.AnimateAsync(.25f, View.LayoutIfNeeded);
		}

		void UpdateViewState()
		{
			UpdateButtonVisibility(ViewModel.Shows.Count > 0);

			if(ViewModel.Shows.Count == 0)
			{
				_emptyMessage.Hidden = false;
				_emptyMessage.Text = "You haven't added any TV shows.\nYou can add new shows by clicking the + button in the top right.";
			}
			else
			{
				_emptyMessage.Hidden = true;
			}
		}

		void UpdateButtonVisibility(bool visible)
		{
			const int width = 80;
			InvokeOnMainThread(() =>
			{
				var value = visible ? width : 0.0f;
				if(_horizontalConstraints[5].Constant == value) //sort button
					return;
				
				_horizontalConstraints[5].Constant = value; //search button`
				_horizontalConstraints[9].Constant = value; //add button
			});

			//await UIView.AnimateAsync(5.25f, View.LayoutIfNeeded);
		}

		void UpdateSearchResults()
		{
			ViewModel.SearchMode = true;
			ViewModel.SearchFor(_searchBar.Text);
			_showList.ReloadData();
		}

		protected override void IsBusyChanged()
		{
			if(!IsLayoutInitialized)
				return;

			InvokeOnMainThread(() =>
			{
				if(ViewModel.IsBusy)
				{
					_activity.StartAnimating();
				}
				else
				{
					_activity.StopAnimating();
				}

				_sortButton.Enabled = true;
				_searchButton.Enabled = true;
				_addButton.Enabled = !ViewModel.IsBusy;
				_settingsButton.Enabled = !ViewModel.IsBusy;
				_emptyMessage.Hidden = ViewModel.Shows.Count > 0;
			});

			base.IsBusyChanged();
		}

		void DisplayShowPopover(UICollectionViewCell cell)
		{
			//var actionSheet = UIAlertController.Create(null, null, UIAlertControllerStyle.ActionSheet);
			//var removeAction = UIAlertAction.Create("Remove", UIAlertActionStyle.Destructive, (action) => {
			//	Console.WriteLine("Removed");
			//});

			//actionSheet.AddAction(removeAction);
			//actionSheet.ModalPresentationStyle = UIModalPresentationStyle.Popover;
			//actionSheet.PopoverPresentationController.SourceView = cell;
			//actionSheet.PopoverPresentationController.SourceRect = cell.Bounds;

			//PresentViewControllerAsync(actionSheet, true);
		}

		async Task InitializeDatabase()
		{
			await DataService.Instance.Initialize();
			await ViewModel.LoadCachedShows();

			_showList.ReloadData();
		}

		async Task ClearLibrary()
		{
			await ViewModel.ClearAllShows();

			SDImageCache.SharedImageCache.ClearDisk();
			SDImageCache.SharedImageCache.ClearMemory();

			if(File.Exists(AppSettings.UpcomingShowsPath))
				File.Delete(AppSettings.UpcomingShowsPath);
			
			_showList.ReloadData();
			UpdateViewState();
		}

		#region Refresh

		async public Task RefreshShows()
		{
			if(_isRefreshing || _isSyncing || ViewModel.Shows.Count == 0)
				return;

			if(_titleLabel != null)
			{
				ToggleCancelControls(true);
				View.LayoutIfNeeded();

				_isRefreshing = true;
				_cancelButton.Hidden = false;
				_titleLabel.Text = "Getting a list of updated shows...";
				await ViewModel.RefreshShows((show) => {
					_titleLabel.Text = "Refreshing {0}...".Fmt(show.Title);
				});

				_showList.ReloadData();
				UpdateSortedShows();

				_isRefreshing = false;
				_titleLabel.Text = "Refresh complete";

				ScheduleLocalNotifications();
				UpdateTitle(_title);
				IsBusyChanged();
				_cancelButton.Hidden = true;
				ToggleCancelControls(false);
				//await Task.Delay(3000);
			}
		}

		void ScheduleLocalNotifications()
		{
			UIApplication.SharedApplication.CancelAllLocalNotifications();
			foreach(var show in ViewModel.Shows.Where(s => s.NextEpisode != null && s.NextEpisode.InitialBroadcastDate.HasValue && s.Runtime.HasValue))
			{
				var n = new UILocalNotification();
				n.AlertTitle = $"{show.Title} is about to air {show.NextEpisode.S0E0}";
				n.AlertBody = $"Check out {show.Title} - {show.NextEpisode.Title} - it's going to air in 5 minutes";
				n.FireDate = show.NextEpisode.InitialBroadcastDate.Value.ToLocalTime().AddMinutes(show.Runtime.Value * -1 - 5).ToNSDate();
				UIApplication.SharedApplication.ScheduleLocalNotification(n);
			}
		}

		#endregion

		#region Search

		async void InitiateSearchForShow()
		{
			_searchBlurView.Hidden = false;
			_searchBlurView.Alpha = 0f;
			_searchBar.BecomeFirstResponder();
			_searchBar.Frame = new CGRect(_searchBlurView.Frame.Width - 30, 5 + UIApplication.SharedApplication.StatusBarFrame.Height, _searchBlurView.Frame.Width - 30, _searchBlurView.Frame.Height - 30);

			await UIView.AnimateAsync(.5f, () =>
			{
				_searchBar.Frame = new CGRect(15, _searchBar.Frame.Y, _searchBar.Frame.Width, _searchBar.Frame.Height);
				_searchBlurView.Alpha = 1f;
			});
		}

		async Task CancelSearch()
		{
			await UIView.AnimateAsync(.5f, () =>
			{
				_searchBlurView.Alpha = 0f;
				_searchBar.Frame = new CGRect(_searchBlurView.Frame.Width - 30, _searchBar.Frame.Y, _searchBar.Frame.Width, _searchBar.Frame.Height);
			});

			_searchBar.Text = string.Empty;
			_searchBlurView.Hidden = true;
			ViewModel.SearchMode = false;

			_showList.ReloadData();
		}

		#endregion

		#region Import

		async Task SyncFavorites()
		{
			if(_isRefreshing || _isSyncing)
				return;

			ToggleCancelControls(true);
			View.LayoutIfNeeded();

			SDImageCache.SharedImageCache.ClearDisk();
			SDImageCache.SharedImageCache.ClearMemory();
			_initializedCells.Clear();
			_emptyMessage.Text = string.Empty;
			_titleLabel.Text = "Syncing your watchlist...";
			_sortMode = SortMode.Title;
			_cancelButton.Hidden = false;

			try
			{
				_isSyncing = true;
				await ViewModel.SyncFavoriteShows((show, index) =>
				{
					InvokeOnMainThread(() =>
					{
						if(show == null)
						{
							_showList.ReloadData();
						}
						else
						{
							_titleLabel.Text = "Adding {0}...".Fmt(show.Title);
							if(ViewModel.Shows.Count > 0)
							{
								_showList.InsertItems(new NSIndexPath[] {
									NSIndexPath.FromRowSection(index, 0)
								});
							}
						}
						IsBusyChanged();
					});
				},(show, index) =>
				{
					InvokeOnMainThread(() => {
						_titleLabel.Text = "Removing {0}...".Fmt(show.Title);
						_showList.DeleteItems(new NSIndexPath[] {
							NSIndexPath.FromRowSection(index, 0)
						});
					});
				});

				_titleLabel.Text = "Sync complete";
				ScheduleLocalNotifications();
			}
			catch(Exception e)
			{
				Insights.Report(e, Insights.Severity.Error);
				_titleLabel.Text = "Sync failed";
			}
			finally
			{
				_isSyncing = false;
				IsBusyChanged();
				UpdateViewState();
				_cancelButton.Hidden = true;
				ToggleCancelControls(false);
			}

			if(ViewModel.Shows.Count > _showList.NumberOfItemsInSection(0))
			{
				_showList.InsertItems(new [] {
					NSIndexPath.FromRowSection(ViewModel.Shows.Count - 1, 0)
				});
			}

			IsBusyChanged();
			await Task.Delay(3000);
			_titleLabel.Text = _title;
		}

		void CancelSyncFromTrakt()
		{
			ViewModel.CancelTasks();
			ToggleCancelControls(false);
			_cancelButton.Hidden = true;
		}

		void CancelRefresh()
		{
			ViewModel.CancelTasks();
			ToggleCancelControls(false);
			_cancelButton.Hidden = true;
		}

		#endregion

		#region Reordering

		void DisplaySortActionSheet()
		{
			var alphaAction = UIAlertAction.Create("TV Show Title", UIAlertActionStyle.Default, async(action) =>
			{
				if(_sortMode == SortMode.Title)
					return;

				_sortMode = SortMode.Title;
				await SortByTitle();
			});

			var upcomingAction = UIAlertAction.Create("Upcoming Episodes", UIAlertActionStyle.Default, async(action) =>
			{
				if(_sortMode == SortMode.UpcomingEpisodes)
					return;

				_sortMode = SortMode.UpcomingEpisodes;
				await SortByUpcoming();
			});

			var recentAction = UIAlertAction.Create("Recent Episodes", UIAlertActionStyle.Default, async(action) =>
			{
				if(_sortMode == SortMode.RecentEpisodes)
					return;

				_sortMode = SortMode.RecentEpisodes;
				await SortByRecent();
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

		async Task SortByTitle()
		{
			_titleLabel.Text = "TV Shows";
			await ReorderShowsList(() =>
			{
				ViewModel.Shows = ViewModel.SortListByEpisodeTitle(ViewModel.Shows);
			});
		}

		async Task SortByRecent()
		{
			UpdateTitle("Recently Aired Shows");
			await ReorderShowsList(() =>
			{
				ViewModel.Shows = ViewModel.SortListByRecentEpisodes(ViewModel.Shows);
			});
		}

		async Task SortByUpcoming()
		{
			UpdateTitle("Upcoming Shows");
			await ReorderShowsList(() =>
			{
				ViewModel.Shows = ViewModel.SortListByUpcomgingEpisodes(ViewModel.Shows);
			});
		}

		void UpdateTitle(string title)
		{
			_title = title;
			_titleLabel.Text = _title;
		}

		public async void UpdateSortedShows()
		{
			switch(_sortMode)
			{
				case SortMode.Title:
					await SortByTitle();
					break;
				case SortMode.UpcomingEpisodes:
					await SortByUpcoming();
					break;
				case SortMode.RecentEpisodes:
					await SortByRecent();
					break;
			}
		}

		async Task ReorderShowsList(Action reorder)
		{
			var currentList = ViewModel.Shows.ToList();
			reorder();

			_showList.ScrollRectToVisible(new CGRect(0, 0, 10, 10), false);
			var indexSet = new List<NSIndexPath>();
			await _showList.PerformBatchUpdatesAsync(() =>
			{
				int newIndex = 0;

				foreach(var s in ViewModel.Shows)
				{
					var currentIndex = currentList.IndexOf(currentList.FirstOrDefault(sh => sh.ID == s.ID));
					_showList.MoveItem(currentIndex.ToIndexPath(), newIndex.ToIndexPath());

					if(_showList.IndexPathsForVisibleItems.Any(i => i.Row == newIndex))
					{
						indexSet.Add(newIndex.ToIndexPath());
					}
					newIndex++;
				}
			});

			await _showList.PerformBatchUpdatesAsync(() =>
			{
				_showList.ReloadItems(indexSet.ToArray());
			});
		}

		#endregion

		#region Settings Config

		UIPopoverController _settingsPopover;

		void DisplaySettingsView()
		{
			var content = new SettingsViewController();
			content.OnSyncClicked = async() =>
			{
				_settingsPopover.Dismiss(true);
				await SyncFavorites();
			};

			content.OnRefreshClicked = async() =>
			{
				_settingsPopover.Dismiss(true);
				await RefreshShows();
			};

			content.OnLogOutClicked = async() =>
			{
				_settingsPopover.Dismiss(true);
				await ClearLibrary().ConfigureAwait(false);
				Settings.Instance.TraktUsername = null;
				Settings.Instance.RefreshToken = null;
				Settings.Instance.AuthToken = null;
				await Settings.Instance.Save();

				NSUrlCache.SharedCache.RemoveAllCachedResponses();
				NSHttpCookieStorage.SharedStorage.Cookies.ToList().ForEach(NSHttpCookieStorage.SharedStorage.DeleteCookie);
				TraktService.Instance.Reset();
			};

			_settingsPopover = new UIPopoverController(new UINavigationController(content));
			_settingsPopover.PopoverContentSize = new CGSize(300, 220);
			_settingsPopover.PresentFromRect(_settingsButton.Frame, View, UIPopoverArrowDirection.Up, true);
		}

		#endregion

		#region Add New Show

		void DisplayAddShowView()
		{
			var content = new AddShowSearchViewController();
			content.ModalPresentationStyle = UIModalPresentationStyle.Popover;
			content.PopoverPresentationController.SourceView = _addButton;
			content.PopoverPresentationController.SourceRect = _addButton.Bounds;
			content.PopoverPresentationController.PermittedArrowDirections = UIPopoverArrowDirection.Up;
			content.PreferredContentSize = new CGSize(300, 650);
			PresentViewController(content, true, null);

			content.ViewModel.ExistingShowIds = ViewModel.Shows.Select(s => s.Identifiers.Trakt).ToList();
		
			content.OnShowSelected = async(show) =>
			{
				DismissModalViewController(true);
				_titleLabel.Text = "Adding {0}...".Fmt(show.Title);

				var index = await ViewModel.AddShow(show, _sortMode);

				var oldIndex = (ViewModel.Shows.Count - 1).ToIndexPath();
				_showList.InsertItems(new NSIndexPath[] {
					index.ToIndexPath()
				});

				UpdateViewState();

				if(ViewModel.Shows.Count == 1)
				{
					_showList.ReloadData();
				}

				_titleLabel.Text = "{0} has been added".Fmt(show.Title);
				await Task.Delay(3000);
				_titleLabel.Text = _title;
			};
		}

		#endregion

		void DisplayDownloadsList()
		{
			int visibleRows = 4;
			_downloadListViewController = new DownloadListViewController();
			_downloadListViewController.ModalPresentationStyle = UIModalPresentationStyle.FormSheet;
			var height = DownloadsTableView.DownloadRowCell.Height * visibleRows + DownloadListViewController.HeaderBarHeight;
			_downloadListViewController.PreferredContentSize = new CGSize(1920f * .43f, height);

			PresentViewControllerAsync(_downloadListViewController, true);
		}

		#region CollectionView

		void SelectShow(Show show)
		{
			_detailsViewController = new ShowDetailsViewController();
			_detailsViewController.PreferredContentSize = new CGSize(1920f * .5f, 1080f * .57f);
			_detailsViewController.ModalPresentationStyle = UIModalPresentationStyle.FormSheet;
			_detailsViewController.ShowRemoved = async(toRemove) =>
			{
				if(ViewModel.IsBusy)
				{
					var alert = new UIAlertView("Silver Screen is busy right now", "Please wait until the current task completes before removing this show.", null, "OK");
					alert.Show();
					return;
				}

				var index = ViewModel.Shows.IndexOf(show);
				await ViewModel.RemoveShow(toRemove);

				await DismissViewControllerAsync(true);
				 	
				_showList.DeleteItems(new NSIndexPath[] {
					NSIndexPath.FromRowSection(index, 0)
				});

				UpdateViewState();
			};

			_detailsViewController.ViewModel.Show = show;
			PresentViewControllerAsync(_detailsViewController, true);
		}

		public nint GetItemsCount(UICollectionView collectionView, nint section)
		{
			return ViewModel.Shows.Count;
		}

		public UICollectionViewCell GetCell(UICollectionView collectionView, NSIndexPath indexPath)
		{
			var cell = (ShowGridCell)_showList.DequeueReusableCell(showCellId, indexPath);
			var show = ViewModel.Shows[indexPath.Row];
			UpdateCell(cell, show);
			return cell;
		}

		[Export("collectionView:didSelectItemAtIndexPath:")]
		public void ItemSelected(UICollectionView collectionView, NSIndexPath indexPath)
		{
			var show = ViewModel.Shows[indexPath.Row];
			SelectShow(show);
		}

		void UpdateCell(ShowGridCell cell, Show show)
		{
			cell.TitleLabel.Text = show.Title + "\n";
			cell.Tag = show.Identifiers.Trakt;
			cell.DaysDescLabel.Hidden = true;
			cell.DaysLabel.Hidden = true;
			cell.TodayLabel.Hidden = true;
			cell.ImageView.TintView.Hidden = true;
			cell.ImageView.ImageView.Image = null;
			cell.ImageView.WhiteBorder.Hidden = true;

			cell.OnLongPress = (gridCell) => {
				DisplayShowPopover(gridCell);
			};
			
			switch(_sortMode)
			{
				case SortMode.UpcomingEpisodes:
					if(show.NextEpisode != null && show.NextEpisode.InitialBroadcastDate.HasValue)
					{
						var days = (int)(Math.Round(show.NextEpisode.InitialBroadcastDate.Value.Date.Subtract(DateTime.Today).TotalDays, 0));
						//var hour = int.Parse(show.Airs.Time.Split(":".ToCharArray())[0]);
						var hour = show.PreviousEpisode.InitialBroadcastDate.Value.Hour;
						var atNight = (hour >= 17 && hour <= 24) || (hour >= 0 && hour <= 3);

						if(days == 0)
						{
							cell.TodayLabel.Text = atNight ? "tonight" : "today";
							cell.TodayLabel.Hidden = false;
						}
						else if(days == 1)
						{
							cell.TodayLabel.Text = atNight ? "tomorrow\nnight" : "tomorrow";
							cell.TodayLabel.Hidden = false;
						}
						else
						{
							var tuple = GetUnitAndType(days);
							cell.DaysLabel.Text = tuple.Item1.ToString();
							cell.DaysDescLabel.Text = "{0}{1} from now".Fmt(tuple.Item2, tuple.Item1 == 1 ? string.Empty : "s");
							cell.DaysLabel.Hidden = false;
							cell.DaysDescLabel.Hidden = false;
						}
						cell.ImageView.TintView.Hidden = false;
					}
					break;
				case SortMode.RecentEpisodes:
					if(show.PreviousEpisode != null && show.PreviousEpisode.InitialBroadcastDate.HasValue)
					{
						var days = (int)(Math.Round(DateTime.Today.Subtract(show.PreviousEpisode.InitialBroadcastDate.Value.Date).TotalDays, 0));
						//var hour = int.Parse(show.Airs.Time.Split(":".ToCharArray())[0]);
						var hour = show.PreviousEpisode.InitialBroadcastDate.Value.Hour;
						var atNight = (hour >= 17 && hour <= 24) || (hour >= 0 && hour <= 3);

						if(days == 0)
						{
							cell.TodayLabel.Text = atNight ? "tonight" : "today";
							cell.TodayLabel.Hidden = false;
						}
						else if(days == 1)
						{
							cell.TodayLabel.Text = atNight ? "last night" : "yesterday";
							cell.TodayLabel.Hidden = false;
						}
						else
						{
							var tuple = GetUnitAndType(days);
							cell.DaysLabel.Text = tuple.Item1.ToString();
							cell.DaysDescLabel.Text = "{0}{1} ago".Fmt(tuple.Item2, tuple.Item1 == 1 ? string.Empty : "s");
							cell.DaysLabel.Hidden = false;
							cell.DaysDescLabel.Hidden = false;
						}
						cell.ImageView.TintView.Hidden = false;
					}
					break;
			}

			UpdatePosterImage(cell, show);
		}

		void UpdatePosterImage(ShowGridCell cell, Show show)
		{
			if(show.Images.Poster?.Thumb != null)
			{
				cell.ImageView.ImageView.SetImage(new NSUrl(show.Images?.Poster?.Thumb), null, SDWebImageOptions.HighPriority & SDWebImageOptions.AllowInvalidSSLCertificates, async (image, error, cachetype, imageUrl) => {
					if(error != null)
					{
						Debug.WriteLine(error);
						show.Images.Poster.Thumb = show.Seasons.Where(s => s.Images?.Poster?.Thumb != null).LastOrDefault()?.Images?.Poster?.Thumb;
						await show.Save();
						UpdatePosterImage(cell, show);
						return;
					}

					if(!_initializedCells.Contains(show.Identifiers.Trakt))
						_initializedCells.Add(show.Identifiers.Trakt);

					var visibleCell = _showList.VisibleCells.FirstOrDefault(c => c.Tag == show.Identifiers.Trakt) as ShowGridCell;

					if(visibleCell != null)
					{
						visibleCell.ImageView.ImageView.Frame = new CGRect(cell.Bounds.Width / 2, cell.Bounds.Height / 2, 1, 1);
						await UIView.AnimateAsync(.25f, () => {
							visibleCell.ImageView.ImageView.Frame = visibleCell.ImageViewFrame;
						});

						visibleCell.ImageView.WhiteBorder.Hidden = false;
					}
					cell.ImageView.WhiteBorder.Hidden = cell.ImageView.ImageView.Image == null;
				});
			}
			else
			{
				cell.ImageView.ImageView.Image = UIImage.FromBundle("Images/missing_poster.png");
				cell.ImageView.WhiteBorder.Hidden = false;
			}
		}

		Tuple<int, string> GetUnitAndType(int days)
		{
			var unit = days;
			var unitType = "day";

			if(days >= 6 && days <= 25)
			{
				unit = (int)Math.Round(days / 7f, 0);
				unitType = "week";
			}
			else if(days > 25 && days <= 330)
			{
				unit = (int)Math.Round(days / 30f, 0);
				unitType = "month";
			}
			else if(days > 330)
			{
				unit = (int)Math.Round(days / 365f, 0);
				unitType = "year";
			}

			return Tuple.Create(unit, unitType);
		}

		#endregion

		#region ShowGridCell

		public class ShowGridCell : UICollectionViewCell
		{
			#region Constructors

			public ShowGridCell(IntPtr p) : base(p)
			{
				Initialize();
			}

			#endregion

			#region Properties

			public WhiteBorderImageView ImageView
			{
				get;
				set;
			}

			public UILabel TitleLabel
			{
				get;
				set;
			}

			public UILabel DaysLabel
			{
				get;
				set;
			}

			public UILabel DaysDescLabel
			{
				get;
				set;
			}

			public UILabel TodayLabel
			{
				get;
				set;
			}

			public Action<ShowGridCell> OnLongPress
			{
				get;set;
			}

			public CGRect ImageViewFrame
			{
				get
				{
					return new CGRect(0, 0, 300 * .4f, 450 * .4f);
				}
			}

			#endregion

			void Initialize()
			{
				float imageViewBottom = (float)(_posterImageSize.Height * _posterImageScale);
				float imageViewRight = (float)(_posterImageSize.Width * _posterImageScale);

				ImageView = new WhiteBorderImageView(new CGRect(0, 0, imageViewRight, imageViewBottom));
				ImageView.WhiteBorder.Hidden = true;

				TitleLabel = new UILabel(new CGRect(0, ImageView.Frame.Bottom, imageViewRight, 40));
				TitleLabel.Font = UIFont.FromName("HelveticaNeue-Light", 12f);
				TitleLabel.TextColor = UIColor.FromRGB(107, 107, 107);
				TitleLabel.LineBreakMode = UILineBreakMode.WordWrap;
				TitleLabel.Lines = 2;

				DaysLabel = new UILabel(new CGRect(6, 6, imageViewRight - 12, 70));
				DaysLabel.Font = UIFont.FromName("HelveticaNeue-UltraLight", 90f);
				DaysLabel.TextColor = UIColor.FromRGBA(255, 255, 255, 200);
				DaysLabel.TextAlignment = UITextAlignment.Right;
				DaysLabel.AdjustsFontSizeToFitWidth = true;

				DaysDescLabel = new UILabel(new CGRect(DaysLabel.Frame.Left, DaysLabel.Frame.Bottom, DaysLabel.Frame.Width, 15));
				DaysDescLabel.Font = UIFont.FromName("HelveticaNeue-Light", 12);
				DaysDescLabel.TextAlignment = UITextAlignment.Right;
				DaysDescLabel.TextColor = UIColor.FromRGBA(255, 255, 255, 215);

				TodayLabel = new UILabel(new CGRect(6, ImageView.Frame.Top + 12, imageViewRight - 12, 80));
				TodayLabel.Font = UIFont.FromName("HelveticaNeue-UltraLight", 50f);
				TodayLabel.TextColor = UIColor.White;
				TodayLabel.Lines = 2;
				TodayLabel.LineBreakMode = UILineBreakMode.Clip;
				TodayLabel.TextAlignment = UITextAlignment.Left;
				TodayLabel.AdjustsFontSizeToFitWidth = true;

				ContentView.AddSubview(ImageView);
				ContentView.AddSubview(TitleLabel);
				ContentView.AddSubview(TodayLabel);
				ContentView.AddSubview(DaysLabel);
				ContentView.AddSubview(DaysDescLabel);

				//var gesture = new UILongPressGestureRecognizer((sender) => {
				//	if(sender.State == UIGestureRecognizerState.Began)
				//		OnLongPress?.Invoke(this);
				//});

				//ContentView.AddGestureRecognizer(gesture);
			}
		}

		#endregion
	}

	#region WhiteBorderImageView

	public sealed class WhiteBorderImageView : UIView
	{
		bool _initialized;

		public UIView TintView
		{
			get;
			set;
		}

		public UIImageView ImageView
		{
			get;
			set;
		}

		public WhiteBorderImageView.WhiteBorderView WhiteBorder
		{
			get;
			set;
		}

		public WhiteBorderImageView() : base()
		{
		}

		public WhiteBorderImageView(CGRect frame) : base(frame)
		{
		}

		void Initialize()
		{
			if(_initialized)
				return;

			BackgroundColor = UIColor.Clear;

			ImageView = new UIImageView();
			ImageView.ContentMode = UIViewContentMode.ScaleToFill;
			ImageView.ClipsToBounds = true;

			WhiteBorder = new WhiteBorderImageView.WhiteBorderView();

			TintView = new UIView();
			TintView.BackgroundColor = UIColor.FromRGBA(0, 0, 0, 150);
			TintView.Hidden = true;

			AddSubview(ImageView);
			AddSubview(TintView);
			AddSubview(WhiteBorder);

			_initialized = true;
		}

		public override void SetNeedsDisplay()
		{
			InvokeOnMainThread(() =>
			{
				Initialize();
				ImageView.Frame = Bounds;
				WhiteBorder.Frame = Bounds;
				WhiteBorder.SetNeedsDisplay();

				TintView.Frame = Bounds;
				base.SetNeedsDisplay();
			});
		}

		public class WhiteBorderView : UIView
		{
			public WhiteBorderView() : base()
			{
				BackgroundColor = UIColor.Clear;
			}

			public WhiteBorderView(CGRect frame) : base(frame)
			{
				BackgroundColor = UIColor.Clear;
			}

			public override void Draw(CGRect rect)
			{
				UIColor.FromRGBA(255, 255, 255, 35).SetFill();
				UIGraphics.RectFill(rect);

				var passThru = new CGRect(1, 1, rect.Width - 2, rect.Height - 2);
				var intersection = CGRect.Intersect(passThru, rect);

				UIColor.Clear.SetFill();
				UIGraphics.RectFill(intersection);
			}
		}
	}

	#endregion

	public enum SortMode
	{
		UpcomingEpisodes,
		RecentEpisodes,
		Title,
	}
}