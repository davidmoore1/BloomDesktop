/// <reference path="../../lib/jquery.d.ts" />
/**
* Class to hold information passed between iframes
* @constructor
*/
var interIframeChannel = (function () {
    function interIframeChannel() {
        this.localizationManagerDictionary = {};
        this.readableFileExtensions = ['txt', 'js'];
    }
    /**
    * Opens the application help topic
    * @param topic
    * @returns {boolean} Returns false to prevent navigation if link clicked.
    */
    interIframeChannel.prototype.help = function (topic) {
        this.simpleAjaxNoCallback('/bloom/help', topic);
        return false;
    };

    /**
    * Retrieve data from localhost
    * @param {String} url The URL to request
    * @param {Function} callback Function to call when the ajax request returns
    * @param {String} [dataValue] Passed in the query string under the "data" key
    */
    interIframeChannel.prototype.simpleAjaxGet = function (url, callback, dataValue) {
        var ajaxSettings = { type: 'GET', url: url };
        if (dataValue)
            ajaxSettings['data'] = { data: dataValue };

        $.ajax(ajaxSettings).done(function (data) {
            callback(data);
        });
    };

    /**
    * Retrieve data from localhost
    * @param {String} url The URL to request
    * @param {Function} callback Function to call when the ajax request returns
    * @param {String} [dataValue] Passed in the post under the "data" key
    */
    interIframeChannel.prototype.simpleAjaxPost = function (url, callback, dataValue) {
        var ajaxSettings = { type: 'POST', url: url };
        if (dataValue)
            ajaxSettings['data'] = { data: dataValue };

        $.ajax(ajaxSettings).done(function (data) {
            callback(data);
        });
    };

    /**
    * Sends data to localhost
    * @param {String} url The URL to request
    * @param {String} [dataValue] Passed in the post under the "data" key
    */
    interIframeChannel.prototype.simpleAjaxNoCallback = function (url, dataValue) {
        var ajaxSettings = { type: 'POST', url: url };
        if (dataValue)
            ajaxSettings['data'] = { data: dataValue };

        $.ajax(ajaxSettings);
    };
    return interIframeChannel;
})();
//# sourceMappingURL=interIframeChannel.js.map