using System;
using UIKit;
using SilverScreen.iOS.Shared;
using Foundation;
using CoreGraphics;
using System.Threading.Tasks;
using System.Threading;
using SDWebImage;

namespace SilverScreen.iOS
{
	public class ShowCastHorizontalViewController : BaseViewController<ShowDetailsViewModel>, IUITableViewDataSource, IUITableViewDelegate
	{
		static NSString _cellId = new NSString("CastMemberCell");
		UITableView _castList;

		protected override void LayoutInterface()
		{
			_castList = new UITableView() {
				DataSource = this,
				Delegate = this,
				AllowsSelection = false,
				ContentInset = new UIEdgeInsets(CastMemberCell.Padding, 0, CastMemberCell.Padding, 0),
				BackgroundColor = UIColor.Clear,
				ShowsHorizontalScrollIndicator = false,
				ShowsVerticalScrollIndicator = false,
				Transform = CGAffineTransform.MakeRotation((nfloat)(-Math.PI / 2)),
				Frame = View.Bounds,
				SeparatorStyle = UITableViewCellSeparatorStyle.None,
				BackgroundView = new UIView {
					BackgroundColor = UIColor.Clear	
				},
			};

			_castList.RegisterClassForCellReuse(typeof(CastMemberCell), _cellId);
			Add(_castList);

			base.LayoutInterface();
			UpdateView();
		}

		public override void ViewWillAppear(bool animated)
		{
			base.ViewWillAppear(animated);
			ViewModel.ResetCancellationToken();
			UpdateView();
		}

		public async Task RefreshData(bool force = false, Action<Season> onSeasonUpdated = null)
		{
			if(ViewModel.Show.CastAndCrew?.Cast == null || ViewModel.Show.CastAndCrew?.Cast.Count == 0 || force)
			{
				if(_castList != null)
					_castList.ReloadData();

				App.NetworkInUse(true);
				await ViewModel.EnsureCastAndCrewLoaded(force);
				App.NetworkInUse(false);
			}
		}

		internal void UpdateView()
		{
			if(!IsLayoutInitialized || ViewModel.Show == null)
				return;

			_castList.ReloadData();
			_castList.LayoutIfNeeded();

			if(_castList.IndexPathForSelectedRow != null)
				RowSelected(_castList, _castList.IndexPathForSelectedRow);
		}

		[Export("tableView:didSelectRowAtIndexPath:")]
		public void RowSelected(UITableView tableView, NSIndexPath indexPath)
		{
		}

		[Export("numberOfSectionsInTableView:")]
		public nint NumberOfSections(UITableView tableView)
		{
			return 1;
		}

		public nint RowsInSection(UITableView tableView, nint section)
		{
			return ViewModel.Show.CastAndCrew?.Cast == null ? 0 : ViewModel.Show.CastAndCrew.Cast.Count;
		}

		[Export("tableView:heightForRowAtIndexPath:")]
		public nfloat GetHeightForRow(UITableView tableView, NSIndexPath indexPath)
		{
			return CastMemberCell.Width;
		}

		public UITableViewCell GetCell(UITableView tableView, NSIndexPath indexPath)
		{
			var castMember = ViewModel.Show.CastAndCrew.Cast[indexPath.Row];
			var cell = (CastMemberCell)_castList.DequeueReusableCell(_cellId, indexPath);

			cell.SelectedBackgroundView = new UIView {
				BackgroundColor = UIColor.FromRGBA(255, 255, 255, 25),
			};

			UpdateCell(cell, castMember);
			return cell;
		}

		void UpdateCell(CastMemberCell cell, CastMember castMember)
		{
			cell.CharacterName.Text = $"as {castMember.Character}";
			cell.ActorName.Text = castMember.Person.Name;
			cell.UpdateHeadshotImage(castMember);
		}

		#region CastMemberCell

		public class CastMemberCell : UITableViewCell
		{
			static nfloat _imageWidth = 175;

			public WhiteBorderImageView HeaderImage
			{
				get;
				set;
			}

			public UILabel CharacterName
			{
				get;
				set;
			}

			public UILabel ActorName
			{
				get;
				set;
			}

			public static nfloat Width
			{
				get
				{
					return _imageWidth + Padding * 2;
				}
			}

			public CastMemberCell(IntPtr p) : base(p)
			{
				Initialize();
			}

			public static nfloat Padding
			{
				get;
				set;
			} = 26;

			public UIImage ResizeImage(UIImage image, CGSize size)
			{
				UIGraphics.BeginImageContextWithOptions(size, false, 0.0f);
				image.Draw(new CGRect(0, 0, size.Width, size.Height));

				var resizedImage = UIGraphics.GetImageFromCurrentImageContext();
				UIGraphics.EndImageContext();
				return resizedImage;
			}

			void Initialize()
			{
				//BackgroundColor = UIColor.FromRGBA(255, 255, 255, 40);
				BackgroundColor = UIColor.Clear;

				HeaderImage = new WhiteBorderImageView();
				HeaderImage.ImageView.ContentMode = UIViewContentMode.ScaleAspectFill;
				HeaderImage.Frame = new CGRect(Padding, Padding + 10, _imageWidth, _imageWidth / .666f);
				HeaderImage.SetNeedsDisplay();

				//double min = Math.Min(HeaderImage.Frame.Width, HeaderImage.Frame.Height);
				//HeaderImage.Layer.CornerRadius = (float)(min / 2.0);
				//HeaderImage.Layer.MasksToBounds = false;
				//HeaderImage.Layer.BorderColor = Theme.OrangeColor.CGColor;
				//HeaderImage.Layer.BorderWidth = 2f;
				//HeaderImage.BackgroundColor = UIColor.Clear;
				//HeaderImage.ClipsToBounds = true;

				ActorName = new UILabel
				{
					Font = UIFont.FromName("HelveticaNeue-Light", 18f),
					TextColor = Theme.LightOrangeColor,
					AdjustsFontSizeToFitWidth = true,
					TextAlignment = UITextAlignment.Center,
					Frame = new CGRect(Padding, HeaderImage.Frame.Bottom + 6, HeaderImage.Frame.Width, 24),
				};

				CharacterName = new UILabel
				{
					Font = UIFont.FromName("HelveticaNeue-UltraLight", 16f),
					TextColor = Theme.OrangeColor,
					TextAlignment = UITextAlignment.Center,
					AdjustsFontSizeToFitWidth = true,
					Frame = new CGRect(Padding, ActorName.Frame.Bottom + 6, HeaderImage.Frame.Width, 20),
				};

				AddSubview(HeaderImage);
				AddSubview(ActorName);
				AddSubview(CharacterName);

				Transform = CGAffineTransform.MakeRotation((nfloat)(Math.PI / 2));
			}

			internal void UpdateHeadshotImage(CastMember castMember)
			{
				if(castMember.Person.Images.Headshot.Medium != null)
				{
					App.NetworkInUse(true);
					HeaderImage.ImageView.SetImage(new NSUrl(castMember.Person.Images.Headshot.Medium), null, SDWebImageOptions.LowPriority & SDWebImageOptions.AllowInvalidSSLCertificates, async (image, error, cachetype, imageUrl) => {
						App.NetworkInUse(false);
						if(error != null)
						{
							Console.WriteLine(error);
							return;
						}

						//var resized = ResizeImage(image, new CGSize(image.Size.Width * .5, image.Size.Height  * .75));
						//HeaderImage.Image = resized;
					});
				}
				else
				{
					//cell.ImageView.ImageView.Image = UIImage.FromBundle("Images/missing_poster.png");
					//cell.ImageView.WhiteBorder.Hidden = false;
				}
			}
		}


		#endregion
	}
}
