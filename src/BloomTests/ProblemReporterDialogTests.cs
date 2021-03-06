﻿// Copyright (c) 2014-2015 SIL International
// This software is licensed under the MIT License (http://opensource.org/licenses/MIT)
using System;
using System.Reflection;
using NUnit.Framework;
using Bloom.MiscUI;

namespace BloomTests
{
	[TestFixture]
	[Category("RequiresUI")]
	public class ProblemReporterDialogTests
	{
		class ProblemReporterDialogDouble: ProblemReporterDialog
		{
			public ProblemReporterDialogDouble(): base(null, null)
			{
				Success = true;
				_jiraProjectKey = "AUT";

				this.Load += (sender, e) =>
				{
					_description.Text = "Created by unit test of " + Assembly.GetAssembly(this.GetType()).FullName;
					_okButton_Click(sender, e);
				};
			}

			public bool Success { get; private set; }

			protected override void UpdateDisplay()
			{
				if (_state == State.Success)
					Close();
			}

			protected override void ChangeState(State state)
			{
				if (state == State.CouldNotAutomaticallySubmit)
				{
					Success = false;
					Close();
				}
				base.ChangeState(state);
			}
		}

		/// <summary>
		/// This is just a smoke-test that will notify us if the SIL JIRA stops working with the API we're relying on.
		/// It sends reports to https://jira.sil.org/browse/AUT
		/// </summary>
		[Test]
		public void CanSubmitToSILJiraAutomatedTestProject()
		{
			using (var dlg = new ProblemReporterDialogDouble())
			{
				dlg.ShowDialog();
				Assert.That(dlg.Success, Is.True, "Automatic submission of issue failed");
			}
		}
	}
}