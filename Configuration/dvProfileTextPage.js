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
                for (var h = 0, len = config.DVProfileItems.Count; h < len; h++) {
                    var innerText = ``
                    var currMovieGroup = config.DVProfileItems.Movies[h];

                    currMovieGroup.Movies.forEach((v) => {
                        innerText += makeTable(v, config.ServerId);
                    });

                    var currHtml = `<h2 id = "` + currMovieGroup.Title + `Title">` + currMovieGroup.Title + `</h2><div><table id="` + currMovieGroup.Title + `">` + innerText + `</table></div>`;
                    view.querySelector("#pagestart").innerHTML += currHtml;
                }

                Dashboard.hideLoadingMsg();
            });
        };

        function makeTable(movie, serverId) {
            var html = `<tr>`;
            html += `<td><a is="emby-linkbutton" href="/item?id=` + movie.Id + `&serverId=` + serverId + `">` + movie.Name + `</a></td>`;
            html += `<td>` + movie.Year + `</td>`;
            return html + `</tr>`;
        }

        Object.assign(View.prototype, BaseView.prototype);

        View.prototype.onResume = function (options) {
            BaseView.prototype.onResume.apply(this, arguments);
        };

        return View;
    });