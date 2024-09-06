using System;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using CoreGraphics;
using Foundation;
using MessageBar;
using ObjCRuntime;
using Plugin.Connectivity;
using SilverScreen.iOS.Shared;
using SilverScreen.Shared;
using UIKit;
using Xamarin;

namespace SilverScreen.iOS
{
	[Register("AppDelegate")]
	public partial class AppDelegate : UIApplicationDelegate, IAuthenticationHandler
	{
		[DllImport(Constants.ObjectiveCLibrary, EntryPoint = "objc_msgSendSuper")]
		static extern void void_objc_msgSendSuper_int(IntPtr deviceHandle, IntPtr setterHandle, long value);

		UIWindow window;
		AuthenticationViewController _authController;
		ShowCollectionViewController _landingViewController;
		internal string _deviceToken;

		public static AppDelegate Instance
		{
			get;
			set;
		}

		internal ServiceClient Client
		{
			get;
			private set;
		}

		public override bool FinishedLaunching(UIApplication app, NSDictionary options)
		{
			AppDomain.CurrentDomain.UnhandledException += HandleUnhandledException;
			//SDImageCache.SharedImageCache.ClearDisk();
			//SDImageCache.SharedImageCache.ClearMemory();

			Instance = this;

			DataService.DatabaseFilePath = AppSettings.SharedDatabasePath;
			ServiceContainer.Register<IAuthenticationHandler>(this);
			Insights.Initialize("c6011a71dc7b466eb29de9fbb7e0d4319e1dd136");
			DependencyContainer.Initialize();
			UIApplication.SharedApplication.StatusBarStyle = UIStatusBarStyle.LightContent;

			void_objc_msgSendSuper_int(UITextField.Appearance.SuperHandle, Selector.GetHandle("setKeyboardAppearance:"), (long)UIKeyboardAppearance.Dark);

			Client = new ServiceClient();
			window = new UIWindow(UIScreen.MainScreen.Bounds);

			_landingViewController = new ShowCollectionViewController();
			window.RootViewController = _landingViewController;
			window.MakeKeyAndVisible();

			App.IsNetworkReachable = CrossConnectivity.Current.IsConnected;
			CrossConnectivity.Current.ConnectivityChanged += (sender, e) =>
			{
				App.IsNetworkReachable = e.IsConnected;
			};
				
			Client.StateChanged += async(sender, e) => {

				if(e == MultipeerConnectivity.MCSessionState.Connected && _deviceToken != null)
				{
					await Task.Delay(2000);
					Client.SendMessage(MessageAction.RegisterDevice, _deviceToken);
				}
			};

			App.NetworkInUseChanged += (sender, e) => UIApplication.SharedApplication.NetworkActivityIndicatorVisible = e;

			#if DEBUG
			StartClient(0);
			#else
			StartClient(1000);
			#endif

			var settings = UIUserNotificationSettings.GetSettingsForTypes(UIUserNotificationType.Alert
						   | UIUserNotificationType.Badge
						   | UIUserNotificationType.Sound, new NSSet());


			if(settings != null)
			{
				UIApplication.SharedApplication.RegisterUserNotificationSettings(settings);
				UIApplication.SharedApplication.RegisterForRemoteNotifications();
			}

			App.TaskExceptionOccurred += (sender, e) =>
			{
				var severity = Insights.Severity.Warning;
				if(!(e.InnerException is WebException))
					severity = Insights.Severity.Error;

				Console.WriteLine(e);
				Insights.Report(e, severity);
				MessageBarManager.SharedInstance.ShowMessage(e.GetBaseException().Message);
			};

			return true;
		}

		public override void RegisteredForRemoteNotifications(UIApplication application, NSData deviceToken)
		{
			_deviceToken = deviceToken.Description.Trim('<', '>').Replace(" ", "");
			Console.WriteLine(_deviceToken);
		}

		public override void ReceivedRemoteNotification(UIApplication application, NSDictionary userInfo)
		{
			NSObject aps, alert;

			if(!userInfo.TryGetValue(new NSString("aps"), out aps))
				return;

			var apsHash = aps as NSDictionary;
			if(apsHash.TryGetValue(new NSString("alert"), out alert))
			{
				MessageBarManager.SharedInstance.ShowMessage(alert.ToString());
			}
		}

		async void StartClient(int delay = 0)
		{
			await Task.Delay(delay);
			Client.Start();

			await Task.Delay(3000);
		}

		#region Lifecycle

		public override void DidEnterBackground(UIApplication application)
		{
			Client.Stop();

			if(DateTime.Now.Subtract(Settings.Instance.LastUpcomingEpisodesWriteTime).TotalHours >= 24)
			{
				_landingViewController.ViewModel.WriteUpcomingShowsToSharedDisk(AppSettings.UpcomingShowsPath);
			}
		}

		public override void WillEnterForeground(UIApplication application)
		{
			StartClient(1000);
			EnsureShowsRefreshed();
		}

		public override void WillTerminate(UIApplication application)
		{
			Client.Stop();
		}

		#endregion

		internal void EnsureShowsRefreshed()
		{
			if(DateTime.UtcNow.Subtract(Settings.Instance.LastRefreshDate).TotalHours >= 24)
			{
				#pragma warning disable 4014
				_landingViewController.RefreshShows();
				#pragma warning restore 4014
			}
		}

		#region IAuthenticationHandler implementation

		static AutoResetEvent _autoEvent = new AutoResetEvent(false);

		async public Task AuthenticateUser()
		{
			BeginInvokeOnMainThread(() =>
			{
				if(_authController == null)
				{
					_authController = new AuthenticationViewController();
					_authController.PreferredContentSize = new CGSize(440, 514);
					_authController.ModalPresentationStyle = UIModalPresentationStyle.FormSheet;
					_authController.OnAuthenticated = async() =>
					{
						await _authController.DismissViewControllerAsync(true);
						_autoEvent.Set();
					};

					_authController.OnCanceled = async() =>
					{
						await _authController.DismissViewControllerAsync(true);
						_autoEvent.Set();
					};
				}

				window.RootViewController.PresentViewControllerAsync(_authController, true);
			});

			await Task.Run(() =>
			{
				_autoEvent.WaitOne();
			});
		}

		#endregion

		void HandleUnhandledException(object sender, UnhandledExceptionEventArgs e)
		{
			Console.WriteLine(e.ExceptionObject);
		}
	}
}