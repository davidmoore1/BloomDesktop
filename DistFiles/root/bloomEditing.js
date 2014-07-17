$.fn.CenterVerticallyInParent = function() {
    return this.each(function(i) {
        var ah = $(this).height();
        var ph = $(this).parent().height();
        var mh = Math.ceil((ph - ah) / 2);
        $(this).css('margin-top', mh);

        ///There is a bug in wkhtmltopdf where it determines the height of these incorrectly, causing, in a multlingual situation, the 1st text box to hog up all the room and
        //push the other guys off the page. So the hack solution of the moment is to remember the correct height here, in gecko-land, and use it over there to set the max-height.
        //See bloomPreview.SetMaxHeightForHtmlToPDFBug()
        $(this).children().each(function(){
            var h= $(this).height();
            $(this).attr('data-firefoxheight', h);
        });
    });
};


function isBrOrWhitespace(node) {
    return node && ( (node.nodeType == 1 && node.nodeName.toLowerCase() == "br") ||
           (node.nodeType == 3 && /^\s*$/.test(node.nodeValue) ) );
}

function TrimTrailingLineBreaksInDivs(node) {
    while ( isBrOrWhitespace(node.firstChild) ) {
        node.removeChild(node.firstChild);
    }
    while ( isBrOrWhitespace(node.lastChild) ) {
        node.removeChild(node.lastChild);
    }
}

function Cleanup() {

    // remove the div's which qtip makes for the tips themselves
    $("div.qtip").each(function() {
        $(this).remove();
    });

    // remove the attributes qtips adds to the things being annotated
    $("*[aria-describedby]").each(function() {
        $(this).removeAttr("aria-describedby");
    });
    $("*[ariasecondary-describedby]").each(function() {
        $(this).removeAttr("ariasecondary-describedby");
    });
    $("*.editTimeOnly").remove();
    $("*.dragHandle").remove();
    $("*").removeAttr("data-easytabs");

    $("div.ui-resizable-handle").remove();
    $('div, figure').each(function() {
        $(this).removeClass('ui-draggable');
        $(this).removeClass('ui-resizable');
        $(this).removeClass('hoverUp');
    });

    $('button').each(function () {
            $(this).remove();
    });


  $('div.bloom-editable').each( function() {
    TrimTrailingLineBreaksInDivs(this);
    });

  $('.bloom-imageContainer').css('opacity', '');//comes in on img containers from an old version of myimgscale, and is a major problem if the image is missing
    $('.bloom-imageContainer').css('overflow', '');//review: also comes form myimgscale; is it a problem?
}

 //Make a toolbox off to the side (implemented using qtip), with elements that can be dragged
 //onto the page
function AddToolbox(){
    $('div.bloom-page.enablePageCustomization').each(function () {
        $(this).find('.marginBox').droppable({
            hoverClass: "ui-state-hover",
            accept: function () { return true; },
            drop: function (event, ui) {
                //is it being dragged in from a toolbox, or just moved around?
                if ($(ui.draggable).hasClass('toolbox')) {
                    var x = $(ui.draggable).clone();
                    //    $(x).text("");
                    $(this).append($(x));
                    $(this).find('.toolbox.bloom-imageContainer')
                             .each(function () { SetupImageContainer(this) });

                    $(this).find('.toolbox')
                            .removeAttr("style")
                            .draggable({ containment: "parent" })
                            .removeClass("toolbox")
                           .each(function () { SetupResizableElement(this) })
                           .each(function () { SetupDeletable(this) });
                }
            }
        });
        var lang1ISO = GetSettings().languageForNewTextBoxes;
        var translationBox = '<div class="bloom-translationGroup bloom-resizable bloom-deletable bloom-draggable toolbox"><div class="bloom-editable bloom-content1" lang="' + lang1ISO + '">Text</div></div>';
        var heading1Box = '<div class="bloom-translationGroup heading1 bloom-resizable bloom-deletable bloom-draggable toolbox"><div class="bloom-editable bloom-content1" lang="' + lang1ISO + '">Heading</div></div>';
        var imageBox = '<div class="bloom-imageContainer bloom-resizable bloom-draggable  bloom-deletable toolbox"><img src="placeholder.png"></div>';

        $(this).qtip({
            content: "<h3>Toolbox</h3><ul class='toolbox'><li>" + heading1Box + "</li><li>" + translationBox + "</li><li>" + imageBox + "</li></ul>"
                 , show: { ready: true }
                 , hide: false
                 , position: { at: 'right center',
                     my: 'left center'
                 }
                 , events: {
                     render: function (event, api) {
                         $(this).find('.toolbox').draggable({
                             //note: this is just used for drawing what you drag around..
                             //it isn't what the droppable is actually given
                             helper: function (event) {
                                 var tearOff = $(this).clone()//.removeClass('toolbox');//by removing this, we show it with the actual size it will be when dropped
                                 return tearOff;
                             }

                         });
                     }
                 }
                 , style: {
                     width: 200,
                     height: 300,
                     classes: 'ui-tooltip-dark',
                     tip: { corner: false }
                 }
        })

        $(this).qtipSecondary({
            content: "<div id='experimentNotice'><img src='file://"+GetSettings().bloomProgramFolder+"/images/experiment.png'/>This is an experimental prototype of template-making within Bloom itself. Much more work is needed before it is ready for real work, so don't bother reporting problems with it yet. The Trello board is <a href='https://trello.com/board/bloom-custom-template-dev/4fb2501b34909fbe417a7b7d'>here</a></b></div>"
                         , show: { ready: true }
                         , hide: false
                         , position: { at: 'right top',
                             my: 'left top'
                         },
            style: { classes: 'ui-tooltip-red',
                tip: { corner: false}
            }
        })
    })
}

function AddExperimentalNotice(element) {
    $(element).qtipSecondary({
        content: "<div id='experimentNotice'><img src='file://" + GetSettings().bloomProgramFolder + "/images/experiment.png'/>This page is an experimental prototype which may have many problems, for which we apologize.<div/>"
                         , show: { ready: true }
                         , hide: false
                         , position: { at: 'right top',
                             my: 'left top'
                         },
        style: { classes: 'ui-tooltip-red',
            tip: { corner: false }
        }
    });
}

//:empty is not quite enough... we don't want to show bubbles if all there is is an empty paragraph
jQuery.expr[':'].hasNoText = function (obj) {
    return jQuery.trim(jQuery(obj).text()).length == 0;
}

 //Sets up the (currently green) qtip bubbles that give you the contents of the box in the source languages
function MakeSourceTextDivForGroup(group) {

    var divForBubble = $(group).clone();
    $(divForBubble).removeAttr('style');

    //make the source texts in the bubble read-only
    $(divForBubble).find("textarea, div").each(function() {
        $(this).attr("readonly", "readonly");
        $(this).removeClass('bloom-editable');
        $(this).attr("contenteditable", "false");
    });

    $(divForBubble).removeClass(); //remove them all
    $(divForBubble).addClass("ui-sourceTextsForBubble");
    //don't want the vernacular in the bubble
    $(divForBubble).find("*[lang='" + GetDictionary().vernacularLang + "']").each(function() {
        $(this).remove();
    });
    //don't want empty items in the bubble
    $(divForBubble).find("textarea:empty, div:hasNoText").each(function () {
        $(this).remove();
    });

    //don't want bilingual/trilingual boxes to be shown in the bubble
    $(divForBubble).find("*.bloom-content2, *.bloom-content3").each(function() {
        $(this).remove();
    });

    //if there are no languages to show in the bubble, bail out now
    if ($(divForBubble).find("textarea, div").length == 0)
        return;

    $(this).after(divForBubble);

    var selectorOfDefaultTab="li:first-child";

    //make the li's for the source text elements in this new div, which will later move to a tabbed bubble
    $(divForBubble).each(function() {
        $(this).prepend('<ul class="editTimeOnly z"></ul>');
        var list = $(this).find('ul');
        //nb: Jan 2012: we modified "jquery.easytabs.js" to target @lang attributes, rather than ids.  If that change gets lost,
        //it's just a one-line change.
        var dictionary = GetDictionary();
        var items = $(this).find("textarea, div");
        items.sort(function(a, b) {
            var keyA = $(a).attr('lang');
            var keyB = $(b).attr('lang');
            if (keyA == dictionary.vernacularLang)
                return -1;
            if (keyB == dictionary.vernacularLang)
                return 1;
            if (keyA < keyB)
                return -1;
            if (keyA > keyB)
                return 1;
            return 0;
        });
        var shellEditingMode = false;
        items.each(function() {
            var iso = $(this).attr('lang');
            var languageName = dictionary[iso];
            if (!languageName)
                languageName = iso;
            var shouldShowOnPage = (iso == dictionary.vernacularLang)  /* could change that to 'bloom-content1' */ || $(this).hasClass('bloom-contentNational1') || $(this).hasClass('bloom-contentNational2') || $(this).hasClass('bloom-content2') || $(this).hasClass('bloom-content3');

            if(iso=== GetSettings().defaultSourceLanguage) {
                selectorOfDefaultTab="li:#"+iso;
            }
            // in translation mode, don't include the vernacular in the tabs, because the tabs are being moved to the bubble
            if (iso !== "z" && (shellEditingMode || !shouldShowOnPage)) {
                $(list).append('<li id="' + iso + '"><a class="sourceTextTab" href="#' + iso + '">' + languageName + '</a></li>');
            }
        });
    });

    //now turn that new div into a set of tabs
    if ($(divForBubble).find("li").length > 0) {
        $(divForBubble).easytabs({
            animate: false,
            defaultTab: selectorOfDefaultTab
        })
//        $(divForBubble).bind('easytabs:after', function(event, tab, panel, settings){
//            alert(panel.selector)
//        });

  }
  else {
    $(divForBubble).remove();//no tabs, so hide the bubble
    return;
  }

    // turn that tab thing into a bubble, and attach it to the original div ("group")
  $(group).each(function () {
      var targetHeight = $(this).height();

      showEvents = false;
      hideEvents = false;
      shouldShowAlways = true;

        //todo: really, this should detect some made-up style, so thatwe can control this behavior via the stylesheet
        if($(this).hasClass('wordsDiv')) {
            showEvents = " focusin ";
            hideEvents = ' focusout ';
            shouldShowAlways = false;
        }
      $(this).qtip({
          position: { at: 'right center',
              my: 'left center',
              adjust: {
                  x: 10,
                  y: 0
              }
          },
          content: $(divForBubble),

          show: {
              event: showEvents,
              ready: shouldShowAlways
          },
          events: {
              render: function (event, api) {
                  api.elements.content.height(targetHeight);
              }
          },
          style: {
              //doesn't work: tip:{ size: {height: 50, width:50}             },
              //doesn't work: tip:{ size: {x: 50, y:50}             },
              classes: 'ui-tooltip-green ui-tooltip-rounded uibloomSourceTextsBubble'
          },
          hide: hideEvents
      });
  });
}


 function GetLocalizedHint(element) {
     var whatToSay = $(element).data("hint");
     if(whatToSay.startsWith("*")){
         whatToSay = whatToSay.substring(1,1000);
     }

     var dictionary = GetDictionary();

     if(whatToSay in dictionary) {
         whatToSay = dictionary[whatToSay];
     }

     //stick in the language
     for (key in dictionary) {
         if (key.startsWith("{"))
             whatToSay = whatToSay.replace(key, dictionary[key]);

         whatToSay = whatToSay.replace("{lang}", dictionary[$(element).attr('lang')]);
     }
     return whatToSay;
 }

 //add a delete button which shows up when you hover
 function SetupDeletable(containerDiv) {
     $(containerDiv).mouseenter(
         function () {
             var button = $("<button class='deleteButton smallImageButton' title='Delete'></button>");
             $(button).click(function(){
                 $(containerDiv).remove()});
             $(this).prepend(button);
         })
        .mouseleave(function () {
            $(this).find(".deleteButton").each(function () {
                $(this).remove()
            });
    });
 }

 //Bloom "imageContainer"s are <div>'s with wrap an <img>, and automatically proportionally resize
 //the img to fit the available space
 function SetupImageContainer(containerDiv) {
     $(containerDiv).mouseenter(
         function () {
             var buttonModifier = "largeImageButton";
             if ($(this).height() < 80) {
                 buttonModifier = 'smallImageButton';
             }
             $(this).prepend("<button class='pasteImageButton " + buttonModifier + "' title='Paste Image'></button>");
             $(this).prepend("<button class='changeImageButton " + buttonModifier + "' title='Change Image'></button>");

//             if ($(this).find('button.imgMetadataProblem').length==0) {
                 var img = $(this).find('img');
                 if (CreditsAreRelevantForImage(img)) {
                     $(this).prepend("<button class='editMetadataButton " + buttonModifier + "' title='Edit Image Credits, Copyright, & License'></button>");
                 }
  //           }

             $(this).addClass('hoverUp');
         })
         .mouseleave(function () {
             $(this).removeClass('hoverUp');
             $(this).find(".changeImageButton").each(function () {
                 $(this).remove()
             });
             $(this).find(".pasteImageButton").each(function () {
                 $(this).remove()
             });
             $(this).find(".editMetadataButton").each(function () {
                 if (!$(this).hasClass('imgMetadataProblem')) {
                     $(this).remove()
                 }
             });
         });
     }

     function CreditsAreRelevantForImage(img) {
         return $(img).attr('src').toLowerCase().indexOf('placeholder') == -1; //don't offer to edit placeholder credits
     }

 //While the actual metada is embedded in the images (Bloom/palaso does that), Bloom sticks some metadata in data-* attributes
 // so that we can easily & quickly get to the here.
 function SetOverlayForImagesWithoutMetadata() {
     $(".bloom-imageContainer").each(function () {
         var img = $(this).find('img');
         if (!CreditsAreRelevantForImage(img)) {
            return;
         }
         var container = $(this);

         UpdateOverlay(container, img);

         //and if the bloom program changes these values (i.e. the user changes them using bloom), I
         //haven't figured out a way (appart from polling) to know that. So for now I'm using a hack
         //where Bloom calls click() on the image when it wants an update, and we detect that here.
         $(img).click(function () {
                   UpdateOverlay(container, img);
         });
     });
 }

 function UpdateOverlay(container, img) {

     $(container).find(".imgMetadataProblem").each(function () {
         $(this).remove()
     });

     //NB: we have 3 things we *want*: illustrator, copyright, and license. But we can only "demand" copyright.
     //For example, a org hires illustrators and doesn't normally retain (or remember) who did the illustration.
     //And offers no license; you have to contact them (this is recorded as data-license="").
     var copyright = $(img).attr('data-copyright');
     if (!copyright || copyright.length == 0) {

         var buttonModifier = "largeImageButton";
         if ($(container).height() < 80) {
             buttonModifier = 'smallImageButton';
         }

         $(container).prepend("<button class='editMetadataButton imgMetadataProblem "+buttonModifier+"' title='Image is missing Copyright information'></button>");
     }
 }


 // Instead of "missing", we want to show it in the right ui language. We also want the text
 // to indicate that it might not be missing, just didn't load (this happens on slow machines)
 // TODO: internationalize
 function SetAlternateTextOnImages(element) {
     if ($(element).attr('src').length > 0) { //don't show this on the empty license image when we don't know the license yet
         $(element).attr('alt', 'This picture, ' + $(element).attr('src') + ', is missing or was loading too slowly.');
     }
     else {
         $(element).attr('alt', '');//don't be tempted to show something like a '?' unless you fix the result when you have a custom book license on top of that '?'
     }

 }


 function SetupResizableElement(element) {

     $(element).mouseenter(
         function () {
             $(this).addClass("ui-mouseOver")
         }).mouseleave(function () {
             $(this).removeClass("ui-mouseOver")
         });

     var childImgContainer = $(element).find(".bloom-imageContainer");

     // A Picture Dictionary Word-And-Image
     if ($(childImgContainer).length > 0) {
         /* The case here is that the thing with this class actually has an
          inner image, as is the case for the Picture Dictionary.
          The key, non-obvious, difficult requirement is keeping the text below
          a picture dictionary item centered underneath the image.  I'd be
          surprised if this wasn't possible in CSS, but I'm not expert enough.
          So, I switched from having the image container be resizable, to having the
          whole div (image+headwords) be resizable, then use the "alsoResize"
          parameter to make the imageContainer resize.  Then, in order to make
          the image resize in real-time as you're dragging, I use the "resize"
          event to scale the image up proportionally (and centered) inside the
          newly resized container.
          */
         var img = $(childImgContainer).find("img");
         $(element).resizable({handles:'nw, ne, sw, se',
             containment: "parent",
             alsoResize:childImgContainer,
            resize:function (event, ui) {
                 img.scaleImage({scale:"fit"})
             }});


     }
     //An Image Container div (which must have an inner <img>
     else if ($(element).hasClass('bloom-imageContainer')) {
         var img = $(element).find("img");
         $(element).resizable({handles:'nw, ne, sw, se',
             containment: "parent",
             resize:function (event, ui) {
                 img.scaleImage({scale:"fit"})
             }});
     }
     // some other kind of resizable
     else {
         $(element).resizable({
             handles:'nw, ne, sw, se',
             containment: "parent",
              stop: ResizeUsingPercentages,
             start: function(e,ui){
                if($(ui.element).css('top')=='0px' && $(ui.element).css('left')=='0px'){
                    $(ui.element).data('doRestoreRelativePosition', 'true');
                }
             }
         });

     }
 }

 //jquery resizable normally uses pixels. This makes it use percentages, which are mor robust across page size/orientation changes
function ResizeUsingPercentages(e,ui){
    var parent = ui.element.parent();
    ui.element.css({
        width: ui.element.width()/parent.width()*100+"%",
        height: ui.element.height()/parent.height()*100+"%"
    });

    //after any resize jquery adds an absolute position, which we don't want unless the user has resized
    //so this removes it, unless we previously noted that the user had moved it
    if($(ui.element).data('doRestoreRelativePosition'))
    {
        ui.element.css({
            position: '',
            top: '',
            left: ''
        });
    }
    $(ui.element).removeData('hadPreviouslyBeenRelocated');
 }


 //---------------------------------------------------------------------------------

 jQuery(document).ready(function () {

     $.fn.qtip.zindex = 1000000;
     //gives an error $.fn.qtip.plugins.modal.zindex = 1000000 - 20;

     //add a marginBox if it's missing. We introduced it early in the first beta
     $(".bloom-page").each(function () {
         if ($(this).find(".marginBox").length == 0) {
             $(this).wrapInner("<div class='marginBox'></div>");
         }
     });

     AddToolbox();

     //make textarea edits go back into the dom (they were designed to be POST'ed via forms)
     jQuery("textarea").blur(function () {
         this.innerHTML = this.value;
     });

     jQuery.fn.reverse = function () {
         return this.pushStack(this.get().reverse(), arguments);
     };

     //if this browser doesn't have endsWith built in, add it
     if (typeof String.prototype.endsWith !== 'function') {
         String.prototype.endsWith = function (suffix) {
             return this.indexOf(suffix, this.length - suffix.length) !== -1;
         };
     }


     //firefox adds a <BR> when you press return, which is lame because you can't put css styles on BR, such as indent.
     //Eventually we may use a wysywig add-on which does this conversion as you type, but for now, we change it when
     //you tab or click out.
     jQuery(".bloom-editable").blur(function () {

         //This might mess some things up, so we're only applying it selectively
         if($(this).closest('.bloom-requiresParagraphs').length==0
                && ($(this).css('border-top-style') != 'dashed')) //this signal used to let the css add this conversion after some SIL-LEAD SHRP books were already typed
             return;

         var x = $(this).html();

         //the first time we see a field editing in Firefox, it won't have a p opener
         if (!x.startsWith('<p>')) {
             x = "<p>" + x;
         }

         x = x.split("<br>").join("</p><p>");

         //the first time we see a field editing in Firefox, it won't have a p closer
         if (!x.endsWith('</p>')) {
             x = x + "</p>";
         }
         $(this).html(x);

         //If somehow you get leading empty paragraphs, FF won't let you delete them
         $('p').each(function () {
             if ($(this).text() === "") {
                 $(this).remove();
             } else {
                 return false; //break
             }
         });

         //for some reason, perhaps FF-related, we end up with a new empty paragraph each time
         //so remove trailing <p></p>s
         $('p').reverse().each(function () {
             if ($(this).text() === "") {
                 $(this).remove();
             } else {
                 return false; //break
             }
         });
     });

     //when we discover an emtpy text box that has been marked to use PRs, start us off on the right foot
     $('.bloom-editable').focus(function () {
         if ($(this).closest('.bloom-requiresParagraphs').length==0
               && ($(this).css('border-top-style') != 'dashed')) //this signal used to let the css add this conversion after some SIL-LEAD SHRP books were already typed
             return;

         if ($(this).text() == '') {
             //stick in a paragraph, which makes FF do paragarphs instead of BRs.
             $(this).html('<p>&nbsp;</p>'); //zwnj makes the cursor invisible

             //now select that space, so we delete it when we start typing

             var el = $(this).find('p')[0].childNodes[0];
             var range = document.createRange();
             range.selectNodeContents(el);
             var sel = window.getSelection();
             sel.removeAllRanges();
             sel.addRange(range);
         }
         else {
             var el = $(this).find('p')[0];
             if (!el)
                 return; //these have text, but not p's yet. We'll have to wait until they leave (blur) to add in the P's.
             var range = document.createRange();
             range.selectNodeContents(el);
             range.collapse(true);//move to start of first paragraph
             var sel = window.getSelection();
             sel.removeAllRanges();
             sel.addRange(range);
         }
         //TODO if you do Ctrl+A and delete, you're now outside of our <p></p> zone. clicking out will trigger the blur handerl above, which will restore it.
     });


     SetBookCopyrightAndLicenseButtonVisibility();

     /*
     //when a textarea gets focus, send Bloom a dictionary of all the translations found within
     //the same parent element
     jQuery("textarea, div.bloom-editable").focus(function () {
     event = document.createEvent('MessageEvent');
     var origin = window.location.protocol + '//' + window.location.host;
     var obj = {};
     $(this).parent().find("textarea, div.bloom-editable").each(function () {
     obj[$(this).attr("lang")] = $(this).text();
     })
     var json = obj; //.get();
     json = JSON.stringify(json);
     event.initMessageEvent('textGroupFocused', true, true, json, origin, 1234, window, null);
     document.dispatchEvent(event);
     });
     */

     //in bilingual/trilingual situation, re-order the boxes to match the content languages, so that stylesheets don't have to
     $(".bloom-translationGroup").each(function () {
         var contentElements = $(this).find("textarea, div.bloom-editable");
         contentElements.sort(function (a, b) {
             //using negatives so that something with none of these labels ends up with a > score and at the end
             var scoreA = $(a).hasClass('bloom-content1') * -3 + ($(a).hasClass('bloom-content2') * -2) + ($(a).hasClass('bloom-content3') * -1);
             var scoreB = $(b).hasClass('bloom-content1') * -3 + ($(b).hasClass('bloom-content2') * -2) + ($(b).hasClass('bloom-content3') * -1);
             if (scoreA < scoreB)
                 return -1;
             if (scoreA > scoreB)
                 return 1;
             return 0;
         });
         //do the actual rearrangement
         $(this).append(contentElements);
     });

  // Add overflow event handlers so that when a div is overfull,
    // we add the overflow class and it gets a red background or something
    AddOverflowHandler();

  $("div.bloom-editable").bind('keydown', 'ctrl+b', function (e) {
        e.preventDefault();
        //document.execCommand("formatBlock", false, "strong");
    document.execCommand("bold", false, null);
  });

  $("div.bloom-editable").bind('keydown', 'ctrl+u', function (e) {
        e.preventDefault();
        document.execCommand("underline", false, null);
  });
  $("div.bloom-editable").bind('keydown', 'ctrl+i', function (e) {
        e.preventDefault();
        document.execCommand("italic", false, null);
  });

  //nb: it wasn't possible to catch *keypress* for ctrl+space
  $("div.bloom-editable").bind('keydown', 'ctrl+space', function (e) {
    e.preventDefault();
        document.execCommand("RemoveFormat", false, null);
  });


  // Actual testable determination of overflow or not
  jQuery.fn.IsOverflowing = function () {
    var element = $(this)[0];
    // We want to prevent an inner div from expanding past the borders set by any containing marginBox class.
    var marginBoxParent = $(element).parents('.marginBox');
    var parentBottom;
    if(marginBoxParent && marginBoxParent.length > 0)
      parentBottom = $(marginBoxParent[0]).offset().top + $(marginBoxParent[0]).outerHeight(true);
    else
      parentBottom = 999999;
    var elemBottom = $(element).offset().top + $(element).outerHeight(true);
    // If css has "overflow: visible;", scrollHeight is always 2 greater than clientHeight.
    // This is because of the thin grey border on a focused input box.
    // In fact, the focused grey border causes the same problem in detecting the bottom of a marginBox
    // so we'll apply the same 'fudge' factor to both comparisons.
     var focusedBorderFudgeFactor = 2;

     //the "basic book" template has a "Just Text" page which does some weird things to get vertically-centered
     //text. I don't know why, but this makes the clientHeight 2 pixels larger than the scrollHeight once it
     //is beyond its minimum height. We can detect that we're using this because it has this "firefoxHeight" data
     //element.
     var growFromCenterVerticalFudgeFactor =0;
     if($(element).data('firefoxheight')){
      growFromCenterVerticalFudgeFactor = 2;
     }

   //in the Picture Dictionary template, all words have a scrollheight that is 3 greater than the client height.
   //In the Headers of the Term Intro of the SHRP C1 P3 Pupil's book, scrollHeight = clientHeight + 6!!! Sigh.
   // the focussedBorderFudgeFactor takes care of 2 pixels, this adds one more.
   var shortBoxFudgeFactor = 4;

    //console.log('s='+element.scrollHeight+' c='+element.clientHeight);

     return element.scrollHeight > element.clientHeight + focusedBorderFudgeFactor + growFromCenterVerticalFudgeFactor + shortBoxFudgeFactor ||
         element.scrollWidth > element.clientWidth + focusedBorderFudgeFactor ||
       elemBottom > parentBottom + focusedBorderFudgeFactor;
  };

  // When a div is overfull,
  // we add the overflow class and it gets a red background or something
  function AddOverflowHandler() {
    //NB: for some historical reason in March 2014 the calendar still uses textareas
    $("div.bloom-editable, textarea").bind("keyup paste", function (e) {
      var $this = $(this);
      // Give the browser time to get the pasted text into the DOM first, before testing for overflow
      // GJM -- One place I read suggested that 0ms would work, it just needs to delay one 'cycle'.
      //        At first I was concerned that this might slow typing, but it doesn't seem to.
      setTimeout(function () {
        if ($this.IsOverflowing())
          $this.addClass('overflow');
        else {
          if ($this.hasClass('overflow'))
            $this.removeClass('overflow');
        }
      }, 100); // 100 milliseconds
      e.stopPropagation();
    });
  }



     //--------------------------------
     //keep divs vertically centered (yes, I first tried *all* the css approaches, they don't work for our situation)

     //do it initially
     $(".bloom-centerVertically").CenterVerticallyInParent();
     //reposition as needed
     $(".bloom-centerVertically").resize(function () { //nb: this uses a 3rd party resize extension from Ben Alman; the built in jquery resize only fires on the window
         $(this).CenterVerticallyInParent();
     });




     /* Defines a starts-with function*/
     if (typeof String.prototype.startsWith != 'function') {
         String.prototype.startsWith = function (str) {
             return this.indexOf(str) == 0;
         };
     }

     //Add little language tags
     $("div.bloom-editable:visible").each(function () {
         var key = $(this).attr("lang");
         var dictionary = GetDictionary();
         var whatToSay = dictionary[key];
         if (whatToSay == null)
             whatToSay = key; //just show the code

         if (key = "*")
             return; //seeing a "*" was confusing even to me

         //with a really small box that also had a hint qtip, there wasn't enough room and the two fough with each other, leading to flashing back and forth
         if ($(this).width() < 100) {
             return;
         }

         //TODO: I haven't been able to get these to work right... I just want the tooltip to hide when the user is in the box at the moment,
         //then reappear

         var shouldShowAlways = true; // "mouseleave unfocus";
         var hideEvents = false; // "mouseover focusin";

         //             shouldShowAlways = false;
         //           hideEvents = 'unfocus mouseleave';


         $(this).qtip({
             content: whatToSay,

             position: {
                 my: 'top right',
                 at: 'bottom right'
                 , adjust: { y: -25 }
             },
             show: { ready: shouldShowAlways },
             hide: {
                 event: hideEvents
             },
             style: {
                 classes: 'ui-languageToolTip'
             }
         })
         // doing this makes it imposible to reposition them           .removeData('qtip'); // allows multiple tooltips. See http://craigsworks.com/projects/qtip2/tutorials/advanced/
     });

     // I took away this feature becuase qtip was changing titles to "oldtitle" which caused problems because we save the result. So now, we just
     // say that if you want a momentary qtip, do a data-hint and start it with '*'   //Add popup yellow bubbles to match title attributes
     //    $("*[title]").each(function() {
     //        $(this).qtip({ position: {
     //                at: 'right bottom', //I like this, but it doesn't reposition well -->at: 'right center',
     //                my: 'top left', //I like this, but it doesn't reposition well-->  my: 'left center',
     //                viewport: $(window)
     //            },
     //            style: { classes:'ui-tooltip-shadow ui-tooltip-plain' } });
     //    });

     //put hint bubbles next to elements which call for them.
     //show those bubbles if the item is empty, or if it's not empty, then if it is in focus OR the mouse is over the item
     $("*[data-hint]").each(function () {

         if($(this).css('display') == 'none'){
             return;
         }

         if ($(this).css('border-bottom-color') == 'transparent') {
             return; //don't put tips if they can't edit it. That's just confusing
         }
         if ($(this).css('display') == 'none') {
             return; //don't put tips if they can't see it.
         }
         theClasses = 'ui-tooltip-shadow ui-tooltip-plain';
         if ($(this).height() < 100) {
             pos = {
                 at: 'right center', //I like this, but it doesn't reposition well -->'right center',
                 my: 'left center' //I like this, but it doesn't reposition well-->  'left center',
                , viewport: $(window)
                 // , adjust: { y: -20 }
             };
         }
         else { // with the big back covers, the adjustment just makes things worse.
             pos = {
                 at: 'right center',
                 my: 'left center'
             }
         }

         //temporarily disabling this; the problem is that its more natural to put the hint on enclosing 'translationgroup' element, but those elements are *never* empty.
         //maybe we could have this logic, but change this logic so that for all items within a translation group, they get their a hint from a parent, and then use this isempty logic
         //at the moment, the logic is all around whoever has the data-hint
         //var shouldShowAlways = $(this).is(':empty'); //if it was empty when we drew the page, keep the tooltip there
         var shouldShowAlways = true;
         var hideEvents = shouldShowAlways ? null : "focusout mouseleave";

         //make hints that start with a * only show when the field has focus
         if ($(this).data("hint").startsWith("*")) {
             shouldShowAlways = false;
             hideEvents = 'unfocus mouseleave';
         }

         var whatToSay = GetLocalizedHint(this);

         var functionCall = $(this).data("functiononhintclick");
         if (functionCall) {
             shouldShowAlways = true;
             whatToSay = "<a href='" + functionCall + "'>" + whatToSay + "</a>";
             hideEvents = false;
         }

         $(this).qtip({
             content: whatToSay,
             position: pos,
             show: {
                 event: " focusin mouseenter",
                 ready: shouldShowAlways //would rather have this kind of dynamic thing, but it isn't right: function(){$(this).is(':empty')}//
             }
            , tip: { corner: "left center" }
            , hide: {
                event: hideEvents
            },
             adjust: { method: "flip none" },
             style: {
                 classes: theClasses
             }
             //            ,adjust:{screen:true, resize:true}
         });
     });

     $.fn.hasAttr = function (name) {
         var attr = $(this).attr(name);

         // For some browsers, `attr` is undefined; for others,
         // `attr` is false.  Check for both.
         return (typeof attr !== 'undefined' && attr !== false);
     };

     //Show data on fields
     /* disabled to see if we can do fine without it
     $("*[data-book], *[data-library], *[lang]").each(function() {

     var data = " ";
     if ($(this).hasAttr("data-book")) {
     data = $(this).attr("data-book");
     }
     if ($(this).hasAttr("data-library")) {
     data = $(this).attr("data-library");
     }
     $(this).qtipSecondary({
     content: {text: $(this).attr("lang") + "<br>" + data}, //, title: { text:  $(this).attr("lang")}},

     position: {
     my: 'top right',
     at: 'top left'
     },
     //                  show: {
     //                                  event: false, // Don't specify a show event...
     //                                  ready: true // ... but show the tooltip when ready
     //                              },
     //                  hide:false,//{     fixed: true },// Make it fixed so it can be hovered over    },
     style: {'default': false,
     tip: {corner: false,border: false},
     classes: 'fieldInfo-qtip'
     }
     });
     });
     */


     //eventually we want to run this *after* we've used the page, but for now, it is useful to clean up stuff from last time
     Cleanup();

     //make images look click-able when you cover over them
     jQuery(".bloom-imageContainer").each(function () {
         SetupImageContainer(this);
     });


     //todo: this had problems. Check out the later approach, seen in draggableLabel (e.g. move handle on the inside, using a background image on a div)
     jQuery(".bloom-draggable").mouseenter(function () {
         $(this).prepend("<button class='moveButton' title='Move'></button>");
         $(this).find(".moveButton").mousedown(function (e) {
             $(this).parent().trigger(e);
         });
     });
     jQuery(".bloom-draggable").mouseleave(function () {
         $(this).find(".moveButton").each(function () {
             $(this).remove()
         });
     });

     $('div.bloom-editable').each(function () {
         $(this).attr('contentEditable', 'true');
     });
     // Bloom needs to make some fields readonly. E.g., the original license when the user is translating a shellbook
     // Normally, we'd control this is a style in editTranslationMode.css/editOriginalMode.css. However, "readonly" isn't a style, just
     // an attribute, so it can't be included in css.
     // The solution here is to add the readonly attribute when we detect that the css has set the cursor to "not-allowed".
     $('textarea, div').focus(function () {
         //        if ($(this).css('border-bottom-color') == 'transparent') {
         if ($(this).css('cursor') == 'not-allowed') {
             $(this).attr("readonly", "true");
             $(this).removeAttr("contentEditable");
         }
         else {
             $(this).removeAttr("readonly");
             //review: do we need to add contentEditable... that could lead to making things editable that shouldn't be
         }
     });


     // If the user moves over something they can't edit, show a tooltip explaining why not
     $('*[data-hint]').each(function () {
         if ($(this).css('cursor') == 'not-allowed') {
             var whyDisabled = "You cannot change these because this is not the original copy.";
             if ($(this).hasClass('bloom-readOnlyInEditMode')) {
                 whyDisabled = "You cannot put anything in there while making an original book.";
             }

             var whatToSay = GetLocalizedHint(this) + " <br/>" + whyDisabled;
             var theClasses = 'ui-tooltip-shadow ui-tooltip-red';
             var pos = { at: 'right center',
                 my: 'left center'
             };
             $(this).qtip({
                 content: whatToSay,
                 position: pos,
                 show: {
                     event: " focusin mouseenter"
                 },
                 style: {
                     classes: theClasses
                 }
             });
         }


     });



     //Same thing for divs which are potentially editable, but via the contentEditable attribute instead of TextArea's ReadOnly attribute
     // editTranslationMode.css/editOriginalMode.css can't get at the contentEditable (css can't do that), so
     // so they set the cursor to "not-allowed", and we detect that and set the contentEditable appropriately
     $('div.bloom-readOnlyInTranslationMode').focus(function () {
         if ($(this).css('cursor') == 'not-allowed') {
             $(this).removeAttr("contentEditable");
         }
         else {
             $(this).attr("contentEditable", "true");
         }
     });



     // this is gone because of a memory violation bug in geckofx 11 with messaging. Now we just notice the click from within c#
     //    // Send all the data from this div in a message, so Bloom can do something like show a custom dialog box
     // for editing the data. We only notice the click if the cursor style is 'pointer', so that CSS can turn this on/off.
     //    $('div.bloom-metaData').each(function() {
     //        if ($(this).css('cursor') == 'pointer') {
     //            $(this).click(function() {
     //                event = document.createEvent('MessageEvent');
     //                var origin = window.location.protocol + '//' + window.location.host;
     //                var obj = {};
     //                $(this).find("*[data-book]").each(function() {
     //                    obj[$(this).attr("data-book")] = $(this).text();
     //                })
     //                var json = obj; //.get();
     //                json = JSON.stringify(json);
     //                event.initMessageEvent('divClicked', true, true, json, origin, "", window, null);
     //                document.dispatchEvent(event);
     //            })
     //        }
     //    });

     //first used in the Uganda SHRP Primer 1 template, on the image on day 1
     //This took *enormous* fussing in the css. TODO: copy what we learned there
     //to the (currently experimental) Toolbox template (see 'bloom-draggable')

     $(".bloom-draggableLabel").each(function () {
         // previous to June 2014, containment was working, so some items may be
         // out of bounds. This gets any such back in bounds.
         if ($(this).position().left < 0) {
             $(this).css('left', 0);
         }
         if ($(this).position().top < 0) {
             $(this).css('top', 0);
         }
         if ($(this).position().left + $(this).width() > $(this).parent().width()) {
             $(this).css('left', $(this).parent().width() - $(this).width());
         }
         if ($(this).position().top > $(this).parent().height()) {
             $(this).css('top', $(this).parent().height() - $(this).height());
         }

         $(this).draggable(
         {
             containment: "parent", //NB: this containment is of the translation group, not the editable inside it. So avoid margins on the translation group.
             handle: '.dragHandle'
         });
     });


     $(".bloom-draggableLabel")
        .mouseenter(function () {
         $(this).prepend(" <div class='dragHandle'></div>")
         });

        jQuery(".bloom-draggableLabel").mouseleave(function () {
         $(this).find(".dragHandle").each(function () {
             $(this).remove()
         })});





     //add drag and resize ability where elements call for it
     //   $(".bloom-draggable").draggable({containment: "parent"});
     $(".bloom-draggable").draggable({ containment: "parent",
         handle: '.bloom-imageContainer',
         stop: function (event, ui) {
             $(this).find('.wordsDiv').find('div').each(function () {
                 $(this).qtip('reposition');
             })
         } //yes, this repositions *all* qtips on the page. Yuck.
     }); //without this "handle" restriction, clicks on the text boxes don't work. NB: ".moveButton" is really what we wanted, but didn't work, probably because the button is only created on the mouseEnter event, and maybe that's too late.
     //later note: using a real button just absorbs the click event. Other things work better
     //http://stackoverflow.com/questions/10317128/how-to-make-a-div-contenteditable-and-draggable


     /* Support in page combo boxes that set a class on the parent, thus making some change in the layout of the pge.
     Example:
          <select name="Story Style" class="bloom-classSwitchingCombobox">
              <option value="Fictional">Fiction</option>
              <option value="Informative">Informative</option>
      </select>
      */
     //First we select the initial value based on what class is currently set, or leave to the default if none of them
     $(".bloom-classSwitchingCombobox").each(function(){
         //look through the classes of the parent for any that match one of our combobox values
         var i;
         for(i=0; i< this.options.length;i++) {
             var c = this.options[i].value;
             if($(this).parent().hasClass(c)){
                 $(this).val(c);
                 break;
             }
         }
     });
     //And now we react to the user choosing a different value
     $(".bloom-classSwitchingCombobox").change(function(){
         //remove any of the values that might already be set
         var i;
        for(i=0; i< this.options.length;i++) {
             var c = this.options[i].value;
            $(this).parent().removeClass(c);
         }
         //add back in the one they just chose
         $(this).parent().addClass(this.value);
     });

     //only make things deletable if they have the deletable class *and* page customization is enabled
     $("DIV.bloom-page.enablePageCustomization DIV.bloom-deletable").each(function () {
         SetupDeletable(this);
     });

     $(".pictureDictionaryPage").each(function () {
         AddExperimentalNotice(this);
     });

     $(".bloom-resizable").each(function () {
         SetupResizableElement(this);
     });

     $("img").each(function () {
         SetAlternateTextOnImages(this);
     });

     SetOverlayForImagesWithoutMetadata();

     //focus on the first editable field
     //$(':input:enabled:visible:first').focus();
     $("textarea, div.bloom-editable").first().focus(); //review: this might choose a textarea which appears after the div. Could we sort on the tab order?

     SetupShowingTopicChooserWhenTopicIsClicked();

     //copy source texts out to their own div, where we can make a bubble with tabs out of them
     //We do this because if we made a bubble out of the div, that would suck up the vernacular editable area, too, and then we couldn't translate the book.
     $("*.bloom-translationGroup").each(function () {
         if ($(this).find("textarea, div").length > 1) {
             MakeSourceTextDivForGroup(this);
         }
     });


     //make images scale up to their container without distorting their proportions, while being centered within it.
     $(".bloom-imageContainer img").scaleImage({ scale: "fit" }); //uses jquery.myimgscale.js

     // when the image changes, we need to scale again:
     $(".bloom-imageContainer img").load(function () {
         $(this).scaleImage({ scale: "fit" });
     });

     //and when their parent is resized by the user, we need to scale again:
     $(".bloom-imageContainer img").each(function () {
         $(this).parent().resize(function () {
             $(this).find("img").scaleImage({ scale: "fit" });
             try {
                 ResetRememberedSize(this);
             } catch (error) {
                 console.log(error);
             }
         });
     });
 });

//function SetCopyrightAndLicense(data) {
//    $('*[data-book="copyright"]').each(function(){
//        $(this).text(data.copyright);}
//    )
function SetCopyrightAndLicense(data) {
    //nb: for textarea, we need val(). But for div, it would be text()
    $("DIV[data-book='copyright']").text(data.copyright);
    $("DIV[data-book='licenseUrl']").text(data.licenseUrl);
    $("DIV[data-book='licenseDescription']").text(data.licenseDescription);
    $("DIV[data-book='licenseNotes']").text(data.licenseNotes);
    var licenseImageValue = data.licenseImage + "?" + new Date().getTime(); //the time thing makes the browser reload it even if it's the same name
    if (data.licenseImage.length == 0) {
        licenseImageValue = ""; //don't wan the date on there
        $("IMG[data-book='licenseImage']").attr('alt', '');
    }

    $("IMG[data-book='licenseImage']").attr("src", licenseImageValue);
    SetBookCopyrightAndLicenseButtonVisibility();
}

function SetBookCopyrightAndLicenseButtonVisibility() {
    var shouldShowButton = !($("DIV.copyright").text());
    $("button#editCopyrightAndLicense").css("display", shouldShowButton ? "inline" : "none");
}

function FindOrCreateTopicDialogDiv() {
    var dialogContents = $("body").find("div#topicChooser");
    if (!dialogContents.length) {
        //$(temp).load(url);//this didn't work in bloom (it did in my browser, but it was FFver 9 wen Bloom was 8. Or the FF has the cross-domain security loosened perhaps?
        dialogContents = $("<div id='topicChooser' title='Topics'/>").appendTo($("body"));
        var topics = JSON.parse(GetSettings().topics);
        // var topics = ["Agriculture", "Animal Stories", "Business", "Culture", "Community Living", "Dictionary", "Environment", "Fiction", "Health", "How To", "Math", "Non Fiction", "Spiritual", "Personal Development", "Primer", "Science", "Tradition"];

        dialogContents.append("<ol id='topics'></ol>");
        for (i in topics) {
            $("ol#topics").append("<li class='ui-widget-content'>" + topics[i] + "</li>");
        }

        $("#topics").selectable();

        //This weird stuff is to make up for the jquery uI not automatically theme-ing... without the following, when you select an item, nothing visible happens (from stackoverflow)
        $("#topics").selectable({
            unselected: function() {
                $(":not(.ui-selected)", this).each(function() {
                    $(this).removeClass('ui-state-highlight');
                });
            },
            selected: function() {
                $(".ui-selected", this).each(function() {
                    $(this).addClass('ui-state-highlight');
                });
            }
        });
        $("#topics li").hover(
        function() {
            $(this).addClass('ui-state-hover');
        },
        function() {
            $(this).removeClass('ui-state-hover');
        }
        );
    }
    return dialogContents;
}

//note, the normal way is for the user to click the link on the qtip.
//But clicking on the exiting topic may be natural too, and this prevents
//them from editing it by hand.
function SetupShowingTopicChooserWhenTopicIsClicked() {
    $("div[data-book='topic']").click(function () {
        if ($(this).css('cursor') == 'not-allowed')
            return;
        ShowTopicChooser();
    });
}

// This is called directly from Bloom via RunJavaScript()
function ShowTopicChooser() {
    var dialogContents = FindOrCreateTopicDialogDiv();
    var dlg = $(dialogContents).dialog({
        autoOpen: "true",
        modal: "true",
        zIndex: 30000, //qtip is in the 15000 range
        buttons: {
            "OK": function () {
                var t = $("ol#topics li.ui-selected");
                if (t.length) {
                    $("div[data-book='topic']").filter("[class~='bloom-contentNational1']").text(t[0].innerHTML);
                }
                $(this).dialog("close");
            }
        }
    });

    //make a double click on an item close the dialog
    dlg.find("li").dblclick(function () {
        var x = dlg.dialog("option", "buttons");
        x['OK'].apply(dlg);
    });
}