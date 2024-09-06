using System;
using System.Collections.Generic;
using System.Linq;
using CoreGraphics;
using Foundation;
using SDWebImage;
using UIKit;
using SilverScreen.iOS.Shared;
using SilverScreen.Shared;

namespace SilverScreen.iOS
{
	public class DownloadTableViewController : BaseViewController<BaseViewModel>
	{
		#region Properties and Fields

		public bool IsLoading
		{
			get;
			set;
		}

		public bool HasDownloads
		{
			get
			{
				return Table.Downloads != null && Table.Downloads.Count > 0;
			}
		}

		public DownloadsTableView Table
		{
			get;
			set;
		}

		public UILabel EmptyMessage
		{
			get;
			set;
		}

		public UILabel LoadingMessage
		{
			get;
			set;
		}

		public UIActivityIndicatorView Activity
		{
			get;
			set;
		}

		#endregion

		public DownloadTableViewController(CGRect frame)
		{
			View.Frame = frame;
			InitializeInterface();
		}

		public void StartLoading(string loadingMessage)
		{
			IsLoading = true;
			Activity.StartAnimating();
			LoadingMessage.Hidden = false;
			LoadingMessage.Text = loadingMessage;

			Table.Hidden = true;
			EmptyMessage.Hidden = true;
		}

		public void StopLoading(string emptyMessage)
		{
			Activity.StopAnimating();
			LoadingMessage.Hidden = true;

			EmptyMessage.Hidden = string.IsNullOrWhiteSpace(emptyMessage);
			EmptyMessage.Text = emptyMessage;

			Table.Hidden = !EmptyMessage.Hidden;
			IsLoading = false;
		}

		void InitializeInterface()
		{
			Table = new DownloadsTableView();

			EmptyMessage = new UILabel {
				Font = UIFont.FromName("HelveticaNeue-UltraLight", 36f),
				TextColor = UIColor.White,
				TextAlignment = UITextAlignment.Center,
				Lines = 3,
				Alpha = .2f,
				TranslatesAutoresizingMaskIntoConstraints = false,
			};

			LoadingMessage = new UILabel {
				Font = UIFont.FromName("HelveticaNeue-UltraLight", 36f),
				TextColor = UIColor.White,
				TextAlignment = UITextAlignment.Center,
				Lines = 3,
				Alpha = .2f,
				TranslatesAutoresizingMaskIntoConstraints = false,
			};

			Activity = new UIActivityIndicatorView(UIActivityIndicatorViewStyle.WhiteLarge) {
				HidesWhenStopped = true,
				Alpha = .3f,
				TranslatesAutoresizingMaskIntoConstraints = false,
			};

			Add(Table);
			Add(EmptyMessage);
			Add(LoadingMessage);
			Add(Activity);
		}

		protected override void LayoutInterface()
		{
			Table.Frame = new CGRect(0, 0, View.Bounds.Width, View.Bounds.Height);

			View.AddConstraint(NSLayoutConstraint.Create(LoadingMessage, NSLayoutAttribute.CenterX, NSLayoutRelation.Equal, View, NSLayoutAttribute.CenterX, 1, 0));
			View.AddConstraint(NSLayoutConstraint.Create(LoadingMessage, NSLayoutAttribute.CenterY, NSLayoutRelation.Equal, View, NSLayoutAttribute.CenterY, 1, 0));
			View.AddConstraint(NSLayoutConstraint.Create(Activity, NSLayoutAttribute.CenterY, NSLayoutRelation.Equal, View, NSLayoutAttribute.CenterY, 1, 0));
			View.AddConstraint(NSLayoutConstraint.Create(Activity, NSLayoutAttribute.Right, NSLayoutRelation.Equal, LoadingMessage, NSLayoutAttribute.Left, 1, -20));

			View.AddConstraint(NSLayoutConstraint.Create(EmptyMessage, NSLayoutAttribute.CenterX, NSLayoutRelation.Equal, View, NSLayoutAttribute.CenterX, 1, 0));
			View.AddConstraint(NSLayoutConstraint.Create(EmptyMessage, NSLayoutAttribute.CenterY, NSLayoutRelation.Equal, View, NSLayoutAttribute.CenterY, 1, 0));
		}
	}

	#region DownloadsTableView

	public class DownloadsTableView : UITableView, IUITableViewDataSource, IUITableViewDelegate
	{
		UIImage _noThumbImage = UIImage.FromBundle("Images/missing_poster.png");
		UIImage _noScreenImage = UIImage.FromBundle("Images/missing_screen.png");

		public event EventHandler<DownloadResponse> RowSelected;
		public event EventHandler<DownloadResponse> RowDownloadClicked;
		public event EventHandler<DownloadResponse> RowDeleted;

		static NSString episodeCell = new NSString("EpisodeCell");

		public List<DownloadResponse> Downloads
		{
			get;
			set;
		}

		public string DestructionText
		{
			get;
			set;
		}

		public DownloadsTableView(CGRect frame) : base(frame)
		{
			Initialize();
		}

		public DownloadsTableView()
		{
			Initialize();
		}

		void Initialize()
		{
			BackgroundColor = UIColor.Clear;
			SeparatorColor = UIColor.FromRGBA(255, 255, 255, 25);
			SeparatorInset = new UIEdgeInsets(0, 0, 0, 0);
			Delegate = this;
			DataSource = this;

			RegisterClassForCellReuse(typeof(DownloadRowCell), episodeCell);
		}

		void SelectShow(Show show)
		{
		}

		void HandleStatusButtonClicked(object sender, EventArgs e)
		{
			var traktId = ((UIButton)sender).Tag.ToString();
			var download = Downloads.FirstOrDefault(r => r.Request.EpisodeTraktID == traktId);
			RowDownloadClicked?.Invoke(this, download);
		}

		[Export("tableView:didSelectRowAtIndexPath:")]
		public void OnRowSelected(UITableView tableView, NSIndexPath indexPath)
		{
			var download = Downloads[indexPath.Row];
			RowSelected?.Invoke(this, download);
		}

		[Export("tableView:canEditRowAtIndexPath:")]
		public bool CanEditRow(UIKit.UITableView tableView, Foundation.NSIndexPath indexPath)
		{
			return true;
		}

		[Export("tableView:editActionsForRowAtIndexPath:")]
		public UITableViewRowAction[] EditActionsForRow(UIKit.UITableView tableView, NSIndexPath indexPath)
		{
			var action = UITableViewRowAction.Create(UITableViewRowActionStyle.Default, DestructionText, (act, index) =>
			{
				var download = Downloads[indexPath.Row];
				RowDeleted?.Invoke(this, download);
				SetEditing(false, true);
			});

			action.BackgroundColor = UIColor.FromRGBA(185, 0, 8, 50);
			return new [] { action };
		}

		[Export("tableView:heightForRowAtIndexPath:")]
		public nfloat GetHeightForRow(UIKit.UITableView tableView, NSIndexPath indexPath)
		{
			return DownloadRowCell.Height;
		}

		public nint RowsInSection(UITableView tableView, nint section)
		{
			if(Downloads == null)
				return 0;

			return Downloads.Count;
		}

		public void UpdateDownload(DownloadResponse response)
		{
			if(Downloads == null)
				return;

			var download = Downloads.FirstOrDefault(r => r.Request.EpisodeTraktID == response.Request.EpisodeTraktID);

			if(download == null)
				return;

			var index = Downloads.IndexOf(download);
			var path = NSIndexPath.FromRowSection(index, 0);

			if(IndexPathsForVisibleRows.Any(p => p.Row == path.Row && p.Section == path.Section))
			{
				var cell = CellAt(path);
				UpdateCell((DownloadRowCell)cell, response);
			}
		}

		public UITableViewCell GetCell(UITableView tableView, NSIndexPath indexPath)
		{
			var cell = (DownloadRowCell)DequeueReusableCell(episodeCell, indexPath);
			var download = Downloads[indexPath.Row];
			UpdateCell(cell, download);

			return cell;
		}

		void UpdateCell(DownloadRowCell cell, DownloadResponse download)
		{
			cell.TitleLabel.Text = "{0}: {1}".Fmt(download.Request.ShowTitle, download.Request.Episode.Title);
			cell.DescriptionLabel.Text = download.Request.Episode.Overview;
			cell.MetadataLabel.Text = download.Request.Episode.S0E0;
			cell.StatusImage.Tag = nint.Parse(download.Request.EpisodeTraktID);

			if(!string.IsNullOrWhiteSpace(download.Request.Episode.FormattedBroadcastDate))
				cell.MetadataLabel.Text += " - " + download.Request.Episode.FormattedBroadcastDate;

			cell.PosterImageView.ImageView.Image = null;
			cell.PosterImageView.WhiteBorder.Hidden = true;

			cell.StatusImage.TouchUpInside -= HandleStatusButtonClicked;
			cell.StatusImage.TouchUpInside += HandleStatusButtonClicked;

			switch(download.State)
			{
				case DownloadState.None:
					cell.Activity.StopAnimating();
					cell.StatusLabel.Text = "download";

					cell.StatusImage.Hidden = false;
					cell.StatusImage.SetImage(UIImage.FromBundle("Images/download_icon.png"), UIControlState.Normal);

					break;
				case DownloadState.Enqueued:
					cell.StatusLabel.Text = download.State.ToString().ToLower();
					cell.Activity.StopAnimating();
					break;
				case DownloadState.Complete:
					cell.Activity.StopAnimating();
					cell.StatusImage.Hidden = false;

					if(download.Result == DownloadResult.Success)
					{
						cell.StatusImage.SetImage(UIImage.FromBundle("Images/thumbsup_icon.png"), UIControlState.Normal);
						cell.StatusLabel.Text = "completed";
					}
					else
					{
						cell.StatusImage.SetImage(UIImage.FromBundle("Images/thumbsdown_icon.png"), UIControlState.Normal);
						cell.StatusLabel.Text = string.IsNullOrWhiteSpace(download.FailureReason) ? download.Result.ToString().ToLower() : download.FailureReason.ToLower();
					}

					break;

				case DownloadState.AddingToiTunes:
					cell.StatusImage.Hidden = true;
					cell.StatusLabel.Text = "adding to iTunes...";
					cell.Activity.StartAnimating();
					break;
				case DownloadState.Converting:
					cell.Activity.StartAnimating();
					cell.StatusImage.Hidden = true;
					cell.StatusLabel.Text = "{0} {1}...".Fmt(download.State.ToString().ToLower(), download.Progress);
					break;

				case DownloadState.Downloading:
					cell.Activity.StartAnimating();
					cell.StatusLabel.Text = "{0} {1:P}...".Fmt(download.State.ToString().ToLower(), download.PercentComplete);
					cell.StatusImage.Hidden = true;

					break;

				default:
					cell.Activity.StartAnimating();
					cell.StatusImage.Hidden = true;
					cell.StatusLabel.Text = download.State.ToString().ToLower() + "...";
					break;
			}

			cell.ScreenImageView.SetEpisodeImage(download.Request.Episode, download.Request.Show);

			cell.PosterImageView.WhiteBorder.Hidden = false;
			if(download.Request.Show.Images?.Poster?.Thumb != null)
			{
				cell.PosterImageView.ImageView.SetImage(new NSUrl(download.Request.Show.Images?.Poster?.Thumb), null, SDWebImageOptions.HighPriority, (image, error, cachetype, imageUrl) =>
				{
					if(error != null)
					{
						download.Request.Show.Images.Poster.Thumb = download.Request.Show.Seasons.Where(s => s.Images.Poster?.Thumb != null).LastOrDefault()?.Images?.Poster?.Thumb;
						return;
					}
					cell.PosterImageView.WhiteBorder.Hidden = cell.PosterImageView.ImageView.Image == null;
				});

				if(cell.PosterImageView.ImageView.Image == null)
					cell.PosterImageView.ImageView.Image = _noThumbImage;
			}
			else
			{
				cell.PosterImageView.ImageView.Image = _noThumbImage;
			}
		}

		#region DownloadRowCell

		static readonly nfloat _posterImageScale = .2f;
		static readonly CGSize _posterImageSize = new CGSize(300, 450);

		public class DownloadRowCell : UITableViewCell
		{
			public DownloadRowCell(IntPtr p) : base(p)
			{
				Initialize();
			}

			#region Properties

			public static readonly nfloat Margin = 10f;
			public static readonly nfloat Height = _posterImageSize.Height * _posterImageScale + (Margin * 2);

			public WhiteBorderImageView PosterImageView
			{
				get;
				set;
			}

			public WhiteBorderImageView ScreenImageView
			{
				get;
				set;
			}

			public UILabel TitleLabel
			{
				get;
				set;
			}

			public UILabel StatusLabel
			{
				get;
				set;
			}

			public UIButton StatusImage
			{
				get;
				set;
			}

			public UILabel SubtitleLabel
			{
				get;
				set;
			}

			public UILabel MetadataLabel
			{
				get;
				set;
			}

			public UIView MetadataView
			{
				get;
				set;
			}

			public UIActivityIndicatorView Activity
			{
				get;
				set;
			}

			public UIProgressView Progress
			{
				get;
				set;
			}

			public UILabel DescriptionLabel
			{
				get;
				set;
			}

			#endregion

			void Initialize()
			{
				float imageViewBottom = (float)(_posterImageSize.Height * _posterImageScale);
				float imageViewRight = (float)(_posterImageSize.Width * _posterImageScale);
				const float percWidth = .75f;

				BackgroundColor = UIColor.Clear;
				var view = new UIView(Bounds) {
					BackgroundColor = UIColor.FromRGBA(0, 0, 0, 50)
				};

				SelectedBackgroundView = view;

				PosterImageView = new WhiteBorderImageView(new CGRect(Margin, Margin, imageViewRight, imageViewBottom));
				PosterImageView.WhiteBorder.Hidden = true;

				var screenWidth = Height * 1.5f;
				ScreenImageView = new WhiteBorderImageView(new CGRect(Bounds.Right - screenWidth - Margin, Margin, screenWidth, imageViewBottom));
				ScreenImageView.WhiteBorder.Hidden = true;

				const int metaScreenHeight = 30;
				MetadataView = new UIView(new CGRect(ScreenImageView.Frame.X, ScreenImageView.Frame.Bottom - metaScreenHeight, ScreenImageView.Frame.Width, metaScreenHeight));
				MetadataView.BackgroundColor = UIColor.FromRGBA(0, 0, 0, 150);

				TitleLabel = new UILabel(new CGRect(PosterImageView.Frame.Right + 20, PosterImageView.Frame.Top - 4, (Bounds.Width * percWidth) - PosterImageView.Frame.Width - Margin * 2, 22)) {
					Font = UIFont.FromName("HelveticaNeue-Thin", 16f),
					TextColor = UIColor.White,
					AdjustsFontSizeToFitWidth = true
				};

				MetadataLabel = new UILabel(new CGRect(TitleLabel.Frame.X, TitleLabel.Frame.Bottom + 2, TitleLabel.Frame.Width, 16)) {
					Font = UIFont.FromName("HelveticaNeue", 12f),
					TextColor = UIColor.White,
				};

				DescriptionLabel = new UILabel(new CGRect(TitleLabel.Frame.X, MetadataLabel.Frame.Bottom, TitleLabel.Frame.Width, Bounds.Height - (MetadataLabel.Frame.Bottom + 5))) {
					Font = UIFont.FromName("HelveticaNeue-Thin", 11f),
					TextColor = UIColor.White,
					LineBreakMode = UILineBreakMode.WordWrap,
					Lines = 3
				};

				Activity = new UIActivityIndicatorView(UIActivityIndicatorViewStyle.White);
				Activity.HidesWhenStopped = true;
				Activity.Frame = new CGRect(Margin, MetadataView.Frame.Height / 2 - 8, 16, 16);

				StatusLabel = new UILabel(new CGRect(Activity.Frame.Right + 10, MetadataView.Frame.Height / 2 - 11, MetadataView.Frame.Width - Margin * 2 - Activity.Frame.Width - 4, 20)) {
					Font = UIFont.FromName("HelveticaNeue", 12f),
					TextColor = UIColor.White,
					TextAlignment = UITextAlignment.Right,
					AdjustsFontSizeToFitWidth = true,
					BaselineAdjustment = UIBaselineAdjustment.AlignBaselines
				};

				StatusImage = new UIButton(UIButtonType.Custom) {
					Frame = new CGRect(Activity.Frame.X, MetadataView.Frame.Height / 2 - 13, 24, 24),
					ContentMode = UIViewContentMode.ScaleAspectFill,
					Alpha = .55f,
					ClipsToBounds = true
				};

				ContentView.AddSubview(PosterImageView);
				ContentView.AddSubview(ScreenImageView);
				ContentView.AddSubview(MetadataView);
				ContentView.AddSubview(TitleLabel);
				ContentView.AddSubview(MetadataLabel);
				ContentView.AddSubview(DescriptionLabel);
				MetadataView.AddSubview(Activity);
				MetadataView.AddSubview(StatusImage);
				MetadataView.AddSubview(StatusLabel);
			}
		}

		#endregion
	}

	#endregion

	public enum ViewOptions
	{
		None = -1,
		ActiveDownloads = 0,
		PreviousDownloads = 1,
		MissingDownloads = 2,
	}
}