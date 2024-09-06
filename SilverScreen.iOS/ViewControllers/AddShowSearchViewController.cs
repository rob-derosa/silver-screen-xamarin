using System;
using UIKit;
using SilverScreen.iOS.Shared;
using System.Threading.Tasks;
using System.Timers;
using Foundation;
using SDWebImage;
using System.Linq;
using CoreGraphics;
using System.Collections.Generic;
using SilverScreen.Shared;

namespace SilverScreen.iOS
{
	public class AddShowSearchViewController : BaseViewController<AddShowSearchViewModel>, IUITableViewDataSource, IUITableViewDelegate
	{
		static readonly nfloat _posterImageScale = .25f;
		static readonly CGSize _posterImageSize = new CGSize(300, 450);
		static NSString showCellId = new NSString("ShowCell");
		UIImage _noThumbImage = UIImage.FromBundle("Images/missing_poster.png");
		static readonly nfloat _margin = 10f;
		readonly List<int> _initializedCells = new List<int>();
		UISearchBar _searchBar;
		UILabel _emptyLabel;
		UIActivityIndicatorView _activity;
		UITableView _showList;
		Timer _timer;

		public Action<Show> OnShowSelected
		{
			get;
			set;
		}

		protected override async void LayoutInterface()
		{
			ViewModel.SearchResults.Clear();
			base.LayoutInterface();

			_searchBar = new UISearchBar();
			_searchBar.SearchBarStyle = UISearchBarStyle.Minimal;
			//_searchBar.KeyboardAppearance = UIKeyboardAppearance.Dark;

			_showList = new UITableView {
				BackgroundColor = UIColor.Clear,
				Delegate = this,
				DataSource = this,
			};
			_showList.RegisterClassForCellReuse(typeof(SearchResultCell), showCellId);

			_emptyLabel = new UILabel {
				Font = UIFont.FromName("HelveticaNeue-Light", 24f),
				TextColor = UIColor.FromRGB(40, 40, 40),
				TextAlignment = UITextAlignment.Center,
				LineBreakMode = UILineBreakMode.WordWrap,
				Lines = 3,
				AdjustsFontSizeToFitWidth = true,
			};

			_activity = new UIActivityIndicatorView(UIActivityIndicatorViewStyle.Gray) {
				HidesWhenStopped = true,
			};

			AddWithKey(_searchBar, "search");
			AddWithKey(_showList, "list");
			AddWithKey(_emptyLabel, "label");
			AddWithKey(_activity, "activity");

			AddConstraint("H:|-(10)-[search]-(10)-|");
			AddConstraint("H:|-(10)-[list(280)]-(10)-|");
			AddConstraint("V:|-(10)-[search]-[list]-(10)-|");

			View.AddConstraint(NSLayoutConstraint.Create(_emptyLabel, NSLayoutAttribute.CenterY, NSLayoutRelation.Equal, View, NSLayoutAttribute.CenterY, 1, 0));
			View.AddConstraint(NSLayoutConstraint.Create(_activity, NSLayoutAttribute.CenterY, NSLayoutRelation.Equal, View, NSLayoutAttribute.CenterY, 1, 0));
			View.AddConstraint(NSLayoutConstraint.Create(_emptyLabel, NSLayoutAttribute.CenterX, NSLayoutRelation.Equal, View, NSLayoutAttribute.CenterX, 1, 0));
			View.AddConstraint(NSLayoutConstraint.Create(_emptyLabel, NSLayoutAttribute.Width, NSLayoutRelation.Equal, View, NSLayoutAttribute.Width, 1, -40));
			View.AddConstraint(NSLayoutConstraint.Create(_activity, NSLayoutAttribute.Right, NSLayoutRelation.Equal, _emptyLabel, NSLayoutAttribute.Left, 1, 0));

			_searchBar.TextChanged += (sender, e) =>
			{
				ViewModel.SearchQuery = _searchBar.Text;
				_showList.Hidden = true;
				_emptyLabel.Hidden = false;
				_emptyLabel.Text = "Searching...";

				if(string.IsNullOrWhiteSpace(ViewModel.SearchQuery))
				{
					ViewModel.SearchResults.Clear();
					_showList.ReloadData();
					UpdateViewState();
					return;
				}

				if(_timer == null)
				{
					_timer = new Timer {
						Interval = 500,
					};

					_timer.Elapsed += (sender2, e2) =>
					{
						_timer.Stop();
						UpdateSearchResults();
					};
				}

				_timer.Stop();
				_timer.Start();
			};

			await Task.Delay(300);
			_searchBar.BecomeFirstResponder();
			UpdateViewState();
		}

		public override void ViewWillAppear(bool animated)
		{
			ViewModel.SearchQuery = null;
			base.ViewWillAppear(animated);
		}

		void UpdateViewState()
		{
			if(_emptyLabel == null)
				return;
			
			if(string.IsNullOrWhiteSpace(ViewModel.SearchQuery))
			{
				_emptyLabel.Text = "Enter a search term above";
			}
			else
			{
				_emptyLabel.Text = "Your search for '{0}' yielded no results".Fmt(ViewModel.SearchQuery);
			}

			_showList.Hidden = ViewModel.SearchResults.Count == 0 || string.IsNullOrWhiteSpace(ViewModel.SearchQuery);
			_emptyLabel.Hidden = !_showList.Hidden;
		}

		async void UpdateSearchResults()
		{
			_initializedCells.Clear();
			ViewModel.SearchResults.Clear();

			BeginInvokeOnMainThread(() =>
			{
				_emptyLabel.Text = "Searching...";
				//_activity.StartAnimating();
				_showList.ReloadData();
			});

			await ViewModel.SearchShows();

			BeginInvokeOnMainThread(() =>
			{
				UpdateViewState();
				//_activity.StopAnimating();
				_showList.ReloadData();
				_showList.LayoutIfNeeded();
			});

			ViewModel.CancellationToken.ThrowIfCancellationRequested();
		}

		#region ListView

		[Export("tableView:heightForRowAtIndexPath:")]
		public nfloat GetHeightForRow(UIKit.UITableView tableView, Foundation.NSIndexPath indexPath)
		{
			return _posterImageSize.Height * _posterImageScale + (_margin * 2);
		}

		[Export("tableView:didSelectRowAtIndexPath:")]
		public void RowSelected(UITableView tableView, NSIndexPath indexPath)
		{
			var show = ViewModel.SearchResults[indexPath.Row];

			if(OnShowSelected != null)
				OnShowSelected(show);

			_showList.DeselectRow(indexPath, true);
		}

		public nint RowsInSection(UITableView tableView, nint section)
		{
			return ViewModel.SearchResults.Count;
		}

		public UITableViewCell GetCell(UITableView tableView, NSIndexPath indexPath)
		{
			if(ViewModel.SearchResults.Count <= indexPath.Row)
				return null;

			var cell = (SearchResultCell)_showList.DequeueReusableCell(showCellId, indexPath);
			var show = ViewModel.SearchResults[indexPath.Row];
			UpdateCell(cell, show);
			return cell;
		}

		void UpdateCell(SearchResultCell cell, Show show)
		{
			cell.TitleLabel.Text = show.Title + "\n";
			cell.SummaryLabel.Text = show.Overview + "\n";
			cell.Tag = show.Identifiers.Trakt;
			cell.ImageView.Image = null;
			cell.ImageView.Frame = cell.ImageViewFrame;
			cell.BackgroundColor = UIColor.Clear;
			cell.SelectedBackgroundView = new UIView();
			cell.SelectedBackgroundView.BackgroundColor = UIColor.FromRGBA(0, 0, 0, 30);

			if(show.Images?.Poster?.Thumb != null)
			{
				cell.ImageView.SetImage(new NSUrl(show.Images?.Poster?.Thumb), null, SDWebImageOptions.HighPriority, (image, error, cachetype, imageUrl) =>
				{
					if(cell.ImageView.Image == null)
					{
						cell.ImageView.Image = _noThumbImage;
					}
					//if(!_initializedCells.Contains(show.Identifiers.Trakt))
					//{
					//	_initializedCells.Add(show.Identifiers.Trakt);
					//	cell.ImageView.Frame = new CGRect(cell.ImageViewFrame.Right / 2, cell.Bounds.Bottom / 2, 1, 1);
					//	await UIView.AnimateAsync(.25f, () =>
					//	{
					//		cell.ImageView.Frame = cell.ImageViewFrame;
					//	});
					//}
				});
			}
			else
			{
				cell.ImageView.Image = _noThumbImage;
			}
		}

		#endregion

		#region SearchResultCell

		public class SearchResultCell : UITableViewCell
		{
			#region Constructors

			public SearchResultCell()
			{
				Initialize();
			}

			public SearchResultCell(IntPtr p) : base(p)
			{
				Initialize();
			}

			#endregion

			#region Properties

			public new UIImageView ImageView
			{
				get;
				set;
			}

			public UILabel TitleLabel
			{
				get;
				set;
			}

			public UILabel SummaryLabel
			{
				get;
				set;
			}

			public CGRect ImageViewFrame
			{
				get
				{
					return new CGRect(0, 0, _posterImageSize.Width * _posterImageScale, _posterImageSize.Height * _posterImageScale);
				}
			}

			#endregion

			public override void LayoutSubviews()
			{
				float imageViewBottom = (float)(_posterImageSize.Height * _posterImageScale);
				float imageViewRight = (float)(_posterImageSize.Width * _posterImageScale);

				ImageView.Frame = new CGRect(_margin, _margin, imageViewRight, imageViewBottom);
				TitleLabel.Frame = new CGRect(ImageView.Frame.Right + _margin, _margin, Frame.Width - ImageView.Frame.Width - (_margin * 2), 20);
				SummaryLabel.Frame = new CGRect(TitleLabel.Frame.Left, TitleLabel.Frame.Bottom, TitleLabel.Frame.Width, ImageView.Frame.Height - TitleLabel.Frame.Height);
				base.LayoutSubviews();
			}

			void Initialize()
			{
				ImageView = new UIImageView
				{
					ContentMode = UIViewContentMode.ScaleToFill,
					ClipsToBounds = true,
				};

				TitleLabel = new UILabel();
				TitleLabel.Font = UIFont.FromName("HelveticaNeue-Bold", 12f);
				TitleLabel.TextColor = UIColor.FromRGB(20, 20, 20);

				SummaryLabel = new UILabel {
					Font = UIFont.FromName("HelveticaNeue-Light", 10),
					Lines = 10,
					LineBreakMode = UILineBreakMode.WordWrap,
					TextAlignment = UITextAlignment.Left,
					TextColor = UIColor.FromRGB(40, 40, 40),
				};

				SummaryLabel.SizeToFit();

				ContentView.AddSubview(ImageView);
				ContentView.AddSubview(TitleLabel);
				ContentView.AddSubview(SummaryLabel);
			}
		}

		#endregion
	}
}

