﻿// Enhance: this could be turned into a Typescript Module with only two public methods

function cleanupImages() {
    $('.bloom-imageContainer').css('opacity', '');//comes in on img containers from an old version of myimgscale, and is a major problem if the image is missing
    $('.bloom-imageContainer').css('overflow', '');//review: also comes form myimgscale; is it a problem?

}

function SetupImagesInContainer(container)
{
    $(container).find(".bloom-imageContainer img").each(function() {
        SetupImage(this);
    });

    $(container).find(".bloom-imageContainer").each(function() {
        SetupImageContainer(this);
    });

    //todo: this had problems. Check out the later approach, seen in draggableLabel (e.g. move handle on the inside, using a background image on a div)
    $(container).find(".bloom-draggable").mouseenter(function() {
        $(this).prepend("<button class='moveButton' title='Move'></button>");
        $(this).find(".moveButton").mousedown(function(e) {
            $(this).parent().trigger(e);
        });
    });
    $(container).find(".bloom-draggable").mouseleave(function() {
        $(this).find(".moveButton").each(function() {
            $(this).remove()
        });
    });

    $(container).find("img").each(function () {
        SetAlternateTextOnImages(this);
    });
}

function SetupImage(image) {
    //make images scale up to their container without distorting their proportions, while being centered within it.
    $(image).scaleImage({ scale: "fit" }); //uses jquery.myimgscale.js

    // when the image changes, we need to scale again:
    $(image).load(function () {
        $(this).scaleImage({ scale: "fit" });
    });

    //and when their parent is resized by the user, we need to scale again:
    $(image).parent().resize(function () {
        $(this).find("img").scaleImage({ scale: "fit" });
        try {
            ResetRememberedSize(this);
        } catch (error) {
            console.log(error);
        }
    });
}

//Bloom "imageContainer"s are <div>'s with wrap an <img>, and automatically proportionally resize
//the img to fit the available space
function SetupImageContainer(containerDiv) {
    $(containerDiv).mouseenter(function () {
        var img = $(this).find('img');

        var buttonModifier = "largeImageButton";
        if ($(this).height() < 95) {
            buttonModifier = 'smallImageButton';
        }
        $(this).prepend('<button class="pasteImageButton ' + buttonModifier + '" title="' + localizationManager.getText("EditTab.Image.PasteImage") + '"></button>');
        $(this).prepend('<button class="changeImageButton ' + buttonModifier + '" title="' + localizationManager.getText("EditTab.Image.ChangeImage") + '"></button>');

        SetImageTooltip(containerDiv, img);

        if (CreditsAreRelevantForImage(img)) {
            $(this).prepend('<button class="editMetadataButton ' + buttonModifier + '" title="' + localizationManager.getText("EditTab.Image.EditMetadata") + '"></button>');
        }

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

function SetImageTooltip(container, img) {
    getIframeChannel().simpleAjaxGet('/bloom/imageInfo?image=' + $(img).attr('src'), function (response) {
        var info = response.name + "\n"
                + getFileLengthString(response.bytes) + "\n"
                + response.width + " x " + response.height;
        container.title = info;
        });
}

function getFileLengthString(bytes) {
    var units = ['Bytes', 'KB', 'MB'];
    for (var i = units.length; i-- > 0;) {
        var unit = Math.pow(1024, i);
        if (bytes >= unit)
            return parseFloat(Math.round(bytes / unit * 100) / 100).toFixed(2) + ' ' + units[i];
    }
}


function CreditsAreRelevantForImage(img) {
    return $(img).attr('src').toLowerCase().indexOf('placeholder') == -1; //don't offer to edit placeholder credits
}

//While the actual metadata is embedded in the images (Bloom/palaso does that), Bloom sticks some metadata in data-* attributes
// so that we can easily & quickly get to the here.
function SetOverlayForImagesWithoutMetadata(container) {
    $(container).find(".bloom-imageContainer").each(function () {
        var img = $(this).find('img');
        if (!CreditsAreRelevantForImage(img)) {
            return;
        }
        var container = $(this);

        UpdateOverlay(container, img);

        //and if the bloom program changes these values (i.e. the user changes them using bloom), I
        //haven't figured out a way (apart from polling) to know that. So for now I'm using a hack
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

    //review: should we also require copyright, illustrator, etc? In many contexts the id of the work-for-hire illustrator isn't available
    var copyright = $(img).attr('data-copyright');
    if (!copyright || copyright.length == 0) {

        var buttonModifier = "largeImageButton";
        if ($(container).height() < 80) {
            buttonModifier = 'smallImageButton';
        }

        $(container).prepend("<button class='editMetadataButton imgMetadataProblem " + buttonModifier + "' title='Image is missing information on Credits, Copyright, or License'></button>");
    }
}

// Instead of "missing", we want to show it in the right ui language. We also want the text
// to indicate that it might not be missing, just didn't load (this happens on slow machines)
// TODO: internationalize
function SetAlternateTextOnImages(element) {
    if ($(element).attr('src').length > 0) { //don't show this on the empty license image when we don't know the license yet
        var nameWithoutQueryString = $(element).attr('src').split("?")[0];
        $(element).attr('alt', 'This picture, ' + nameWithoutQueryString + ', is missing or was loading too slowly.');
    } else {
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
        $(element).resizable({
            handles: 'nw, ne, sw, se',
            containment: "parent",
            alsoResize: childImgContainer,
            resize: function (event, ui) {
                img.scaleImage({ scale: "fit" })
            }
        });
        return $(element);
    }
        //An Image Container div (which must have an inner <img>
    else if ($(element).hasClass('bloom-imageContainer')) {
        var img = $(element).find("img");
        $(element).resizable({
            handles: 'nw, ne, sw, se',
            containment: "parent",
            resize: function (event, ui) {
                img.scaleImage({ scale: "fit" })
            }
        });
    }
        // some other kind of resizable
    else {
        $(element).resizable({
            handles: 'nw, ne, sw, se',
            containment: "parent",
            stop: ResizeUsingPercentages,
            start: function (e, ui) {
                if ($(ui.element).css('top') == '0px' && $(ui.element).css('left') == '0px') {
                    $(ui.element).data('doRestoreRelativePosition', 'true');
                }
            }
        });
    }
}

//jquery resizable normally uses pixels. This makes it use percentages, which are mor robust across page size/orientation changes
function ResizeUsingPercentages(e, ui) {
    var parent = ui.element.parent();
    ui.element.css({
        width: ui.element.width() / parent.width() * 100 + "%",
        height: ui.element.height() / parent.height() * 100 + "%"
    });

    //after any resize jquery adds an absolute position, which we don't want unless the user has resized
    //so this removes it, unless we previously noted that the user had moved it
    if ($(ui.element).data('doRestoreRelativePosition')) {
        ui.element.css({
            position: '',
            top: '',
            left: ''
        });
    }
    $(ui.element).removeData('hadPreviouslyBeenRelocated');
}
