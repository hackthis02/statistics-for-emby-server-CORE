define([`baseView`, `emby-button`, `emby-select`],
    function (BaseView) {
        `use strict`;

        const pluginId = "291d866f-baad-464a-aed6-a4a8b95a8fd7";

        function View(view, params) {
            BaseView.apply(this, arguments);
            loadStats(view);
        };

        function loadStats(view) {
            Dashboard.showLoadingMsg();

            ApiClient.getPluginConfiguration(pluginId).then(function (config) {
                for (var h = 0, len = config.MovieQualityItems.Count; h < len; h++) {
                    var innerText = ``
                    config.MovieQualityItems.Movies[h].Movies.forEach((v) => {
                        innerText += `<a is="emby-linkbutton" href="/item?id=` + v.Id + `&serverId=` + config.ServerId + `"><img src="/Items/` + v.Id + `/Images/Primary" height="200px" alt="` + v.Name + `" /></a>`;
                    });

                    view.querySelector("#pagestart").innerHTML += (`<h2 id = "` + config.MovieQualityItems.Movies[h].Title + `Title">` + config.MovieQualityItems.Movies[h].Title + `</h2><div id="` + config.MovieQualityItems.Movies[h].Title + `">` + innerText + `</div>`);
                }

                Dashboard.hideLoadingMsg();
            });
        };

        Object.assign(View.prototype, BaseView.prototype);

        View.prototype.onResume = function (options) {
            BaseView.prototype.onResume.apply(this, arguments);
        };

        return View;
    });