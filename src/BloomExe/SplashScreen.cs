﻿using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using Gecko.Net;
using L10NSharp;

namespace Bloom
{
	/// <summary>
	/// Make these 3 calls: CreateAndShow(), StayAboveThisWindow(), FadeAndClose()
	///
	/// </summary>
	public partial class SplashScreen : Form
	{
		public static SplashScreen CreateAndShow()
		{
			var f = new SplashScreen();
			f.Show();
			Application.DoEvents();//show the splash
			return f;
		}

		public void StayAboveThisWindow(Form window)
		{
			TopMost = true;
			Owner = window;//required to keep it on top
		}

		public void FadeAndClose()
		{
			_fadeOutTimer.Enabled = true;
		}

		private SplashScreen()
		{
			InitializeComponent();
			_shortVersionLabel.Text = Shell.GetShortVersionInfo();
			_longVersionInfo.Text = Shell.GetBuiltOnDate();
			_feedbackStatusLabel.Visible = !DesktopAnalytics.Analytics.AllowTracking;
		}

		private void _fadeOutTimer_Tick(object sender, EventArgs e)
		{
			if (Opacity <= 0)
			{
				//Close();
				//if were were showing a dialog (like to choose a new project), Close would close that dialog too!
				//I tried setting the splashform.owner to the dlg, but that wasn't allowed.
				Hide();
			}
			Opacity -= 0.20;
		}

		private void SplashScreen_Load(object sender, EventArgs e)
		{
			//try really hard to become top most. See http://stackoverflow.com/questions/5282588/how-can-i-bring-my-application-window-to-the-front
			TopMost = true;
			Focus();
			var channel = ApplicationUpdateSupport.ChannelName;
			_channelLabel.Visible = channel.ToLower() != "release";
			_channelLabel.Text = LocalizationManager.GetDynamicString("Bloom", "SplashScreen." + channel, channel);
			BringToFront();
		}


		private void SplashScreen_Paint(object sender, PaintEventArgs e)
		{
			ControlPaint.DrawBorder(e.Graphics, ClientRectangle, Palette.SILInternationalBlue,5, ButtonBorderStyle.Solid,
										Palette.SILInternationalBlue,5, ButtonBorderStyle.Solid,
										Palette.SILInternationalBlue,5, ButtonBorderStyle.Solid,
										Palette.SILInternationalBlue,5, ButtonBorderStyle.Solid);
		}

		protected override void OnHandleCreated(EventArgs e)
		{
			base.OnHandleCreated(e);

			// BL-552, BL-779: a bug in Mono requires us to wait to set Icon until handle created.
			this.Icon = global::Bloom.Properties.Resources.Bloom;
		}
	}
}