[![Build Status](https://jenkins.lsdev.sil.org/buildStatus/icon?job=Bloom-Wrapper-Trigger-debug)](https://jenkins.lsdev.sil.org/view/Bloom/job/Bloom-Wrapper-Trigger-debug/)

Bloom Desktop is a hybrid c#/javascript/html/css application for Windows and Linux that dramatically "lowers the bar" for language communities who want books in their own languages. Bloom delivers a low-training, high-output system where mother tongue speakers and their advocates work together to foster both community authorship and access to external material in the vernacular.

# Development Process

## RoadMap / Day-to-day progress

See the Trello boards:
[Bloom 2](https://trello.com/b/UA7QLibU/bloom-desktop-2-0), [Bloom 3](https://trello.com/b/ErDHtpNe/bloom-3-0)

## Bug Reports

Reports can be entered in [jira](https://jira.sil.org/browse/BL). They can be entered there via email by sending to [issues@bloom.palaso.org](mailto:issues@bloom.palaso.org); things sent there will be visible on the web to anyone who makes an account on the jira system.

## Continuous Build System

Each time code is checked in, an automatic build begins on our [TeamCity build server](http://build.palaso.org/project.html?projectId=project16&amp;tab=projectOverview), running all the unit tests. Similarly, when there is a new version of some Bloom dependency (e.g. Palaso, PdfDroplet, our fork of GeckoFX), that server automatically rebuilds Bloom. This automatic build doesn't publish a new installer, however. That kind of build is launched manually, by pressing a button on the TeamCity page. This "publish" process builds Bloom, makes and installer, rsyncs it to the distribution server, and writes out a little bit of html which the [Bloom download page](http://bloomlibrary.org/#/installers) then displays to the user.

|            | Windows | Linux |
| :--------: | :-----: | :---: |
| Build      | [![Build Status](https://jenkins.lsdev.sil.org/buildStatus/icon?job=Bloom-Win32-master-debug)](https://jenkins.lsdev.sil.org/view/Bloom/job/Bloom-Win32-master-debug/)| [![Build Status](https://jenkins.lsdev.sil.org/buildStatus/icon?job=Bloom-Linux-any-master-debug)](https://jenkins.lsdev.sil.org/view/Bloom/job/Bloom-Linux-any-master-debug/) |
| Unit tests | [![Build Status](https://jenkins.lsdev.sil.org/buildStatus/icon?job=Bloom-Win32-master-debug-Tests)](https://jenkins.lsdev.sil.org/view/Bloom/job/Bloom-Win32-master-debug-Tests/)| [![Build Status](https://jenkins.lsdev.sil.org/buildStatus/icon?job=Bloom-Linux-any-master-debug-Tests)](https://jenkins.lsdev.sil.org/view/Bloom/job/Bloom-Linux-any-master-debug-Tests/)|
| JS tests   | | [![Build Status](https://jenkins.lsdev.sil.org/buildStatus/icon?job=Bloom-Linux-any-master--JSTests)](https://jenkins.lsdev.sil.org/view/Bloom/job/Bloom-Linux-any-master--JSTests/)|

## Source Code

Bloom is written in C# with Winforms, with an embedded Gecko (Firefox) browser and a bunch of jquery-using javascript & Typescript.

On Windows you'll need at least a 2010 edition of Visual Studio (Version 2010 SP1), including the free Express version.

It will avoid some complications if you update to default branch now, before adding the dependencies that follow.

### Building the Source Code

Before you'll be able to build you'll have to download some binary dependencies (see below).

On Linux you'll also have to make sure that you have installed some dependencies (see below).

To build Bloom you can open and build the solution in Visual Studio or MonoDevelop, or build from the command line using msbuild/xbuild.

## Get Binary Dependencies

In the `build` directory, run

`getDependencies-windows.sh`

or

`./getDependencies-linux.sh`

That will take several minutes the first time, and afterwards will be quick as it only downloads what has changed. When you change branches, run this again.

## JADE Sources

We use [JADE](http://www.google.com/url?q=http%3A%2F%2Fjade-lang.com%2F&sa=D&sntz=1&usg=AFQjCNGt56mizPKbPZPjua7fjmzoTXAiEQ) as the source language for html. See [these instructions](https://docs.google.com/a/sil.org/document/d/1dYv-yQ18Jandi1TqDwzXIYrZf3M8NIgIQ8Y0rWlXVAI/edit) for setting up a nice JADE evironment in WebStorm.

#### About Bloom Dependencies

Our **[Palaso libraries](https://github.com/sillsdev/libpalaso)** hold the classes that are common to multiple products.

Our **[PdfDroplet ](http://pdfdroplet.palaso.org)**engine drives the booklet-making in the Publish tab. If you need to build PdfDroplet from source, see [projects.palaso.org/projects/pdfdroplet/wiki](http://projects.palaso.org/projects/palaso/wiki).

Our **[Chorus](https://github.com/sillsdev/chorus)** library provides the Send/Receive functionality.

**GeckoFX**: Much of Bloom happens in its embedded Firefox browser. This has two parts: the XulRunner engine, and the [GeckoFX .net wrapper](https://bitbucket.org/geckofx). As of Bloom version 3, we are using xulrunner 29.

**XulRunner**: If you need some other version, they come from here: [http://ftp.mozilla.org/pub/mozilla.org/xulrunner/releases](http://ftp.mozilla.org/pub/mozilla.org/xulrunner/releases). You want a "runtime", not an "sdk". Note, in addition to the generic "lib/xulrunner", the code will also work if it finds "lib/xulrunner8" (or 9, or 10, or whatever the current version is).

More information on XulRunner and GeckoFX: Firefox is a browser which uses XulRunner which uses Gecko rendering engine. GeckoFX is the name of the .net dll which lets you use XulRunner in your WinForms applications, with .net or mono. This is a bit confusing, because GeckoFX is the wrapper but you won't find something called "gecko" coming out of Mozilla and shipping with Bloom. Instead, "XulRunner" comes from Mozilla and ships with Bloom, which accesses it using the GeckoFX dll. Got it?

Now, Mozilla puts out a new version of XulRunner every 6 weeks at the time of this writing, and Hindle's GeckoFX keeps up with that, which is great, but also adds a level of complexity when you're trying to get Bloom going. Bloom needs to have 3 things in sync:
1) XulRunner
2) GeckoFX intended for that version of XulRunner
3) Bloom source code which is expecting that same version of GeckoFX.

Bloom uses various web services that require identification. We can't really keep those a secret, but we can at least not make them google'able by not checking them into github. To get the file that contains user and test-level authorization codes, just get the connections.dll file out of a shipping version of a Bloom, and place it in your Bloom/DistFiles directory.


## Disable analytics

We don't want developer and tester runs (and crashes) polluting our statistics. On Windows, add the environment variable "feedback" with value "off". On Linux, edit $HOME/.profile and add:

		export FEEDBACK=off 

# Special instructions for building on Linux

These notes were written by JohnT on 16 July 2014 based on previous two half-days working with
Eberhard to get Bloom to build on a Precise Linux box. The computer was previously used to
develop FLEx, so may have already had something that is needed. Sorry, I have not had the chance
to try them on another system. If you do, please correct as needed.

Note that as of 16 July 2014, Bloom does not work very well on Linux. Something more may be
needed by the time we get it fully working. Hopefully these notes will be updated.

Updated when Andrew was setting up his system on Precise Linux VM Oct. 2014. Note this VM also had previously been used for FLEx development.

At various points you will be asked for your password.

1. Install `wget`

		sudo apt-get install wget

2. Add the SIL keys for the main and testing SIL package repositiories

		wget -O - http://linux.lsdev.sil.org/downloads/sil-testing.gpg | sudo apt-key add -
		wget -O - http://packages.sil.org/sil.gpg | sudo apt-key add -

3. Make sure you have your system set up to look at the main and testing SIL repositories

	Install Synaptic if you haven't (sudo apt-get install synaptic).

	You need Synaptic to look in some extra places for components. In Synaptic, go to
	`Settings->Repositories`, `Other Software` tab. You want to see the following lines (replace
	`precise` with your distribution version):

		http://packages.sil.org/ubuntu precise main
		http://packages.sil.org/ubuntu precise-experimental main
		http://linux.lsdev.sil.org/ubuntu precise main
		http://linux.lsdev.sil.org/ubuntu precise-experimental main

	If some are missing, click add and paste the missing line, then insert 'deb' at the start,
	then confirm.

	(May help to check for and remove any lines that refer to the obsolete `ppa.palaso.org`, if
	you've been doing earlier work on SIL stuff.)

4. Update your system:

		sudo apt-get update
		sudo apt-get upgrade

5. Clone the Bloom repository:

		mkdir $HOME/palaso
		cd $HOME/palaso
		git clone https://github.com/BloomBooks/BloomDesktop.git

	This should leave you in the default branch, which is currently correct for Linux. Don't be
	misled into activating the Linux branch, which is no longer used.

6. Install MonoDevelop 5 (or later)

	A current MonoDevelop can be found on launchpad: https://launchpad.net/~ermshiperete/+archive/ubuntu/monodevelop
	or https://launchpad.net/~ermshiperete/+archive/ubuntu/monodevelop-beta.

	Follow the installation instructions on the launchpad website (currently a link called "Read about installing").

	Make a shortcut to launch MonoDevelop (or just use this command line). The shortcut should execute something like this:

		bash -c 'PATH=/opt/monodevelop/bin:$PATH; \
			export MONO_ENVIRON="$HOME/palaso/bloom-desktop/environ"; \
			export MONO_GAC_PREFIX=/opt/monodevelop:/opt/mono-sil:/usr:/usr/local; \
			monodevelop-launcher.sh'

	Correct the path in MONO_ENVIRON to point to the Bloom source code directory.

7. Install the dependencies needed for Bloom

		cd $HOME/palaso/bloom-desktop/build
		./install-deps # (Note the initial dot)

	This will also install a custom mono version in `/opt/mono-sil`. However, to successfully
	use it with MonoDevelop, you'll need to do some additional steps.

	Copy this script to /opt/mono-sil/bin:

		wget https://raw.githubusercontent.com/sillsdev/mono-calgary/develop/mono-sil
		sudo mv mono-sil /opt/mono-sil/bin
		sudo chmod +x /opt/mono-sil/bin/mono-sil

	Delete /opt/mono-sil/bin/mono and create two symlinks instead:

		sudo rm /opt/mono-sil/bin/mono
		sudo ln -s /opt/mono-sil/bin/mono-sgen /opt/mono-sil/bin/mono-real
		sudo ln -s /opt/mono-sil/bin/mono-sil /opt/mono-sil/bin/mono

8. Get binary dependencies:

		cd $HOME/palaso/bloom-desktop/build
		./getDependencies-Linux.sh  # (Note the initial dot)
		cd ..
		. environ #(note the '.')
		sudo mozroots --import --sync

9. Open solution in MonoDevelop

	Run MonoDevelop using the shortcut. Open the solution BloomLinux.sln. Go to
	`Edit -> Preferences`, `Packages/Sources`. The list should include
	`https://www.nuget.org/api/v2/`, and `http://build.palaso.org/guestAuth/app/nuget/v1/FeedService.svc/`
	(not sure the second is necessary).

	Add the /opt/mono-sil/ as additional runtime in MonoDevelop (`Edit -> Preferences`, `Projects/.NET Runtimes`). Currently, this is 3.0.4.1 (Oct. 2014).

	When you want to run Bloom you'll have to select the /opt/mono-sil/ as current runtime (Project/Active Runtime).

	At this point you should be able to build the whole BloomLinux solution (right-click in
	Solution pane, choose Build).

10. You'll have to remember to redo the symlink step (end of #7) every time you install a new mono-sil package. You'll notice quickly if you forget because you get an error saying that it can't find XULRUNNER - that's an indication that it didn't source the environ file, either because the wrong runtime is selected or /opt/mono-sil/bin/mono points to mono-sgen instead of the wrapper script mono-sil.

Hopefully we can streamline this process eventually.

# Registry settings

One responsibility of Bloom desktop is to handle url's starting with "bloom://"", such as those used in the bloom library web site when the user clicks "open in Bloom." Making this work requires some registry settings. These are automatically created when you install Bloom. Developers who need this functionality can get it using the build/bloom link.reg file. You need to edit this file first. It contains a full path to Bloom.exe, and the first part of the path will depend on where you have put your working folder. After adjusting that, just double-click it to create the registry entries for handling bloom: urls.

# Testers

Please see [Tips for Testing Palaso Software](https://docs.google.com/document/d/1dkp0edjJ8iqkrYeXdbQJcz3UicyilLR7GxMRIUAGb1E/edit)

# License

Bloom is open source, using the [MIT License](http://sil.mit-license.org). It is Copyright SIL International.
