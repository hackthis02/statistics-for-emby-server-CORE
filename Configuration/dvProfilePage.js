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
                for (var h = 0, len = config.MovieDVProfileItems.Count; h < len; h++) {
                    var innerText = ``
                    var currMovieGroup = config.MovieDVProfileItems.Movies[h];

                    currMovieGroup.Movies.forEach((v) => {
                        var imageUrl = ApiClient.getImageUrl(v.Id, { type: "Primary", quality: 90 });
                        innerText += `<a is="emby-linkbutton" href="/item?id=` + v.Id + `&serverId=` + config.ServerId + `"><img src="` + imageUrl + `" height="200px" alt="` + v.Name + `" /></a>`;
                    });

                    var currHtml = `<h2 id = "` + currMovieGroup.Title + `Title">` + currMovieGroup.Title + `</h2><div id="` + currMovieGroup.Title + `">` + innerText + `</div>`;
                    view.querySelector("#pagestart").innerHTML += currHtml;
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