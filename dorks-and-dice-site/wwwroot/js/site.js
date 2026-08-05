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

(() => {
    const filterContainer = document.getElementById("projectFilters");
    if (!filterContainer) {
        return;
    }

    const filterButtons = filterContainer.querySelectorAll("[data-filter]");
    const projectCards = document.querySelectorAll(".project-card");
    const status = document.getElementById("projectFilterStatus");

    const applyFilter = (filter) => {
        let visibleCount = 0;
        projectCards.forEach((card) => {
            const category = card.getAttribute("data-category");
            const isVisible = filter === "all" || category === filter;
            card.classList.toggle("d-none", !isVisible);
            if (isVisible) {
                visibleCount += 1;
            }
        });

        filterButtons.forEach((button) => {
            const isActive = button.getAttribute("data-filter") === filter;
            button.classList.toggle("active", isActive);
            button.setAttribute("aria-pressed", isActive ? "true" : "false");
        });

        if (status) {
            status.textContent = `${visibleCount} project${visibleCount === 1 ? "" : "s"} shown.`;
        }
    };

    filterButtons.forEach((button) => {
        button.addEventListener("click", () => applyFilter(button.getAttribute("data-filter") ?? "all"));
    });

    applyFilter("all");
})();

(() => {
    const filterContainer = document.getElementById("articleFilters");
    if (!filterContainer) {
        return;
    }

    const searchInput = document.getElementById("articleSearch");
    const filterButtons = filterContainer.querySelectorAll("[data-article-category]");
    const articleCards = document.querySelectorAll(".article-card");
    const emptyState = document.getElementById("articleEmptyState");
    const status = document.getElementById("articleFilterStatus");
    const forceProfessionalOnly = filterContainer.getAttribute("data-professional-filter") === "true";
    let activeCategory = "all";

    const applyFilters = () => {
        const query = (searchInput?.value ?? "").trim().toLowerCase();
        let visibleCount = 0;

        articleCards.forEach((card) => {
            const category = card.getAttribute("data-article-category");
            const isProfessional = card.getAttribute("data-article-professional") === "true";
            const searchText = card.getAttribute("data-article-search") ?? "";
            const isVisible = (activeCategory === "all" || category === activeCategory)
                && (query.length === 0 || searchText.includes(query))
                && (!forceProfessionalOnly || isProfessional);

            card.classList.toggle("d-none", !isVisible);
            if (isVisible) {
                visibleCount += 1;
            }
        });

        filterButtons.forEach((button) => {
            const isActive = button.getAttribute("data-article-category") === activeCategory;
            button.classList.toggle("active", isActive);
            button.setAttribute("aria-pressed", isActive ? "true" : "false");
        });

        emptyState?.classList.toggle("d-none", visibleCount > 0);
        if (status) {
            status.textContent = `${visibleCount} article${visibleCount === 1 ? "" : "s"} shown.`;
        }
    };

    filterButtons.forEach((button) => {
        button.addEventListener("click", () => {
            activeCategory = button.getAttribute("data-article-category") ?? "all";
            applyFilters();
        });
    });

    searchInput?.addEventListener("input", applyFilters);
    applyFilters();
})();
