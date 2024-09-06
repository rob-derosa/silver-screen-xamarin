using System;
using UIKit;
using SilverScreen.iOS.Shared;
using CoreGraphics;
using Foundation;
using System.Linq;
using SilverScreen.Shared;
using System.Diagnostics;
using System.Runtime.InteropServices;
using ObjCRuntime;

namespace SilverScreen.iOS
{
	public class AuthenticationViewController : BaseViewController<AuthenticationViewModel>
	{
		[DllImport(Constants.ObjectiveCLibrary, EntryPoint = "objc_msgSendSuper")]
		static extern void void_objc_msgSendSuper_IntPtr(IntPtr deviceHandle, IntPtr setterHandle, long value);

		UIButton _cancelButton;
		UIWebView _webView;

		public Action OnAuthenticated
		{
			get;
			set;
		}

		public Action OnCanceled
		{
			get;
			set;
		}

		public UIActivityIndicatorView _activity;

		protected override void LayoutInterface()
		{
			base.LayoutInterface();

			_cancelButton = new UIButton(UIButtonType.RoundedRect);
			_cancelButton.Frame = new CGRect(View.Frame.Width - 100 - 10, 10, 100, 32);
			_cancelButton.SetTitle("Cancel", UIControlState.Normal);
			_cancelButton.Layer.BorderWidth = 1f;
			_cancelButton.Layer.CornerRadius = 4f;
			_cancelButton.Layer.BackgroundColor = UIColor.FromRGBA(255, 255, 255, 75).CGColor;
			_cancelButton.Layer.BorderColor = View.TintColor.CGColor;

			var dlegate = new WebViewDelegate {
				ViewController = this
			};

			_webView = new UIWebView(View.Frame);
			_webView.KeyboardDisplayRequiresUserAction = false;
			_webView.ScalesPageToFit = true;
			_webView.Delegate = dlegate;

			_activity = new UIActivityIndicatorView(UIActivityIndicatorViewStyle.WhiteLarge);
			_activity.Color = View.TintColor;
			_activity.Frame = new CGRect(View.Frame.Width / 2 - 20, View.Frame.Height / 2 - 20, 60, 60);
			_activity.HidesWhenStopped = true;

			Add(_webView);
			Add(_activity);
			Add(_cancelButton);

			var a = UITextView.AppearanceWhenContainedIn(typeof(UIWebView));
			void_objc_msgSendSuper_IntPtr(a.SuperHandle, Selector.GetHandle("setKeyboardAppearance:"), (long)UIKeyboardAppearance.Dark);
		}

		async void EvaluateUrlToLoad(NSUrl url)
		{
			if(url.Query != null)
			{
				var host = url.AbsoluteString.Replace(url.Query, string.Empty).TrimEnd("?");

				if(host == TraktService.Instance.RedirectUrl)
				{
					var dictionary = url.Query.Replace("?", "").Split('&').ToDictionary(x => x.Split('=')[0], x => x.Split('=')[1]);

					var code = dictionary["code"];
					var result = await ViewModel.RunSafe(TraktService.Instance.GetTokenForCode(code));

					if(result != null)
					{
						Settings.Instance.AuthToken = result.AccessToken;
						Settings.Instance.RefreshToken = result.RefreshToken;
						await Settings.Instance.Save();
						OnAuthenticated();
					}
				}
			}
		}

		public override void ViewDidAppear(bool animated)
		{
			base.ViewDidAppear(animated);
			_cancelButton.TouchUpInside += OnCancelButtonClicked;
			_activity.StartAnimating();
			_webView.Hidden = true;
			_webView.LoadRequest(new NSUrlRequest(new NSUrl(TraktService.Instance.TraktAuthUrl)));
		}

		public override void ViewWillAppear(bool animated)
		{
			base.ViewWillAppear(animated);

			if(_webView != null)
			{
				_activity.StartAnimating();
				_webView.Hidden = true;
			}
		}

		public override void ViewDidDisappear(bool animated)
		{
			base.ViewDidDisappear(animated);
			_cancelButton.TouchUpInside -= OnCancelButtonClicked;
		}

		void OnCancelButtonClicked(object sender, EventArgs e)
		{
			OnCanceled?.Invoke();
		}

		class WebViewDelegate : UIWebViewDelegate
		{
			internal AuthenticationViewController ViewController
			{
				get;
				set;
			}

			public override void LoadingFinished(UIWebView webView)
			{
				Debug.WriteLine("Finished loading");
				webView.Hidden = false;
				ViewController._activity.StopAnimating();
			}

			public override bool ShouldStartLoad(UIWebView webView, NSUrlRequest request, UIWebViewNavigationType navigationType)
			{
				var host = request.Url.AbsoluteString;

				if(request.Url.Query != null)
					host = host.Replace(request.Url.Query, string.Empty).TrimEnd("?");
				
				if(host == TraktService.Instance.RedirectUrl)
				{
					ViewController.EvaluateUrlToLoad(request.Url);
					return false;
				}

				return true;
			}
		}
	}
}

