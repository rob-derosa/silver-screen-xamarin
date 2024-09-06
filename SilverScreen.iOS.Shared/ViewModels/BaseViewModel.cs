using System;
using System.Threading;
using System.Threading.Tasks;
using System.Net;
using SilverScreen.Shared;
using System.Runtime.CompilerServices;
using Foundation;

namespace SilverScreen.iOS.Shared
{
	public class BaseViewModel
	{
		bool _isBusy;
		CancellationTokenSource _cancellationTokenSource;

		public event EventHandler IsBusyChanged;

		public bool IsBusy
		{
			get
			{
				return _isBusy;
			}
			set
			{
				if(_isBusy == value)
					return;

				_isBusy = value;

				if(IsBusyChanged != null)
				{
					IsBusyChanged(this, new EventArgs());
				}
			}
		}

		public bool WasCancelledAndReset
		{
			get
			{
				var cancelled = _cancellationTokenSource != null && _cancellationTokenSource.IsCancellationRequested;

				if(cancelled)
					ResetCancellationToken();

				return cancelled;
			}
		}

		public CancellationToken CancellationToken
		{
			get
			{
				if(_cancellationTokenSource == null)
					_cancellationTokenSource = new CancellationTokenSource();

				return _cancellationTokenSource.Token;
			}
		}

		public void ResetCancellationToken()
		{
			_cancellationTokenSource = new CancellationTokenSource();
		}

		public virtual void CancelTasks()
		{
			if(!_cancellationTokenSource.IsCancellationRequested && CancellationToken.CanBeCanceled)
			{
				_cancellationTokenSource.Cancel();
			}
		}

		public Action<Exception> OnTaskException
		{
			get;
			set;
		}

		public async Task RunSafe(Task task, [CallerMemberName] string caller = null, [CallerLineNumber] long line = 0, [CallerFilePath] string path = null)
		{
			try
			{
				if(!App.IsNetworkReachable)
					throw new WebException("No internet connection detected");

				App.NetworkInUse(true);
				await TaskRunner.RunSafe(task, null, CancellationToken);
			}
			catch(Exception e)
			{
				var wrapped = HandleTaskException(e, task, caller, line, path);
				App.OnTaskException(wrapped);
			}
			finally
			{
				App.NetworkInUse(false);
			}
		}

		public async Task<T> RunSafe<T>(Task<T> task, [CallerMemberName] string caller = null, [CallerLineNumber] long line = 0, [CallerFilePath] string path = null)
		{
			try
			{
				if(!App.IsNetworkReachable)
					throw new WebException("No internet connection detected");

				App.NetworkInUse(true);
				await TaskRunner.RunSafe(task, null, CancellationToken);

				if(!task.IsFaulted && task.IsCompleted)
					return task.Result;

				return default(T);
			}
			catch(Exception e)
			{
				var wrapped = HandleTaskException(e, task, caller, line, path);
				App.OnTaskException(wrapped);
				return default(T);
			}
			finally
			{
				App.NetworkInUse(false);
			}
		}

		RunSafeException HandleTaskException(Exception e, Task task, string caller, long line, string path)
		{
			var desc = $"Filepath: {path}\nLine: {line}\nCalling Method: {caller}\nTask:{task}";
			var excep = new RunSafeException($"RunSafe Task error occurred from:\n{desc}\nTask: {task}", e)
			{
				CallingMethod = desc,
				Task = task,
			};

			return excep;
		}
	}

	public class RunSafeException : Exception
	{
		public RunSafeException(string message, Exception inner) : base(message, inner)
		{
		}

		public Task Task
		{
			get;set;
		}

		public string CallingMethod
		{
			get;set;
		}
	}
}