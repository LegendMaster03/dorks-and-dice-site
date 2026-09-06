(() => {
    const guardKey = "__dorksMinecraftServerStatusLive";
    if (window[guardKey]) {
        return;
    }
    window[guardKey] = true;

    const endpoint = "/plugins/minecraft-server-status/snapshot";
    const defaultDelayMilliseconds = 15000;

    function forEachField(name, callback) {
        document.querySelectorAll(`[data-minecraft-status-field="${name}"]`).forEach(callback);
    }

    function setTextField(name, value, visible) {
        forEachField(name, element => {
            const text = value ?? "";
            if (element.textContent !== text) {
                element.textContent = text;
            }
            element.hidden = !visible;
        });
    }

    function setPlayersField(snapshot) {
        const visible = snapshot.isOnline === true;
        const online = Number.isInteger(snapshot.onlinePlayers) ? snapshot.onlinePlayers : 0;
        const maximum = Number.isInteger(snapshot.maximumPlayers) ? snapshot.maximumPlayers : "?";

        forEachField("players", element => {
            element.hidden = !visible;
            if (!visible) {
                return;
            }

            const expectedText = `${online} of ${maximum} players online`;
            if (element.textContent.replace(/\s+/g, " ").trim() === expectedText) {
                return;
            }

            const onlineStrong = document.createElement("strong");
            onlineStrong.textContent = String(online);
            const maximumStrong = document.createElement("strong");
            maximumStrong.textContent = String(maximum);
            element.replaceChildren(
                onlineStrong,
                document.createTextNode(" of "),
                maximumStrong,
                document.createTextNode(" players online"));
        });
    }

    function applySnapshot(snapshot) {
        const isOnline = snapshot.isOnline === true;

        forEachField("badge", element => {
            element.textContent = isOnline ? "Online" : "Unavailable";
            element.classList.toggle("text-bg-success", isOnline);
            element.classList.toggle("text-bg-secondary", !isOnline);
            element.hidden = false;
        });

        const motd = typeof snapshot.motd === "string" ? snapshot.motd.trim() : "";
        setTextField("motd", motd, isOnline && motd.length > 0);

        const onlinePlayers = Number.isInteger(snapshot.onlinePlayers) ? String(snapshot.onlinePlayers) : "0";
        setTextField("online-players", onlinePlayers, isOnline);

        const maximumPlayers = Number.isInteger(snapshot.maximumPlayers) ? String(snapshot.maximumPlayers) : "?";
        setTextField("maximum-players", maximumPlayers, isOnline);

        setPlayersField(snapshot);

        const version = typeof snapshot.version === "string" ? snapshot.version.trim() : "";
        setTextField("version", version.length > 0 ? `Version ${version}` : "", isOnline && version.length > 0);

        forEachField("unavailable", element => {
            element.hidden = isOnline;
        });
    }

    async function refresh() {
        let nextDelay = defaultDelayMilliseconds;
        try {
            const response = await fetch(endpoint, {
                method: "GET",
                credentials: "same-origin",
                cache: "no-store",
                headers: { "Accept": "application/json" }
            });
            if (response.ok) {
                const snapshot = await response.json();
                applySnapshot(snapshot);
                if (Number.isFinite(snapshot.refreshAfterMilliseconds)) {
                    nextDelay = Math.max(1000, snapshot.refreshAfterMilliseconds);
                }
            }
        }
        catch {
            // Keep the last rendered snapshot when the browser can not refresh it.
        }
        finally {
            window.setTimeout(refresh, nextDelay);
        }
    }

    refresh();
})();
