define([`baseView`, `emby-button`, `emby-select`],
    function (BaseView) {
        `use strict`;

        const pluginId = `291d866f-baad-464a-aed6-a4a8b95a8fd7`;

        function showInfo(text, title) {
            Dashboard.alert({ message: text, title: title });
        }

        function stupidTable(element) {
            // Attach click event to the span inside the th elements
            element.addEventListener(`click`, function (event) {
                if (event.target.tagName === `SPAN`) {
                    stupidSort(event.target.parentElement);
                }
            });

            return element;
        }
        
        function stupidSort(element, customSortFn) {
            var table = element.closest(`table`);
            var columnIndex = Array.from(element.parentElement.children).indexOf(element);

            var sortColumn = element.dataset.sort || null;

            if (sortColumn !== null) {
                var cumulativeColspan = 0;
                element.parentElement.querySelectorAll(`th`).forEach(function (th, index) {
                    if (index < columnIndex) {
                        var colspan = parseInt(th.getAttribute(`colspan`)) || 1;
                        cumulativeColspan += colspan;
                    }
                });

                var sortDirection;
                if (arguments.length === 2) {
                    sortDirection = customSortFn;
                } else {
                    sortDirection =
                        customSortFn ||
                        element.dataset.sortDefault ||
                        stupidTable.dir.ASC;

                    if (element.dataset.sortDir && element.dataset.sortDir === stupidTable.dir.ASC) {
                        sortDirection = stupidTable.dir.DESC;
                    }
                }

                if (element.dataset.sortDir !== sortDirection) {
                    element.dataset.sortDir = sortDirection;
                    var beforeTableSortEvent = new CustomEvent(`beforetablesort`, {
                        detail: { column: cumulativeColspan, direction: sortDirection },
                    });
                    table.dispatchEvent(beforeTableSortEvent);
                    table.style.display;

                    setTimeout(function () {
                        var rows = [];
                        var sortFn = stupidTable.defaultSortFns[sortColumn];
                        var tbodyRows = table.tBodies[0].querySelectorAll(`tr`);

                        tbodyRows.forEach(function (row, index) {
                            var cell = row.children[cumulativeColspan];
                            var sortValue = cell.dataset.sortValue || cell.textContent;
                            rows.push([sortValue, row]);
                        });

                        rows.sort(function (a, b) {
                            return sortFn(a[1].childNodes[columnIndex].innerHTML, b[1].childNodes[columnIndex].innerHTML);
                        });

                        if (sortDirection !== stupidTable.dir.ASC) {
                            rows.reverse();
                        }

                        var sortedRows = rows.map(function (row) {
                            return row[1];
                        });

                        table.tBodies[0].append(...sortedRows);
                        
                        table.querySelectorAll(`th`).forEach(function (th) {
                            th.dataset.sortDir = null;
                            th.classList.remove(`sorting-desc`, `sorting-asc`);
                        });

                        element.dataset.sortDir = sortDirection;
                        element.classList.add(`sorting-` + sortDirection);

                        var afterTableSortEvent = new CustomEvent(`aftertablesort`, {
                            detail: { column: cumulativeColspan, direction: sortDirection },
                        });
                        table.dispatchEvent(afterTableSortEvent);
                        table.style.display;
                    }, 10);

                    return element;
                }
            }
        }

        //function updateSortVal(element, value) {
        //    if (element.hasAttribute(`data-sort-value`)) {
        //        element.setAttribute(`data-sort-value`, value);
        //    }
        //    element.dataset.sortValue = value;
        //    return element;
        //}

        stupidTable.dir = { ASC: `asc`, DESC: `desc` };

        stupidTable.defaultSortFns = {
            int: function (a, b) {
                return parseInt(a, 10) - parseInt(b, 10);
            },
            float: function (a, b) {
                return parseFloat(a) - parseFloat(b);
            },
            string: function (a, b) {
                return a.toString().localeCompare(b.toString());
            },
            "string-ins": function (a, b) {
                a = a.toString().toLocaleLowerCase();
                b = b.toString().toLocaleLowerCase();
                return a.localeCompare(b);
            },
        };

        function loadStats(view, user) {
            Dashboard.showLoadingMsg();
            ApiClient.getPluginConfiguration(pluginId).then(function (config) {
                var tbl = view.querySelector(`#ShowsTable > tbody`);
                var userStat = config.UserStats.find(v => v.UserName === user);

                for (var i = 1; i < tbl.rows.length;) {
                    tbl.deleteRow(i);
                }
                                
                userStat.ShowProgresses.forEach((v) => {
                    var index = 0;
                    var newRow = tbl.insertRow(-1);
                    var newCell = newRow.insertCell(index++);
                    var newText = document.createTextNode(v.Name);
                    newCell.setAttribute("data-sort-value", v.SortName);
                    newCell.appendChild(newText);

                    newCell = newRow.insertCell(index++);
                    newCell.className = (`center`);
                    newText = document.createTextNode(v.StartYear);
                    newCell.setAttribute("data-sort-value", v.Watched);
                    newCell.appendChild(newText);

                    newCell = newRow.insertCell(index++);
                    newCell.className = (`center ` + calculateProgressClass(v.Watched));
                    newText = document.createTextNode(v.SeenEpisodes + ` / ` + v.Episodes + ` (` + v.Watched + ` %)` + (v.SeenSpecials > 0 ? ` +` + v.SeenSpecials + ` sp` : ``));
                    newCell.setAttribute("data-sort-value", v.Watched);
                    newCell.appendChild(newText);

                    newCell = newRow.insertCell(index++);
                    newCell.className = (`center ` + calculateProgressClass(v.Collected));
                    newText = document.createTextNode(v.Episodes + ` / ` + v.Total + ` (` + v.Collected + `%)` + (v.SeenSpecials > 0 ? ` +` + v.SeenSpecials + ` sp` : ``));
                    newCell.setAttribute("data-sort-value", v.Collected);
                    newCell.appendChild(newText);

                    newCell = newRow.insertCell(index++);
                    newCell.className = (`center`);
                    newText = document.createTextNode(v.Score);
                    newCell.appendChild(newText);

                    newCell = newRow.insertCell(index++);
                    newCell.className = (`center`);
                    newText = document.createTextNode(v.Status);
                    newCell.appendChild(newText);
                });

                Dashboard.hideLoadingMsg();
            });
        };
        function calculateProgressClass(value) {
            if (value == 0)
                return ``;
            else if (value < 40)
                return `progress-20`;
            else if (value < 60)
                return `progress-40`;
            else if (value < 80)
                return `progress-60`;
            else if (value < 100)
                return `progress-80`;
            else
                return `progress-100`;
        };

        function View(view, params) {
            BaseView.apply(this, arguments);

            var table = stupidTable(view.querySelector(`#ShowsTable`));

            view.querySelector(`#ShowsTable`).addEventListener(`aftertablesort`, function (event, data) {
                var th = view.querySelector(`#ShowsTable`).getElementsByTagName(`th`);
                for (var i = 0; i < th.length; i++) {
                    th[i].classList.remove(`selectLabelFocused`);
                };
                th[event.detail.column].classList.add(`selectLabelFocused`);
            });

            stupidSort(view.querySelector(`#defaultColumn span`).parentElement, `asc`);

            view.querySelector(`#selectUserShowProgress`).addEventListener(`change`, function () {
                const user = this.options[this.selectedIndex].text;
                loadStats(view, user);
                stupidSort(view.querySelector(`#defaultColumn span`).parentElement, `asc`);
            });

            ApiClient.getUsers().then(function (users) {
                
                loadStats(view, users[0].Name);

                var select = view.querySelector(`#selectUserShowProgress`);
                users.forEach(function (user) {
                    var option = document.createElement(`option`);
                    option.value = user.Id;
                    option.innerHTML = user.Name;
                    select.appendChild(option);
                });
            });

            view.querySelector("#watchedInfo").addEventListener(`click`, function () {
                showInfo('This column displays the number of watched episodes and the number of collected episodes. You will have 100% when you viewed all normal episodes (no specials, only aired)<br/><br/>If any special episodes are watched it will be displayed as \'+1 sp\'. ', 'Watched episodes');
            });
            view.querySelector("#collectedInfo").addEventListener(`click`, function () {
                showInfo('This column displays the number of collected episodes and the number of episodes aired on THETVDB. You will have 100% when you collected all normal episodes (no specials, only aired)<br/><br/>If any special episodes are collected it will be displayed as \'+1 sp\'. ', 'Collected episodes');
            });
        };

        Object.assign(View.prototype, BaseView.prototype);

        View.prototype.onResume = function (options) {
            BaseView.prototype.onResume.apply(this, arguments);
        };

        return View;
    });