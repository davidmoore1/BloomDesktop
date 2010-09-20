﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Xml;
using Bloom.Edit;
using Bloom.Properties;
using Palaso.Code;
using Palaso.IO;

namespace Bloom
{
	public class Book
	{
		public delegate Book Factory(BookStorage storage);//autofac uses this

		private readonly ITemplateFinder _templateFinder;
		private readonly IFileLocator _fileLocator;
		private HtmlThumbNailer _thumbnailProvider;
		private readonly PageSelection _pageSelection;
		private readonly PageListChangedEvent _pageListChangedEvent;
		private IBookStorage _storage;

		public Book(IBookStorage storage, ITemplateFinder templateFinder,
			IFileLocator fileLocator, HtmlThumbNailer thumbnailProvider,
			PageSelection pageSelection,
			PageListChangedEvent pageListChangedEvent)
		{
			_storage = storage;
			_templateFinder = templateFinder;
			_fileLocator = fileLocator;
			_thumbnailProvider = thumbnailProvider;
			_pageSelection = pageSelection;
			_pageListChangedEvent = pageListChangedEvent;
		}



		public enum BookType { Unknown, Template, Shell, Publication }

		public string Title
		{
			get { return _storage.Title; }
		}

		public  Image GetThumbNail()
		{
			Image thumb;
			if(_storage.TryGetPremadeThumbnail(out thumb))
				return thumb;

			var dom = GetPreviewXmlDocumentForFirstPage();
			if(dom == null)
			{
				return Resources.GenericPage32x32;
			}
			return _thumbnailProvider.GetThumbnail(_storage.Key, dom);
		}

		public XmlDocument GetEditableHtmlDomForPage(IPage page)
		{
			if (!_storage.LooksOk)
			{
				return GetErrorDom();
			}

			XmlDocument dom = GetHtmlDomWithJustOnePage(page);
			dom.AddStyleSheet(_fileLocator.LocateFile(@"editMode.css"));
			return dom;

		}

		private XmlDocument GetHtmlDomWithJustOnePage(IPage page)
		{
			var dom = new XmlDocument();
			var head = _storage.Dom.SelectSingleNodeHonoringDefaultNS("/html/head").OuterXml;
			dom.LoadXml(@"<html xmlns='http://www.w3.org/1999/xhtml'>"+head+"<body></body></html>");
			var body = dom.SelectSingleNodeHonoringDefaultNS("//body");
			var pageDom = dom.ImportNode(page.GetDivNodeForThisPage(), true);
			body.AppendChild(pageDom);
			return dom;
		}

		public XmlDocument GetPreviewXmlDocumentForPage(IPage page)
		{
			if(!_storage.LooksOk)
			{
				return GetErrorDom();
			}
			var dom = GetHtmlDomWithJustOnePage(page);
			dom.AddStyleSheet(_fileLocator.LocateFile(@"previewMode.css"));
			return dom;
		}

		public XmlDocument GetPreviewXmlDocumentForFirstPage()
		{
			if (!_storage.LooksOk)
			{
				return null;
			}

			XmlDocument bookDom = GetBookDomWithStyleSheet("previewMode.css");
			HideEverythingButFirstPage(bookDom);
			return bookDom;
		}

		private static void HideEverythingButFirstPage(XmlDocument bookDom)
		{
			bool onFirst = true;
			foreach (XmlElement node in bookDom.SafeSelectNodes("//div[contains(@class, 'page')]"))
			{
				if (!onFirst)
				{
					node.SetAttribute("style", "", "display:none");
				}
				onFirst =false;
			}
		}


		private XmlDocument GetBookDomWithStyleSheet(string cssFileName)
		{
			XmlDocument dom = (XmlDocument)_storage.Dom.Clone();
			dom.AddStyleSheet(_fileLocator.LocateFile(cssFileName));
			return dom;
		}


		private XmlDocument GetPageSayingCantShowBook()
		{
			var dom = new XmlDocument();
			dom.LoadXml("<html><body>Could not display that book.</body></html>");
			return dom;
		}

		private XmlDocument GetErrorDom()
		{
			var dom = new XmlDocument();
			dom.LoadXml("<html><body>Something went wrong</body></html>");
			return dom;
		}


		public bool CanPublish
		{
			get { return CanEdit; }
		}

		public bool CanEdit
		{
			get {return _storage.BookType == BookType.Publication;  }
		}

		public IPage FirstPage
		{
			get { return GetPages().First(); }
		}

		public Book TemplateBook
		{
			get
			{
				Guard.AgainstNull(_templateFinder, "_templateFinder");
				if(_storage.BookType!=BookType.Publication)
					return null;
				return _templateFinder.FindTemplateBook(_storage.GetTemplateKey());
			}
		}

		public BookType Type
		{
			get { return _storage.BookType; }
		}

		public XmlDocument RawDom
		{
			get {return  _storage.Dom; }
		}


		public XmlDocument GetPreviewHtmlFileForWholeBook()
		{
			if (!_storage.LooksOk)
			{
				return GetPageSayingCantShowBook();
			}
			return GetBookDomWithStyleSheet("previewMode.css");
		}


		public IEnumerable<IPage> GetPages()
		{
			if (!_storage.LooksOk)
				yield break;

			int pageNumber = 0;
			foreach (XmlElement pageNode in _storage.Dom.SafeSelectNodes("//div[contains(@class,'page')]"))
			{
				//review: we want to show titles for template books, numbers for other books.
				//this here requires that titles be removed when the page is inserted, kind of a hack.
				var caption = pageNode.GetAttribute("title");
				if(string.IsNullOrEmpty(caption))
				{
					caption = (pageNumber + 1).ToString();
				}
				yield return CreatePageDecriptor(pageNode, caption);
				++pageNumber;
			}
		}

		private IPage CreatePageDecriptor(XmlElement pageNode, string caption)
		{
			return new Page(pageNode, caption,
					(page => _thumbnailProvider.GetThumbnail(page.Id, GetPreviewXmlDocumentForPage(page))),
					(page => FindPageDiv(page)));
		}

		private XmlElement FindPageDiv(IPage page)
		{
			//review: could move to page
			return _storage.Dom.SelectSingleNodeHonoringDefaultNS(page.XPathToDiv) as XmlElement;
		}

		public void InsertPageAfter(IPage pageBefore, IPage templatePage)
		{
			Guard.Against(Type != BookType.Publication, "Tried to edit a non-editable book.");

			XmlDocument dom = _storage.Dom;
			var newPageElement = dom.ImportNode(templatePage.GetDivNodeForThisPage(), true) as XmlElement;
			newPageElement.SetAttribute("id", Guid.NewGuid().ToString());
			ClearEditableValues(newPageElement);
			newPageElement.RemoveAttribute("title"); //titles are just for templates

			var elementOfPageBefore = FindPageDiv(pageBefore);
			elementOfPageBefore.ParentNode.InsertAfter(newPageElement, elementOfPageBefore);
			_pageSelection.SelectPage(CreatePageDecriptor(newPageElement, "should not show"));
			_storage.Save();
			if (_pageListChangedEvent != null)
				_pageListChangedEvent.Raise(null);
		}

		private void ClearEditableValues(XmlElement newPageElement)
		{
			foreach (XmlElement editNode in newPageElement.SafeSelectNodes("//input"))
			{
				if (editNode.GetAttribute("value").ToLower().StartsWith("lorem ipsum"))
				{
					editNode.SetAttribute("value", string.Empty);
				}
			}
			foreach (XmlElement editNode in newPageElement.SafeSelectNodes("//textarea"))
			{
				if (editNode.InnerText.ToLower().StartsWith("lorem ipsum"))
				{
					editNode.InnerText = string.Empty;
				}
			}
		}

		public void DeletePage(IPage page)
		{
			Guard.Against(Type != BookType.Publication, "Tried to edit a non-editable book.");

			if(GetPageCount() <2)
				return;

			var pageNode = FindPageDiv(page);
		   pageNode.ParentNode.RemoveChild(pageNode);
	//        InvokePageDeleted(page);

			var prevNod = pageNode.PreviousSibling;
			if(prevNod == null)
			{
				_pageSelection.SelectPage(FirstPage);
			}
			else
			{
				_pageSelection.SelectPage(FirstPage);

				//todo       var previousPage = GetPageFromNode(pageNode);
//                _pageSelection.SelectPage(previousPage);
			}
			_storage.Save();
			if(_pageListChangedEvent !=null)
				_pageListChangedEvent.Raise(null);
		}

		private int GetPageCount()
		{
			return GetPages().Count();
		}


		/// <summary>
		/// Earlier, we handed out a single-page version of the document. Now it has been edited,
		/// so we now we need to fold changes back in
		/// </summary>
		public void SavePage(XmlDocument pageDom)
		{
			string pageSelector = Page.GetPageSelectorXPath(pageDom);
			//review: does this belong down in the storage?

			foreach (XmlElement editNode in pageDom.SafeSelectNodes(pageSelector + "//input"))
			{
				var id = editNode.GetAttribute("id");
				var storageNode = _storage.Dom.SelectSingleNodeHonoringDefaultNS("//input[@id='"+id+"']") as XmlElement;
				Guard.AgainstNull(storageNode,id);
				storageNode.SetAttribute("value", editNode.GetAttribute("value"));
			}
			foreach (XmlElement editNode in pageDom.SafeSelectNodes("//textarea"))
			{
				var id = editNode.GetAttribute("id");
				if (string.IsNullOrEmpty(id))
				{
					Debug.Fail(id);
				}
				else
				{
					var destNode = _storage.Dom.SelectSingleNodeHonoringDefaultNS(pageSelector+"//textarea[@id='" + id + "']") as XmlElement;
					Guard.AgainstNull(destNode, id);
					destNode.InnerText = editNode.InnerText;
				}
			}
			_storage.Save();
		}

		/// <summary>
		/// Move a page to somewhere else in the book
		/// </summary>
		public void RelocatePage(IPage page, int indexOfItemAfterRelocation)
		{
			Guard.Against(Type != BookType.Publication, "Tried to edit a non-editable book.");

			var pages = _storage.Dom.SafeSelectNodes("/html/body/div");
			var pageDiv = FindPageDiv(page);
			var body = pageDiv.ParentNode;
				body.RemoveChild(pageDiv);
			if(indexOfItemAfterRelocation == 0)
			{
				body.InsertBefore(pageDiv, body.FirstChild);
			}
			else
			{
				body.InsertAfter(pageDiv, pages[indexOfItemAfterRelocation-1]);
			}

			_storage.Save();
		}

		public void CopyToFolder(string destinationPath)
		{
			_storage.CopyToFolder(destinationPath);
		}
	}
}