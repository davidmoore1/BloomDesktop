﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using System.Xml;
using Bloom.Book;
using Bloom.Collection;
using Bloom.web;
using DesktopAnalytics;
using Palaso.IO;

namespace Bloom.Publish
{
	/// <summary>
	/// Contains the logic behind the PublishView control, which involves creating a pdf from the html book and letting you print it.
	/// </summary>
	public class PublishModel : IDisposable
	{
		public BookSelection BookSelection { get; private set; }

		public string PdfFilePath { get; private set; }

		public enum DisplayModes
		{
			WaitForUserToChooseSomething,
			Working,
			ShowPdf,
			Upload,
			Printing,
			ResumeAfterPrint
		}

		public enum BookletPortions
		{
			None,
			AllPagesNoBooklet,
			BookletCover,
			BookletPages,//include front and back matter that isn't coverstock
			InnerContent//excludes all front and back matter
		}

		public enum BookletLayoutMethod
		{
			NoBooklet,
			SideFold,
			CutAndStack,
			Calendar
		}

		private Book.Book _currentlyLoadedBook;
		private PdfMaker _pdfMaker;
		private readonly CurrentEditableCollectionSelection _currentBookCollectionSelection;
		private readonly CollectionSettings _collectionSettings;
		private readonly BookServer _bookServer;
		private readonly HtmlThumbNailer _htmlThumbNailer;
		private string _lastDirectory;

		public PublishModel(BookSelection bookSelection, PdfMaker pdfMaker, CurrentEditableCollectionSelection currentBookCollectionSelection, CollectionSettings collectionSettings,
			BookServer bookServer, HtmlThumbNailer htmlThumbNailer)
		{
			BookSelection = bookSelection;
			_pdfMaker = pdfMaker;
			//_pdfMaker.EngineChoice = collectionSettings.PdfEngineChoice;
			_currentBookCollectionSelection = currentBookCollectionSelection;
			ShowCropMarks=false;
			_collectionSettings = collectionSettings;
			_bookServer = bookServer;
			_htmlThumbNailer = htmlThumbNailer;
			bookSelection.SelectionChanged += new EventHandler(OnBookSelectionChanged);
			//we don't want to default anymore: BookletPortion = BookletPortions.BookletPages;
		}

		public PublishView View { get; set; }

		// True when we are showing the controls for uploading. (Review: does this belong in the model or view?)
		public bool UploadMode { get; set; }

		public bool PdfGenerationSucceeded { get; set; }

		private void OnBookSelectionChanged(object sender, EventArgs e)
		{
			//some of this checking is about bl-272, which was replicated by having one book, going to publish, then deleting that last book.
			if (BookSelection != null && View != null && BookSelection.CurrentSelection!=null && _currentlyLoadedBook != BookSelection.CurrentSelection && View.Visible)
			{
				PageLayout = BookSelection.CurrentSelection.GetLayout();
			}
		}


		public void LoadBook(BackgroundWorker worker, DoWorkEventArgs doWorkEventArgs)
		{
			_currentlyLoadedBook = BookSelection.CurrentSelection;

			try
			{
				using(var tempHtml = MakeFinalHtmlForPdfMaker())
				{
					if (doWorkEventArgs.Cancel)
						return;

					BookletLayoutMethod layoutMethod;
					if (this.BookletPortion == BookletPortions.AllPagesNoBooklet)
						layoutMethod = BookletLayoutMethod.NoBooklet;
					else
						layoutMethod = BookSelection.CurrentSelection.GetDefaultBookletLayout();

					_pdfMaker.MakePdf(tempHtml.Key, PdfFilePath, PageLayout.SizeAndOrientation.PageSizeName,
						PageLayout.SizeAndOrientation.IsLandScape, LayoutPagesForRightToLeft,
						layoutMethod, BookletPortion, worker, doWorkEventArgs, View);
				}
			}
			catch (Exception e)
			{
				//we can't safely do any ui-related work from this thread, like putting up a dialog
				doWorkEventArgs.Result = e;
				//                Palaso.Reporting.ErrorReport.NotifyUserOfProblem(e, "There was a problem creating a PDF from this book.");
				//                SetDisplayMode(DisplayModes.WaitForUserToChooseSomething);
				//                return;
			}
		}

		private bool LayoutPagesForRightToLeft
		{
			get { return _collectionSettings.IsLanguage1Rtl;  }

		}

		private SimulatedPageFile MakeFinalHtmlForPdfMaker()
		{
			PdfFilePath = GetPdfPath(Path.GetFileName(_currentlyLoadedBook.FolderPath));

			var dom = BookSelection.CurrentSelection.GetDomForPrinting(BookletPortion, _currentBookCollectionSelection.CurrentSelection, _bookServer);

			HtmlDom.AddPublishClassToBody(dom.RawDom);
			HtmlDom.AddHidePlaceHoldersClassToBody(dom.RawDom);

			//we do this now becuase the publish ui allows the user to select a different layout for the pdf than what is in the book file
			SizeAndOrientation.UpdatePageSizeAndOrientationClasses(dom.RawDom, PageLayout);
			PageLayout.UpdatePageSplitMode(dom.RawDom);

			XmlHtmlConverter.MakeXmlishTagsSafeForInterpretationAsHtml(dom.RawDom);
			dom.UseOriginalImages = true; // don't want low-res images or transparency in PDF.
			return EnhancedImageServer.MakeRealPublishModeFileInBookFolder(dom);
		}

		private string GetPdfPath(string fname)
		{
			string path = null;

			// Sanitize fileName first
			string fileName = SanitizeFileName(fname);

			for (int i = 0; i < 100; i++)
			{
				path = Path.Combine(Path.GetTempPath(), string.Format("{0}-{1}.pdf", fileName, i));
				if (!File.Exists(path))
					break;

				try
				{
					File.Delete(path);
					break;
				}
				catch (Exception)
				{
					//couldn't delete it? then increment the suffix and try again
				}
			}
			return path;
		}

		/// <summary>
		/// Ampersand in book title was causing Publish problems
		/// </summary>
		/// <param name="fileName"></param>
		/// <returns></returns>
		private static string SanitizeFileName(string fileName)
		{
			fileName = Path.GetInvalidFileNameChars().Aggregate(
				fileName, (current, character) => current.Replace(character, ' '));
			// I (GJM) set this up to keep ampersand out of the book title,
			// but discovered that ampersand isn't one of the characters that GetInvalidFileNameChars returns!
			fileName = fileName.Replace('&', ' ');
			return fileName;
		}

		DisplayModes _currentDisplayMode = DisplayModes.WaitForUserToChooseSomething;
		internal DisplayModes DisplayMode
		{
			get
			{
				return _currentDisplayMode;
			}
			set
			{
				_currentDisplayMode = value;
				if (View != null)
					View.Invoke((Action) (() => View.SetDisplayMode(value)));
			}
		}

		public void Dispose()
		{
			if (File.Exists(PdfFilePath))
			{
				try
				{
					File.Delete(PdfFilePath);
				}
				catch (Exception)
				{

				}
			}

			GC.SuppressFinalize(this);
		}

		public BookletPortions BookletPortion { get; set; }

		/// <summary>
		/// The book itself has a layout, but we can override it here during publishing
		/// </summary>
		public Layout PageLayout { get; set; }

		public bool ShowCropMarks
		{
			get { return _pdfMaker.ShowCropMarks; }
			set { _pdfMaker.ShowCropMarks = value; }
		}

		public bool AllowUpload {
			get { return BookSelection.CurrentSelection.BookInfo.AllowUploading; }
		}

		public bool ShowBookletOption
		{
			get { return BookSelection.CurrentSelection.BookInfo.BookletMakingIsAppropriate; }
		}

		public bool ShowCoverOption
		{
			//currently the only cover option we have is a booklet one
			get { return BookSelection.CurrentSelection.BookInfo.BookletMakingIsAppropriate; }
		}

		public override bool Equals(object obj)
		{
			if (obj == null)
				return false;

			var m = (PublishModel)obj ;
			return m.BookletPortion == BookletPortion && m.PageLayout == PageLayout;
		}

		public void Save()
		{
			try
			{
				// Give a slight preference to USB keys, though if they used a different directory last time, we favor that.

				if (string.IsNullOrEmpty(_lastDirectory) || !Directory.Exists(_lastDirectory))
				{
					var drives = Palaso.UsbDrive.UsbDriveInfo.GetDrives();
					if (drives != null && drives.Count > 0)
					{
						_lastDirectory = drives[0].RootDirectory.FullName;
					}
				}

				using (var dlg = new SaveFileDialog())
				{
					if (!string.IsNullOrEmpty(_lastDirectory) && Directory.Exists(_lastDirectory))
						dlg.InitialDirectory = _lastDirectory;
					var portion = "";
					switch (BookletPortion)
					{
						case BookletPortions.None:
							Debug.Fail("Save should not be enabled");
							return;
						case BookletPortions.AllPagesNoBooklet:
							portion = "Pages";
							break;
						case BookletPortions.BookletCover:
							portion = "Cover";
							break;
						case BookletPortions.BookletPages:
							portion = "Inside";
							break;
						default:
							throw new ArgumentOutOfRangeException();
					}
					string suggestedName = string.Format("{0}-{1}-{2}.pdf", Path.GetFileName(_currentlyLoadedBook.FolderPath),
														 _collectionSettings.GetLanguage1Name("en"), portion);
					dlg.FileName = suggestedName;
					dlg.Filter = "PDF|*.pdf";
					if (DialogResult.OK == dlg.ShowDialog())
					{
						_lastDirectory = Path.GetDirectoryName(dlg.FileName);
						File.Copy(PdfFilePath, dlg.FileName, true);
						Analytics.Track("Save PDF", new Dictionary<string, string>()
							{
								{"Portion",  Enum.GetName(typeof(BookletPortions), BookletPortion)},
								{"Layout", PageLayout.ToString()}
							});
					}
				}
			}
			catch (Exception err)
			{
				Palaso.Reporting.ErrorReport.NotifyUserOfProblem("Bloom was not able to save the PDF.  {0}", err.Message);
			}
		}

		public void DebugCurrentPDFLayout()
		{

//			var dom = BookSelection.CurrentSelection.GetDomForPrinting(BookletPortion, _currentBookCollectionSelection.CurrentSelection, _bookServer);
//
//			SizeAndOrientation.UpdatePageSizeAndOrientationClasses(dom, PageLayout);
//			PageLayout.UpdatePageSplitMode(dom);
//
//			XmlHtmlConverter.MakeXmlishTagsSafeForInterpretationAsHtml(dom);
//			var tempHtml = BloomTemp.TempFile.CreateHtm5FromXml(dom); //nb: we intentially don't ever delete this, to aid in debugging
//			//var tempHtml = TempFile.WithExtension(".htm");
//
//			var settings = new XmlWriterSettings {Indent = true, CheckCharacters = true};
//			using (var writer = XmlWriter.Create(tempHtml.Path, settings))
//			{
//				dom.WriteContentTo(writer);
//				writer.Close();
//			}

//			System.Diagnostics.Process.Start(tempHtml.Path);

			var htmlFilePath = MakeFinalHtmlForPdfMaker().Key;
			if (Palaso.PlatformUtilities.Platform.IsWindows)
				Process.Start("Firefox.exe", '"' + htmlFilePath + '"');
			else
				Process.Start("xdg-open", htmlFilePath);
		}

		public void RefreshValuesUponActivation()
		{
			if (BookSelection.CurrentSelection != null)
			{
				PageLayout = BookSelection.CurrentSelection.GetLayout();
			}

		}

		[Import("GetPublishingMenuCommands")]//, AllowDefault = true)]
		private Func<IEnumerable<ToolStripItem>> _getExtensionMenuItems;

		public IEnumerable<HtmlDom> GetPageDoms()
		{
			if (BookSelection.CurrentSelection.IsFolio)
			{
				foreach (var bi in _currentBookCollectionSelection.CurrentSelection.GetBookInfos())
				{
					var book = _bookServer.GetBookFromBookInfo(bi);
					//need to hide the "notes for illustrators" on SHRP, which is controlled by the layout
					book.SetLayout(new Layout()
					{
						SizeAndOrientation =  SizeAndOrientation.FromString("B5Portrait"),
						Style = "HideProductionNotes"
					});
					foreach (var page in  book.GetPages())
					{
						//yield return book.GetPreviewXmlDocumentForPage(page);

						var previewXmlDocumentForPage = book.GetPreviewXmlDocumentForPage(page);
						BookStorage.SetBaseForRelativePaths(previewXmlDocumentForPage, book.FolderPath);
						HtmlDom.AddPublishClassToBody(previewXmlDocumentForPage.RawDom);
						HtmlDom.AddHidePlaceHoldersClassToBody(previewXmlDocumentForPage.RawDom);

						yield return previewXmlDocumentForPage;
					}
				}
			}
			else //this one is just for testing, it's not especially fruitfal to export for a single book
			{
				//need to hide the "notes for illustrators" on SHRP, which is controlled by the layout
				BookSelection.CurrentSelection.SetLayout(new Layout()
				{
					SizeAndOrientation = SizeAndOrientation.FromString("B5Portrait"),
					Style = "HideProductionNotes"
				});

				foreach (var page in BookSelection.CurrentSelection.GetPages())
				{
					var previewXmlDocumentForPage = BookSelection.CurrentSelection.GetPreviewXmlDocumentForPage(page);
					//get the original images, not compressed ones (just in case the thumbnails are, like, full-size & they want quality)
					BookStorage.SetBaseForRelativePaths(previewXmlDocumentForPage, BookSelection.CurrentSelection.FolderPath);
					HtmlDom.AddPublishClassToBody(previewXmlDocumentForPage.RawDom);
					HtmlDom.AddHidePlaceHoldersClassToBody(previewXmlDocumentForPage.RawDom);
					yield return previewXmlDocumentForPage;
				}
			}
		}


		public void GetThumbnailAsync(int width, int height, HtmlDom dom,Action<Image> onReady ,Action<Exception> onError)
		{
			var thumbnailOptions = new HtmlThumbNailer.ThumbnailOptions()
			{
				BackgroundColor = Color.White,
				BorderStyle = HtmlThumbNailer.ThumbnailOptions.BorderStyles.None,
				CenterImageUsingTransparentPadding = false,
				//210x147 is about what the TG's expect, but we're going to tripple that in case it makes for better printing
				Height = 630,
				Width = 441,
			};
			dom.UseOriginalImages = true; // apparently these thumbnails can be big...anyway we want printable images.
			_htmlThumbNailer.GetThumbnailAsync(String.Empty, string.Empty, dom, thumbnailOptions,onReady, onError);
		}

		public IEnumerable<ToolStripItem> GetExtensionMenuItems()
		{
			//for now we're not doing real extension dlls, just kind of faking it. So we will limit this load
			//to books we know go with this currently "built-in" "extension" for SIL LEAD's SHRP Project.
			if (SHRP_PupilBookExtension.ExtensionIsApplicable(BookSelection.CurrentSelection))
			{
				//load any extension assembly found in the template's root directory
				//var catalog = new DirectoryCatalog(this.BookSelection.CurrentSelection.FindTemplateBook().FolderPath, "*.dll");
				var catalog = new AssemblyCatalog(Assembly.GetExecutingAssembly());
				var container = new CompositionContainer(catalog);
				//inject what we have to offer for the extension to consume
				container.ComposeExportedValue<string>("PathToBookFolder",BookSelection.CurrentSelection.FolderPath);
				container.ComposeExportedValue<string>("Language1Iso639Code", _collectionSettings.Language1Iso639Code);
				container.ComposeExportedValue<Func<IEnumerable<HtmlDom>>>(GetPageDoms);
			  //  container.ComposeExportedValue<Func<string>>("pathToPublishedHtmlFile",GetFileForPrinting);
				//get the original images, not compressed ones (just in case the thumbnails are, like, full-size & they want quality)
				container.ComposeExportedValue<Action<int, int, HtmlDom, Action<Image>, Action<Exception>>>(GetThumbnailAsync);
				container.SatisfyImportsOnce(this);
				return _getExtensionMenuItems == null ? new List<ToolStripItem>() : _getExtensionMenuItems();
			}
			else
			{
				return new List<ToolStripMenuItem>();
			}
		}
//
//	    private IEnumerable<XmlDocument> GetFileForPrinting()
//	    {
////            XmlDocument dom = BookSelection.CurrentSelection.GetDomForPrinting(BookletPortions.InnerContent, _currentBookCollectionSelection.CurrentSelection, _bookServer);
////            HtmlDom.AddPublishClassToBody(dom);
////
////	        foreach (var pageDom in dom.SelectNodes("/html/body//div[contains(@class,'bloom-page')]"))
////	        {
////	            yield return pageDom;
////	        }
//	    }
	}
}
