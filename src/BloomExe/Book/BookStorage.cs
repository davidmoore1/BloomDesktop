using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Security;
using System.Text;
using System.Xml;
using Bloom.Collection;
using Bloom.ImageProcessing;
using Bloom.Properties;
using Palaso.Code;
using Palaso.Extensions;
using Palaso.IO;
using Palaso.Progress;
using Palaso.Reporting;
using Palaso.UI.WindowsForms.FileSystem;
using Palaso.Xml;

namespace Bloom.Book
{
	/* The role of this class is simply to isolate the actual storage mechanism (e.g. file system)
	 * to a single place.  All the other classes can then just pass around DOMs.
	 */


	public interface IBookStorage
	{
		HtmlDom Dom { get; }
		Book.BookType BookType { get; }
		//string GetTemplateName();
		string Key { get; }
		bool LooksOk { get; }
		string FileName { get; }
		string FolderPath { get; }
		string PathToExistingHtml { get; }
		void Save();
		bool TryGetPremadeThumbnail(out Image image);
		HtmlDom GetRelocatableCopyOfDom(IProgress log);
		bool DeleteBook();
		//void HideAllTextAreasThatShouldNotShow(string vernacularIso639Code, string optionalPageSelector);
		string SaveHtml(HtmlDom bookDom);
		//string GetVernacularTitleFromHtml(string Iso639Code);
		void SetBookName(string name);
		string GetValidateErrors();
		void UpdateBookFileAndFolderName(CollectionSettings settings);
		bool RemoveBookThumbnail();
		IFileLocator GetFileLocator();
	}

	public class BookStorage : IBookStorage
	{
		/// <summary>
		/// History of this number:
		///		0.4 had version 0.4
		/// </summary>
		private const string kBloomFormatVersion = "0.8";

		private  string _folderPath;
		private readonly IChangeableFileLocator _fileLocator;
		private readonly BookRenamedEvent _bookRenamedEvent;
		public string ErrorMessages;
		private static bool _alreadyNotifiedAboutOneFailedCopy;
		private readonly HtmlDom _dom; //never remove the readonly: this is shared by others

		public delegate BookStorage Factory(string folderPath);//autofac uses this

		public BookStorage(string folderPath, Palaso.IO.IChangeableFileLocator baseFileLocator, BookRenamedEvent bookRenamedEvent)
		{
			Debug.WriteLine(string.Format("BookStorage({0})", folderPath));
			_folderPath = folderPath;
			//the fileLocator we get doesn't know anything about this particular book
			_fileLocator = baseFileLocator;
			_bookRenamedEvent = bookRenamedEvent;
			_fileLocator.AddPath(folderPath);

			_dom = new HtmlDom();

			RequireThat.Directory(folderPath).Exists();
			if (!File.Exists(PathToExistingHtml))
			{
				var files = new List<string>(Directory.GetFiles(folderPath));
				var b = new StringBuilder();
				b.AppendLine("Could not determine which html file in the folder to use.");
				if (files.Count == 0)
					b.AppendLine("***There are no files.");
				else
				{
					b.AppendLine("Files in this book are:");
					foreach (var f in files)
					{
						b.AppendLine("  "+f);
					}
				}
				throw new ApplicationException(b.ToString());
			}
			else
			{
				//Validating here was taking a 1/3 of the startup time
				// eventually, we need to restructure so that this whole Storage isn't created until actually needed, then maybe this can come back
				//			ErrorMessages = ValidateBook(PathToExistingHtml);

				if (!string.IsNullOrEmpty(ErrorMessages))
				{
					//hack so we can package this for palaso reporting
//                    var ex = new XmlSyntaxException(ErrorMessages);
//                    Palaso.Reporting.ErrorReport.NotifyUserOfProblem(ex, "Bloom did an integrity check of the book named '{0}', and found something wrong. This doesn't mean your work is lost, but it does mean that there is a bug in the system or templates somewhere, and the developers need to find and fix the problem (and your book).  Please click the 'Details' button and send this report to the developers.", Path.GetFileName(PathToExistingHtml));
					_dom.RawDom.LoadXml("<html><body>There is a problem with the html structure of this book which will require expert help.</body></html>");
					Logger.WriteEvent("{0}: There is a problem with the html structure of this book which will require expert help: {1}", PathToExistingHtml, ErrorMessages);
			   }
				else
				{
					Logger.WriteEvent("BookStorage Loading Dom from {0}", PathToExistingHtml);

					var xmlDomFromHtmlFile = XmlHtmlConverter.GetXmlDomFromHtmlFile(PathToExistingHtml);
					_dom = new HtmlDom(xmlDomFromHtmlFile); //with throw if there are errors
				}

				//todo: this would be better just to add to those temporary copies of it. As it is, we have to remove it for the webkit printing
				//SetBaseForRelativePaths(Dom, folderPath); //needed because the file itself may be off in the temp directory

				//UpdateStyleSheetLinkPaths(fileLocator);

				Dom.UpdatePageDivs();

				UpdateSupportFiles();

			}
		}

		/// <summary>
		/// we update these so that the file continues to look the same when you just open it in firefox
		/// </summary>
		private void UpdateSupportFiles()
		{
			UpdateIfNewer("placeHolder.png");
			UpdateIfNewer("basePage.css");
			UpdateIfNewer("previewMode.css");

			foreach (var path in Directory.GetFiles(_folderPath, "*.css"))
			{
				var file = Path.GetFileName(path);
				//if (file.ToLower().Contains("portrait") || file.ToLower().Contains("landscape"))
					UpdateIfNewer(file);
			}

		}
		private void UpdateIfNewer(string fileName)
		{
			string factoryPath="notSet";
			string documentPath="notSet";
			try
			{
				factoryPath = _fileLocator.LocateFile(fileName);
				if(string.IsNullOrEmpty(factoryPath))//happens during unit testing
					return;

				var factoryTime = File.GetLastWriteTimeUtc(factoryPath);
				documentPath = Path.Combine(_folderPath, fileName);
				if(!File.Exists(documentPath))
				{
					Logger.WriteEvent("BookStorage.UpdateIfNewer() Copying missing file {0} to {1}", factoryPath, documentPath);
					File.Copy(factoryPath, documentPath);
					return;
				}
				var documentTime = File.GetLastWriteTimeUtc(documentPath);
				if(factoryTime> documentTime)
				{
					if((File.GetAttributes(documentPath) & FileAttributes.ReadOnly) != 0)
					{
						Palaso.Reporting.ErrorReport.NotifyUserOfProblem("Could not update one of the support files in this document ({0}) because the destination was marked ReadOnly.", documentPath);
						return;
					}
					Logger.WriteEvent("BookStorage.UpdateIfNewer() Updating file {0} to {1}", factoryPath, documentPath);

					File.Copy(factoryPath, documentPath,true);
					//if the source was locked, don't copy the lock over
					File.SetAttributes(documentPath,FileAttributes.Normal);
				}
			}
			catch (Exception e)
			{
				if(documentPath.Contains(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFiles))
					|| documentPath.ToLower().Contains("program"))//english only
				{
					Logger.WriteEvent("Could not update file {0} because it was in the program directory.", documentPath);
					return;
				}
				if(_alreadyNotifiedAboutOneFailedCopy)
					return;//don't keep bugging them
				_alreadyNotifiedAboutOneFailedCopy = true;
				Palaso.Reporting.ErrorReport.NotifyUserOfProblem(e,
					"Could not update one of the support files in this document ({0} to {1}). This is normally because the folder is 'locked' or the file is marked 'read only'.", factoryPath,documentPath);
			}
		}

		private void EnsureHasCollectionAndBookStylesheets(HtmlDom dom)
		{
			string autocssFilePath = _fileLocator.LocateFile(@"settingsCollectionStyles.css");
			if (!string.IsNullOrEmpty(autocssFilePath))
				EnsureHasStyleSheet(dom, autocssFilePath);

			string customCssFilePath = _fileLocator.LocateFile(@"customCollectionStyles.css");
			if (!string.IsNullOrEmpty(customCssFilePath))
				EnsureHasStyleSheet(dom, customCssFilePath);

			if (File.Exists(Path.Combine(_folderPath, "customBookStyles.css")))
				EnsureHasStyleSheet(dom,"customBookStyles.css");
		}

		private void EnsureHasStyleSheet(HtmlDom dom, string path)
		{
			foreach (XmlElement link in dom.SafeSelectNodes("//link[@rel='stylesheet']"))
			{
				var fileName = link.GetStringAttribute("href");
				if (fileName == path)
					return;
			}
			dom.AddStyleSheet(path);
		}

		private void UpdateStyleSheetLinkPaths(HtmlDom dom, IFileLocator fileLocator, IProgress log)
		{
			foreach (XmlElement linkNode in dom.SafeSelectNodes("/html/head/link"))
			{
				var href = linkNode.GetAttribute("href");
				if (href == null)
				{
					continue;
				}
				//TODO: what cause this to get encoded this way? Saw it happen when creating wall calendar
				href = href.Replace("%5C", "/");


				var fileName = Path.GetFileName(href);
				if (!fileName.StartsWith("xx")) //I use xx  as a convenience to temporarily turn off stylesheets during development
				{
					var path = fileLocator.LocateOptionalFile(fileName);

					if (string.IsNullOrEmpty(path)||
							path.Contains("languageDisplay.css")) //todo: this feels hacky... problem is that unlike most stylesheets, it is customized for this folder, and the ones found in the factorytemplates should not be used.
					{
						//look in the same directory as the book
						var local = Path.Combine(_folderPath, fileName);
						if (File.Exists(local))
							path = local;
					}
					if (!string.IsNullOrEmpty(path))
					{
						linkNode.SetAttribute("href", "file://" + path);
					}
					else
					{
						log.WriteError("Bloom could not find the stylesheet '{0}', which is used in {1}", fileName, _folderPath);
					}
				}
			}
		}


		//while in Bloom, we could have and edit style sheet or (someday) other modes. But when stored,
		//we want to make sure it's ready to be opened in a browser.
		private static void MakeCssLinksAppropriateForStoredFile(HtmlDom dom)
		{
			dom.RemoveModeStyleSheets();
			dom.AddStyleSheet("previewMode.css");
			dom.AddStyleSheet("basePage.css");
		}


		public static void SetBaseForRelativePaths(HtmlDom dom, string folderPath, bool pointAtEmbeddedServer)
		{
			string path = "";
			if (!string.IsNullOrEmpty(folderPath))
			{
				if (pointAtEmbeddedServer && Settings.Default.ImageHandler=="http" && ImageServer.IsAbleToUsePort)
				{
					//this is only used by relative paths, and only img src's are left relative.
					//we are redirecting through our build-in httplistener in order to shrink
					//big images before giving them to gecko which has trouble with really hi-res ones
					var uri = folderPath + Path.DirectorySeparatorChar;
					uri = uri.Replace(":", "%3A");
					uri = uri.Replace('\\', '/');
					uri = ImageServer.GetPathEndingInSlash() + uri;
					path = uri;
				}
				else
				{
					path = "file://" + folderPath + Path.DirectorySeparatorChar;
				}
			}
			dom.SetBaseForRelativePaths(path);
		}



		public HtmlDom Dom
		{
			get {return _dom;}
		}

		public Book.BookType BookType
		{
			get
			{
				var pathToHtml = PathToExistingHtml;
				if (pathToHtml.EndsWith("templatePages.htm"))
					return Book.BookType.Template;
				if (pathToHtml.EndsWith("shellPages.htm"))
					return Book.BookType.Shell;

				//directory name matches htm name
//                if (!string.IsNullOrEmpty(pathToHtml) && Path.GetFileName(Path.GetDirectoryName(pathToHtml)) == Path.GetFileNameWithoutExtension(pathToHtml))
//                {
//                    return Book.BookType.Publication;
//                }
				return Book.BookType.Publication;
			}
		}


		public string PathToExistingHtml
		{
			get { return FindBookHtmlInFolder(_folderPath); }
		}

		public static string FindBookHtmlInFolder(string folderPath)
		{
			string p = Path.Combine(folderPath, Path.GetFileName(folderPath) + ".htm");
			if (File.Exists(p))
				return p;

			if (!Directory.Exists(folderPath)) //bl-291 (user had 4 month-old version, so the bug may well be long gone)
			{
				Palaso.Reporting.ErrorReport.NotifyUserOfProblem("Bloom has a pesky bug we've been searching for, and you've found it. Most likely, you won't lose any work, but we do need to report the problem and then have you restart. Bloom will now show an error box where you can tell us anything that might help us understand how to reproduce the problem, and let you email it to us.\r\nThanks for your help!");
				throw new ApplicationException(string.Format("In FindBookHtmlInFolder('{0}'), the folder does not exist. (ref bl-291)", folderPath));
			}

			//ok, so maybe they changed the name of the folder and not the htm. Can we find a *single* html doc?
			var candidates = new List<string>(Directory.GetFiles(folderPath, "*.htm"));
			candidates.Remove(folderPath.CombineForPath("configuration.htm"));
			candidates.Remove(folderPath.CombineForPath("credits.htm"));
			candidates.Remove(folderPath.CombineForPath("instructions.htm"));
			if (candidates.Count == 1)
				return candidates[0];

			//template
			p = Path.Combine(folderPath, "templatePages.htm");
			if (File.Exists(p))
				return p;

			return string.Empty;
		}




		public string Key
		{
			get
			{
				return _folderPath;
			}
		}

		public bool LooksOk
		{
			get { return File.Exists(PathToExistingHtml) && string.IsNullOrEmpty(ErrorMessages); }
		}

		public string FileName
		{
			get { return Path.GetFileNameWithoutExtension(_folderPath); }
		}

		public string FolderPath
		{
			get { return _folderPath; }
		}

		public void Save()
		{
			Logger.WriteEvent("BookStorage.Saving... (eventual destination: {0})",PathToExistingHtml);

			Guard.Against(BookType != Book.BookType.Publication, "Tried to save a non-editable book.");
			Dom.UpdateMetaElement("Generator", "Bloom " + ErrorReport.GetVersionForErrorReporting());
			if(null!= Assembly.GetEntryAssembly()) // null during unit tests
			{
				var ver = Assembly.GetEntryAssembly().GetName().Version;
				Dom.UpdateMetaElement("BloomFormatVersion", kBloomFormatVersion);
			}
			string tempPath = SaveHtml(Dom);


			string errors = ValidateBook(tempPath);
			if (!string.IsNullOrEmpty(errors))
			{
				var badFilePath = PathToExistingHtml + ".bad";
				File.Copy(tempPath, badFilePath,true);
				//hack so we can package this for palaso reporting
				errors += "\r\n\r\n\r\nContents:\r\n\r\n" + File.ReadAllText(badFilePath);
				var ex = new XmlSyntaxException(errors);

				Palaso.Reporting.ErrorReport.NotifyUserOfProblem(ex, "Before saving, Bloom did an integrity check of your book, and found something wrong. This doesn't mean your work is lost, but it does mean that there is a bug in the system or templates somewhere, and the developers need to find and fix the problem (and your book).  Please click the 'Details' button and send this report to the developers.  Bloom has saved the bad version of this book as " + badFilePath + ".  Bloom will now exit, and your book will probably not have this recent damage.  If you are willing, please try to do the same steps again, so that you can report exactly how to make it happen.");
				Process.GetCurrentProcess().Kill();
			}
			else
			{
				Logger.WriteMinorEvent("ReplaceFileWithUserInteractionIfNeeded({0},{1})", tempPath, PathToExistingHtml);
				if (!string.IsNullOrEmpty(tempPath))
				{    Palaso.IO.FileUtils.ReplaceFileWithUserInteractionIfNeeded(tempPath, PathToExistingHtml, null);}

			}
		}



		public string SaveHtml(HtmlDom dom)
		{
			string tempPath = Path.GetTempFileName();
			MakeCssLinksAppropriateForStoredFile(dom);
			SetBaseForRelativePaths(dom, string.Empty, false);// remove any dependency on this computer, and where files are on it.

			return XmlHtmlConverter.SaveDOMAsHtml5(dom.RawDom, tempPath);
		}



		public static string ValidateBook(string path)
		{
			var dom = new HtmlDom(XmlHtmlConverter.GetXmlDomFromHtmlFile(path));//with throw if there are errors
			return dom.ValidateBook(path);
		}


		/// <summary>
		///
		/// </summary>
		/// <returns>false we shouldn't mess with the thumbnail</returns>
		public bool RemoveBookThumbnail()
		{
			string path = Path.Combine(_folderPath, "thumbnail.png");
			if(new System.IO.FileInfo(path).IsReadOnly) //readonly is good when you've put in a custom thumbnail
				return false;
			if (File.Exists(path))
			{
				File.Delete(path);
			}
			return true;
		}

		/// <summary>
		/// this is a method because it wasn't clear if we will eventually generate it on the fly (book paths do change as they are renamed)
		/// </summary>
		/// <returns></returns>
		public IFileLocator GetFileLocator()
		{
			return _fileLocator;
		}

		public bool TryGetPremadeThumbnail(out Image image)
		{
			string path = Path.Combine(_folderPath, "thumbnail.png");
			if (File.Exists(path))
			{
				//this FromFile thing locks the file until the image is disposed of. Therefore, we copy the image and dispose of the original.
				using (var tempImage = Image.FromFile(path))
				{
					image = new Bitmap(tempImage);
				}
				return true;
			}
			image = null;
			return false;
		}


		public HtmlDom GetRelocatableCopyOfDom(IProgress log)
		{
			HtmlDom relocatableDom = Dom.Clone();

			SetBaseForRelativePaths(relocatableDom, _folderPath, true);
			EnsureHasCollectionAndBookStylesheets(relocatableDom);
			UpdateStyleSheetLinkPaths(relocatableDom, _fileLocator, log);

			return relocatableDom;
		}



		public bool DeleteBook()
		{
			var didDelete= ConfirmRecycleDialog.Recycle(_folderPath);
			if(didDelete)
				Logger.WriteEvent("After BookStorage.DeleteBook({0})", _folderPath);
			return didDelete;
		}


		public void SetBookName(string name)
		{
			if (!Directory.Exists(_folderPath)) //bl-290 (user had 4 month-old version, so the bug may well be long gone)
			{
				Palaso.Reporting.ErrorReport.NotifyUserOfProblem("Bloom has a pesky bug we've been searching for, and you've found it. Most likely, you won't lose any work, but we do need to report the problem and then have you restart. Bloom will now show an error box where you can tell us anything that might help us understand how to reproduce the problem, and let you email it to us.\r\nThanks for your help!");
				throw new ApplicationException(string.Format("In SetBookName('{0}'), BookStorage thinks the existing folder is '{1}', but that does not exist. (ref bl-290)", name,_folderPath  ));
			}
			name = SanitizeNameForFileSystem(name);

			var currentFilePath =PathToExistingHtml;
			//REVIEW: This doesn't immediataly make sense; if this functino is told to call it Foo but it's current Foo1... why does this just return?

			if (Path.GetFileNameWithoutExtension(currentFilePath).StartsWith(name)) //starts with because maybe we have "myBook1"
				return;

			//figure out what name we're really going to use (might need to add a number suffix)
			var newFolderPath = Path.Combine(Directory.GetParent(FolderPath).FullName, name);
			newFolderPath = GetUniqueFolderPath(newFolderPath);

			Logger.WriteEvent("Renaming html from '{0}' to '{1}.htm'", currentFilePath,newFolderPath);

			//next, rename the file
			File.Move(currentFilePath, Path.Combine(FolderPath, Path.GetFileName(newFolderPath) + ".htm"));

			 //next, rename the enclosing folder
			var fromToPair = new KeyValuePair<string, string>(FolderPath, newFolderPath);
			try
			{
				Logger.WriteEvent("Renaming folder from '{0}' to '{1}'", FolderPath, newFolderPath);

				Palaso.IO.DirectoryUtilities.MoveDirectorySafely(FolderPath, newFolderPath);

				_fileLocator.RemovePath(FolderPath);
				_fileLocator.AddPath(newFolderPath);

				_folderPath = newFolderPath;
			}
			catch (Exception e)
			{
				Logger.WriteEvent("Failed folder rename: "+e.Message);
				Debug.Fail("(debug mode only): could not rename the folder");
			}

			_bookRenamedEvent.Raise(fromToPair);
		}

		public string GetValidateErrors()
		{
			if(!Directory.Exists(_folderPath))
			{
				return "The directory (" + _folderPath + ") could not be found.";
			}
			if(!File.Exists(PathToExistingHtml))
			{
				return "Could not find an html file to use.";
			}
			return ValidateBook(PathToExistingHtml);
		}

		public void UpdateBookFileAndFolderName(CollectionSettings collectionSettings)
		{
			var title = Dom.Title;
			if (title != null)
			{
				SetBookName(title);
			}
		}

		private string SanitizeNameForFileSystem(string name)
		{
			foreach(char c in Path.GetInvalidFileNameChars())
			{
				name = name.Replace(c, ' ');
			}
			name = name.Trim();
			const int MAX = 50;//arbitrary
			if(name.Length >MAX)
				return name.Substring(0, MAX);
			return name;
		}

		/// <summary>
		/// if necessary, append a number to make the folder path unique
		/// </summary>
		/// <param name="folderPath"></param>
		/// <returns></returns>
		private string GetUniqueFolderPath(string folderPath)
		{
			int i = 0;
			string suffix = "";
			var parent = Directory.GetParent(folderPath).FullName;
			var name = Path.GetFileName(folderPath);
			while (Directory.Exists(Path.Combine(parent, name + suffix)))
			{
				++i;
				suffix = i.ToString();
			}
			return Path.Combine(parent, name + suffix);
		}
	}

}