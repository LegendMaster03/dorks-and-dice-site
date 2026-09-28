const SESSION_MODES = Object.freeze([
    "inline",
    "immersive-vr",
    "immersive-ar"
]);

async function isSessionModeSupported(xrSystem, mode) {
    if (!xrSystem?.isSessionSupported) {
        return false;
    }

    try {
        return await xrSystem.isSessionSupported(mode);
    } catch {
        return false;
    }
}

function detectWebGl(documentLike) {
    if (!documentLike?.createElement) {
        return Object.freeze({ webGL: false, webGL2: false });
    }

    const canvas = documentLike.createElement("canvas");
    let webGL2 = false;
    let webGL = false;

    try {
        webGL2 = Boolean(canvas.getContext("webgl2"));
        webGL = webGL2 || Boolean(canvas.getContext("webgl"));
    } catch {
        webGL2 = false;
        webGL = false;
    }

    return Object.freeze({ webGL, webGL2 });
}

export async function detectXrCapabilities({
    navigatorLike = globalThis.navigator,
    documentLike = globalThis.document,
    secureContext = globalThis.isSecureContext === true
} = {}) {
    const xrSystem = navigatorLike?.xr ?? null;
    const webGl = detectWebGl(documentLike);
    const supportedModes = Object.fromEntries(
        await Promise.all(SESSION_MODES.map(async mode => [mode, await isSessionModeSupported(xrSystem, mode)]))
    );

    const sessionModes = Object.freeze({
        inline: supportedModes["inline"] === true,
        immersiveVr: supportedModes["immersive-vr"] === true,
        immersiveAr: supportedModes["immersive-ar"] === true
    });

    return Object.freeze({
        available: Boolean(xrSystem) && secureContext && webGl.webGL,
        secureContext,
        webXR: Boolean(xrSystem),
        webGL: webGl.webGL,
        webGL2: webGl.webGL2,
        sessionModes
    });
}

export { SESSION_MODES };
