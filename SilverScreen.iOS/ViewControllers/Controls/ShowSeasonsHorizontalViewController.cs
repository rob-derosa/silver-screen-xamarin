using System;
using UIKit;
using SilverScreen.iOS.Shared;
using Foundation;
using CoreGraphics;
using System.Threading.Tasks;
using System.Threading;
using SilverScreen.Shared;
using System.Linq;

namespace SilverScreen.iOS
{
	public class ShowSeasonsHorizontalViewController : BaseViewController<ShowDetailsViewModel>, IUITableViewDataSource, IUITableViewDelegate
	{
		static NSString episodeCellId = new NSString("EpisodeCell");
		UITableView _seasonsList;
		bool _didLayout;

		protected override void LayoutInterface()
		{
			if(_didLayout)
				return;
			
			_seasonsList = new UITableView() {
				DataSource = this,
				Delegate = this,
				AllowsSelection = false,
				ContentInset = new UIEdgeInsets(20, 0, 0, 0),
				BackgroundColor = UIColor.Clear,
				Transform = CGAffineTransform.MakeRotation((nfloat)(Math.PI / 2)),
				Frame = View.Bounds,
				SeparatorStyle = UITableViewCellSeparatorStyle.None,
				BackgroundView = new UIView {
					BackgroundColor = UIColor.Clear	
				},
			};

			_seasonsList.RegisterClassForCellReuse(typeof(EpisodeCell), episodeCellId);
			Add(_seasonsList);

			base.LayoutInterface();
			_didLayout = true;
			UpdateView();

			ScrollToLatest();
		}

		public override void ViewWillAppear(bool animated)
		{
			base.ViewWillAppear(animated);
			ViewModel.ResetCancellationToken();
			UpdateView();
		}

		public async Task RefreshData(bool force = false)
		{
			if(_seasonsList != null)
				_seasonsList.ReloadData();

			var didScroll = false;
			App.NetworkInUse(true);

			await ViewModel.EnsureAllSeasonsLoaded(force, (s) =>
			{
				if(!didScroll)
				{
					if(ScrollToLatest())
						didScroll = true;
				}
			});

			App.NetworkInUse(false);
		}

		bool ScrollToLatest()
		{
			var ep = ViewModel.Show.NextEpisode ?? ViewModel.Show.PreviousEpisode;
			if(ep != null)
			{
				var season = ViewModel.Show.SeasonByNumber(ep.SeasonNumber);
				var row = season.EpisodesReversed.IndexOf(ep);
				var section = ViewModel.Show.SeasonsReversed.IndexOf(season);

				_seasonsList?.ScrollToRow(NSIndexPath.FromRowSection(row, section), UITableViewScrollPosition.Top, true);
				return true;
			}

			return false;
		}

		internal void UpdateView()
		{
			if(!IsLayoutInitialized || ViewModel.Show == null)
				return;

			Console.WriteLine("UpdateView Called");
			_seasonsList.ReloadData();
			_seasonsList.LayoutIfNeeded();

			if(_seasonsList.IndexPathForSelectedRow != null)
				RowSelected(_seasonsList, _seasonsList.IndexPathForSelectedRow);
		}

		[Export("tableView:didSelectRowAtIndexPath:")]
		public void RowSelected(UITableView tableView, NSIndexPath indexPath)
		{
		}

		[Export("numberOfSectionsInTableView:")]
		public nint NumberOfSections(UITableView tableView)
		{
			if(ViewModel.Show.Seasons == null)
				return 0;

			return ViewModel.Show.Seasons.Count;
		}

		public nint RowsInSection(UITableView tableView, nint section)
		{
			var season = ViewModel.Show.SeasonsReversed[(int)section];

			if(season == null || season.Episodes == null)
				return 0;

			return season.Episodes.Count;
		}

		[Export("tableView:heightForHeaderInSection:")]
		public nfloat HeightForHeaderInSection(UITableView tableView, nint section)
		{
			return 1f;
		}

		[Export("tableView:viewForHeaderInSection:")]
		public UIView ViewForHeader(UITableView tableView, nint index)
		{
			var view = new SeasonView();

			var season = ViewModel.Show.SeasonsReversed[(int)index];
			view.TitleLabel.Text = "Season {0}".Fmt(season.Number);

			return view;
		}

		[Export("tableView:heightForRowAtIndexPath:")]
		public nfloat GetHeightForRow(UITableView tableView, NSIndexPath indexPath)
		{
			return EpisodeCell.Width;
		}

		public UITableViewCell GetCell(UITableView tableView, NSIndexPath indexPath)
		{
			var season = ViewModel.Show.SeasonsReversed[indexPath.Section];
			var cell = (EpisodeCell)_seasonsList.DequeueReusableCell(episodeCellId, indexPath);

			cell.SelectedBackgroundView = new UIView {
				BackgroundColor = UIColor.FromRGBA(255, 255, 255, 0),
			};

			if(season.EpisodesReversed.Count > indexPath.Row)
			{
				var episode = season.EpisodesReversed[indexPath.Row];
				UpdateCell(cell, episode);
			}

			return cell;
		}

		void UpdateCell(EpisodeCell cell, Episode episode)
		{
			cell.EpisodeView.ViewModel.SetEpisode(episode, ViewModel.Show);

			if(!cell.EpisodeView.IsLayoutInitialized)
				cell.LayoutIfNeeded();

			cell.EpisodeView.CancelImageLoad();
			cell.EpisodeView.UpdateView(true);
		}

		#region SeasonView

		public class SeasonView : UIView
		{
			#region Constructors

			public SeasonView() : base()
			{
				Initialize();
			}

			public SeasonView(IntPtr p) : base(p)
			{
				Initialize();
			}

			#endregion

			#region Properties

			public UILabel TitleLabel
			{
				get;
				set;
			}

			#endregion

			void Initialize()
			{
				TitleLabel = new UILabel() {
					Font = UIFont.FromName("HelveticaNeue-CondensedBold", 20f),
					TextColor = Theme.OrangeColor,
					Alpha = .65f,
					TextAlignment = UITextAlignment.Right,
					AdjustsFontSizeToFitWidth = true,
				};

				BackgroundColor = UIColor.Clear;
				AddSubview(TitleLabel);

				Transform = CGAffineTransform.MakeRotation((nfloat)(-Math.PI / 2));
			}

			public override void LayoutSubviews()
			{
				TitleLabel.Frame = new CGRect(-190, -10, 160, 60);
				base.LayoutSubviews();
			}
		}

		#endregion

		#region EpisodeCell

		public class EpisodeCell : UITableViewCell
		{
			public EpisodeDetailsViewController EpisodeView
			{
				get;
				set;
			}

			public static nfloat Width
			{
				get
				{
					return 462;
				}
			}

			public EpisodeCell(IntPtr p) : base(p)
			{
				Initialize();
			}

			public nfloat Padding
			{
				get;
				set;
			} = 24;

			void Initialize()
			{
				BackgroundColor = UIColor.Clear;

				EpisodeView = new EpisodeDetailsViewController();
				EpisodeView.View.Frame = new CGRect(Padding, 0, Bounds.Width - Padding * 2, Bounds.Height);
				AddSubview(EpisodeView.View);

				Transform = CGAffineTransform.MakeRotation((nfloat)(-Math.PI / 2));
			}
		}

		#endregion
	}
}
