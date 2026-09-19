/**
 * Jellyfin Anime Recommendations Plugin - Client-Side Injected Script
 * Renders a customizable weekly recommendations row with genre tabs on the Jellyfin Home page.
 */
(function () {
    'use strict';

    console.log('[AnimeRecommendations] Client script loaded!');

    const SECTION_ID = 'anime-weekly-recommendations-section';
    let cachedData = null;
    let currentSelectedGenre = 'all';
    let isFetching = false;

    // Inject custom CSS styling
    function injectStyles() {
        if (document.getElementById('anime-recommendations-styles')) return;

        const style = document.createElement('style');
        style.id = 'anime-recommendations-styles';
        style.textContent = `
            #${SECTION_ID} {
                margin: 1.5em 0;
                position: relative;
                transition: opacity 0.2s ease-in-out;
            }
            #${SECTION_ID} .sectionHeaderRow {
                display: flex;
                align-items: center;
                justify-content: space-between;
                flex-wrap: wrap;
                gap: 10px;
                margin-bottom: 0.8em;
                padding: 0 0.5em;
            }
            #${SECTION_ID} .sectionTitle {
                font-size: 1.35em;
                font-weight: 600;
                margin: 0;
                display: flex;
                align-items: center;
                gap: 8px;
            }
            #${SECTION_ID} .genreTabsWrapper {
                display: flex;
                align-items: center;
                gap: 6px;
                overflow-x: auto;
                max-width: 100%;
                padding: 4px 0;
                scrollbar-width: none;
            }
            #${SECTION_ID} .genreTabsWrapper::-webkit-scrollbar {
                display: none;
            }
            #${SECTION_ID} .genreTabBtn {
                background: rgba(255, 255, 255, 0.08);
                color: inherit;
                border: 1px solid rgba(255, 255, 255, 0.12);
                border-radius: 20px;
                padding: 5px 14px;
                font-size: 0.85em;
                font-weight: 500;
                cursor: pointer;
                transition: all 0.2s ease;
                white-space: nowrap;
                outline: none;
            }
            #${SECTION_ID} .genreTabBtn:hover {
                background: rgba(255, 255, 255, 0.18);
                border-color: rgba(255, 255, 255, 0.25);
            }
            #${SECTION_ID} .genreTabBtn.active {
                background: #00a4dc;
                color: #ffffff;
                border-color: #00a4dc;
                box-shadow: 0 2px 8px rgba(0, 164, 220, 0.4);
            }
            #${SECTION_ID} .cardsScroller {
                display: flex;
                gap: 14px;
                overflow-x: auto;
                overflow-y: hidden;
                padding: 6px 4px 16px 4px;
                scroll-behavior: smooth;
                scrollbar-width: thin;
            }
            #${SECTION_ID} .recommendationCard {
                flex: 0 0 155px;
                width: 155px;
                cursor: pointer;
                border-radius: 8px;
                transition: transform 0.22s ease, box-shadow 0.22s ease;
                user-select: none;
                position: relative;
            }
            @media (min-width: 900px) {
                #${SECTION_ID} .recommendationCard {
                    flex: 0 0 175px;
                    width: 175px;
                }
            }
            #${SECTION_ID} .recommendationCard:hover {
                transform: translateY(-4px) scale(1.02);
            }
            #${SECTION_ID} .cardImageWrapper {
                width: 100%;
                aspect-ratio: 2/3;
                border-radius: 8px;
                overflow: hidden;
                position: relative;
                background: #181818;
                box-shadow: 0 3px 10px rgba(0, 0, 0, 0.35);
            }
            #${SECTION_ID} .cardImage {
                width: 100%;
                height: 100%;
                object-fit: cover;
                transition: opacity 0.2s;
            }
            #${SECTION_ID} .playedBadge {
                position: absolute;
                top: 6px;
                right: 6px;
                background: rgba(0, 164, 220, 0.9);
                color: white;
                width: 22px;
                height: 22px;
                border-radius: 50%;
                display: flex;
                align-items: center;
                justify-content: center;
                font-size: 13px;
                font-weight: bold;
                box-shadow: 0 2px 4px rgba(0,0,0,0.4);
            }
            #${SECTION_ID} .cardDetails {
                padding: 6px 2px 2px 2px;
            }
            #${SECTION_ID} .cardTitle {
                font-weight: 600;
                font-size: 0.9em;
                margin: 0;
                white-space: nowrap;
                overflow: hidden;
                text-overflow: ellipsis;
            }
            #${SECTION_ID} .cardMeta {
                font-size: 0.8em;
                opacity: 0.72;
                margin-top: 2px;
                display: flex;
                align-items: center;
                gap: 6px;
                white-space: nowrap;
                overflow: hidden;
                text-overflow: ellipsis;
            }
            #${SECTION_ID} .ratingBadge {
                color: #ffb703;
                font-weight: 600;
            }
            #${SECTION_ID} .emptyNotice {
                padding: 1.5em;
                text-align: center;
                opacity: 0.6;
                font-style: italic;
            }
        `;
        document.head.appendChild(style);
    }

    // Helper to check if current page is the Home screen
    function isHomePage() {
        const hash = window.location.hash || '';
        if (!hash || hash === '#!' || hash === '#!/' || hash.startsWith('#!/home') || hash.startsWith('#/home')) {
            return true;
        }

        const activePage = document.querySelector('.page:not(.hide)');
        if (activePage && (activePage.id === 'indexPage' || activePage.classList.contains('homeTab') || activePage.getAttribute('data-type') === 'home')) {
            return true;
        }

        if (document.querySelector('.homeSectionsContainer, #indexPage .sections')) {
            return true;
        }

        return false;
    }

    // Fetch recommendations from plugin API using native ApiClient
    let fetchPromise = null;

    async function fetchRecommendations() {
        if (cachedData && cachedData.items && cachedData.items.length > 0) {
            return cachedData;
        }

        if (fetchPromise) {
            return fetchPromise;
        }

        fetchPromise = (async () => {
            try {
                const apiClient = window.ApiClient;
                if (!apiClient) {
                    console.warn('[AnimeRecommendations] ApiClient not ready.');
                    return null;
                }

                const currentUserId = apiClient.getCurrentUserId ? apiClient.getCurrentUserId() : '';
                const url = apiClient.getUrl('Recommendations/Weekly', { userId: currentUserId });

                console.log('[AnimeRecommendations] Requesting URL:', url);

                let data = null;
                if (apiClient.getJSON) {
                    data = await apiClient.getJSON(url);
                } else {
                    const response = await fetch(url);
                    if (response.ok) {
                        data = await response.json();
                    } else {
                        console.error('[AnimeRecommendations] API fetch error:', response.status, response.statusText);
                    }
                }

                const receivedItems = data ? (data.items || data.Items || []) : [];
                if (data && receivedItems.length > 0) {
                    console.log('[AnimeRecommendations] Received', receivedItems.length, 'recommendation items from API.');
                    cachedData = data;
                } else if (data) {
                    console.warn('[AnimeRecommendations] API returned 0 recommendation items:', data);
                }

                return data;
            } catch (err) {
                console.error('[AnimeRecommendations] Failed to fetch recommendations:', err);
                return null;
            } finally {
                fetchPromise = null;
            }
        })();

        return fetchPromise;
    }

    // Filter cards according to the selected genre tab
    function getFilteredItems(data, genre) {
        const items = data ? (data.items || data.Items || []) : [];
        if (!items || items.length === 0) return [];
        if (!genre || genre === 'all') return items;

        return items.filter(item => {
            const itemGenres = item.genres || item.Genres;
            if (!itemGenres || !Array.isArray(itemGenres)) return false;
            return itemGenres.some(g => g && g.trim().toLowerCase() === genre.trim().toLowerCase());
        });
    }

    // Renders the card elements for a given genre
    function renderCards(container, items) {
        container.innerHTML = '';

        if (!items || items.length === 0) {
            container.innerHTML = '<div class="emptyNotice">Keine Anime-Empfehlungen für dieses Genre gefunden.</div>';
            return;
        }

        const apiClient = window.ApiClient;

        items.forEach(item => {
            const itemId = item.id || item.Id;
            const itemName = item.name || item.Name || '';
            const itemRating = (item.communityRating !== undefined && item.communityRating !== null)
                ? item.communityRating
                : item.CommunityRating;
            const itemYear = (item.productionYear !== undefined && item.productionYear !== null)
                ? item.productionYear
                : item.ProductionYear;
            const itemPlayed = (item.played !== undefined && item.played !== null)
                ? item.played
                : (item.Played || false);
            const itemImageTag = item.primaryImageTag || item.PrimaryImageTag;

            const card = document.createElement('div');
            card.className = 'recommendationCard';
            card.setAttribute('data-id', itemId);

            let imgUrl = '';
            if (apiClient) {
                imgUrl = apiClient.getUrl(`Items/${itemId}/Images/Primary`, {
                    fillWidth: 320,
                    fillHeight: 480,
                    quality: 94,
                    tag: itemImageTag || undefined
                });
            }

            const ratingDisplay = (itemRating !== undefined && itemRating !== null)
                ? `<span class="ratingBadge">★ ${Number(itemRating).toFixed(1)}</span>`
                : '';
            const yearDisplay = itemYear ? `<span>${itemYear}</span>` : '';
            const playedDisplay = itemPlayed ? `<div class="playedBadge">✓</div>` : '';

            card.innerHTML = `
                <div class="cardImageWrapper">
                    <img class="cardImage" src="${imgUrl}" alt="${itemName}" loading="lazy" onerror="this.onerror=null;this.style.display='none';if(this.nextElementSibling)this.nextElementSibling.style.display='flex';" />
                    <div class="cardImageFallback" style="display:none;width:100%;height:100%;align-items:center;justify-content:center;background:#202020;padding:10px;text-align:center;font-size:0.85em;box-sizing:border-box;">${itemName}</div>
                    ${playedDisplay}
                </div>
                <div class="cardDetails">
                    <div class="cardTitle" title="${itemName}">${itemName}</div>
                    <div class="cardMeta">
                        ${ratingDisplay}
                        ${yearDisplay}
                    </div>
                </div>
            `;

            // Open item details on click
            card.addEventListener('click', (e) => {
                e.preventDefault();
                if (window.appRouter && window.appRouter.showItem) {
                    window.appRouter.showItem(itemId);
                } else {
                    window.location.hash = `#!/details?id=${itemId}`;
                }
            });

            container.appendChild(card);
        });
    }

    // Build or update the section on the home page
    async function renderSection() {
        if (!isHomePage()) {
            const existing = document.getElementById(SECTION_ID);
            if (existing) existing.remove();
            return;
        }

        injectStyles();

        // Check target insertion point on home page
        const resumableSection = document.querySelector('#resumableSection, .resumableSection, #resumeSection, [data-type="resume"], .section0');
        const nextUpSection = document.querySelector('#nextUpSection, .nextUpSection, [data-type="nextup"], .section1');
        const latestSection = document.querySelector('#latestSection, .latestSection, [data-type="latest"]');
        const homeContainer = document.querySelector('.homeSectionsContainer, #indexPage .sections, .sections, #indexPage, .page:not(.hide) .content-primary, .homeTab, [data-role="page"]:not(.hide)');

        if (!resumableSection && !nextUpSection && !latestSection && !homeContainer) {
            // Home DOM not yet ready
            return;
        }

        console.log('[AnimeRecommendations] Home screen detected, loading data...');
        const data = await fetchRecommendations();
        if (!data) {
            console.warn('[AnimeRecommendations] Recommendations data is null.');
            return;
        }

        const items = data.items || data.Items || [];
        const genres = data.genres || data.Genres || [];
        const title = data.title || data.Title || 'Anime-Empfehlungen der Woche';

        if (items.length === 0) {
            console.warn('[AnimeRecommendations] No recommendation items available in response:', data);
            return;
        }

        console.log('[AnimeRecommendations] Rendering', items.length, 'recommendation items...');

        let section = document.getElementById(SECTION_ID);
        if (!section) {
            section = document.createElement('div');
            section.id = SECTION_ID;
            section.className = 'verticalSection anime-recommendations-container';
        }

        // Ensure section is in the DOM at the right location
        if (!section.parentNode) {
            // Find best insertion spot: After Continue Watching, or Next Up, or before Latest
            if (resumableSection && resumableSection.parentNode) {
                resumableSection.parentNode.insertBefore(section, resumableSection.nextSibling);
            } else if (nextUpSection && nextUpSection.parentNode) {
                nextUpSection.parentNode.insertBefore(section, nextUpSection.nextSibling);
            } else if (latestSection && latestSection.parentNode) {
                latestSection.parentNode.insertBefore(section, latestSection);
            } else if (homeContainer) {
                homeContainer.prepend(section);
            }
        }

        // Render Header with Title and Genre Tabs (Variante A)
        section.innerHTML = `
            <div class="sectionHeaderRow">
                <h2 class="sectionTitle">
                    <span>✨</span>
                    <span>${title}</span>
                </h2>
                <div class="genreTabsWrapper" id="${SECTION_ID}-tabs">
                    <button class="genreTabBtn ${currentSelectedGenre === 'all' ? 'active' : ''}" data-genre="all">Alle</button>
                    ${genres.map(genre => `
                        <button class="genreTabBtn ${currentSelectedGenre === genre ? 'active' : ''}" data-genre="${genre}">${genre}</button>
                    `).join('')}
                </div>
            </div>
            <div class="cardsScroller" id="${SECTION_ID}-cards"></div>
        `;

        // Attach tab click events
        const tabsWrapper = section.querySelector(`#${SECTION_ID}-tabs`);
        const cardsContainer = section.querySelector(`#${SECTION_ID}-cards`);

        tabsWrapper.querySelectorAll('.genreTabBtn').forEach(btn => {
            btn.addEventListener('click', (e) => {
                e.preventDefault();
                tabsWrapper.querySelectorAll('.genreTabBtn').forEach(b => b.classList.remove('active'));
                btn.classList.add('active');
                currentSelectedGenre = btn.getAttribute('data-genre');
                const filtered = getFilteredItems(data, currentSelectedGenre);
                renderCards(cardsContainer, filtered);
            });
        });

        // Initial card render
        const initialItems = getFilteredItems(data, currentSelectedGenre);
        renderCards(cardsContainer, initialItems);
        console.log('[AnimeRecommendations] Section successfully rendered on home page!');
    }

    // Navigation and lifecycle hooks
    function setupHooks() {
        document.addEventListener('viewshow', () => {
            setTimeout(renderSection, 200);
        });

        window.addEventListener('hashchange', () => {
            setTimeout(renderSection, 200);
        });

        window.addEventListener('popstate', () => {
            setTimeout(renderSection, 200);
        });

        // Periodic check to ensure row stays rendered when navigating or SPA dynamic refresh
        setInterval(() => {
            if (isHomePage()) {
                const section = document.getElementById(SECTION_ID);
                if (!section) {
                    renderSection();
                }
            }
        }, 1500);
    }

    // Initialize when DOM is ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', () => {
            setupHooks();
            renderSection();
        });
    } else {
        setupHooks();
        renderSection();
    }
})();
