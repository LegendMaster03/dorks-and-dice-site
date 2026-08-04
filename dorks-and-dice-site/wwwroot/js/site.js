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
    const galleries = document.querySelectorAll(".project-gallery");
    if (!galleries.length) {
        return;
    }

    let modal = document.getElementById("imageModal");
    if (!modal) {
        modal = document.createElement("div");
        modal.id = "imageModal";
        modal.className = "image-modal";
        modal.innerHTML = `
            <div class="image-modal-backdrop"></div>
            <div class="image-modal-content" role="dialog" aria-modal="true" aria-label="Expanded image view">
                <button type="button" class="image-modal-close" aria-label="Close expanded image view">&times;</button>
                <img id="imageModalImg" src="" alt="Expanded image view" />
            </div>
        `;
        document.body.appendChild(modal);
    }

    const modalImg = modal.querySelector("#imageModalImg");
    const closeBtn = modal.querySelector(".image-modal-close");
    const backdrop = modal.querySelector(".image-modal-backdrop");

    if (!modalImg || !closeBtn || !backdrop) {
        return;
    }

    const openModal = (img) => {
        modalImg.src = img.currentSrc || img.src;
        modalImg.alt = img.alt || "Expanded image view";
        modal.classList.add("is-open");
        document.body.style.overflow = "hidden";
    };

    const closeModal = () => {
        modal.classList.remove("is-open");
        modalImg.src = "";
        document.body.style.overflow = "";
    };

    galleries.forEach((gallery) => {
        gallery.addEventListener("click", (event) => {
            const img = event.target.closest("img.card-img-top");
            if (!img) {
                return;
            }

            openModal(img);
        });
    });

    closeBtn.addEventListener("click", closeModal);
    backdrop.addEventListener("click", closeModal);
    document.addEventListener("keydown", (event) => {
        if (modal.classList.contains("is-open") && event.key === "Escape") {
            closeModal();
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

    const applyFilter = (filter) => {
        projectCards.forEach((card) => {
            const category = card.getAttribute("data-category");
            card.classList.toggle("d-none", filter !== "all" && category !== filter);
        });

        filterButtons.forEach((button) => {
            const isActive = button.getAttribute("data-filter") === filter;
            button.classList.toggle("active", isActive);
        });
    };

    filterButtons.forEach((button) => {
        button.addEventListener("click", () => {
            applyFilter(button.getAttribute("data-filter"));
        });
    });
})();

(() => {
    const filterContainer = document.getElementById("articleFilters");
    if (!filterContainer) {
        return;
    }

    const searchInput = document.getElementById("articleSearch");
    const professionalOnlyInput = document.getElementById("articleProfessionalOnly");
    const filterButtons = filterContainer.querySelectorAll("[data-article-category]");
    const articleCards = document.querySelectorAll(".article-card");
    const emptyState = document.getElementById("articleEmptyState");
    const forceProfessionalOnly = filterContainer.getAttribute("data-professional-filter") === "true";
    let activeCategory = "all";

    const applyFilters = () => {
        const query = (searchInput?.value ?? "").trim().toLowerCase();
        const professionalOnly = forceProfessionalOnly || Boolean(professionalOnlyInput?.checked);
        let visibleCount = 0;

        articleCards.forEach((card) => {
            const category = card.getAttribute("data-article-category");
            const isProfessional = card.getAttribute("data-article-professional") === "true";
            const searchText = card.getAttribute("data-article-search") ?? "";
            const categoryMatches = activeCategory === "all" || category === activeCategory;
            const searchMatches = query.length === 0 || searchText.includes(query);
            const professionalMatches = !professionalOnly || isProfessional;
            const isVisible = categoryMatches && searchMatches && professionalMatches;

            card.classList.toggle("d-none", !isVisible);
            if (isVisible) {
                visibleCount += 1;
            }
        });

        filterButtons.forEach((button) => {
            const isActive = button.getAttribute("data-article-category") === activeCategory;
            button.classList.toggle("active", isActive);
        });

        emptyState?.classList.toggle("d-none", visibleCount > 0);
    };

    filterButtons.forEach((button) => {
        button.addEventListener("click", () => {
            activeCategory = button.getAttribute("data-article-category") ?? "all";
            applyFilters();
        });
    });

    searchInput?.addEventListener("input", applyFilters);
    professionalOnlyInput?.addEventListener("change", applyFilters);
})();
