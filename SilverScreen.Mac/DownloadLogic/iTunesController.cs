using System;
using System.Runtime.InteropServices;
using ObjCRuntime;
using Foundation;
using ScriptingBridge;
using SilverScreen.Shared;
using System.Threading;
using System.Diagnostics;

namespace SilverScreen.Mac
{
	public class iTunesController
	{
		static object syncObject = new object();

		[DllImport(Constants.ObjectiveCLibrary, EntryPoint = "objc_msgSend")]
		static extern IntPtr IntPtr_objc_msgSend(IntPtr deviceHandle, IntPtr setterHandle);

		[DllImport(Constants.ObjectiveCLibrary, EntryPoint = "objc_msgSend")]
		static extern int int_objc_msgSend(IntPtr deviceHandle, IntPtr setterHandle);

		[DllImport(Constants.ObjectiveCLibrary, EntryPoint = "objc_msgSend")]
		static extern void void_objc_msgSend_int(IntPtr deviceHandle, IntPtr setterHandle, int val);

		[DllImport(Constants.ObjectiveCLibrary, EntryPoint = "objc_msgSend")]
		static extern void void_objc_msgSend_long(IntPtr deviceHandle, IntPtr setterHandle, long val);

		[DllImport(Constants.ObjectiveCLibrary, EntryPoint = "objc_msgSend")]
		static extern void void_objc_msgSend_uint(IntPtr deviceHandle, IntPtr setterHandle, uint val);

		[DllImport(Constants.ObjectiveCLibrary, EntryPoint = "objc_msgSend")]
		static extern void void_objc_msgSend_bool(IntPtr deviceHandle, IntPtr setterHandle, bool val);

		[DllImport(Constants.ObjectiveCLibrary, EntryPoint = "objc_msgSend")]
		static extern IntPtr void_objc_msgSend_add(IntPtr deviceHandle, IntPtr setterHandle, IntPtr handle, IntPtr to);

		[DllImport(Constants.ObjectiveCLibrary, EntryPoint = "objc_msgSend")]
		static extern void void_objc_msgSend_IntPtr(IntPtr deviceHandle, IntPtr setterHandle, IntPtr handle);

		public static bool AddMediaToiTunes(DownloadResponse response)
		{
			try
			{
				lock(syncObject)
				{
					//var path = "/Volumes/Documents/Rob/Desktop/South Park - the Hobbit.mp4";
					NSArray array = NSArray.FromNSObjects(new NSObject[] {
						NSUrl.FromFilename(response.FinalPath)
					});
					SBApplication iTunes = SBApplication.FromBundleIdentifier("com.apple.iTunes");

					var trackPtr = void_objc_msgSend_add(iTunes.Handle, Selector.GetHandle("add:to:"), array.Handle, IntPtr.Zero);
					Thread.Sleep(1000);

					void_objc_msgSend_int(trackPtr, Selector.GetHandle("setVideoKind:"), 1800823892);
					void_objc_msgSend_int(trackPtr, Selector.GetHandle("setEpisodeNumber:"), response.Request.EpisodeNumber);
					void_objc_msgSend_IntPtr(trackPtr, Selector.GetHandle("setShow:"), response.Request.ShowTitle.ToNSString().Handle);
					void_objc_msgSend_IntPtr(trackPtr, Selector.GetHandle("setName:"), response.Request.EpisodeTitle.ToNSString().Handle);

					void_objc_msgSend_int(trackPtr, Selector.GetHandle("setSeasonNumber:"), response.Request.SeasonNumber);

					void_objc_msgSend_bool(trackPtr, Selector.GetHandle("setBookmarkable:"), true);

					if(response.Request.EpisodeSummary != null)
						void_objc_msgSend_IntPtr(trackPtr, Selector.GetHandle("setLongDescription:"), response.Request.EpisodeSummary.ToNSString().Handle);

					return DownloadCoordinator.Instance.HasEpisodeOnDisk(response.Request);
				}
			}
			catch(Exception e)
			{
				Debug.WriteLine(e);
				return false;
			}
		}
	}
}