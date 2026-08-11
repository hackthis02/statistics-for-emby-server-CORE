define([`baseView`, `emby-button`, `emby-select`],
    function (BaseView) {
        `use strict`;

        var dynamicbuttons = [];


        const pluginId = "291d866f-baad-464a-aed6-a4a8b95a8fd7";

        function showInfo(text, title) {
            Dashboard.alert({ message: text, title: title });
        }
        function createStat(valueGroup, view, serverId = undefined) {
            if (!valueGroup || !view)
                return "";

            var html = "<div class=\"col " + valueGroup.Size + "\">";
            html += "<div class=\"statCard\">";
            html += "<div class=\"statCard-content\">";

            if (valueGroup.ExtraInformation !== undefined) {
                var id = valueGroup.Title.replace(/\s/g, "");
                html += "<div id=\"" + id + "\" class=\"infoBlock\"><i class=\"md-icon\">info</i></div>";

                dynamicbuttons.push({ id: id, info: valueGroup.ExtraInformation, title: valueGroup.Title });
            }

            var showImage = (serverId !== undefined) && (valueGroup.Id !== undefined);
            if (showImage) {
                var imageUrl = ApiClient.getImageUrl(valueGroup.Id, { type: "Primary", quality: 90 });
                html += "<a is=\"emby-linkbutton\" href=\"/item?id=" + valueGroup.Id + "&serverId=" + serverId + "\"><img src=\"" + imageUrl + "\" height=\"105px\"/></a>";
                html += "<div>";

                if (valueGroup.Title !== undefined) {
                    html += "<div class=\"statCard-stats-title-left\">" + valueGroup.Title + "</div>";
                }
            }
            else if (valueGroup.Title !== undefined) {
                html += "<div style=\"width: 100%;\">";
                html += "<div class=\"statCard-stats-title\">" + valueGroup.Title + "</div>";
            }
            if (valueGroup.ValueLineOne !== undefined) {
                html += "<div class=\"statCard-stats-number\">" + valueGroup.ValueLineOne + "</div>"
            }

            if (valueGroup.ValueLineTwo !== undefined) {
                html += "<div class=\"statCard-stats-number\">" + valueGroup.ValueLineTwo + "</div>"
            }

            if (valueGroup.ValueLineThree !== undefined) {
                html += "<div class=\"statCard-stats-number\">" + valueGroup.ValueLineThree + "</div>"
            }

            html += "</div>";
            html += "</div>";
            html += "</div>";
            html += "</div>";
            html += "</div>"; // not sure why this is here, but it was in the original code.

            return html;
        };

        function loadStats(view) {
            Dashboard.showLoadingMsg();

            ApiClient.getPluginConfiguration(pluginId).then(function (config) {

                view.querySelector("#trackDolbyVisionProfiles").checked = config.enableTrackDolbyVisionProfiles;
                view.querySelector("#showHyperLinks").checked = config.showHyperLinks;

                view.querySelector("#lastRunInfo").innerHTML = "Last statistics finished at <b> " + config.LastUpdated + "</b> - Version <b>" + config.Version + "</b> - Build Date - <b>" + config.BuildDate + "</b>";
                if (config.LastUpdated === undefined) {
                    Dashboard.alert({
                        message:
                            "No configuration found, please run the statistics task on the Scheduled Tasks page and come back for the results."
                    });
                    view.querySelector("#GoToUserStats", page).css("display", "none");
                    view.querySelector("#GoToShowProgress", page).css("display", "none");
                    Dashboard.hideLoadingMsg();
                } else {
                    view.querySelector("#statsIntro").innerHTML = "This plugin will calculate overall and user-based statistics"
                        + "from this Emby server instance. Keep in mind that viewing an item multiple times will not increase the"
                        + "watched count. It will only count as 1.";

                    var userStats = createStat(config.MostActiveUsers, view);
                    userStats += createStat(config.TotalUsers, view);

                    view.querySelector("#userStats").innerHTML = (userStats);

                    var generalStat = "";
                    generalStat += createStat(config.MovieQualities, view);
                    generalStat += createStat(config.MovieCodecs, view);

                    if (config.enableTrackDolbyVisionProfiles) {
                        generalStat += createStat(config.MovieDVProfiles, view);
                    }

                    view.querySelector("#generalStat").innerHTML = (generalStat);

                    var movieStat = "";

                    movieStat += createStat(config.TotalMovies, view);
                    movieStat += createStat(config.TotalBoxsets, view);
                    movieStat += createStat(config.TotalMovieStudios, view);
                    movieStat += createStat(config.BiggestMovie, view, config.ServerId);
                    movieStat += createStat(config.LongestMovie, view, config.ServerId);
                    movieStat += createStat(config.NewestAddedMovie, view, config.ServerId);
                    movieStat += createStat(config.OldestMovie, view, config.ServerId);
                    movieStat += createStat(config.NewestMovie, view, config.ServerId);
                    movieStat += createStat(config.HighestRating, view, config.ServerId);
                    movieStat += createStat(config.LowestRating, view, config.ServerId);
                    movieStat += createStat(config.HighestBitrateMovie, view, config.ServerId);
                    movieStat += createStat(config.LowestBitrateMovie, view, config.ServerId);

                    view.querySelector("#movieStat").innerHTML = (movieStat);

                    var showStat = "";

                    showStat += createStat(config.TotalShows, view);
                    showStat += createStat(config.TotalShowStudios, view);
                    showStat += createStat(config.LeastWatchedShows, view);
                    showStat += createStat(config.MostWatchedShows, view);
                    showStat += createStat(config.BiggestShow, view, config.ServerId);
                    showStat += createStat(config.LongestShow, view, config.ServerId);
                    showStat += createStat(config.OldestShow, view, config.ServerId);
                    showStat += createStat(config.NewestShow, view, config.ServerId);
                    showStat += createStat(config.NewestAddedEpisode, view, config.ServerId);

                    view.querySelector("#showStat").innerHTML = (showStat);

                    Dashboard.hideLoadingMsg();

                    dynamicbuttons.forEach((v) => {
                        view.querySelector("#" + v.id).addEventListener("click",
                            function () {
                                showInfo(v.info, v.title);
                            });
                    });
                }
            });
        }

        function View(view, params) {
            BaseView.apply(this, arguments);
            dynamicbuttons = [];
            loadStats(view);

            view.querySelector("#GoToUserStats").addEventListener("click",
                function () {
                    var href = Dashboard.getConfigurationPageUrl("StatisticsUserBased");
                    Dashboard.navigate(href);
                });

            view.querySelector("#GoToMovieList").addEventListener("click",
                function () {
                    var href = Dashboard.getConfigurationPageUrl("StatisticsMovieList");
                    Dashboard.navigate(href);
                });

            view.querySelector("#GoToMovieTextList").addEventListener("click",
                function () {
                    var href = Dashboard.getConfigurationPageUrl("StatisticsMovieListText");
                    Dashboard.navigate(href);
                });

            view.querySelector("#GoToShowProgress").addEventListener("click",
                function () {
                    Dashboard.navigate(Dashboard.getConfigurationPageUrl("StatisticsShowOverview"));
                });

            view.querySelector("#trackDolbyVisionProfiles").addEventListener("click",
                function () {
                    ApiClient.getPluginConfiguration(pluginId).then(function (config) {
                        config.enableTrackDolbyVisionProfiles = view.querySelector("#trackDolbyVisionProfiles").checked;
                        ApiClient.updatePluginConfiguration(pluginId, config);
                        window.location.reload();
                    });
                }
            );

            view.querySelector("#showHyperLinks").addEventListener("click",
                function () {
                    ApiClient.getPluginConfiguration(pluginId).then(function (config) {
                        config.enableHyperlinks = view.querySelector("#showHyperLinks").checked;
                        ApiClient.updatePluginConfiguration(pluginId, config);
                    });
                }
            );
        }


        Object.assign(View.prototype, BaseView.prototype);

        View.prototype.onResume = function (options) {

            BaseView.prototype.onResume.apply(this, arguments);
        }

        return View;
    });