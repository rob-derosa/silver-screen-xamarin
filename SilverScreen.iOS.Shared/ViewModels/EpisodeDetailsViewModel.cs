using System;
using System.Threading.Tasks;
using SilverScreen.Shared;

namespace SilverScreen.iOS.Shared
{
	public class EpisodeDetailsViewModel : BaseViewModel
	{
		public Show Show
		{
			get;
			private set;
		}

		public Episode Episode
		{
			get;
			private set;
		}

		public bool ExistsOnDisk
		{
			get;
			set;
		}

		public DownloadResponse LastResponse
		{
			get;
			set;
		}

		public bool IsProcessing
		{
			get
			{
				return !ExistsOnDisk && Episode != null && LastResponse != null && (LastResponse.State != DownloadState.None && LastResponse.State != DownloadState.Complete) && LastResponse.Result == null;
			}
		}

		public void SetEpisode(Episode ep, Show show)
		{
			Episode = ep;
			Show = show;
			LastResponse = null;
		}

		public bool CanDownload
		{
			get
			{
				if(Episode == null)
					return false;
				
				if(IsProcessing)
					return false;

				if(LastResponse == null)
					return true;

				if(ExistsOnDisk)
					return false;

				if(LastResponse.State == DownloadState.Enqueued)
					return false;

				if(LastResponse.Result == null)
					return true;
				
				return LastResponse.Result != DownloadResult.Success;
			}
		}

		public bool IsComplete
		{
			get
			{
				return !ExistsOnDisk && Episode != null && LastResponse != null && !IsProcessing && LastResponse.Result != null;
			}
		}
	}
}