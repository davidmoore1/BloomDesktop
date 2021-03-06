/// <reference path="libsynphony/synphony.d.ts" />
/// <reference path="libsynphony/underscore-1.5.2.d.ts" />
/// <reference path="../../lib/jquery.d.ts" />
/// <reference path="readerSettings.ts" />
var SynphonyApi = (function () {
    function SynphonyApi() {
        this.stages = [];
        this.levels = [];
    }
    /**
    *
    * @param fileContent
    */
    SynphonyApi.prototype.loadSettings = function (fileContent) {
        if (!lang_data)
            lang_data = new LanguageData();

        if (!fileContent)
            return;

        var data = jQuery.extend(new ReaderSettings(), fileContent);

        this.source = data;

        if (data.letters !== '') {
            lang_data.addGrapheme(data.letters.split(' '));
            lang_data.addWord(data.moreWords.split(' '));
            lang_data.LanguageSortOrder = data.letters.split(' ');

            var stgs = data.stages;
            if (stgs) {
                this.stages = [];
                for (var j = 0; j < stgs.length; j++) {
                    this.AddStage(jQuery.extend(true, new ReaderStage((j + 1).toString()), stgs[j]));
                }
            }
        }

        var lvls = data.levels;
        if (lvls) {
            this.levels = [];
            for (var i = 0; i < lvls.length; i++) {
                this.addLevel(jQuery.extend(true, new ReaderLevel((i + 1).toString()), lvls[i]));
            }
        }
    };

    SynphonyApi.prototype.loadFromLangData = function (langData) {
        if (!this.source)
            this.source = new ReaderSettings();

        if (this.source.letters === '') {
            var sorted = langData.LanguageSortOrder.join(' ').toLowerCase().split(' ');
            sorted = _.uniq(sorted);

            this.source.letters = sorted.join(' ');
        }
    };

    SynphonyApi.fireCSharpEvent = function (eventName, eventData) {
        //noinspection TaskProblemsInspection
        var event = new MessageEvent(eventName, { 'view': window, 'bubbles': true, 'cancelable': true, 'data': eventData });
        document.dispatchEvent(event);
    };

    // This is at least useful for testing; maybe for real use.
    SynphonyApi.prototype.AddStage = function (stage) {
        this.stages.push(stage);
    };

    //noinspection JSUnusedGlobalSymbols
    /**
    * Gets a URI that points to the directory containing the "synphonyApi.js" file.
    * @returns {String}
    */
    SynphonyApi.prototype.getScriptDirectory = function () {
        var src = $('script[src$="synphonyApi.js"]').attr('src').replace('synphonyApi.js', '').replace(/\\/g, '/');
        if (!src)
            return '';
        return src;
    };

    /**
    * Add a list of words to the lang_data object
    * @param {Object} words The keys are the words, and the values are the counts
    */
    SynphonyApi.prototype.addWords = function (words) {
        if (!words)
            return;

        var wordNames = Object.keys(words);

        if (!lang_data)
            lang_data = new LanguageData();
        for (var i = 0; i < wordNames.length; i++) {
            lang_data.addWord(wordNames[i], words[wordNames[i]]);
        }
    };

    /**
    *
    * @param {int} [stageNumber] Optional. If present, returns all stages up to and including stageNumber. If missing, returns all stages.
    * @returns {ReaderStage[]} An array of ReaderStage objects
    */
    SynphonyApi.prototype.getStages = function (stageNumber) {
        if (typeof stageNumber === 'undefined')
            return this.stages;
        else
            return _.first(this.stages, stageNumber);
    };

    SynphonyApi.prototype.getLevels = function () {
        return this.levels;
    };

    SynphonyApi.prototype.addLevel = function (aLevel) {
        this.levels.push(aLevel);
    };
    return SynphonyApi;
})();
//# sourceMappingURL=synphonyApi.js.map
