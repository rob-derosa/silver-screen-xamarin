using UIKit;
using Foundation;
using Newtonsoft.Json;
using CoreGraphics;
using System;
using SDWebImage;
using SilverScreen.iOS.Shared;
using System.Diagnostics;
using SQLite.Net.Attributes;

namespace SilverScreen.iOS
{
	public static class Extensions
	{
		public static bool SetEpisodeImage(this WhiteBorderImageView view, Episode ep, Show show, SDWebImageCompletionHandler completion = null)
		{
			var url = show.GetBestScreenUrlForEpisode(ep);
			var fanArtUrl = show.GetBestFanArt();
			url = url ?? fanArtUrl;
			view.WhiteBorder.Hidden = false;

			if(url != null)
			{
				view.ImageView.SetImage(new NSUrl(url), null, SDWebImageOptions.LowPriority & SDWebImageOptions.ContinueInBackground, (image, error, cachetype, imageUrl) =>
				{
					if(error != null && url != fanArtUrl)
					{
						Debug.WriteLine(error);
						url = fanArtUrl;
						view.ImageView.SetImage(new NSUrl(url), null, SDWebImageOptions.LowPriority & SDWebImageOptions.ContinueInBackground, (image2, error2, cachetype2, imageUrl2) =>
						{
							if(view.ImageView.Image == null)
								view.ImageView.Image = UIImage.FromBundle("Images/missing_screen.png");

							view.SetNeedsDisplay();
							completion?.Invoke(image2, error2, cachetype2, imageUrl2);
						});
					}
					else
					{
						if(view.ImageView.Image == null)
							view.ImageView.Image = UIImage.FromBundle("Images/missing_screen.png");

						view.SetNeedsDisplay();
						completion?.Invoke(image, error, cachetype, imageUrl);
					}
				});
			}
			else
			{
				view.ImageView.Image = UIImage.FromBundle("Images/missing_screen.png");
				view.SetNeedsDisplay();
				completion?.Invoke(null, null, SDImageCacheType.Disk, null);
			}

			if(url == null)
				return false;
			
			return SDWebImageManager.SharedManager.DiskImageExists(new NSUrl(url));
		}

		public static bool HasValue(this string s)
		{
			return !string.IsNullOrWhiteSpace(s);
		}

		public static NSIndexPath ToIndexPath(this int i)
		{
			return NSIndexPath.FromRowSection(i, 0);
		}

		public static CGRect AddHeight(this CGRect r, nfloat height)
		{
			return new CGRect(r.X, r.Y, r.Width, r.Height + height);
		}

		public static CGRect AddWidth(this CGRect r, nfloat width)
		{
			return new CGRect(r.X, r.Y, r.Width + width, r.Height);
		}

		public static CGRect AddX(this CGRect r, nfloat x)
		{
			return new CGRect(r.X + x, r.Y, r.Width, r.Height);
		}

		public static CGRect AddY(this CGRect r, nfloat y)
		{
			return new CGRect(r.X, r.Y + y, r.Width, r.Height);
		}

		public static UIImage FromUrl(string uri)
		{
			using(var url = new NSUrl(uri))
			{
				using(var data = NSData.FromUrl(url))
				{
					return UIImage.LoadFromData(data);
				}
			}
		}

		public static UIImage ToUIImage(this byte[] bytes)
		{
			if(bytes == null)
				return null;

			return UIImage.LoadFromData(NSData.FromArray(bytes));
		}

		public static NSString ToNSString(this string str)
		{
			if(!string.IsNullOrEmpty(str))
				return new NSString(str);
			else
				return new NSString();
		}
	}
}

namespace SilverScreen.iOS.Shared
{
	public partial class ImageSet
	{
		[JsonIgnore]
		[Ignore]
		public UIImage FullImage
		{
			get;
			set;
		}

		[JsonIgnore]
		[Ignore]
		public UIImage MediumImage
		{
			get;
			set;
		}

		[JsonIgnore]
		[Ignore]
		public UIImage ThumbImage
		{
			get;
			set;
		}
	}
}
