define([`baseView`, `emby-button`, `emby-select`],
    function (BaseView) {
        `use strict`;

        const pluginId = "291d866f-baad-464a-aed6-a4a8b95a8fd7";
        var dynamicbuttons = [];

        function showInfo(text, title) {
            Dashboard.alert({ message: text, title: title });
        };

        function View(view, params) {
            BaseView.apply(this, arguments);

            view.querySelector("#selectUser").addEventListener(`change`, function () {
                const user = this.options[this.selectedIndex].text;
                loadStats(view, user);
            });

            ApiClient.getUsers().then(function (users) {
                var select = view.querySelector(`#selectUser`);

                loadStats(view, users[0].Name);                

                users.forEach((user) => {
                    var option = document.createElement(`option`);
                    option.value = user.Id;
                    option.innerHTML = user.Name;
                    select.appendChild(option);
                });
            });
            console.log("LOADED");
        };

        function loadStats(view, user) {
            Dashboard.showLoadingMsg();
            ApiClient.getPluginConfiguration(pluginId).then(function (config) {
                view.querySelector("#UserTitle").innerHTML = "User statistics for " + user;
                view.querySelector("#overallStat").innerHTML = "";
                view.querySelector("#movieStat").innerHTML = "";
                view.querySelector("#showStat").innerHTML = "";
                var userStat = config.UserStats.find(v => v.UserName === user);

                userStat.OverallStats.forEach((v) => { createStatDiv(v, "#overallStat", view); });
                userStat.MovieStats.forEach((v) => { createStatDiv(v, "#movieStat", view); });
                userStat.ShowStats.forEach((v) => { createStatDiv(v, "#showStat", view); });

                dynamicbuttons.forEach((v) => {
                    view.querySelector(`#` + v.id).addEventListener("click",
                        function () {
                            showInfo(v.info, v.title);
                        });
                });

                Dashboard.hideLoadingMsg();
            });
        };

        function createStatDiv(v, div, view) {
            var html = '<div class="col ' + v.Size + '"><div class="statCard"><div class="statCard-content">';

            if (v.ExtraInformation !== undefined) {
                var id = v.Title.replace(/\s/g, '');
                html += '<div id="' + id + '" class="infoBlock"><i class="md-icon">info_outline</i></div>';
                dynamicbuttons.push({ id: id, info: v.ExtraInformation, title: v.Title });
            }
            html += '<div class="statCard-stats-title">' + v.Title + '</div><div class="statCard-stats-number">' + v.ValueLineOne + '</div></div></div></div>';
            view.querySelector(div).innerHTML += html;
        };
        
        Object.assign(View.prototype, BaseView.prototype);

        View.prototype.onResume = function (options) {
            BaseView.prototype.onResume.apply(this, arguments);
        };

        return View;
    });