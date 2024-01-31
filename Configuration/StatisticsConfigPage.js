define([`baseView`, `emby-button`, `emby-select`],
    function (BaseView) {
        `use strict`;

        var dynamicbuttons = [];


        const pluginId = "291d866f-baad-464a-aed6-a4a8b95a8fd7";

        function showInfo(text, title) {
            Dashboard.alert({ message: text, title: title });
        }

        function createStat(v, view) {
            var html = `<div class="col ` +
                v.Size +
                `"><div class="statCard"><div class="statCard-content">`;

            if (v.ExtraInformation !== undefined) {
                var id = v.Title.replace(/\s/g, '');
                html += `<div id="` + id + `" class=\"infoBlock\"><i class=\"md-icon\">info_outline</i></div>`;

                dynamicbuttons.push({ id: id, info: v.ExtraInformation, title: v.Title });
            }
            html += `<div style="width: 100%;"><div class="statCard-stats-title">` +
                v.Title +
                `</div><div class="statCard-stats-number">` +
                v.ValueLineOne +
                `</div><div class="statCard-stats-number">` +
                v.ValueLineTwo
            if (v.ValueLineThree !== undefined)
                html += `</div><div class="statCard-stats-number">` +
                    v.ValueLineThree + `</div>`
            html += `</div></div></div></div></div>`;

            return html;
        };

        function createStatWithPoster(v, i, view) {
            if (v) {
                var html = `<div class="col ` +
                    v.Size +
                    `"><div class="statCard"><div class="statCard-content">`;

                if (v.ExtraInformation !== undefined) {
                    var id = v.Title.replace(/\s/g, "");
                    html += `<div id="` + id + `" class=\"infoBlock\"><i class=\"md-icon\">info_outline</i></div>`;

                    dynamicbuttons.push({ id: id, info: v.ExtraInformation, title: v.Title });
                }
                if (v.Id !== undefined) {
                    html += `<a is="emby-linkbutton" href="/item?id=` + v.Id + `&serverId=` + i + `"><img src="/Items/` + v.Id + `/Images/Primary" height="105px"></a>`
                    html += `<div>`
                }

                html += `<div class="statCard-stats-title-left">` +
                    v.Title +
                    `</div><div class="statCard-stats-number">` +
                    v.ValueLineOne +
                    `</div><div class="statCard-stats-number">` +
                    v.ValueLineTwo +
                    `</div></div></div></div></div>`;

                if (v.Id !== undefined) {
                    html += `</div>`;
                }

                return html;
            } else {
                return "";
            }
        };

        function loadStats(view) {
            Dashboard.showLoadingMsg();

            ApiClient.getPluginConfiguration(pluginId).then(function (config) {
                
                if (config.LastUpdated === undefined) {
                    Dashboard.alert({
                        message:
                            `No configuration found, please run the statistics task on the Scheduled Tasks page and come back for the results.`
                    });
                    view.querySelector(`#GoToUserStats`, page).css("display", "none");
                    view.querySelector(`#GoToShowProgress`, page).css("display", "none");
                    Dashboard.hideLoadingMsg();
                } else {
                    view.querySelector(`#statsIntro`).innerHTML = (`This plugin will calculate overall and user-based statistics 
                    from this Emby server instance. Keep in mind that viewing an item multiple times will not increase the
                    "watched" count. It will only count as 1. Last statistics finished at <b>` + config.LastUpdated + `</b>`);

                    var generalStat = ``;

                    generalStat += createStat(config.MovieQualities, view);
                    generalStat += createStat(config.MovieCodecs, view);
                    generalStat += createStat(config.MostActiveUsers, view);
                    generalStat += createStat(config.TotalUsers, view);

                    view.querySelector(`#generalStat`).innerHTML = (generalStat);

                    var movieStat = ``;

                    movieStat += createStat(config.TotalMovies, view);
                    movieStat += createStat(config.TotalBoxsets, view);
                    movieStat += createStat(config.TotalMovieStudios, view);
                    movieStat += createStatWithPoster(config.BiggestMovie, config.ServerId, view);
                    movieStat += createStatWithPoster(config.LongestMovie, config.ServerId, view);
                    movieStat += createStatWithPoster(config.NewestAddedMovie, config.ServerId, view);
                    movieStat += createStatWithPoster(config.OldestMovie, config.ServerId, view);
                    movieStat += createStatWithPoster(config.NewestMovie, config.ServerId, view);
                    movieStat += createStatWithPoster(config.HighestRating, config.ServerId, view);
                    movieStat += createStatWithPoster(config.LowestRating, config.ServerId, view);
                    movieStat += createStatWithPoster(config.HighestBitrateMovie, config.ServerId, view);
                    movieStat += createStatWithPoster(config.LowestBitrateMovie, config.ServerId, view);

                    view.querySelector(`#movieStat`).innerHTML = (movieStat);

                    var showStat = ``;

                    showStat += createStat(config.TotalShows, view);
                    showStat += createStat(config.TotalShowStudios, view);
                    showStat += createStat(config.LeastWatchedShows, view);
                    showStat += createStat(config.MostWatchedShows, view);
                    showStat += createStatWithPoster(config.BiggestShow, config.ServerId, view);
                    showStat += createStatWithPoster(config.LongestShow, config.ServerId, view);
                    showStat += createStatWithPoster(config.OldestShow, config.ServerId, view);
                    showStat += createStatWithPoster(config.NewestShow, config.ServerId, view);
                    showStat += createStatWithPoster(config.NewestAddedEpisode, config.ServerId, view);

                    view.querySelector(`#showStat`).innerHTML = (showStat);
                    
                    Dashboard.hideLoadingMsg();

                    dynamicbuttons.forEach((v) => {
                        view.querySelector(`#` + v.id).addEventListener("click",
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

            view.querySelector(`#GoToUserStats`).addEventListener(`click`,
                function () {
                    var href = Dashboard.getConfigurationPageUrl("StatisticsUserBased");
                    Dashboard.navigate(href);
                });

            view.querySelector(`#GoToMovieList`).addEventListener(`click`,
                function () {
                    var href = Dashboard.getConfigurationPageUrl("StatisticsMovieList");
                    Dashboard.navigate(href);
                });

            view.querySelector(`#GoToMovieTextList`).addEventListener(`click`,
                function () {
                    var href = Dashboard.getConfigurationPageUrl("StatisticsMovieListText");
                    Dashboard.navigate(href);
                });

            view.querySelector(`#GoToShowProgress`).addEventListener(`click`,
                function () {
                    Dashboard.navigate(Dashboard.getConfigurationPageUrl("StatisticsShowOverview"));
                });
        }


        Object.assign(View.prototype, BaseView.prototype);

        View.prototype.onResume = function (options) {

            BaseView.prototype.onResume.apply(this, arguments);
        }

        return View;
    });