(() => {
    const themeStorageKey = "site-theme";
    const toggleButton = document.getElementById("themeToggle");
    if (!toggleButton) {
        return;
    }

    const applyTheme = (theme) => {
        const isDark = theme === "dark";
        document.body.classList.toggle("dark-mode", isDark);
        document.documentElement.setAttribute("data-bs-theme", isDark ? "dark" : "light");
        toggleButton.textContent = isDark ? "Light Mode" : "Dark Mode";
        toggleButton.setAttribute("aria-pressed", isDark ? "true" : "false");
    };

    const storedTheme = localStorage.getItem(themeStorageKey);
    const initialTheme = storedTheme
        ?? (window.matchMedia("(prefers-color-scheme: dark)").matches ? "dark" : "light");

    applyTheme(initialTheme);

    toggleButton.addEventListener("click", () => {
        const nextTheme = document.body.classList.contains("dark-mode") ? "light" : "dark";
        localStorage.setItem(themeStorageKey, nextTheme);
        applyTheme(nextTheme);
    });
})();

(() => {
    const forms = document.querySelectorAll("[data-auto-submit='change']");
    forms.forEach((form) => {
        form.addEventListener("change", () => form.requestSubmit());
    });
})();

(() => {
    const galleryImages = document.querySelectorAll(".project-gallery img.card-img-top");
    if (!galleryImages.length) {
        return;
    }

    let modal = document.getElementById("imageModal");
    if (!modal) {
        modal = document.createElement("div");
        modal.id = "imageModal";
        modal.className = "image-modal";
        modal.hidden = true;
        modal.innerHTML = `
            <div class="image-modal-backdrop"></div>
            <div class="image-modal-content" role="dialog" aria-modal="true" aria-labelledby="imageModalTitle" tabindex="-1">
                <h2 id="imageModalTitle" class="visually-hidden">Expanded project image</h2>
                <button type="button" class="image-modal-close" aria-label="Close expanded image view">&times;</button>
                <img id="imageModalImg" src="" alt="" />
            </div>
        `;
        document.body.appendChild(modal);
    }

    const modalContent = modal.querySelector(".image-modal-content");
    const modalImg = modal.querySelector("#imageModalImg");
    const closeBtn = modal.querySelector(".image-modal-close");
    const backdrop = modal.querySelector(".image-modal-backdrop");
    let returnFocusTarget = null;

    if (!modalContent || !modalImg || !closeBtn || !backdrop) {
        return;
    }

    const openModal = (img) => {
        returnFocusTarget = img;
        modalImg.src = img.currentSrc || img.src;
        modalImg.alt = img.alt || "Expanded project image";
        modal.hidden = false;
        modal.classList.add("is-open");
        document.body.style.overflow = "hidden";
        closeBtn.focus();
    };

    const closeModal = () => {
        if (modal.hidden) {
            return;
        }

        modal.classList.remove("is-open");
        modal.hidden = true;
        modalImg.src = "";
        document.body.style.overflow = "";
        returnFocusTarget?.focus();
        returnFocusTarget = null;
    };

    galleryImages.forEach((img) => {
        img.tabIndex = 0;
        img.setAttribute("role", "button");
        img.setAttribute("aria-haspopup", "dialog");
        img.setAttribute("aria-label", `Expand image: ${img.alt || "project image"}`);
        img.addEventListener("click", () => openModal(img));
        img.addEventListener("keydown", (event) => {
            if (event.key === "Enter" || event.key === " ") {
                event.preventDefault();
                openModal(img);
            }
        });
    });

    closeBtn.addEventListener("click", closeModal);
    backdrop.addEventListener("click", closeModal);
    document.addEventListener("keydown", (event) => {
        if (modal.hidden) {
            return;
        }

        if (event.key === "Escape") {
            event.preventDefault();
            closeModal();
            return;
        }

        if (event.key === "Tab") {
            event.preventDefault();
            closeBtn.focus();
        }
    });
})();

const siteSearch = (() => {
    const normalizeSearchValue = (value) => value.trim().toLowerCase().replace(/[_\s]+/g, "-");

    const tokenizeSearchQuery = (query) => {
        const tokens = [];
        let current = "";
        let isQuoted = false;

        for (const character of query) {
            if (character === "\"") {
                isQuoted = !isQuoted;
                continue;
            }

            if (!isQuoted && (/[\s,]/).test(character)) {
                if (current.trim().length > 0) {
                    tokens.push(current.trim());
                    current = "";
                }
                continue;
            }

            if (!isQuoted && (character === "(" || character === ")")) {
                if (current.trim().length > 0) {
                    tokens.push(current.trim());
                    current = "";
                }
                tokens.push(character);
                continue;
            }

            current += character;
        }

        if (current.trim().length > 0) {
            tokens.push(current.trim());
        }

        return tokens;
    };

    const parseTerm = (rawToken) => {
        let token = rawToken.toLowerCase();
        let bucket = "required";

        if (token.startsWith("-")) {
            bucket = "excluded";
            token = token.slice(1);
        } else if (token.startsWith("~")) {
            bucket = "optional";
            token = token.slice(1);
        }

        if (token.length === 0) {
            return null;
        }

        const separatorIndex = token.indexOf(":");
        const term = separatorIndex > 0
            ? { key: token.slice(0, separatorIndex), value: token.slice(separatorIndex + 1) }
            : { key: "any", value: token };

        return { bucket, term };
    };

    const collectGroupTokens = (tokens, startIndex) => {
        const groupTokens = [];
        let depth = 1;
        let index = startIndex + 1;

        for (; index < tokens.length; index += 1) {
            if (tokens[index] === "(") {
                depth += 1;
            } else if (tokens[index] === ")") {
                depth -= 1;
                if (depth === 0) {
                    break;
                }
            }

            groupTokens.push(tokens[index]);
        }

        return { groupTokens, endIndex: index };
    };

    const parseTokens = (tokens) => {
        const parsedQuery = { required: [], excluded: [], optional: [], groups: [], order: null };

        for (let index = 0; index < tokens.length; index += 1) {
            let groupBucket = "required";
            let token = tokens[index];

            if ((token === "~" || token === "-") && tokens[index + 1] === "(") {
                groupBucket = token === "~" ? "optional" : "excluded";
                index += 1;
                token = tokens[index];
            }

            if (token === "(") {
                const { groupTokens, endIndex } = collectGroupTokens(tokens, index);
                parsedQuery.groups.push({ bucket: groupBucket, query: parseTokens(groupTokens) });
                index = endIndex;
                continue;
            }

            const parsedTerm = parseTerm(token);
            if (!parsedTerm) {
                continue;
            }

            if (parsedTerm.term.key === "order") {
                parsedQuery.order = {
                    value: parsedTerm.term.value,
                    reversed: parsedTerm.bucket === "excluded"
                };
                continue;
            }

            parsedQuery[parsedTerm.bucket].push(parsedTerm.term);
        }

        return parsedQuery;
    };

    const parseSearchQuery = (query) => parseTokens(tokenizeSearchQuery(query));

    const wildcardMatches = (pattern, value) => {
        const escapedPattern = pattern.replace(/[.+?^${}()|[\]\\]/g, "\\$&").replace(/\*/g, ".*");
        return new RegExp(`^${escapedPattern}$`).test(value);
    };

    const valueMatches = (pattern, value) => {
        const normalizedPattern = normalizeSearchValue(pattern);
        const normalizedValue = normalizeSearchValue(value);

        if (normalizedPattern.includes("*")) {
            return wildcardMatches(normalizedPattern, normalizedValue);
        }

        return normalizedValue === normalizedPattern || value.toLowerCase().includes(pattern);
    };

    const termMatchesCard = (term, cardData) => {
        const tagMatches = cardData.tags.some((tag) => valueMatches(term.value, tag));

        switch (term.key) {
            case "any":
                return tagMatches || valueMatches(term.value, cardData.searchText);
            case "tag":
                return tagMatches;
            case "category":
                return valueMatches(term.value, cardData.category);
            case "title":
                return valueMatches(term.value, cardData.title);
            case "text":
                return valueMatches(term.value, cardData.searchText);
            case "featured":
                return valueMatches(term.value, cardData.featured);
            case "listed":
                return valueMatches(term.value, cardData.listed);
            case "date":
                return valueMatches(term.value, cardData.date);
            default:
                return false;
        }
    };

    const groupMatchesCard = (group, cardData) => cardMatchesParsedQuery(group.query, cardData);

    const cardMatchesParsedQuery = (parsedQuery, cardData) => {
        const requiredGroups = parsedQuery.groups.filter((group) => group.bucket === "required");
        const excludedGroups = parsedQuery.groups.filter((group) => group.bucket === "excluded");
        const optionalGroups = parsedQuery.groups.filter((group) => group.bucket === "optional");
        const hasOptionalClause = parsedQuery.optional.length > 0 || optionalGroups.length > 0;

        return parsedQuery.required.every((term) => termMatchesCard(term, cardData))
            && parsedQuery.excluded.every((term) => !termMatchesCard(term, cardData))
            && requiredGroups.every((group) => groupMatchesCard(group, cardData))
            && excludedGroups.every((group) => !groupMatchesCard(group, cardData))
            && (!hasOptionalClause
                || parsedQuery.optional.some((term) => termMatchesCard(term, cardData))
                || optionalGroups.some((group) => groupMatchesCard(group, cardData)));
    };

    const activeTagTerms = (parsedQuery) => [
        ...parsedQuery.required,
        ...parsedQuery.optional
    ]
        .filter((term) => term.key === "any" || term.key === "tag")
        .map((term) => normalizeSearchValue(term.value));

    const sortCards = (container, cards, order, getSortData) => {
        if (!container || !order) {
            return;
        }

        const orderedCards = [...cards].sort((firstCard, secondCard) => {
            const firstData = getSortData(firstCard);
            const secondData = getSortData(secondCard);
            let result = 0;

            switch (order.value) {
                case "title":
                case "title_desc":
                    result = secondData.title.localeCompare(firstData.title);
                    break;
                case "title_asc":
                    result = firstData.title.localeCompare(secondData.title);
                    break;
                case "date":
                case "date_desc":
                case "created":
                case "created_desc":
                    result = secondData.dateValue - firstData.dateValue;
                    break;
                case "date_asc":
                case "created_asc":
                    result = firstData.dateValue - secondData.dateValue;
                    break;
                case "tagcount":
                case "tagcount_desc":
                    result = secondData.tagCount - firstData.tagCount;
                    break;
                case "tagcount_asc":
                    result = firstData.tagCount - secondData.tagCount;
                    break;
                case "featured":
                    result = Number(secondData.featured) - Number(firstData.featured);
                    break;
                case "id_desc":
                    result = secondData.defaultIndex - firstData.defaultIndex;
                    break;
                case "id":
                case "id_asc":
                default:
                    result = firstData.defaultIndex - secondData.defaultIndex;
                    break;
            }

            return order.reversed ? -result : result;
        });

        orderedCards.forEach((card) => container.appendChild(card));
    };

    return {
        activeTagTerms,
        cardMatchesParsedQuery,
        parseSearchQuery,
        sortCards
    };
})();

(() => {
    const filterContainer = document.getElementById("projectFilters");
    if (!filterContainer) {
        return;
    }

    const searchInput = document.getElementById("projectSearch");
    const projectList = document.getElementById("projectList");
    const filterButtons = filterContainer.querySelectorAll("[data-filter-tag]");
    const projectCards = document.querySelectorAll(".project-card");
    const status = document.getElementById("projectFilterStatus");
    let activeTag = "all";

    projectCards.forEach((card, index) => {
        card.dataset.defaultIndex = index.toString();
    });

    const applyFilters = () => {
        const parsedQuery = siteSearch.parseSearchQuery(searchInput?.value ?? "");
        const normalizedActiveTags = siteSearch.activeTagTerms(parsedQuery);
        let visibleCount = 0;
        projectCards.forEach((card) => {
            const tags = (card.getAttribute("data-tags") ?? "").split(/\s+/).filter(Boolean);
            const cardData = {
                category: card.getAttribute("data-category") ?? "",
                date: "",
                featured: card.getAttribute("data-featured") ?? "",
                listed: "",
                searchText: card.getAttribute("data-search") ?? "",
                tags,
                title: card.getAttribute("data-title") ?? ""
            };
            const isVisible = (activeTag === "all" || tags.includes(activeTag))
                && siteSearch.cardMatchesParsedQuery(parsedQuery, cardData);
            card.classList.toggle("d-none", !isVisible);
            if (isVisible) {
                visibleCount += 1;
            }
        });

        siteSearch.sortCards(projectList, projectCards, parsedQuery.order, (card) => ({
            dateValue: 0,
            defaultIndex: Number(card.dataset.defaultIndex ?? "0"),
            featured: card.getAttribute("data-featured") === "true",
            tagCount: (card.getAttribute("data-tags") ?? "").split(/\s+/).filter(Boolean).length,
            title: card.getAttribute("data-title") ?? ""
        }));

        filterButtons.forEach((button) => {
            const buttonTag = button.getAttribute("data-filter-tag") ?? "all";
            const isActive = buttonTag === activeTag || normalizedActiveTags.includes(buttonTag);
            button.classList.toggle("active", isActive);
            button.setAttribute("aria-pressed", isActive ? "true" : "false");
        });

        if (status) {
            status.textContent = `${visibleCount} project${visibleCount === 1 ? "" : "s"} shown.`;
        }
    };

    filterButtons.forEach((button) => {
        button.addEventListener("click", () => {
            activeTag = button.getAttribute("data-filter-tag") ?? "all";
            applyFilters();
        });
    });

    searchInput?.addEventListener("input", applyFilters);
    applyFilters();
})();

(() => {
    const filterContainer = document.getElementById("articleFilters");
    if (!filterContainer) {
        return;
    }

    const searchInput = document.getElementById("articleSearch");
    const articleList = document.getElementById("articleList");
    const categoryButtons = filterContainer.querySelectorAll("[data-article-category]");
    const tagButtons = filterContainer.querySelectorAll("[data-article-tag]");
    const articleCards = document.querySelectorAll(".article-card");
    const emptyState = document.getElementById("articleEmptyState");
    const status = document.getElementById("articleFilterStatus");
    const forceProfessionalOnly = filterContainer.getAttribute("data-professional-filter") === "true";
    let activeCategory = "all";
    let activeTag = "all";

    articleCards.forEach((card, index) => {
        card.dataset.defaultIndex = index.toString();
    });

    const applyFilters = () => {
        const parsedQuery = siteSearch.parseSearchQuery(searchInput?.value ?? "");
        const normalizedActiveTags = siteSearch.activeTagTerms(parsedQuery);
        let visibleCount = 0;

        articleCards.forEach((card) => {
            const category = card.getAttribute("data-article-category");
            const isProfessional = card.getAttribute("data-article-professional") === "true";
            const tags = (card.getAttribute("data-article-tags") ?? "").split(/\s+/).filter(Boolean);
            const cardData = {
                category: category ?? "",
                date: card.getAttribute("data-article-date") ?? "",
                featured: "",
                listed: card.getAttribute("data-article-listed") ?? "",
                searchText: card.getAttribute("data-article-search") ?? "",
                tags,
                title: card.getAttribute("data-article-title") ?? ""
            };
            const isVisible = (activeCategory === "all" || category === activeCategory)
                && (activeTag === "all" || tags.includes(activeTag))
                && siteSearch.cardMatchesParsedQuery(parsedQuery, cardData)
                && (!forceProfessionalOnly || isProfessional);

            card.classList.toggle("d-none", !isVisible);
            if (isVisible) {
                visibleCount += 1;
            }
        });

        siteSearch.sortCards(articleList, articleCards, parsedQuery.order, (card) => ({
            dateValue: Date.parse(card.getAttribute("data-article-date") ?? "") || 0,
            defaultIndex: Number(card.dataset.defaultIndex ?? "0"),
            tagCount: (card.getAttribute("data-article-tags") ?? "").split(/\s+/).filter(Boolean).length,
            title: card.getAttribute("data-article-title") ?? ""
        }));

        categoryButtons.forEach((button) => {
            const isActive = button.getAttribute("data-article-category") === activeCategory;
            button.classList.toggle("active", isActive);
            button.setAttribute("aria-pressed", isActive ? "true" : "false");
        });
        tagButtons.forEach((button) => {
            const buttonTag = button.getAttribute("data-article-tag") ?? "all";
            const isActive = buttonTag === activeTag || normalizedActiveTags.includes(buttonTag);
            button.classList.toggle("active", isActive);
            button.setAttribute("aria-pressed", isActive ? "true" : "false");
        });

        emptyState?.classList.toggle("d-none", visibleCount > 0);
        if (status) {
            status.textContent = `${visibleCount} article${visibleCount === 1 ? "" : "s"} shown.`;
        }
    };

    categoryButtons.forEach((button) => {
        button.addEventListener("click", () => {
            activeCategory = button.getAttribute("data-article-category") ?? "all";
            applyFilters();
        });
    });
    tagButtons.forEach((button) => {
        button.addEventListener("click", () => {
            activeTag = button.getAttribute("data-article-tag") ?? "all";
            applyFilters();
        });
    });

    searchInput?.addEventListener("input", applyFilters);
    applyFilters();
})();
