/// <reference path="readerToolsModel.ts" />
/// <reference path="directoryWatcher.ts" />
// listen for messages sent to this page
window.addEventListener('message', processDLRMessage, false);

function getSetupDialogWindow() {
    return parent.window.document.getElementById("settings_frame").contentWindow;
}

/**
* Respond to messages
* @param {Event} event
*/
function processDLRMessage(event) {
    var params = event.data.split("\n");

    switch (params[0]) {
        case 'Texts':
            if (model.texts)
                getSetupDialogWindow().postMessage('Files\n' + model.texts.join("\r"), '*');
            return;

        case 'Words':
            var words = ReaderToolsModel.selectWordsFromSynphony(false, params[1].split(' '), params[1].split(' '), true, true);
            getSetupDialogWindow().postMessage('Words\n' + JSON.stringify(words), '*');
            return;

        case 'Refresh':
            var synphony = model.getSynphony();
            synphony.loadSettings(JSON.parse(params[1]));
            model.updateControlContents();
            model.doMarkup();
            return;

        case 'SetupType':
            getSetupDialogWindow().postMessage('SetupType\n' + model.setupType, '*');
            return;

        case 'SetMarkupType':
            model.setMarkupType(parseInt(params[1]));
            return;

        default:
    }
}

function initializeDecodableRT() {
    // make sure synphony is initialized
    if (!model.getSynphony().source) {
        iframeChannel.simpleAjaxGet('/bloom/readers/getDefaultFont', setDefaultFont);
        iframeChannel.simpleAjaxGet('/bloom/readers/loadReaderToolSettings', initializeSynphony);
    }

    // use the off/on pattern so the event is not added twice if the tool is closed and then reopened
    $('#incStage').onOnce('click.readerTools', function () {
        model.incrementStage();
    });

    $('#decStage').onOnce('click.readerTools', function () {
        model.decrementStage();
    });

    $('#sortAlphabetic').onOnce('click.readerTools', function () {
        model.sortAlphabetically();
    });

    $('#sortLength').onOnce('click.readerTools', function () {
        model.sortByLength();
    });

    $('#sortFrequency').onOnce('click.readerTools', function () {
        model.sortByFrequency();
    });

    model.updateControlContents();

    setTimeout(function () {
        resizeWordList();
    }, 100);
    setTimeout(function () {
        $.divsToColumns('letter');
    }, 100);
}

function initializeLeveledRT() {
    // make sure synphony is initialized
    if (!model.getSynphony().source) {
        iframeChannel.simpleAjaxGet('/bloom/readers/getDefaultFont', setDefaultFont);
        iframeChannel.simpleAjaxGet('/bloom/readers/loadReaderToolSettings', initializeSynphony);
    }

    $('#incLevel').onOnce('click.readerTools', function () {
        model.incrementLevel();
    });

    $('#decLevel').onOnce('click.readerTools', function () {
        model.decrementLevel();
    });

    model.updateControlContents();
}

if (typeof ($) === "function") {
    // Running for real, and jquery properly loaded first
    $(document).ready(function () {
        model = new ReaderToolsModel();
        model.setSynphony(new SynphonyApi());
    });
}

/**
* The function that is called to hook everything up.
* Note: settingsFileContent may be empty.
*
* @param settingsFileContent The content of the standard JSON) file that stores the Synphony settings for the collection.
* @global {ReaderToolsModel) model
*/
function initializeSynphony(settingsFileContent) {
    var synphony = model.getSynphony();
    synphony.loadSettings(settingsFileContent);
    model.restoreState();

    model.updateControlContents();

    // change markup based on visible options
    $('#accordion').onOnce('accordionactivate.readerTools', function (event, ui) {
        model.setMarkupType(ui.newHeader.data('markuptype'));
    });

    // set up a DirectoryWatcher on the Sample Texts directory
    model.directoryWatcher = new DirectoryWatcher('Sample Texts', 10);
    model.directoryWatcher.onChanged('SampleFilesChanged.ReaderTools', readerSampleFilesChanged);
    model.directoryWatcher.start();

    // get the list of sample texts
    iframeChannel.simpleAjaxGet('/bloom/readers/getSampleTextsList', setTextsList);
}

/**
* Called in response to a request for the files in the sample texts directory
* @param textsList List of file names delimited by \r
*/
function setTextsList(textsList) {
    model.texts = textsList.split(/\r/).filter(function (e) {
        return e ? true : false;
    });
    model.getNextSampleFile();
}

function setDefaultFont(fontName) {
    model.fontName = fontName;
}

/**
* This method is called whenever a change is detected in the Sample Files directory
*/
function readerSampleFilesChanged() {
    // reset the file and word list
    lang_data = new LanguageData();
    model.allWords = {};
    model.textCounter = 0;

    var settings = model.getSynphony().source;
    model.setSynphony(new SynphonyApi());

    var synphony = model.getSynphony();
    synphony.loadSettings(settings);

    // reload the sample texts
    iframeChannel.simpleAjaxGet('/bloom/readers/getSampleTextsList', setTextsList);
}

/**
* Adds a function to the list of functions to call when the word list changes
*/
function addWordListChangedListener(listenerNameAndContext, callback) {
    model.wordListChangedListeners[listenerNameAndContext] = callback;
}

function makeLetterWordList() {
    // get a copy of the current settings
    var settings = jQuery.extend(true, {}, model.getSynphony().source);

    // remove levels
    if (typeof settings.levels !== null)
        settings.levels = null;

    // get the words for each stage
    var knownGPCS = [];
    for (var i = 0; i < settings.stages.length; i++) {
        var stageGPCS = settings.stages[i].letters.split(' ');
        knownGPCS = _.union(knownGPCS, stageGPCS);
        var stageWords = ReaderToolsModel.selectWordsFromSynphony(true, stageGPCS, knownGPCS, true, true);
        settings.stages[i].words = _.toArray(stageWords);
    }

    // get list of all words
    var allGroups = [];
    for (var j = 1; j <= lang_data.VocabularyGroups; j++)
        allGroups.push('group' + j);
    allGroups = libsynphony.chooseVocabGroups(allGroups);

    var allWords = [];
    for (var g = 0; g < allGroups.length; g++) {
        allWords = allWords.concat(allGroups[g]);
    }
    allWords = _.compact(_.pluck(allWords, 'Name'));

    // export the word list
    var ajaxSettings = { type: 'POST', url: '/bloom/readers/makeLetterAndWordList' };
    ajaxSettings['data'] = {
        settings: JSON.stringify(settings),
        allWords: allWords.join('\t')
    };

    $.ajax(ajaxSettings);
}

function loadExternalLink(url) {
    $.get(url, function () {
        // ignore response
        // in this case, we just want to open an external browser with a link, so we don't want to process the response
    });
}

/**
* We need to check the size of the decodable reader tool pane periodically so we can adjust the height of the word list
* @global {number} previousHeight
*/
function resizeWordList() {
    var div = $('body').find('div[data-panelId="DecodableRT"]');
    if (div.length === 0)
        return;

    var currentHeight = div.height();

    // resize the word list if the size of the pane changed
    if (previousHeight !== currentHeight) {
        previousHeight = currentHeight;

        var wordList = div.find('#wordList');
        var top = wordList.parent().position().top;

        var height = Math.floor(currentHeight - top - 20);

        if (height < 50)
            height = 50;

        wordList.parent().css('height', height + 'px');
    }

    setTimeout(function () {
        resizeWordList();
    }, 500);
}
//# sourceMappingURL=readerTools.js.map
