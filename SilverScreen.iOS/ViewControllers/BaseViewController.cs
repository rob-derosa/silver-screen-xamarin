using System;
using UIKit;
using SilverScreen.iOS.Shared;
using System.Collections.Generic;
using Foundation;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;

namespace SilverScreen.iOS
{
	public class BaseViewController<T> : UIViewController where T : BaseViewModel
	{
		protected Dictionary<UIView, AutoLayoutParent> _autoLayoutViews = new Dictionary<UIView, AutoLayoutParent>();

		public bool IsLayoutInitialized
		{
			get;
			set;
		}

		//protected CancellationTokenSource CancelToken
		//{
		//	get;
		//	set;
		//}

		public BaseViewController()
		{
			//CancelToken = new CancellationTokenSource();
		}

		protected T _viewModel;

		public T ViewModel
		{
			get
			{
				if(_viewModel == null)
				{
					_viewModel = ServiceContainer.Resolve<T>();
				}

				return _viewModel;
			}
		}

		public override void ViewDidAppear(bool animated)
		{
			base.ViewDidAppear(animated);
			ViewModel.IsBusyChanged += OnIsBusyChanged;
			OnIsBusyChanged(null, null);

			NSNotificationCenter.DefaultCenter.AddObserver(UIApplication.WillEnterForegroundNotification, OnEnterForeground);
			NSNotificationCenter.DefaultCenter.AddObserver(UIApplication.DidEnterBackgroundNotification, OnEnteredBackground);
		}

		public override void ViewDidDisappear(bool animated)
		{
			base.ViewDidDisappear(animated);
			ViewModel.IsBusyChanged -= OnIsBusyChanged;
			NSNotificationCenter.DefaultCenter.RemoveObserver(this);
		}

		void OnIsBusyChanged(object sender, EventArgs e)
		{
			IsBusyChanged();
		}

		protected virtual void IsBusyChanged()
		{
		}

		void OnEnterForeground(NSNotification n)
		{
			ViewWillEnterForeground();
		}

		void OnEnteredBackground(NSNotification n)
		{
			ViewDidEnterBackground();
		}

		protected virtual void ViewWillEnterForeground()
		{

		}

		protected virtual void ViewDidEnterBackground()
		{
		}

		public override void ViewWillLayoutSubviews()
		{
			if(!IsLayoutInitialized)
				LayoutInterface();

			base.ViewWillLayoutSubviews();
		}

		protected virtual void LayoutInterface()
		{
			IsLayoutInitialized = true;
		}

		public override UIStatusBarStyle PreferredStatusBarStyle()
		{
			return UIStatusBarStyle.LightContent;
		}

		#region Constraints

		AutoLayoutParent GetAutoLayout(UIView view)
		{
			if(!_autoLayoutViews.ContainsKey(view))
			{
				_autoLayoutViews.Add(view, new AutoLayoutParent {
					Parent = view
				});
			}

			return _autoLayoutViews[view];
		}

		protected NSLayoutConstraint[] AddConstraint(string visualFormat, UIView parent = null, NSLayoutFormatOptions options = 0)
		{
			parent = parent ?? View;
			var alp = GetAutoLayout(parent);
			var constraint = NSLayoutConstraint.FromVisualFormat(visualFormat, options, null, alp.ConstrainedViews);
			parent.AddConstraints(constraint);
			return constraint;
		}

		protected void AddWithKey(UIView view, string key, UIView parent = null)
		{
			parent = parent ?? View;
			parent.Add(view);
			RegisterWithKey(view, key, parent);
		}

		protected void RegisterWithKey(UIView view, string key, UIView parent = null)
		{
			parent = parent ?? View;
			var alp = GetAutoLayout(parent);
			view.TranslatesAutoresizingMaskIntoConstraints = false;
			alp.ConstrainedViews = null;
			alp.ConstraintDictionary.Add(key, view);
		}

		protected void CenterX(UIView view, float width = -1, UIView parent = null)
		{
			parent = parent ?? View;
			parent.AddConstraint(NSLayoutConstraint.Create(view, NSLayoutAttribute.CenterX, NSLayoutRelation.Equal, this.View, NSLayoutAttribute.CenterX, 1f, 0f));

			if(width > -1)
				parent.AddConstraint(NSLayoutConstraint.Create(view, NSLayoutAttribute.Width, NSLayoutRelation.Equal, null, NSLayoutAttribute.NoAttribute, 1f, width));
		}

		protected void CenterY(UIView view, float height = -1, UIView parent = null)
		{
			parent = parent ?? View;
			parent.AddConstraint(NSLayoutConstraint.Create(view, NSLayoutAttribute.CenterY, NSLayoutRelation.Equal, this.View, NSLayoutAttribute.CenterY, 1f, 0f));

			if(height > -1)
				parent.AddConstraint(NSLayoutConstraint.Create(view, NSLayoutAttribute.Height, NSLayoutRelation.Equal, null, NSLayoutAttribute.NoAttribute, 1f, height));
		}

		protected class AutoLayoutParent
		{
			NSDictionary _constrainedViews;

			public AutoLayoutParent()
			{
				ConstraintDictionary = new Dictionary<string, UIView>();
			}

			public UIView Parent
			{
				get;
				set;
			}

			public Dictionary<string, UIView> ConstraintDictionary
			{
				get;
				set;
			}

			public NSDictionary ConstrainedViews
			{
				get
				{
					if(_constrainedViews == null)
					{
						_constrainedViews = NSDictionary.FromObjectsAndKeys(ConstraintDictionary.Values.ToArray(), ConstraintDictionary.Keys.Select(k => k.ToNSString()).ToArray());
					}

					return _constrainedViews;
				}
				set
				{
					_constrainedViews = value;
				}
			}

			protected void AddWithKey(UIView view, string key)
			{
				Parent.Add(view);
				RegisterWithKey(view, key);
			}

			protected void RegisterWithKey(UIView view, string key)
			{
				view.TranslatesAutoresizingMaskIntoConstraints = false;
				ConstrainedViews = null;
				ConstraintDictionary.Add(key, view);
			}
		}

		#endregion /Constraints
	}
}