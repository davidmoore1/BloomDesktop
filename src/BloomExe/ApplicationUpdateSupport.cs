﻿using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using Bloom.MiscUI;
using Bloom.Properties;
using L10NSharp;
using Palaso.PlatformUtilities;
using Squirrel;

namespace Bloom
{
	/// <summary>
	/// This class conctains code to work with the Squirrel package to handle automatic updating of
	/// Bloom to new versions. The key methods are called from WorkspaceView when Bloom is first idle or
	/// when the user requests an update.
	/// </summary>
	static class ApplicationUpdateSupport
	{
		internal static UpdateManager _bloomUpdateManager;

		internal enum BloomUpdateMessageVerbosity { Quiet, Verbose }

		internal static bool BloomUpdateInProgress
		{
			get { return _bloomUpdateManager != null; }
		}

		/// <summary>
		/// See if any updates are available and if so do them. Once they are done a notification
		/// pops up and the user can restart Bloom to run the new version.
		/// The restartBloom argument is an action that is executed if the user clicks the toast that suggests
		/// a restart. This is the responsibility of the caller (typically the workspace view). It is passed the new
		/// install directory.
		/// </summary>
		internal static async void CheckForASquirrelUpdate(BloomUpdateMessageVerbosity verbosity, Action<string> restartBloom, bool autoUpdate)
		{
			if (OkToInitiateUpdateManager)
			{
				string updateUrl;
				string rootDirectory = null; // null default causes squirrel to figure out the version actually running.
				if (Debugger.IsAttached)
				{
					// update'Url' can actually also just be a path to where the deltas and RELEASES file are found.
					// When debugging this function we want this to be the directory where we build installers.
					var location = Assembly.GetExecutingAssembly().Location; // typically in output\debug
					var output = Path.GetDirectoryName(Path.GetDirectoryName(location));
					updateUrl = Path.Combine(output, "installer");

					// For testing we will force it to look in the standard local data folder, even though we are not running there.
					// Tester should ensure that the version we want to pretent to upgrade is installed there (under Bloom)...the critical thing
					// seems to be the version of Bloom/packages/RELEASES in this folder which indicates what is already installed.
					rootDirectory = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
				}
				else
				{
					var result = InstallerSupport.LookupUrlOfSquirrelUpdate();

					if (result.Error != null || string.IsNullOrEmpty(result.URL))
					{
						// no need to tell them we can't connect, if they didn't explicitly ask us to look for an update
						if (verbosity != BloomUpdateMessageVerbosity.Verbose) return;

						// but if they did, try and give them a hint about what went wrong
						if (result.IsConnectivityError)
						{

							var failMsg = LocalizationManager.GetString("CollectionTab.UnableToCheckForUpdate",
								"Could not connect to the server to check for an update. Are you connected to the internet?",
								"Shown when Bloom tries to check for an update but can't, for example becuase it can't connect to the internet, or a problems with our server, etc.");
							ShowFailureNotification(failMsg);

						}
						else if (result.Error == null)
						{
							Palaso.Reporting.ErrorReport.NotifyUserOfProblem(
								"Bloom failed to find if there is an update available, for some unknown reason.");
						}
						else
						{
							ShowFailureNotification(result.Error.Message);
						}
						return;
					}
					updateUrl = result.URL;
				}
				if (!autoUpdate)
				{
					ApplicationUpdateSupport.InitiateSquirrelNotifyUpdatesAvailable(updateUrl, restartBloom);
					return;
				}

				string newInstallDir;
				UpdateOutcome outcome;
				ArrangeToDisposeSquirrelManagerOnExit();
				using (_bloomUpdateManager = new UpdateManager(updateUrl, Application.ProductName, FrameworkVersion.Net45, rootDirectory))
				{
					// At this point the method returns(!) and no longer blocks anything.
					var result = await UpdateApp(_bloomUpdateManager, null);
					newInstallDir = result.NewInstallDirectory;
					outcome = result.Outcome;
				}
				// Since this is in the async method _after_ the await we know the UpdateApp has finished.
				_bloomUpdateManager = null;

				if (outcome != UpdateOutcome.GotNewVersion)
				{
					if (verbosity == BloomUpdateMessageVerbosity.Verbose)
					{
						// Enhance: bring this in quiet mode, but only show it after an update.
						var noneNotifier = new ToastNotifier();
						noneNotifier.Image.Image = Resources.Bloom.ToBitmap();
						string message;
						if (outcome == UpdateOutcome.AlreadyUpToDate)
							message = LocalizationManager.GetString("CollectionTab.UpToDate", "Your Bloom is up to date.");
						else
							message = LocalizationManager.GetString("CollectionTab.UpdateFailed", "A new version appears to be available, but Bloom could not install it.");
						noneNotifier.Show(message, "", 5);
					}
					return;
				}

				string version = Path.GetFileName(newInstallDir).Substring("app-".Length); // version folders always start with this
				var msg = String.Format(LocalizationManager.GetString("CollectionTab.UpdateInstalled", "Update for {0} is ready", "Appears after Bloom has downloaded a program update in the background and is ready to switch the user to it the next time they run Bloom."), version);
				var action = String.Format(LocalizationManager.GetString("CollectionTab.RestartToUpdate", "Restart to Update"));
				// Unfortunately, there's no good time to dispose of this object...according to its own comments
				// it's not even safe to close it. It moves itself out of sight eventually if ignored.
				var notifier = new ToastNotifier();
				notifier.Image.Image = Resources.Bloom.ToBitmap();
				notifier.ToastClicked += (sender, args) => restartBloom(newInstallDir);
				notifier.Show(msg, action, -1);//Len wants it to stay up until he clicks on it
			}
		}

		private static void ShowFailureNotification(string failMsg)
		{
			var failNotifier = new ToastNotifier();
			failNotifier.Image.Image = Resources.Bloom.ToBitmap();
			failNotifier.Show(failMsg, "", 5);
		}

		internal static void ArrangeToDisposeSquirrelManagerOnExit()
		{
			Application.ApplicationExit += (sender, args) =>
			{
				if (_bloomUpdateManager != null)
				{
					var temp = _bloomUpdateManager;
					_bloomUpdateManager = null; // in case more than one notification comes
					temp.Dispose(); // otherwise squirrel throws a nasty exception.
				}
			};
		}

		internal static string ChannelNameForUnitTests;

		public static string ChannelName
		{
			get
			{
				if (ChannelNameForUnitTests != null)
				{
					return ChannelNameForUnitTests;
				}
				var s = Assembly.GetEntryAssembly().ManifestModule.Name.Replace("bloom", "").Replace("Bloom", "").Replace(".exe", "").Trim();
				return (s == "") ? "Release" : s;
			}
		}

		private static async void InitiateSquirrelNotifyUpdatesAvailable(string updateUrl, Action<string> restartBloom)
		{
			if (OkToInitiateUpdateManager)
			{
				UpdateInfo info;
				ArrangeToDisposeSquirrelManagerOnExit();
				using (_bloomUpdateManager = new UpdateManager(updateUrl, Application.ProductName, FrameworkVersion.Net45))
				{
					// At this point the method returns(!) and no longer blocks anything.
					info = await _bloomUpdateManager.CheckForUpdate();
				}
				// Since this is in the async method _after_ the await we know the CheckForUpdate has finished.
				_bloomUpdateManager = null;
				if (NoUpdatesAvailable(info))
				{
					Palaso.Reporting.Logger.WriteEvent("Squirrel: No updateavailable.");
					return; // none available.
				}
				var msg = LocalizationManager.GetString("CollectionTab.UpdatesAvailable", "A new version of Bloom is available.");
				var action = LocalizationManager.GetString("CollectionTab.UpdateNow", "Update Now");
				Palaso.Reporting.Logger.WriteEvent("Squirrel: Notifying that an update is available");
				// Unfortunately, there's no good time to dispose of this object...according to its own comments
				// it's not even safe to close it. It moves itself out of sight eventually if ignored.
				var notifier = new ToastNotifier();
				notifier.Image.Image = Resources.Bloom.ToBitmap();
				notifier.ToastClicked += (sender, args) => CheckForASquirrelUpdate(BloomUpdateMessageVerbosity.Verbose, restartBloom, true);
				notifier.Show(msg, action, 10);
			}
		}

		/// <summary>
		/// True if it is currently possible to start checking for or getting updates.
		/// This approach is only relevant for Windows.
		/// If some bloom update activity is already in progress we must not start another one...that crashes.
		/// </summary>
		internal static bool OkToInitiateUpdateManager
		{
			get { return Platform.IsWindows && _bloomUpdateManager == null; }
		}

		internal static bool NoUpdatesAvailable(UpdateInfo info)
		{
			return info == null || info.ReleasesToApply.Count == 0;
		}

		internal enum UpdateOutcome
		{
			GotNewVersion,
			AlreadyUpToDate,
			InstallFailed
		}

		internal class UpdateResult
		{
			public string NewInstallDirectory;
			public UpdateOutcome Outcome;

		}

		// Adapted from Squirrel's EasyModeMixin.UpdateApp, but this version yields the new directory.
		internal static async Task<UpdateResult> UpdateApp(IUpdateManager manager, Action<int> progress = null)
		{
			progress = progress ?? (_ => { });

			bool ignoreDeltaUpdates = false;

			retry:
			var updateInfo = default(UpdateInfo);
			string newInstallDirectory = null;

			try
			{
				updateInfo = await manager.CheckForUpdate(ignoreDeltaUpdates, x => progress(x / 3));
				if (NoUpdatesAvailable(updateInfo))
					return new UpdateResult() { NewInstallDirectory = null, Outcome = UpdateOutcome.AlreadyUpToDate }; // none available.

				var updatingNotifier = new ToastNotifier();
				updatingNotifier.Image.Image = Resources.Bloom.ToBitmap();
				var version = updateInfo.FutureReleaseEntry.Version;
				var size = updateInfo.ReleasesToApply.Sum(x => x.Filesize)/1024;
				var updatingMsg = String.Format(LocalizationManager.GetString("CollectionTab.Updating", "Downloading update to {0} ({1}K)"), version, size);
				Palaso.Reporting.Logger.WriteEvent("Squirrel: "+updatingMsg);
				updatingNotifier.Show(updatingMsg, "", 5);

				await manager.DownloadReleases(updateInfo.ReleasesToApply, x => progress(x / 3 + 33));

				newInstallDirectory = await manager.ApplyReleases(updateInfo, x => progress(x / 3 + 66));

				await manager.CreateUninstallerRegistryEntry();
			}
			catch (Exception ex)
			{
				if (ignoreDeltaUpdates == false)
				{
					// I think the idea here is that if something goes wrong applying deltas we
					// just download and install whatever the update url says is the latest version,
					// as a complete package.
					// Thus we can even recover if the executing program and the package that created
					// it are not part of the sequence on the web site at all, or even if there's
					// some sort of discontinuity in the sequence of deltas.
					ignoreDeltaUpdates = true;
					goto retry;
				}

				// OK, the update failed. We've had cases where somehow Squirrel thinks there should be
				// a new release available and it isn't there yet. Possibly somehow a new version of
				// RELEASES is getting uploaded before the delta and nupkg files (due to subtask overlap
				// in MsBuild? Due to 'eventual consistency' not yet being attained by S3?).
				// In any case we don't need to crash the program over a failed update. Just log it.
				Palaso.Reporting.Logger.WriteEvent("Squirrel update failed: " + ex.Message + ex.StackTrace);
				return new UpdateResult() {NewInstallDirectory = null, Outcome = UpdateOutcome.InstallFailed};
			}

			return new UpdateResult()
			{
				NewInstallDirectory = newInstallDirectory,
				Outcome = newInstallDirectory == null ? UpdateOutcome.AlreadyUpToDate : UpdateOutcome.GotNewVersion
			};
		}
	}
}
