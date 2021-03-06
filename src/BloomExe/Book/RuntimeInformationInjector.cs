﻿using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Xml;
using Bloom.Collection;
using Bloom.Properties;
using L10NSharp;
using Newtonsoft.Json;
using Palaso.IO;

namespace Bloom.Book
{
	/// <summary>
	/// stick in a json with various string values/translations we want to make available to the javascript
	/// </summary>
	public class RuntimeInformationInjector
	{
		// Collecting dynamic strings is slow, it only applies to English, and we only need to do it one time.
		private static bool _collectDynamicStrings;
		private static bool _foundEnglish;

		public static void AddUIDictionaryToDom(HtmlDom pageDom, CollectionSettings collectionSettings)
		{
			CheckDynamicStrings();

			// add dictionary script to the page
			XmlElement dictionaryScriptElement = pageDom.RawDom.SelectSingleNode("//script[@id='ui-dictionary']") as XmlElement;
			if (dictionaryScriptElement != null)
				dictionaryScriptElement.ParentNode.RemoveChild(dictionaryScriptElement);

			dictionaryScriptElement = pageDom.RawDom.CreateElement("script");
			dictionaryScriptElement.SetAttribute("type", "text/javascript");
			dictionaryScriptElement.SetAttribute("id", "ui-dictionary");
			var d = new Dictionary<string, string>();

			d.Add(collectionSettings.Language1Iso639Code, collectionSettings.Language1Name);
			if (!String.IsNullOrEmpty(collectionSettings.Language2Iso639Code))
				SafelyAddLanguage(d, collectionSettings.Language2Iso639Code,
					collectionSettings.GetLanguage2Name(collectionSettings.Language2Iso639Code));
			if (!String.IsNullOrEmpty(collectionSettings.Language3Iso639Code))
				SafelyAddLanguage(d, collectionSettings.Language3Iso639Code,
					collectionSettings.GetLanguage3Name(collectionSettings.Language3Iso639Code));

			SafelyAddLanguage(d, "vernacularLang", collectionSettings.Language1Iso639Code);//use for making the vernacular the first tab
			SafelyAddLanguage(d, "{V}", collectionSettings.Language1Name);
			SafelyAddLanguage(d, "{N1}", collectionSettings.GetLanguage2Name(collectionSettings.Language2Iso639Code));
			SafelyAddLanguage(d, "{N2}", collectionSettings.GetLanguage3Name(collectionSettings.Language3Iso639Code));

			// TODO: Eventually we need to look through all .bloom-translationGroup elements on the current page to determine
			// whether there is text in a language not yet added to the dictionary.
			// For now, we just add a few we know we need
			AddSomeCommonNationalLanguages(d);

			MakePageLabelLocalizable(pageDom, d);

			// Hard-coded localizations for 2.0
			AddHtmlUiStrings(d);

			dictionaryScriptElement.InnerText = String.Format("function GetInlineDictionary() {{ return {0};}}", JsonConvert.SerializeObject(d));

			// add i18n initialization script to the page
			AddLocalizationTriggerToDom(pageDom);

			pageDom.Head.InsertAfter(dictionaryScriptElement, pageDom.Head.LastChild);

			_collectDynamicStrings = false;
		}

		private static void CheckDynamicStrings()
		{
			// if the ui language changes, check for English
			if (!_foundEnglish && (LocalizationManager.UILanguageId == "en"))
			{
				_foundEnglish = true;

				// if the current language is English, check the dynamic strings once
				_collectDynamicStrings = true;
			}
		}

		/// <summary>
		/// Adds a script to the page that triggers i18n after the page is fully loaded.
		/// </summary>
		/// <param name="pageDom"></param>
		private static void AddLocalizationTriggerToDom(HtmlDom pageDom)
		{
			XmlElement i18nScriptElement = pageDom.RawDom.SelectSingleNode("//script[@id='ui-i18n']") as XmlElement;
			if (i18nScriptElement != null)
				i18nScriptElement.ParentNode.RemoveChild(i18nScriptElement);

			i18nScriptElement = pageDom.RawDom.CreateElement("script");
			i18nScriptElement.SetAttribute("type", "text/javascript");
			i18nScriptElement.SetAttribute("id", "ui-i18n");

			// Explanation of the JavaScript:
			//   $(document).ready(function() {...}) tells the browser to run the code inside the braces after the document has completed loading.
			//   $('body') is a jQuery function that selects the contents of the body tag.
			//   .find('*[data-i18n]') instructs jQuery to return a collection of all elements inside the body tag that have a "data-i18n" attribute.
			//   .localize() runs the jQuery.fn.localize() method, which loops through the above collection of elements and attempts to localize the text.
			i18nScriptElement.InnerText = "$(document).ready(function() { $('body').find('*[data-i18n]').localize(); });";

			pageDom.Head.InsertAfter(i18nScriptElement, pageDom.Head.LastChild);
		}

		private static void MakePageLabelLocalizable(HtmlDom singlePageHtmlDom, Dictionary<string, string> d)
		{
			foreach (XmlElement element in singlePageHtmlDom.RawDom.SelectNodes("//*[contains(@class, 'pageLabel')]"))
			{
				if (!element.HasAttribute("data-i18n"))
				{
					var englishLabel = element.InnerText;
					var key = "EditTab.ThumbnailCaptions." + englishLabel;
					AddTranslationToDictionaryUsingEnglishAsKey(d, key, englishLabel);

					element.SetAttribute("data-i18n", key);
				}
			}
		}

		private static void AddSomeCommonNationalLanguages(Dictionary<string, string> d)
		{
			SafelyAddLanguage(d, "en", "English");
			SafelyAddLanguage(d, "ha", "Hausa");
			SafelyAddLanguage(d, "hi", "Hindi");
			SafelyAddLanguage(d, "es", "Spanish");
			SafelyAddLanguage(d, "fr", "French");
			SafelyAddLanguage(d, "pt", "Portuguese");
			SafelyAddLanguage(d, "swa", "Swahili");
			SafelyAddLanguage(d, "th", "Thai");
			SafelyAddLanguage(d, "tpi", "Tok Pisin");
		}

		private static void SafelyAddLanguage(Dictionary<string, string> d, string key, string name)
		{
			if (!d.ContainsKey(key))
				d.Add(key, name);
		}

		/// <summary>
		/// For Bloom 2.0 this list is hard-coded
		/// </summary>
		/// <param name="d"></param>
		private static void AddHtmlUiStrings(Dictionary<string, string> d)
		{
			//ATTENTION: Currently, the english here must exactly match whats in the html. See comment in AddTranslationToDictionaryUsingEnglishAsKey

			AddTranslationToDictionaryUsingEnglishAsKey(d, "EditTab.FontSizeTip", "Changes the text size for all boxes carrying the style '{0}' and language '{1}'.\nCurrent size is {2}pt.");
			AddTranslationToDictionaryUsingEnglishAsKey(d, "EditTab.FrontMatter.BookTitlePrompt", "Book title in {lang}");

			AddTranslationToDictionaryUsingEnglishAsKey(d, "EditTab.FrontMatter.OriginalContributorsPrompt",
				"The contributions made by writers, illustrators, editors, etc., in {lang}");
			AddTranslationToDictionaryUsingEnglishAsKey(d, "EditTab.FrontMatter.TranslatedAcknowledgmentsPrompt", "Acknowledgments for translated version, in {lang}");
			AddTranslationToDictionaryUsingEnglishAsKey(d, "EditTab.FrontMatter.FundingAgenciesPrompt", "Use this to acknowledge any funding agencies.");
			AddTranslationToDictionaryUsingEnglishAsKey(d, "EditTab.FrontMatter.CopyrightPrompt","Click to Edit Copyright & License");

			AddTranslationToDictionaryUsingEnglishAsKey(d, "EditTab.FrontMatter.OriginalAcknowledgmentsPrompt",
				"Original (or Shell) Acknowledgments in {lang}");

			AddTranslationToDictionaryUsingEnglishAsKey(d, "EditTab.FrontMatter.TopicPrompt", "Click to choose topic");
			AddTranslationToDictionaryUsingEnglishAsKey(d, "EditTab.FrontMatter.ISBNPrompt", "International Standard Book Number. Leave blank if you don't have one of these.");

			AddTranslationToDictionaryUsingEnglishAsKey(d, "EditTab.FrontMatter.BigBook.Contributions", "When you are making an original book, use this box to record contributions made by writers, illustrators, editors, etc.");
			AddTranslationToDictionaryUsingEnglishAsKey(d, "EditTab.FrontMatter.BigBook.Translator", "When you make a book from a shell, use this box to tell who did the translation.");
			
			AddTranslationToDictionaryUsingEnglishAsKey(d, "EditTab.BackMatter.InsideBackCoverTextPrompt", "If you need somewhere to put more information about the book, you can use this page, which is the inside of the back cover.");
			AddTranslationToDictionaryUsingEnglishAsKey(d, "EditTab.BackMatter.OutsideBackCoverTextPrompt", "If you need somewhere to put more information about the book, you can use this page, which is the outside of the back cover.");

			AddTranslationToDictionaryUsingKey(d, "EditTab.Image.PasteImage", "Paste Image");
			AddTranslationToDictionaryUsingKey(d, "EditTab.Image.ChangeImage", "Change Image");
			AddTranslationToDictionaryUsingKey(d, "EditTab.Image.EditMetadata", "Edit Image Credits, Copyright, & License");

			// tool tips for style editor
			AddTranslationToDictionaryUsingKey(d, "BookEditor.FontSizeTip", "Changes the text size for all boxes carrying the style '{0}' and language '{1}'.\nCurrent size is {2}pt.");
			//No longer used. See BL-799 AddTranslationToDictionaryUsingKey(d, "EditTab.FormatDialogTip", "Adjust formatting for style");
			AddTranslationToDictionaryUsingKey(d, "EditTab.FormatDialog.WordSpacingNormal", "Normal");
			AddTranslationToDictionaryUsingKey(d, "EditTab.FormatDialog.WordSpacingWide", "Wide");
			AddTranslationToDictionaryUsingKey(d, "EditTab.FormatDialog.WordSpacingExtraWide", "Extra Wide");
			AddTranslationToDictionaryUsingKey(d, "EditTab.FormatDialog.FontFaceToolTip", "Change the font face");
			AddTranslationToDictionaryUsingKey(d, "EditTab.FormatDialog.FontSizeToolTip", "Change the font size");
			AddTranslationToDictionaryUsingKey(d, "EditTab.FormatDialog.LineSpacingToolTip", "Change the spacing between lines of text");
			AddTranslationToDictionaryUsingKey(d, "EditTab.FormatDialog.WordSpacingToolTip", "Change the spacing between words");
			AddTranslationToDictionaryUsingKey(d, "EditTab.FormatDialog.BorderToolTip", "Change the border and background");

			// "No Topic" localization for Topic Chooser
			AddTranslationToDictionaryUsingKey(d, "Topics.NoTopic", "No Topic");
		}

		private static void AddTranslationToDictionaryUsingKey(Dictionary<string, string> dictionary, string key, string defaultText)
		{
			var translation = _collectDynamicStrings
				? LocalizationManager.GetDynamicString("Bloom", key, defaultText)
				: LocalizationManager.GetString(key, defaultText);

			if (!dictionary.ContainsKey(key))
			{
				dictionary.Add(key, translation);
			}
		}

		private static void AddTranslationToDictionaryUsingEnglishAsKey(Dictionary<string, string> dictionary, string key, string defaultText)
		{
			var translation = _collectDynamicStrings
				? LocalizationManager.GetDynamicString("Bloom", key, defaultText)
				: LocalizationManager.GetString(key, defaultText);

			//We have to match on some key. Ideally, we'd match on something "key-ish", like BookEditor.FrontMatter.BookTitlePrompt
			//But that would require changes to all the templates to have that key somehow, in adition to or in place of the current English
			//So for now, we're just keeping the real key on the c#/tmx side of things, and letting the javascript work by matching our defaultText to the English text in the html
			var keyUsedInTheJavascriptDictionary = defaultText;
			if (!dictionary.ContainsKey(keyUsedInTheJavascriptDictionary))
			{
				dictionary.Add(keyUsedInTheJavascriptDictionary, WebUtility.HtmlEncode(translation));
			}
		}

		/// <summary>
		/// keeps track of the most recent set of topics we injected, mapping the localization back to the original.
		/// </summary>
		public static Dictionary<string, string> TopicReversal;

		/// <summary>
		/// stick in a json with various settings we want to make available to the javascript
		/// </summary>
		public static void AddUISettingsToDom(HtmlDom pageDom, CollectionSettings collectionSettings, IFileLocator fileLocator)
		{
			CheckDynamicStrings();

			XmlElement existingElement = pageDom.RawDom.SelectSingleNode("//script[@id='ui-settings']") as XmlElement;

			XmlElement element = pageDom.RawDom.CreateElement("script");
			element.SetAttribute("type", "text/javascript");
			element.SetAttribute("id", "ui-settings");
			var d = new Dictionary<string, string>();

			//d.Add("urlOfUIFiles", "file:///" + fileLocator.LocateDirectory("ui", "ui files directory"));
			if (!String.IsNullOrEmpty(Settings.Default.LastSourceLanguageViewed))
			{
				d.Add("defaultSourceLanguage", Settings.Default.LastSourceLanguageViewed);
			}

			d.Add("languageForNewTextBoxes", collectionSettings.Language1Iso639Code);
			d.Add("isSourceCollection", collectionSettings.IsSourceCollection.ToString());

			d.Add("bloomBrowserUIFolder", FileLocator.GetDirectoryDistributedWithApplication("BloomBrowserUI").ToLocalhost());

	
			element.InnerText = String.Format("function GetSettings() {{ return {0};}}", JsonConvert.SerializeObject(d));

			var head = pageDom.RawDom.SelectSingleNode("//head");
			if (existingElement != null)
				head.ReplaceChild(element, existingElement);
			else
				head.InsertAfter(element, head.LastChild);

			_collectDynamicStrings = false;
		}
	}
}
