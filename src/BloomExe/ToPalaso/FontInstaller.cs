﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Amazon.CognitoSync;
using Palaso.IO;

namespace Bloom.ToPalaso
{
	/// <summary>
	/// Helper class for installing fonts.
	/// To use this: the sourceFolder passed to InstallFont must be one that GetDirectoryDistributedWithApplication can find.
	/// It must contain the fonts you want to be sure are installed.
	/// It MUST also contain the installer program, "InstallSilLiteracyFonts.exe", a renamed version of FontReg.exe (see below).
	/// The user will typically see a UAC dialog asking whether it is OK to run this program (if the fonts are not already
	/// installed).
	/// </summary>
	public class FontInstaller
	{
		public static void InstallFont(string sourceFolder)
		{
			if (Palaso.PlatformUtilities.Platform.IsWindows)
			{
				var sourcePath = FileLocator.GetDirectoryDistributedWithApplication(sourceFolder);
				if (AllFontsExist(sourcePath))
					return; // already installed (Enhance: maybe one day we want to check version?)
				var info = new ProcessStartInfo()
				{
					// Renamed to make the UAC dialog less mysterious.
					// Originally it is FontReg.exe (http://code.kliu.org/misc/fontreg/).
					// Eventually we will probably have to get our version signed.
					FileName = "InstallSilLiteracyFonts.exe",
					Arguments = "/copy",
					WorkingDirectory = sourcePath,
					UseShellExecute = true, // required for runas to achieve privilege elevation
					WindowStyle = ProcessWindowStyle.Hidden,
					Verb = "runas" // that is, run as admin (required to install fonts)
				};

				try
				{
					Process.Start(info);
				}
					// I hate catching 'Exception' but the one that is likely to happen, the user refused the privilege escalation
					// or is not authorized to do it, comes out as Win32Exception, which is not much more helpful.
					// We probably want to ignore anything else that can go wrong with trying to install the fonts.
				catch (Exception)
				{
				}
			}
			// Todo Linux: any way we can install fonts on Linux??
			// The code for checking that they are already installed MIGHT work...
			// very unlikely running a Windows EXE will actually do the installation, though.
			// However, possibly on Linux we don't have to worry about privilege escalation?
		}

		private static bool AllFontsExist(string sourcePath)
		{
			var fontFolder = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
			foreach (var fontFile in Directory.GetFiles(sourcePath, "*.ttf"))
			{
				var destPath = Path.Combine(fontFolder, Path.GetFileName(fontFile));
				if (!File.Exists(destPath))
					return false;
			}
			return true;
		}
	}
}
