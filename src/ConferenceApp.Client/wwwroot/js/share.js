// Teilen über die Web Share API, sonst Link in die Zwischenablage kopieren.
// Rückgabe: "shared" | "cancelled" | "copied" | "failed"
export async function shareOrCopy(title, url) {
    if (navigator.share) {
        try {
            await navigator.share({ title, url });
            return "shared";
        } catch (e) {
            if (e && e.name === "AbortError") return "cancelled";
            // z. B. NotAllowedError: auf Kopieren zurückfallen
        }
    }

    if (navigator.clipboard && window.isSecureContext) {
        try {
            await navigator.clipboard.writeText(url);
            return "copied";
        } catch { /* weiter mit Fallback */ }
    }

    // Fallback für unsichere Kontexte (http ohne localhost) bzw. ältere Browser
    const area = document.createElement("textarea");
    area.value = url;
    area.setAttribute("readonly", "");
    area.style.position = "fixed";
    area.style.opacity = "0";
    document.body.appendChild(area);
    area.select();
    let ok = false;
    try { ok = document.execCommand("copy"); } catch { ok = false; }
    document.body.removeChild(area);
    return ok ? "copied" : "failed";
}
