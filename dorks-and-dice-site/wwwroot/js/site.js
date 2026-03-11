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

// Image gallery modal expand logic
(() => {
    const gallery = document.querySelector('.project-gallery');
    const modal = document.getElementById('imageModal');
    const modalImg = document.getElementById('imageModalImg');
    const closeBtn = modal ? modal.querySelector('.image-modal-close') : null;
    const backdrop = modal ? modal.querySelector('.image-modal-backdrop') : null;

    if (!gallery || !modal || !modalImg || !closeBtn || !backdrop) return;

    gallery.addEventListener('click', function(e) {
        const img = e.target.closest('img.card-img-top');
        if (!img) return;
        modalImg.src = img.src;
        modalImg.alt = img.alt;
        modal.style.display = 'flex';
        document.body.style.overflow = 'hidden';
    });

    function closeModal() {
        modal.style.display = 'none';
        modalImg.src = '';
        document.body.style.overflow = '';
    }

    closeBtn.addEventListener('click', closeModal);
    backdrop.addEventListener('click', closeModal);
    document.addEventListener('keydown', function(e) {
        if (modal.style.display === 'flex' && (e.key === 'Escape' || e.key === 'Esc')) {
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
