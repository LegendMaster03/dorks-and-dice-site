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
