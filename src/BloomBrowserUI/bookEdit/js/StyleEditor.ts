/// <reference path="../../lib/jquery.d.ts" />
/// <reference path="toolbar/toolbar.d.ts"/>

class StyleEditor {

	private _previousBox: Element;
	private _supportFilesRoot: string;

	constructor(supportFilesRoot: string) {
		this._supportFilesRoot = supportFilesRoot;

		var sheet = this.GetOrCreateUserModifiedStyleSheet();
	}

	static GetStyleClassFromElement(target: HTMLElement) {
		var c = $(target).attr("class");
		if (!c)
			c = "";
		var classes = c.split(' ');

		for (var i = 0; i < classes.length; i++) {
			if (classes[i].indexOf('-style') > 0) {
				return classes[i];
			}
		}
		return null;
	}

	MakeBigger(target: HTMLElement) {
		this.ChangeSize(target, 2);
	}
	MakeSmaller(target: HTMLElement) {
		this.ChangeSize(target, -2);
	}

	static GetStyleNameForElement(target: HTMLElement): string {
		var styleName = this.GetStyleClassFromElement(target);
		if (!styleName) {
			var parentPage: HTMLDivElement = <HTMLDivElement><any> ($(target).closest(".bloom-page")[0]);
			// Books created with the original (0.9) version of "Basic Book", lacked "x-style" but had all pages starting with an id of 5dcd48df (so we can detect them)
			var pageLineage = $(parentPage).attr('data-pagelineage');
			if ((pageLineage) && pageLineage.substring(0, 8) == '5dcd48df') {
				styleName = "default-style";
				$(target).addClass(styleName);
			}
			else {
				return null;
			}
		}
		return styleName;
	}

	static GetLangValueOrNull(target: HTMLElement): string {
		var langAttr = $(target).attr("lang");
		if(!langAttr)
			return null;
		return langAttr.valueOf().toString();
	}

	ChangeSize(target: HTMLElement, change: number) {
		var styleName = StyleEditor.GetStyleNameForElement(target);
		if (!styleName)
			return;
		var langAttrValue = StyleEditor.GetLangValueOrNull(target);
		var rule: CSSStyleRule = this.GetOrCreateRuleForStyle(styleName, langAttrValue);
		var sizeString: string = (<any>rule).style.fontSize;
		if (!sizeString)
			sizeString = $(target).css("font-size");
		var units = sizeString.substr(sizeString.length - 2, 2);
		sizeString = (parseInt(sizeString) + change).toString(); //notice that parseInt ignores the trailing units
		rule.style.setProperty("font-size", sizeString + units, "important");
		// alert("New size rule: " + rule.cssText);
	}

	GetOrCreateUserModifiedStyleSheet(): StyleSheet {
		//note, this currently just makes an element in the document, not a separate file
		for (var i = 0; i < document.styleSheets.length; i++) {
			if ((<StyleSheet>(<any>document.styleSheets[i]).ownerNode).title == "userModifiedStyles") {
				// alert("Found userModifiedStyles sheet: i= " + i + ", title= " + (<StyleSheet>(<any>document.styleSheets[i]).ownerNode).title + ", sheet= " + document.styleSheets[i].ownerNode.textContent);
				return <StyleSheet><any>document.styleSheets[i];
			}
		}
		// alert("Will make userModifiedStyles Sheet:" + document.head.outerHTML);

		var newSheet = document.createElement('style');
		document.getElementsByTagName("head")[0].appendChild(newSheet);
		newSheet.title = "userModifiedStyles";
		newSheet.type = "text/css";
		// alert("newSheet: " + document.head.innerHTML);

		return <StyleSheet><any>newSheet;
	}

	GetOrCreateRuleForStyle(styleName: string, langAttrValue: string): CSSStyleRule {
		var styleSheet = this.GetOrCreateUserModifiedStyleSheet();
		var x: CSSRuleList = (<any>styleSheet).cssRules;
		var styleAndLang = styleName;
		if(langAttrValue && langAttrValue.length > 0)
			styleAndLang = styleName + "[lang='" + langAttrValue + "']";
		else
			styleAndLang = styleName + ":not([lang])";

		for (var i = 0; i < x.length; i++) {
			if (x[i].cssText.indexOf(styleAndLang) > -1) {
				return <CSSStyleRule> x[i];
			}
		}
		(<CSSStyleSheet>styleSheet).addRule('.'+styleAndLang);

		return <CSSStyleRule> x[x.length - 1];      //new guy is last
	}


	AttachToBox(targetBox: HTMLElement) {
		if (!StyleEditor.GetStyleNameForElement(targetBox))
			return;

		if (this._previousBox!=null)
		{
			StyleEditor.CleanupElement(this._previousBox);
		}
		this._previousBox = targetBox;

		//REVIEW: we're putting it in the target div, but at the moment we are using exactly the same bar for each editable box, could just have
		//one for the whole document

		//NB: we're placing these *after* the target, don't want to mess with having a div inside our text (if that would work anyhow)

		//  i couldn't get the nice icomoon icon font/style.css system to work in Bloom or stylizer
		//            $(targetBox).after('<div id="format-toolbar" style="opacity:0; display:none;"><a class="smallerFontButton" id="smaller">a</a><a id="bigger" class="largerFontButton" ><i class="bloom-icon-FontSize"></i></a></div>');
		$(targetBox).after('<div id="format-toolbar" class="bloom-ui" style="opacity:0; display:none;"><a class="smallerFontButton" id="smaller"><img src="' + this._supportFilesRoot + '/img/FontSizeLetter.svg"></a><a id="bigger" class="largerFontButton" ><img src="' + this._supportFilesRoot + '/img/FontSizeLetter.svg"></a></div>');


		var bottom = $(targetBox).position().top + $(targetBox).height();
		var t = bottom + "px";
		$(targetBox).after('<div id="formatButton"  style="top: '+t+'" class="bloom-ui" title="Change text size. Affects all similar boxes in this document"><img src="' + this._supportFilesRoot + '/img/cogGrey.svg"></div>');

		$('#formatButton').toolbar({
			content: '#format-toolbar',
			//position: 'left',//nb: toolbar's June 2013 code, pushes the toolbar out to the left by 1/2 the width of the parent object, easily putting it in negative territory!
			position: 'left',
			hideOnClick: false
		});

		var editor = this;
		$('#formatButton').on("toolbarItemClick", function (event, whichButton) {
			if (whichButton.id == "smaller") {
				editor.MakeSmaller(targetBox);
			}
			if (whichButton.id == "bigger") {
				editor.MakeBigger(targetBox);
			}
		});
	  }

	DetachFromBox(element) {
	  //  StyleEditor.CleanupElement(element);
	}

	static CleanupElement(element) {
		//NB: we're placing these controls *after* the target, not inside it; that's why we go up to parent
		$(element).parent().find(".bloom-ui").each(function () {
			$(this).remove();
		});
		$(".tool-container").each(function () {
			$(this).remove();
		});
	}
}