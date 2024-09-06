using System;
using SilverScreen.iOS.Shared;
using UIKit;
using SilverScreen.Shared;
using MultipeerConnectivity;
using System.Diagnostics;
using CoreGraphics;
using SDWebImage;

namespace SilverScreen.iOS
{
	public class EpisodeDetailsViewController : BaseViewController<EpisodeDetailsViewModel>
	{
		public EpisodeDetailsViewController()
		{
			AppDelegate.Instance.Client.StateChanged += HandleStateChanged;
			AppDelegate.Instance.Client.DataReceived += HandleDataReceived;

			//Force non-singleton
			_viewModel = ViewModel;
			_viewModel = new EpisodeDetailsViewModel();
		}

		#region Properties and Fields

		bool _isImageLoading;
		UILabel _titleLabel;
		UILabel _statusLabel;
		UIImageView _statusImage;
		UIActivityIndicatorView _statusActivity;
		UILabel _dateLabel;
		UITextView _descriptionLabel;
		UIButton _downloadButton;
		UIView _downloadView;
		WhiteBorderImageView _screen;
		UIActivityIndicatorView _screenActivity;

		#endregion

		#region Remote Data

		void HandleDataReceived(object sender, MessagePayload message)
		{
			switch(message.Action)
			{
				case MessageAction.DownloadUpdateInquiry:
					HandleDownloadUpdate(message.GetPayload<DownloadResponse>());
					break;
					
				case MessageAction.HasEpisodeOnDisk:
					try
					{
						var payload = message.GetPayload<EpisodeDownloadedCheck>();
						if(payload.Request.EpisodeTraktID == ViewModel.Episode.Identifiers?.Trakt.ToString())
						{
							HandleHasEpisodeOnDiskUpdate(payload.ExistsLocally);
						}
					}
					catch(Exception e)
					{
						Console.WriteLine(e);
					}

					break;
			}
		}

		void HandleStateChanged(object sender, MCSessionState e)
		{
			BeginInvokeOnMainThread(async() =>
			{
				if(ViewModel.Episode == null)
					return;

				switch(e)
				{
					case MCSessionState.Connected:

						if(ViewModel.Episode != null)
						{
							GetLatestEpisodeState();
						}

						_downloadView.Alpha = .0f;
						_downloadView.Hidden = false;

						await UIView.AnimateAsync(5f, () =>
						{
							_downloadView.Alpha = 1f;
						});

						break;
					case MCSessionState.NotConnected:
						ViewModel.LastResponse = null;
						UpdateDownloadingState();

						await UIView.AnimateAsync(5f, () =>
						{
							_downloadView.Alpha = .0f;
						});
						_downloadView.Hidden = true;
						break;
				}
			});
		}

		#endregion

		protected override void LayoutInterface()
		{
			_titleLabel = new UILabel() {
				Font = UIFont.FromName("HelveticaNeue-Thin", 26f),
				TextColor = UIColor.FromRGBA(255, 255, 255, 225),
				AdjustsFontSizeToFitWidth = true
			};

			_descriptionLabel = new UITextView {
				Font = UIFont.FromName("HelveticaNeue-Light", 12f),
				TextColor = UIColor.FromRGBA(255, 255, 255, 200),
				BackgroundColor = UIColor.Clear,
				Selectable = false,
				UserInteractionEnabled = true,
			};

			_descriptionLabel.TextContainer.MaximumNumberOfLines = 10;
			_descriptionLabel.TextContainer.LineBreakMode = UILineBreakMode.TailTruncation;

			_dateLabel = new UILabel {
				Font = UIFont.FromName("HelveticaNeue-UltraLight", 16f),
				TextColor = Theme.LightOrangeColor,
				AdjustsFontSizeToFitWidth = true,
			};

			_downloadView = new UIView {
				BackgroundColor = UIColor.FromRGBA(0, 0, 0, 150),
				Hidden = true,
			};

			_statusLabel = new UILabel {
				Font = UIFont.FromName("HelveticaNeue-Italic", 16f),
				TextColor = UIColor.FromRGBA(255, 255, 255, 250),
				AdjustsFontSizeToFitWidth = true,
				TextAlignment = UITextAlignment.Left,
			};

			_screenActivity = new UIActivityIndicatorView(UIActivityIndicatorViewStyle.WhiteLarge) {
				HidesWhenStopped = true	
			};

			_screen = new WhiteBorderImageView();
			_screen.ImageView.ContentMode = UIViewContentMode.ScaleToFill;

			_statusImage = new UIImageView() {
				ContentMode = UIViewContentMode.ScaleAspectFit,
				Alpha = .75f,
				ClipsToBounds = true
			};

			_statusActivity = new UIActivityIndicatorView(UIActivityIndicatorViewStyle.WhiteLarge) {
				HidesWhenStopped = true
			};

			_downloadButton = new UIButton(UIButtonType.Custom) {
				ImageEdgeInsets = new UIEdgeInsets(20, 20, 20, 20),
				Alpha = .75f
			};

			_downloadButton.TouchUpInside += HandleDownloadClicked;

			AddWithKey(_titleLabel, "title");
			AddWithKey(_descriptionLabel, "desc");
			AddWithKey(_dateLabel, "date");
			AddWithKey(_screen, "screen");
			AddWithKey(_screenActivity, "screenActivity");
			AddWithKey(_downloadButton, "downloadButton", _downloadView);
			AddWithKey(_statusLabel, "status", _downloadView);
			AddWithKey(_statusActivity, "statusActivity", _downloadView);
			AddWithKey(_statusImage, "statusImage", _downloadView);
			AddWithKey(_downloadView, "downloadView");

			AddConstraint("H:|-(14)-[title]-(0)-|");
			AddConstraint("H:|-(16)-[date]-(10)-|");
			AddConstraint("H:|-(14)-[desc]-(10)-|");
			AddConstraint("H:|-(16)-[screen(387.5)]-(10)-|");
			AddConstraint("H:|-(16)-[downloadView(>=10)]-(10)-|");
			AddConstraint("H:|-(68)-[status(>=20)]-(-2)-[downloadButton(80)]-(-4)-|", _downloadView);
			AddConstraint("H:|-(16)-[statusActivity]-(>=20)-|", _downloadView);
			AddConstraint("H:|-(16)-[statusImage(40)]-(>=20)-|", _downloadView);

			AddConstraint("V:|-(40)-[title(40)]-(0)-[date(20)]-(6)-[screen(228)]-(4)-[desc(50)]-(>=10)-|");

			//View.AddConstraint(NSLayoutConstraint.Create(_screen, NSLayoutAttribute.Height, NSLayoutRelation.Equal, _screen, NSLayoutAttribute.Width, .5816f, 0));

			View.AddConstraint(NSLayoutConstraint.Create(_screenActivity, NSLayoutAttribute.CenterX, NSLayoutRelation.Equal, _screen, NSLayoutAttribute.CenterX, 1, 0));
			View.AddConstraint(NSLayoutConstraint.Create(_screenActivity, NSLayoutAttribute.CenterY, NSLayoutRelation.Equal, _screen, NSLayoutAttribute.CenterY, 1, 0));

			View.AddConstraint(NSLayoutConstraint.Create(_downloadView, NSLayoutAttribute.Height, NSLayoutRelation.Equal, null, NSLayoutAttribute.NoAttribute, 1, 70));
			View.AddConstraint(NSLayoutConstraint.Create(_downloadView, NSLayoutAttribute.Bottom, NSLayoutRelation.Equal, _screen, NSLayoutAttribute.Bottom, 1, 0));

			_downloadView.AddConstraint(NSLayoutConstraint.Create(_downloadButton, NSLayoutAttribute.CenterY, NSLayoutRelation.Equal, _downloadView, NSLayoutAttribute.CenterY, 1, 0));
			_downloadView.AddConstraint(NSLayoutConstraint.Create(_downloadButton, NSLayoutAttribute.Height, NSLayoutRelation.Equal, null, NSLayoutAttribute.NoAttribute, 1, 80));

			_downloadView.AddConstraint(NSLayoutConstraint.Create(_statusLabel, NSLayoutAttribute.CenterY, NSLayoutRelation.Equal, _downloadView, NSLayoutAttribute.CenterY, 1, 0));
			_downloadView.AddConstraint(NSLayoutConstraint.Create(_statusLabel, NSLayoutAttribute.Height, NSLayoutRelation.Equal, null, NSLayoutAttribute.NoAttribute, 1, 24));

			_downloadView.AddConstraint(NSLayoutConstraint.Create(_statusImage, NSLayoutAttribute.CenterY, NSLayoutRelation.Equal, _downloadView, NSLayoutAttribute.CenterY, 1f, 0));
			_downloadView.AddConstraint(NSLayoutConstraint.Create(_statusImage, NSLayoutAttribute.Height, NSLayoutRelation.Equal, null, NSLayoutAttribute.NoAttribute, 1, 40));

			_downloadView.AddConstraint(NSLayoutConstraint.Create(_statusActivity, NSLayoutAttribute.CenterY, NSLayoutRelation.Equal, _downloadView, NSLayoutAttribute.CenterY, 1, 0));

			base.LayoutInterface();
		}

		void ClearShowDetails()
		{
			if(!IsLayoutInitialized)
				return;
			
			_titleLabel.Text = string.Empty;
			_descriptionLabel.Text = string.Empty;
			_screen.ImageView.Image = null;
			_screen.WhiteBorder.Hidden = true;
			_dateLabel.Text = string.Empty;
			_downloadView.Hidden = true;
			_statusActivity.StopAnimating();
		}

		public void CancelImageLoad()
		{
			if(_isImageLoading)
			{
				App.NetworkInUse(false);
				_isImageLoading = false;
			}
		}

		public override void ViewDidAppear(bool animated)
		{
			base.ViewDidAppear(animated);
			_descriptionLabel.SetContentOffset(new CGPoint(0, 0), true);
		}

		public void UpdateView(bool getState = false)
		{
			if(!IsLayoutInitialized)
				return;

			ClearShowDetails();

			if(ViewModel.Episode == null)
				return;

			if(getState)
			{
				GetLatestEpisodeState();
			}

			_titleLabel.Text = ViewModel.Episode.Title;
			_descriptionLabel.Text = string.IsNullOrWhiteSpace(ViewModel.Episode.Overview) ? "no description" : ViewModel.Episode.Overview;
			_dateLabel.Text = ViewModel.Episode.S0E0;

			if(AppDelegate.Instance.Client.IsConnected)
			{
				_downloadView.Hidden = false;
			}

			if(ViewModel.Episode.InitialBroadcastDate.HasValue)
			{
				var tense = ViewModel.Episode.InitialBroadcastDate > DateTime.Now ? "airs" : "aired";
				_dateLabel.Text += " : {0} {1}".Fmt(tense, ViewModel.Episode.FormattedBroadcastDate);
			}

			if(string.IsNullOrWhiteSpace(ViewModel.Episode.Title))
				_titleLabel.Text = "Untitled Episode";

			var isHidden = _downloadView.Hidden;
			_downloadView.Hidden = true;
			_screenActivity.StartAnimating();

			bool imageExists = false;
			imageExists = _screen.SetEpisodeImage(ViewModel.Episode, ViewModel.Show, (image, error, cachetype, imageUrl) =>
			{
				_screenActivity.StopAnimating();
				_downloadView.Hidden = isHidden;

				if(_screen.ImageView.Image == null)
				{
					_screen.ImageView.Image = UIImage.FromBundle("Images/missing_screen.png");
					_screen.SetNeedsDisplay();
				}

				if(!imageExists && _isImageLoading)
				{
					_isImageLoading = false;
					App.NetworkInUse(false);
				}
			});

			if(!imageExists)
			{
				App.NetworkInUse(true);
				_isImageLoading = true;
			}
		}

		void GetLatestEpisodeState()
		{
			AppDelegate.Instance.Client.SendMessage(MessageAction.DownloadUpdateInquiry, ViewModel.Episode.ID);
			AppDelegate.Instance.Client.SendMessage(MessageAction.HasEpisodeOnDisk, new DownloadRequest
			{
				ShowTitle = ViewModel.Show.Title,
				EpisodeTitle = ViewModel.Episode.Title,
				SeasonNumber = ViewModel.Episode.SeasonNumber,
				EpisodeTraktID = ViewModel.Episode.Identifiers?.Trakt.ToString()
			});
		}

		void UpdateDownloadingState()
		{
			if(ViewModel.Episode == null)
				return;

			BeginInvokeOnMainThread(() =>
			{
				_downloadButton.SetImage(UIImage.FromBundle("Images/{0}_icon".Fmt(ViewModel.IsProcessing ? "cancel_download" : "download")), UIControlState.Normal);
				_statusImage.Hidden = true;

				if(ViewModel.CanDownload && ViewModel.LastResponse?.Result == null)
				{
					_downloadButton.Hidden = false;
					_downloadButton.SetImage(UIImage.FromBundle("Images/download_icon"), UIControlState.Normal);
					_statusLabel.Hidden = true;
					_statusActivity.StopAnimating();
				}
				else if(ViewModel.LastResponse != null)
				{
					_statusLabel.Hidden = false;
					_statusImage.Hidden = true;
					_downloadButton.Hidden = false;
					_statusActivity.StartAnimating();
					//Debug.WriteLine(ViewModel.LastResponse.State);

					switch(ViewModel.LastResponse.State)
					{
						case DownloadState.None:
							_statusActivity.StopAnimating();
							_statusLabel.Hidden = true;
							break;
						case DownloadState.Enqueued:
							_statusImage.Hidden = false;
							_statusImage.Image = UIImage.FromBundle("Images/hourglass_icon.png");
							_statusLabel.Text = ViewModel.LastResponse.State.ToString().ToLower();
							_statusActivity.StopAnimating();
							break;
						case DownloadState.AddingToiTunes:
							_statusLabel.Text = "adding to iTunes...";
							break;
						case DownloadState.Skipping:
							_statusLabel.Text = string.Format("skipping - {0}...", ViewModel.LastResponse.FailureReason);
							break;

						case DownloadState.Converting:
							_statusLabel.Text = "{0} {1}...".Fmt(ViewModel.LastResponse.State.ToString().ToLower(), ViewModel.LastResponse.Progress);
							break;
						case DownloadState.Complete:
							_statusActivity.StopAnimating();
							_downloadButton.Hidden = true;
							_statusImage.Hidden = false;
							_statusLabel.Hidden = false;

							if(ViewModel.LastResponse.Result == DownloadResult.Success)
							{
								_statusImage.Image = UIImage.FromBundle("Images/thumbsup_icon.png");
								_statusLabel.Text = "completed on {0}".Fmt(ViewModel.LastResponse.DateCompleted.ToMicroString(true));
							}
							else
							{
								_downloadButton.Hidden = false;
								_statusImage.Image = UIImage.FromBundle("Images/thumbsdown_icon.png");
								_statusLabel.Text = string.IsNullOrWhiteSpace(ViewModel.LastResponse.FailureReason) ? ViewModel.LastResponse.Result.ToString().ToLower() : ViewModel.LastResponse.FailureReason.ToLower();
							}

							break;	

						case DownloadState.Downloading:
							_statusLabel.Text = "{0} {1:P}...".Fmt(ViewModel.LastResponse.State.ToString().ToLower(), ViewModel.LastResponse.PercentComplete);

							break;

						default:
							_statusLabel.Text = ViewModel.LastResponse.State.ToString().ToLower() + "...";
							break;
					}
				}
				else
				{
					_downloadButton.Hidden = true;
					_statusImage.Hidden = true;
					_statusActivity.StopAnimating();
					_statusLabel.Hidden = true;
				}

				if(ViewModel.ExistsOnDisk)
				{
					_downloadButton.Hidden = true;
					_statusLabel.Text = "check iTunes to for this episode";
					_statusImage.Image = UIImage.FromBundle("Images/apple_icon.png");
					_statusLabel.Hidden = false;
					_statusImage.Hidden = false;
				}
			});
		}

		void HandleHasEpisodeOnDiskUpdate(bool onDisk)
		{
			BeginInvokeOnMainThread(() =>
			{
				ViewModel.ExistsOnDisk = onDisk;
				UpdateDownloadingState();
			});
		}

		void HandleDownloadUpdate(DownloadResponse response)
		{
			if(response == null || response.Request == null || ViewModel.Episode == null)
				return;
			
			if(response.Request.EpisodeTraktID != ViewModel.Episode.Identifiers?.Trakt.ToString())
				return;

			ViewModel.LastResponse = response;
			UpdateDownloadingState();
		}

		void HandleDownloadClicked(object sender, EventArgs e)
		{
			if(ViewModel.CanDownload)
			{
				var request = new DownloadRequest(ViewModel.Show, ViewModel.Episode);
				AppDelegate.Instance.Client.SendMessage(MessageAction.DownloadEpisode, request);
			}
			else
			{
				_statusLabel.Text = "cancelling...";
				AppDelegate.Instance.Client.SendMessage(MessageAction.CancelDownload, ViewModel.LastResponse.ID);
			}
		}
	}
}