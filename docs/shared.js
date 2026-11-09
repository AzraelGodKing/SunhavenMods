/* ===================================================================
   Sun Haven Mod Hub - Shared JavaScript
   =================================================================== */

// Dark mode: apply stored theme immediately to prevent flash
(function() {
    var stored = localStorage.getItem('sh-mod-theme');
    if (stored) {
        document.documentElement.setAttribute('data-theme', stored);
    }
})();

document.addEventListener('DOMContentLoaded', function() {

    function hubDocsBase() {
        var el = document.querySelector('script[src*="shared.js"]');
        if (!el) return '';
        try {
            return new URL(el.getAttribute('src'), document.baseURI).href.replace(/[^/]+$/, '');
        } catch (err) {
            return '';
        }
    }

    // -----------------------------------------------------------------
    // 1. BACK-TO-TOP BUTTON
    // -----------------------------------------------------------------
    var topBtn = document.createElement('button');
    topBtn.className = 'back-to-top';
    topBtn.setAttribute('aria-label', 'Back to top');
    topBtn.innerHTML = '\u2191';
    topBtn.title = 'Back to top';
    document.body.appendChild(topBtn);

    window.addEventListener('scroll', function() {
        if (window.scrollY > 300) {
            topBtn.classList.add('visible');
        } else {
            topBtn.classList.remove('visible');
        }
    });

    topBtn.addEventListener('click', function() {
        var instant = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
        window.scrollTo({ top: 0, behavior: instant ? 'auto' : 'smooth' });
    });

    // -----------------------------------------------------------------
    // 2. DARK MODE TOGGLE
    // -----------------------------------------------------------------
    var toggle = document.createElement('button');
    toggle.className = 'theme-toggle';
    toggle.setAttribute('aria-label', 'Toggle dark mode');
    var currentTheme = document.documentElement.getAttribute('data-theme');
    toggle.innerHTML = currentTheme === 'dark' ? '\u2600\uFE0F' : '\uD83C\uDF19';
    document.body.appendChild(toggle);

    toggle.addEventListener('click', function() {
        var current = document.documentElement.getAttribute('data-theme');
        var next = current === 'dark' ? 'light' : 'dark';
        document.documentElement.setAttribute('data-theme', next);
        localStorage.setItem('sh-mod-theme', next);
        toggle.innerHTML = next === 'dark' ? '\u2600\uFE0F' : '\uD83C\uDF19';
    });

    // -----------------------------------------------------------------
    // 3. COPY-TO-CLIPBOARD FOR CONFIG PATHS
    // -----------------------------------------------------------------
    var configCodes = document.querySelectorAll('.config-note code');
    configCodes.forEach(function(codeEl) {
        var wrapper = document.createElement('span');
        wrapper.className = 'copy-wrapper';

        var copyBtn = document.createElement('button');
        copyBtn.className = 'copy-btn';
        copyBtn.innerHTML = '\uD83D\uDCCB';
        copyBtn.title = 'Copy to clipboard';

        codeEl.parentNode.insertBefore(wrapper, codeEl);
        wrapper.appendChild(codeEl);
        wrapper.appendChild(copyBtn);

        copyBtn.addEventListener('click', function() {
            var text = codeEl.textContent;
            if (navigator.clipboard && navigator.clipboard.writeText) {
                navigator.clipboard.writeText(text).then(onCopied);
            } else {
                // Fallback for file:// or older browsers
                var ta = document.createElement('textarea');
                ta.value = text;
                ta.style.position = 'fixed';
                ta.style.opacity = '0';
                document.body.appendChild(ta);
                ta.select();
                document.execCommand('copy');
                document.body.removeChild(ta);
                onCopied();
            }

            function onCopied() {
                copyBtn.innerHTML = '\u2705';
                copyBtn.classList.add('copied');
                setTimeout(function() {
                    copyBtn.innerHTML = '\uD83D\uDCCB';
                    copyBtn.classList.remove('copied');
                }, 2000);
            }
        });
    });

    // -----------------------------------------------------------------
    // 4. SEARCH / FILTER ON INDEX PAGE
    // -----------------------------------------------------------------
    var searchInput = document.getElementById('modSearch');
    if (searchInput) {
        var cards = document.querySelectorAll('.mod-card');
        var tagButtons = document.querySelectorAll('.filter-tag');
        var activeTag = 'all';
        var emptyMsg = document.getElementById('modSearchEmpty');
        var resetBtn = emptyMsg ? emptyMsg.querySelector('.mod-search-reset') : null;
        var boardCountEl = document.getElementById('modBoardCount');

        function syncHubURL() {
            if (!window.history || !window.history.replaceState) return;
            var u = new URL(window.location.href);
            var rawQ = searchInput.value.trim();
            if (rawQ) u.searchParams.set('q', rawQ); else u.searchParams.delete('q');
            if (activeTag && activeTag !== 'all') u.searchParams.set('tag', activeTag);
            else u.searchParams.delete('tag');
            var qs = u.searchParams.toString();
            var search = qs ? '?' + qs : '';
            history.replaceState(null, '', u.pathname + search + (window.location.hash || ''));
        }

        function filterCards() {
            var query = searchInput.value.toLowerCase().trim();
            var visibleCount = 0;
            cards.forEach(function(card) {
                var name = (card.getAttribute('data-name') || '').toLowerCase();
                var desc = card.querySelector('.mod-description');
                var descText = desc ? desc.textContent.toLowerCase() : '';
                var features = card.querySelectorAll('.mod-feature');
                var featureText = Array.from(features).map(function(f) {
                    return f.textContent.toLowerCase();
                }).join(' ');
                var tags = (card.getAttribute('data-tags') || '').split(',');

                var matchesSearch = !query || name.indexOf(query) !== -1 ||
                    descText.indexOf(query) !== -1 || featureText.indexOf(query) !== -1;
                var matchesTag = activeTag === 'all' || tags.indexOf(activeTag) !== -1;

                var show = matchesSearch && matchesTag;
                card.style.display = show ? '' : 'none';
                if (show) visibleCount++;
            });
            if (emptyMsg) {
                if (visibleCount === 0) {
                    emptyMsg.hidden = false;
                } else {
                    emptyMsg.hidden = true;
                }
            }
            if (boardCountEl) {
                var total = cards.length;
                if (visibleCount === total) {
                    boardCountEl.textContent = 'Showing all ' + total + ' postings.';
                } else if (visibleCount === 0) {
                    boardCountEl.textContent = 'No postings match — adjust search or filters.';
                } else {
                    boardCountEl.textContent = 'Showing ' + visibleCount + ' of ' + total + ' postings.';
                }
            }
            syncHubURL();
        }

        function applyHubParamsFromURL() {
            var u = new URL(window.location.href);
            var qParam = u.searchParams.get('q');
            var tagParam = u.searchParams.get('tag');
            if (qParam !== null) searchInput.value = qParam;
            if (tagParam) {
                var matchBtn = null;
                tagButtons.forEach(function(b) {
                    if (b.getAttribute('data-tag') === tagParam) matchBtn = b;
                });
                if (matchBtn) {
                    tagButtons.forEach(function(b) { b.classList.remove('active'); });
                    matchBtn.classList.add('active');
                    activeTag = tagParam;
                }
            }
            filterCards();
        }

        searchInput.addEventListener('input', filterCards);

        searchInput.addEventListener('keydown', function(e) {
            if (e.key === 'Escape') {
                e.preventDefault();
                searchInput.blur();
            }
        });

        tagButtons.forEach(function(btn) {
            btn.addEventListener('click', function() {
                tagButtons.forEach(function(b) { b.classList.remove('active'); });
                btn.classList.add('active');
                activeTag = btn.getAttribute('data-tag');
                filterCards();
            });
        });

        if (resetBtn) {
            resetBtn.addEventListener('click', function() {
                searchInput.value = '';
                activeTag = 'all';
                tagButtons.forEach(function(b) { b.classList.remove('active'); });
                var allBtn = document.querySelector('.filter-tag[data-tag="all"]');
                if (allBtn) allBtn.classList.add('active');
                filterCards();
                searchInput.focus();
            });
        }

        applyHubParamsFromURL();

        document.addEventListener('keydown', function(e) {
            if (e.defaultPrevented) return;
            if (document.body.classList.contains('site-search-open')) return;
            var tag = e.target && e.target.tagName;
            if (tag === 'INPUT' || tag === 'TEXTAREA' || tag === 'SELECT') return;
            if (e.target && e.target.isContentEditable) return;
            if (e.ctrlKey || e.metaKey || e.altKey) return;
            if (e.key === '/' || e.key === 's') {
                e.preventDefault();
                searchInput.focus();
            }
        });
    }

    // -----------------------------------------------------------------
    // Hub jump nav (index — select on narrow screens)
    // -----------------------------------------------------------------
    var hubJump = document.getElementById('hubJumpSelect');
    if (hubJump) {
        hubJump.addEventListener('change', function() {
            var v = hubJump.value;
            if (!v) return;
            location.hash = v;
            hubJump.selectedIndex = 0;
        });
    }

    // -----------------------------------------------------------------
    // 5. DYNAMIC VERSION BADGES (from versions.json)
    // -----------------------------------------------------------------
    (function() {
        // Determine base path for versions.json
        var scripts = document.querySelectorAll('script[src*="shared.js"]');
        var basePath = '';
        if (scripts.length) {
            var src = scripts[0].getAttribute('src');
            basePath = src.replace('shared.js', '');
        }

        Promise.all([
            fetch(basePath + 'mod-matrix.json').then(function(r) { return r.json(); }),
            fetch(basePath + 'versions.json').then(function(r) { return r.json(); })
        ])
            .then(function(responses) {
                var matrix = responses[0];
                var data = responses[1];
                var nameToKey = {};
                var navToKey = {};
                matrix.forEach(function(row) {
                    var key = row.jsonKey;
                    if (!key) return;
                    if (row.indexDataName) nameToKey[row.indexDataName] = key;
                    if (Array.isArray(row.docsCardNames)) {
                        row.docsCardNames.forEach(function(name) { nameToKey[name] = key; });
                    }
                    if (Array.isArray(row.docsNavNames)) {
                        row.docsNavNames.forEach(function(name) { navToKey[name] = key; });
                    }
                });

                // Update index page mod cards
                var cards = document.querySelectorAll('.mod-card[data-name]');
                cards.forEach(function(card) {
                    var name = card.getAttribute('data-name');
                    var key = nameToKey[name];
                    if (key && data[key]) {
                        var ver = data[key].version;
                        var badge = card.querySelector('.mod-version');
                        if (badge) badge.textContent = 'v' + ver;
                        // Update update-badge if present
                        var updateBadge = card.querySelector('.update-badge');
                        if (updateBadge && updateBadge.textContent.indexOf('Updated') !== -1) {
                            updateBadge.textContent = 'Updated v' + ver;
                        }
                    }
                });

                // Update subpage version badge
                var pageBadge = document.querySelector('.hero .version-badge');
                if (pageBadge) {
                    // Find which mod this page is for by checking title or nav
                    var navCurrent = document.querySelector('.nav-current');
                    var pageName = navCurrent ? navCurrent.textContent.trim() : '';
                    var pageKey = navToKey[pageName];
                    if (pageKey && data[pageKey]) {
                        pageBadge.textContent = 'v' + data[pageKey].version;
                    }
                }
            })
            .catch(function() { /* versions.json not available, keep static values */ });
    })();

    // -----------------------------------------------------------------
    // 5b. MOD DOWNLOAD NAV (Thunderstore, Nexus, Direct Download)
    // -----------------------------------------------------------------
    (function() {
        var nav = document.querySelector('.download-nav[data-mod-key]');
        if (!nav) return;

        var modKey = nav.getAttribute('data-mod-key');
        var baseUrl = '';
        var scriptEl = document.querySelector('script[src*="shared.js"]');
        if (scriptEl) {
            var src = scriptEl.getAttribute('src');
            baseUrl = new URL(src, document.baseURI).href.replace(/[^/]+$/, '');
        }

        fetch(baseUrl + 'versions.json')
            .then(function(r) { return r.json(); })
            .then(function(data) {
                var m = data[modKey];
                if (!m) return;

                var links = [];
                var ghBase = 'https://github.com/AzraelGodKing/SunhavenMods/releases';

                if (m.thunderstore) {
                    links.push({
                        href: m.thunderstore,
                        cls: 'download-thunderstore',
                        text: 'Thunderstore',
                        icon: '\u26A1',
                        external: true
                    });
                }
                if (m.nexus) {
                    links.push({
                        href: m.nexus,
                        cls: 'download-nexus',
                        text: 'Nexus Mods',
                        icon: '\u{1F4E6}',
                        external: true
                    });
                }
                if (m.thunderstoreName && m.version) {
                    var tag = m.thunderstoreName + '-v' + m.version;
                    var zip = m.thunderstoreName + '-' + m.version + '.zip';
                    links.push({
                        href: ghBase + '/download/' + tag + '/' + zip,
                        cls: 'download-direct',
                        text: 'Download Latest',
                        icon: '\u2B07',
                        external: true
                    });
                }

                if (links.length === 0) {
                    nav.style.display = 'none';
                    return;
                }

                var html = '<span class="download-nav-label">Get this mod</span><div class="download-nav-links">';
                links.forEach(function(l) {
                    html += '<a href="' + l.href + '" class="' + l.cls + '"' +
                        (l.external ? ' target="_blank" rel="noopener noreferrer"' : '') + '>' +
                        '<span aria-hidden="true">' + l.icon + '</span>' + l.text + '</a>';
                });
                html += '</div>';
                nav.innerHTML = html;
            })
            .catch(function() {
                nav.style.display = 'none';
            });
    })();

    // -----------------------------------------------------------------
    // 6. AUTO-GENERATED TABLE OF CONTENTS (long pages)
    // -----------------------------------------------------------------
    (function() {
        var tocContainer = document.querySelector('.toc-sidebar');
        if (!tocContainer) return;

        var headings = document.querySelectorAll('.container h2.section-title');
        if (headings.length < 3) { tocContainer.style.display = 'none'; return; }

        var tocList = document.createElement('ul');
        tocList.className = 'toc-list';
        headings.forEach(function(h, i) {
            var id = h.id || 'section-' + i;
            h.id = id;
            var li = document.createElement('li');
            var a = document.createElement('a');
            a.href = '#' + id;
            a.textContent = h.textContent;
            a.className = 'toc-link';
            li.appendChild(a);
            tocList.appendChild(li);
        });
        tocContainer.appendChild(tocList);

        // Collapsed TOC on narrow viewports (sidebar hidden via CSS)
        var mainContainer = document.querySelector('.container');
        if (mainContainer && tocList.querySelector('li')) {
            var mobileToc = document.createElement('details');
            mobileToc.className = 'toc-mobile';
            mobileToc.open = false;
            var sum = document.createElement('summary');
            sum.className = 'toc-mobile-summary';
            sum.textContent = 'On this page';
            mobileToc.appendChild(sum);
            mobileToc.appendChild(tocList.cloneNode(true));
            mainContainer.insertBefore(mobileToc, mainContainer.firstChild);
        }

        // Scroll-spy for TOC
        var tocLinks = tocContainer.querySelectorAll('.toc-link');
        var lastTocActive = null;
        window.addEventListener('scroll', function() {
            var scrollPos = window.scrollY + 160;
            var activeLink = tocLinks[0];
            headings.forEach(function(h, i) {
                if (h.offsetTop <= scrollPos) activeLink = tocLinks[i];
            });
            tocLinks.forEach(function(l) { l.classList.remove('active'); });
            if (activeLink) activeLink.classList.add('active');
            if (activeLink && activeLink !== lastTocActive) {
                lastTocActive = activeLink;
                activeLink.scrollIntoView({ block: 'nearest', behavior: 'auto' });
            }
        });
    })();

    // -----------------------------------------------------------------
    // 7. PAGE TRANSITION ANIMATIONS
    // -----------------------------------------------------------------
    (function() {
        var animateEls = document.querySelectorAll(
            '.mod-card, .feature-card, .install-step, .step, .faq-item, ' +
            '.pack-card, .keybind-card, .related-card, .race-card, ' +
            '.currency-category, .rule-card, .tool-category, .comparison-table, ' +
            '.mod-status-badge'
        );
        if (!animateEls.length) return;

        var reduceMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
        if (reduceMotion) {
            animateEls.forEach(function(el) {
                el.classList.add('animate-in', 'animated');
            });
            return;
        }

        animateEls.forEach(function(el) { el.classList.add('animate-in'); });

        var observer = new IntersectionObserver(function(entries) {
            entries.forEach(function(entry) {
                if (entry.isIntersecting) {
                    entry.target.classList.add('animated');
                    observer.unobserve(entry.target);
                }
            });
        }, { threshold: 0.1, rootMargin: '0px 0px -40px 0px' });

        animateEls.forEach(function(el, i) {
            el.style.transitionDelay = (i % 6) * 0.08 + 's';
            observer.observe(el);
        });
    })();

    // -----------------------------------------------------------------
    // 8. ANNOUNCEMENT BANNER (Index Page)
    // -----------------------------------------------------------------
    (function() {
        var banner = document.getElementById('announcementBanner');
        if (!banner) return;

        var key = banner.getAttribute('data-dismiss-key') || 'announce-default';
        if (localStorage.getItem('sh-dismiss-' + key)) {
            banner.remove();
            return;
        }

        banner.classList.add('visible');

        var dismissBtn = banner.querySelector('.announcement-dismiss');
        if (dismissBtn) {
            dismissBtn.addEventListener('click', function() {
                banner.classList.remove('visible');
                banner.classList.add('hiding');
                setTimeout(function() { banner.remove(); }, 400);
                localStorage.setItem('sh-dismiss-' + key, '1');
            });
        }
    })();

    // -----------------------------------------------------------------
    // 9. REPORT A BUG FLOATING BUTTON
    // -----------------------------------------------------------------
    (function() {
        var bugBtn = document.createElement('a');
        bugBtn.href = hubDocsBase() + 'feedback.html';
        bugBtn.className = 'bug-report-btn';
        bugBtn.setAttribute('aria-label', 'Report a bug');
        bugBtn.title = 'Report a bug';
        bugBtn.innerHTML = '<span class="bug-icon">&#x1F41B;</span><span class="bug-label">Report Bug</span>';
        document.body.appendChild(bugBtn);
    })();

    // -----------------------------------------------------------------
    // 10. SITE-WIDE SEARCH (Ctrl/Cmd+K, FAB)
    // -----------------------------------------------------------------
    (function() {
        var base = hubDocsBase();

        var root = document.createElement('div');
        root.className = 'site-search';
        root.setAttribute('hidden', '');
        root.innerHTML =
            '<div class="site-search-backdrop" tabindex="-1" aria-hidden="true"></div>' +
            '<div class="site-search-dialog" role="dialog" aria-modal="true" aria-label="Search all pages">' +
            '<label class="visually-hidden" for="siteSearchInput">Search documentation</label>' +
            '<input type="search" id="siteSearchInput" class="site-search-input" autocomplete="off" spellcheck="false" placeholder="Search mods and hub sections…">' +
            '<p id="siteSearchStatus" class="site-search-meta" aria-live="polite" aria-atomic="true"></p>' +
            '<ul class="site-search-results" role="listbox" aria-label="Results"></ul>' +
            '<p class="site-search-hint"><kbd>Esc</kbd> close · <kbd>↑</kbd><kbd>↓</kbd> move · <kbd>Enter</kbd> open · <kbd>Ctrl</kbd>+<kbd>K</kbd> anytime</p>' +
            '</div>';
        document.body.appendChild(root);

        var backdrop = root.querySelector('.site-search-backdrop');
        var dialog = root.querySelector('.site-search-dialog');
        var input = root.querySelector('#siteSearchInput');
        var listEl = root.querySelector('.site-search-results');
        var statusEl = root.querySelector('#siteSearchStatus');
        var allRows = [];
        var filtered = [];
        var sel = -1;
        var debounceT = null;
        var lastFocus = null;
        var indexFetchDone = false;

        function norm(s) {
            return (s || '').toLowerCase();
        }

        function rowScore(row, q) {
            if (!q) return 1;
            var t = norm(row.t);
            var d = norm(row.d);
            var k = norm(row.k);
            var tags = row.tags ? row.tags.map(norm).join(' ') : '';
            if (t.indexOf(q) !== -1) return 4;
            if (q.split(/\s+/).filter(Boolean).every(function(w) {
                return (t + ' ' + d + ' ' + k + ' ' + tags).indexOf(w) !== -1;
            })) return 3;
            if (d.indexOf(q) !== -1 || k.indexOf(q) !== -1) return 2;
            return 0;
        }

        function hrefFor(u) {
            try { return new URL(u, base).href; } catch (e2) { return u; }
        }

        function render() {
            listEl.innerHTML = '';
            var q = norm(input.value.trim());
            filtered = [];
            if (!q) {
                filtered = allRows.slice(0, 8);
            } else {
                allRows.forEach(function(r) {
                    var sc = rowScore(r, q);
                    if (sc > 0) filtered.push({ r: r, sc: sc });
                });
                filtered.sort(function(a, b) { return b.sc - a.sc; });
                filtered = filtered.slice(0, 12).map(function(x) { return x.r; });
            }
            sel = filtered.length ? 0 : -1;
            filtered.forEach(function(row, i) {
                var li = document.createElement('li');
                li.setAttribute('role', 'option');
                li.setAttribute('aria-selected', i === sel ? 'true' : 'false');
                li.className = 'site-search-item' + (i === sel ? ' active' : '');
                li.dataset.idx = String(i);
                var a = document.createElement('a');
                a.href = hrefFor(row.u);
                a.className = 'site-search-hit';
                var spanT = document.createElement('span');
                spanT.className = 'site-search-title';
                spanT.textContent = row.t;
                var spanD = document.createElement('span');
                spanD.className = 'site-search-desc';
                spanD.textContent = row.d;
                a.appendChild(spanT);
                a.appendChild(spanD);
                li.appendChild(a);
                listEl.appendChild(li);
            });
            if (!filtered.length && q) {
                var empty = document.createElement('li');
                empty.className = 'site-search-empty';
                empty.textContent = 'No results — try Vault, museum, todo, or BepInEx.';
                listEl.appendChild(empty);
            }

            if (statusEl) {
                if (!indexFetchDone) {
                    statusEl.textContent = 'Loading search index…';
                } else if (!allRows.length) {
                    statusEl.textContent = 'Search index unavailable — use the Mod Hub and nav links.';
                } else if (!q) {
                    statusEl.textContent = 'Popular pages — type to filter ' + allRows.length + ' entries.';
                } else if (!filtered.length) {
                    statusEl.textContent = 'No matches.';
                } else {
                    statusEl.textContent = filtered.length + ' match' + (filtered.length === 1 ? '' : 'es') + '.';
                }
            }
        }

        function moveSel(delta) {
            if (!filtered.length) return;
            sel = (sel + delta + filtered.length) % filtered.length;
            Array.prototype.forEach.call(listEl.querySelectorAll('[role="option"]'), function(li, i) {
                li.classList.toggle('active', i === sel);
                li.setAttribute('aria-selected', i === sel ? 'true' : 'false');
            });
            var activeLi = listEl.querySelector('.site-search-item.active');
            if (activeLi) {
                activeLi.scrollIntoView({ block: 'nearest', behavior: 'auto' });
            }
        }

        function goSelected() {
            if (sel < 0 || !filtered[sel]) return;
            window.location.href = hrefFor(filtered[sel].u);
        }

        function openSearch() {
            lastFocus = document.activeElement;
            root.removeAttribute('hidden');
            document.body.classList.add('site-search-open');
            input.focus();
            input.select();
            render();
        }

        function closeSearch() {
            root.setAttribute('hidden', '');
            document.body.classList.remove('site-search-open');
            if (lastFocus && typeof lastFocus.focus === 'function') lastFocus.focus();
        }

        function toggleSearch() {
            if (root.hasAttribute('hidden')) openSearch(); else closeSearch();
        }

        fetch(base + 'search-index.json')
            .then(function(r) { return r.json(); })
            .then(function(data) {
                allRows = Array.isArray(data) ? data : [];
                indexFetchDone = true;
                if (!root.hasAttribute('hidden')) render();
            })
            .catch(function() {
                allRows = [];
                indexFetchDone = true;
                if (!root.hasAttribute('hidden')) render();
            });

        input.addEventListener('input', function() {
            if (debounceT) clearTimeout(debounceT);
            debounceT = setTimeout(render, 100);
        });

        input.addEventListener('keydown', function(e) {
            if (e.key === 'ArrowDown') { e.preventDefault(); moveSel(1); }
            else if (e.key === 'ArrowUp') { e.preventDefault(); moveSel(-1); }
            else if (e.key === 'Enter') { e.preventDefault(); goSelected(); }
        });

        backdrop.addEventListener('click', closeSearch);

        listEl.addEventListener('click', function(e) {
            if (e.button !== 0) return;
            if (e.ctrlKey || e.metaKey || e.shiftKey || e.altKey) return;
            var li = e.target.closest('.site-search-item');
            if (!li) return;
            var a = li.querySelector('a');
            if (a) {
                e.preventDefault();
                window.location.href = a.href;
            }
        });

        listEl.addEventListener('mouseover', function(e) {
            var li = e.target.closest('.site-search-item');
            if (!li || !listEl.contains(li)) return;
            var idx = parseInt(li.dataset.idx, 10);
            if (isNaN(idx) || idx === sel) return;
            sel = idx;
            Array.prototype.forEach.call(listEl.querySelectorAll('[role="option"]'), function(el, i) {
                el.classList.toggle('active', i === sel);
                el.setAttribute('aria-selected', i === sel ? 'true' : 'false');
            });
        });

        document.addEventListener('keydown', function(e) {
            if ((e.ctrlKey || e.metaKey) && String(e.key).toLowerCase() === 'k') {
                e.preventDefault();
                toggleSearch();
            } else if (e.key === 'Escape' && document.body.classList.contains('site-search-open')) {
                e.preventDefault();
                closeSearch();
            }
        }, true);

        dialog.addEventListener('keydown', function(e) {
            if (e.key !== 'Tab' || !document.body.classList.contains('site-search-open')) return;
            var focusables = dialog.querySelectorAll('a,button,input,[tabindex]:not([tabindex="-1"])');
            var arr = Array.prototype.filter.call(focusables, function(n) { return n.offsetParent !== null || n === input; });
            if (!arr.length) return;
            var ix = arr.indexOf(document.activeElement);
            if (e.shiftKey) {
                if (ix <= 0) { e.preventDefault(); arr[arr.length - 1].focus(); }
            } else {
                if (ix === arr.length - 1) { e.preventDefault(); arr[0].focus(); }
            }
        });

        var fab = document.createElement('button');
        fab.type = 'button';
        fab.className = 'site-search-fab';
        fab.setAttribute('aria-haspopup', 'dialog');
        fab.title = 'Search all pages (Ctrl+K)';
        fab.innerHTML = '<span class="site-search-fab-icon" aria-hidden="true">&#x1F50D;</span><span class="site-search-fab-text">Search</span>';
        fab.addEventListener('click', openSearch);
        document.body.appendChild(fab);
    })();

    // -----------------------------------------------------------------
    // 11. SCROLL-SPY FOR RACIALBONUS NAV PILLS
    // -----------------------------------------------------------------
    var pills = document.querySelectorAll('.nav-pill');
    if (pills.length) {
        var sections = [];
        pills.forEach(function(pill) {
            var href = pill.getAttribute('href');
            if (href && href.charAt(0) === '#') {
                var section = document.getElementById(href.substring(1));
                if (section) sections.push({ pill: pill, section: section });
            }
        });

        if (sections.length) {
            function updateActivePill() {
                var scrollPos = window.scrollY + 150;
                var active = sections[0];
                sections.forEach(function(s) {
                    if (s.section.offsetTop <= scrollPos) active = s;
                });
                pills.forEach(function(p) { p.classList.remove('active'); });
                if (active) active.pill.classList.add('active');
            }
            window.addEventListener('scroll', updateActivePill);
            updateActivePill();
        }
    }

});
