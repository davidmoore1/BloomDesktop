//
// Typescript configuration file for Bloom Wall Calendar
//
// Creates calendar pages for a Bloom book.
//
//   This script is fed a configuration object with elements
//       configuration.calendar.year
//
//       These ones are in the "library" zone because they will be reused in future years/other calendar types
//       configuration.library.calendar.monthNames[0..11]
//       configuration.library.calendar.dayAbbreviations[0..6]
//
//   This script relies on the 2 pages that should be in the DOM it operates on:
//       One with classes 'calendarMonthTop'
//       One with classes 'calendarMonthBottom'
//
// <reference path="jquery.d.ts" />
// Test class
var TestCalendar = (function () {
    function TestCalendar() {
        // test config is in French, just for fun
        this.testConfig = '{"calendar": { "year": "2015" },' + '"library":  {"calendar": {' + '"monthNames": ["janv", "fév", "mars", "avr", "mai", "juin", "juil", "aôut", "sept", "oct", "nov", "déc"],' + '"dayAbbreviations": ["Dim", "Lun", "Mar", "Mer", "Jeu", "Ven", "Sam"]}}}';
        var configurator = new CalendarConfigurator(this.testConfig);
        configurator.updateDom();
    }
    return TestCalendar;
})();

function runUpdate(config) {
    var configurator = new CalendarConfigurator(config);
    configurator.updateDom();
}

// Used to create a calendar in a Wall Calendar book
var CalendarConfigurator = (function () {
    function CalendarConfigurator(configString) {
        if (configString && configString.length > 0)
            this.configObject = new CalendarConfigObject(configString);
        else
            this.configObject = new CalendarConfigObject(); // shouldn't happen, even in test...
    }
    //
    // Updates the dom to reflect the given configuration settings
    // Called directly by Bloom, in a context where the current dom is the book.
    // @param {configuration} members come from the name attributes of the corresponding configuration.htm file.
    //       Put a new input control in that file, give it a @name attribute, and the value will be available here.
    //
    CalendarConfigurator.prototype.updateDom = function () {
        // TODO: As is, the following line is not localizable. Also the class doesn't exist anywhere.
        // $('.vernacularBookTitle').set("text", configuration.year +" Calendar");
        var year = this.configObject.year;
        var previous = $('.calendarMonthTop')[0];
        var originalMonthsPicturePage = $('.calendarMonthTop')[0];
        for (var month = 0; month < 12; month++) {
            var monthsPicturePage = $(originalMonthsPicturePage).clone();
            $(monthsPicturePage).removeClass('templateOnly').removeAttr('id'); // don't want to copy a Guid!

            $(previous).after(monthsPicturePage);

            var monthDaysPage = this.generateMonth(year, month, this.configObject.monthNames[month], this.configObject.dayAbbreviations);
            $(monthsPicturePage).after(monthDaysPage);
            previous = monthDaysPage;
        }
        $('.templateOnly').remove();
    };

    CalendarConfigurator.prototype.generateMonth = function (year, month, monthName, dayAbbreviations) {
        var marginBox = document.createElement('div');
        $(marginBox).addClass('marginBox');

        var monthPage = document.createElement('div');
        $(monthPage).addClass('bloom-page bloom-required A5Landscape calendarMonthBottom');
        $(monthPage).attr('data-page', 'required');
        this.initialize(marginBox);
        this.draw(year, month, monthName, dayAbbreviations);
        monthPage.appendChild(marginBox);
        return monthPage;
    };

    CalendarConfigurator.prototype.initialize = function (wrapper) {
        if (typeof wrapper !== "undefined" && wrapper != null)
            this.wrapper = wrapper;
    };

    CalendarConfigurator.prototype.draw = function (year, month, monthName, dayAbbreviations) {
        var header = document.createElement('p');
        $(header).addClass('calendarBottomPageHeader');
        $(header).text(monthName + " " + year);
        $(this.wrapper).append(header);
        this.table = document.createElement('table');
        this.drawHeader(dayAbbreviations);
        this.drawBody(year, month);
        $(this.wrapper).append(this.table);
        //TODO we'll see if we need this later...
        //        var style:HTMLStyleElement = document.createElement('style');
        //        style.type = 'text/css';
        //        style.innerHTML = '.book-font { font-family: ' + params[1] + '; }';
        //        document.getElementsByTagName('head')[0].appendChild(style);
    };

    CalendarConfigurator.prototype.drawHeader = function (dayAbbreviations) {
        var thead = document.createElement('thead');
        var row = document.createElement('tr');
        dayAbbreviations.forEach(function (abbr) {
            var thElem = document.createElement('th');
            $(thElem).text(abbr);
            $(row).append(thElem);
        });
        $(thead).append(row);
        $(this.table).append(thead);
    };

    CalendarConfigurator.prototype.drawBody = function (year, month) {
        var body = document.createElement('tbody');
        var start = new Date(parseInt(year), month, 1);
        start.setDate(1 - start.getDay());
        do {
            start = this.drawWeek(body, start, month);
        } while(start.getMonth() == month);
        $(this.table).append(body);
    };

    CalendarConfigurator.prototype.drawWeek = function (body, date, month) {
        var row = document.createElement('tr');
        for (var i = 0; i < 7; i++) {
            var dayCell = document.createElement('td');
            if (date.getMonth() == month) {
                var dayNumberElement = document.createElement('p');
                $(dayNumberElement).text(date.getDate());
                $(dayCell).append(dayNumberElement);
                var holidayText = document.createElement('textarea');
                $(dayCell).append(holidayText);
            }
            $(row).append(dayCell);
            date.setDate(date.getDate() + 1);
        }
        $(body).append(row);
        return date;
    };
    return CalendarConfigurator;
})();

var CalendarConfigObject = (function () {
    function CalendarConfigObject(jsonString) {
        this.defaultConfig = '{"calendar": { "year": "2015" },' + '"library":  {"calendar": {' + '"monthNames": ["jan", "feb", "mar", "apr", "may", "jun", "jul", "aug", "sep", "oct", "nov", "dec"],' + '"dayAbbreviations": ["Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"]}}}';
        if (typeof jsonString === "undefined" || jsonString.length == 0)
            jsonString = this.defaultConfig;
        var object = $.parseJSON(jsonString);
        this.year = object.calendar.year;
        this.monthNames = object.library.calendar.monthNames;
        this.dayAbbreviations = object.library.calendar.dayAbbreviations;
    }
    return CalendarConfigObject;
})();
//# sourceMappingURL=configure.js.map
