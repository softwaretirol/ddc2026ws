// Farbschema (Hell/Dunkel/System): gespeicherte Wahl im localStorage, Systemeinstellung per matchMedia.
// Wird synchron im <head> geladen, damit die Lade-Shell schon vor dem WASM-Start im richtigen Modus erscheint.
(function () {
    var storageKey = "conferenceapp.theme";
    var media = window.matchMedia ? window.matchMedia("(prefers-color-scheme: dark)") : null;
    var listener = null;

    function getPreference() {
        try {
            var value = window.localStorage.getItem(storageKey);
            return value === "light" || value === "dark" ? value : null;
        } catch (e) {
            return null; // kein Zugriff (z. B. privater Modus, gesperrter Speicher) → Systemeinstellung
        }
    }

    // data-theme am <html> steuert die Farben der Lade-Shell (siehe app.css); ohne Attribut gilt prefers-color-scheme.
    function apply(mode) {
        if (mode === "light" || mode === "dark") document.documentElement.setAttribute("data-theme", mode);
        else document.documentElement.removeAttribute("data-theme");
    }

    window.conferenceTheme = {
        getPreference: getPreference,

        setPreference: function (mode) {
            try {
                if (mode === "light" || mode === "dark") window.localStorage.setItem(storageKey, mode);
                else window.localStorage.removeItem(storageKey);
            } catch (e) {
                // Wahl gilt dann nur für diese Sitzung.
            }
        },

        // Tatsächlich angezeigter Modus (true = dunkel), damit Seitenhintergrund und Scrollbars zur App passen.
        applyDarkMode: function (isDark) {
            apply(isDark ? "dark" : "light");
        },

        systemPrefersDark: function () {
            return !!(media && media.matches);
        },

        watchSystem: function (dotNetRef) {
            if (!media || listener) return;
            listener = function (e) { dotNetRef.invokeMethodAsync("OnSystemDarkModeChanged", e.matches); };
            if (media.addEventListener) media.addEventListener("change", listener);
            else if (media.addListener) media.addListener(listener);
        },

        unwatchSystem: function () {
            if (!media || !listener) return;
            if (media.removeEventListener) media.removeEventListener("change", listener);
            else if (media.removeListener) media.removeListener(listener);
            listener = null;
        }
    };

    apply(getPreference());
})();
