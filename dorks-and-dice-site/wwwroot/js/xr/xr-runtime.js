import { detectXrCapabilities } from "./xr-capabilities.js";
import { createDefaultXrDeviceProfileRegistry } from "./xr-device-profiles.js";
import { XrInputManager } from "./xr-input.js";

export const DEFAULT_THREE_MODULE_URL = "https://cdn.jsdelivr.net/npm/three@0.186.1/build/three.module.js";

const DEFAULT_OPTIONAL_FEATURES = Object.freeze([
    "local-floor",
    "bounded-floor",
    "hand-tracking",
    "layers"
]);

const DEFAULT_REFERENCE_SPACES = Object.freeze([
    "local-floor",
    "bounded-floor",
    "local",
    "viewer"
]);

function uniqueStrings(values) {
    return Array.from(new Set(values.filter(value => typeof value === "string" && value.length > 0)));
}

async function requestPreferredReferenceSpace(session, preferredTypes) {
    let lastError = null;
    for (const type of preferredTypes) {
        try {
            return {
                type,
                space: await session.requestReferenceSpace(type)
            };
        } catch (error) {
            lastError = error;
        }
    }

    throw lastError ?? new Error("The XR runtime could not acquire a reference space.");
}

function createSessionCapabilities(session, renderer) {
    return Object.freeze({
        environmentBlendMode: session.environmentBlendMode ?? null,
        interactionMode: session.interactionMode ?? null,
        enabledFeatures: Object.freeze(Array.from(session.enabledFeatures ?? [])),
        foveation: renderer.xr.getFoveation?.() ?? null,
        hasDepthSensing: renderer.xr.hasDepthSensing?.() === true
    });
}

function createRuntimeEvent(type, detail) {
    return new CustomEvent(type, { detail });
}

export class XrRuntime extends EventTarget {
    constructor({
        threeModuleUrl = DEFAULT_THREE_MODULE_URL,
        deviceProfiles = createDefaultXrDeviceProfileRegistry(),
        navigatorLike = globalThis.navigator,
        documentLike = globalThis.document,
        logger = globalThis.console
    } = {}) {
        super();
        this.threeModuleUrl = threeModuleUrl;
        this.deviceProfiles = deviceProfiles;
        this.navigatorLike = navigatorLike;
        this.documentLike = documentLike;
        this.logger = logger;
        this._experiences = new Map();
        this._active = null;
        this._threePromise = null;
    }

    get active() {
        return this._active?.context ?? null;
    }

    get isPresenting() {
        return Boolean(this._active);
    }

    async detectCapabilities() {
        return detectXrCapabilities({
            navigatorLike: this.navigatorLike,
            documentLike: this.documentLike,
            secureContext: globalThis.isSecureContext === true
        });
    }

    registerExperience(experience) {
        if (!experience || typeof experience !== "object") {
            throw new TypeError("An XR experience definition is required.");
        }
        if (typeof experience.id !== "string" || experience.id.trim().length === 0) {
            throw new TypeError("An XR experience must have a non-empty id.");
        }
        if (this._experiences.has(experience.id)) {
            throw new Error(`XR experience '${experience.id}' is already registered.`);
        }

        this._experiences.set(experience.id, experience);
        return () => {
            if (this._active?.experience === experience) {
                throw new Error(`XR experience '${experience.id}' can not be unregistered while it is active.`);
            }
            this._experiences.delete(experience.id);
        };
    }

    async start(experienceId, {
        mode = "immersive-vr",
        requiredFeatures = [],
        optionalFeatures = DEFAULT_OPTIONAL_FEATURES,
        referenceSpaces = DEFAULT_REFERENCE_SPACES
    } = {}) {
        if (this._active) {
            throw new Error("An XR session is already active.");
        }

        const experience = this._experiences.get(experienceId);
        if (!experience) {
            throw new Error(`XR experience '${experienceId}' is not registered.`);
        }

        const xrSystem = this.navigatorLike?.xr;
        if (!xrSystem?.requestSession) {
            throw new Error("WebXR is not available in this browser context.");
        }

        // requestSession must remain directly in the user-activation path. Do not add
        // asynchronous capability probes before this call.
        const session = await xrSystem.requestSession(mode, {
            requiredFeatures: uniqueStrings(requiredFeatures),
            optionalFeatures: uniqueStrings(optionalFeatures)
        });

        let renderer = null;
        let inputs = null;
        let canvas = null;
        let context = null;
        let referenceSpace = null;

        try {
            const THREE = await this._loadThree();
            canvas = this.documentLike.createElement("canvas");
            canvas.setAttribute("aria-hidden", "true");
            canvas.tabIndex = -1;

            renderer = new THREE.WebGLRenderer({
                canvas,
                antialias: true,
                alpha: mode === "immersive-ar",
                powerPreference: "high-performance"
            });
            renderer.xr.enabled = true;

            // 'viewer' is guaranteed by WebXR. A better reference space is selected
            // immediately after the session is attached and then injected into Three.js.
            renderer.xr.setReferenceSpaceType("viewer");
            await renderer.xr.setSession(session);

            const requestedReferenceSpace = await requestPreferredReferenceSpace(
                session,
                uniqueStrings(referenceSpaces.length ? referenceSpaces : DEFAULT_REFERENCE_SPACES)
            );
            referenceSpace = requestedReferenceSpace.space;
            renderer.xr.setReferenceSpace(referenceSpace);

            const scene = new THREE.Scene();
            const camera = new THREE.PerspectiveCamera(70, 1, 0.01, 1000);
            inputs = new XrInputManager(session, {
                deviceProfiles: this.deviceProfiles
            });

            context = {
                runtime: this,
                THREE,
                mode,
                session,
                renderer,
                scene,
                camera,
                canvas,
                referenceSpace,
                referenceSpaceType: requestedReferenceSpace.type,
                inputs,
                headsetProfile: this.deviceProfiles?.resolveHeadset?.({
                    session,
                    navigatorLike: this.navigatorLike
                }) ?? null,
                sessionCapabilities: createSessionCapabilities(session, renderer),
                state: undefined
            };

            context.state = await experience.initialize?.(context);

            const active = {
                experience,
                session,
                renderer,
                inputs,
                canvas,
                context,
                cleanupPromise: null
            };
            this._active = active;

            session.addEventListener("end", () => {
                void this._cleanup(active, "session-ended");
            }, { once: true });

            renderer.setAnimationLoop((time, frame) => {
                if (!frame || this._active !== active) {
                    return;
                }

                try {
                    const currentReferenceSpace = renderer.xr.getReferenceSpace?.() ?? referenceSpace;
                    inputs.update(frame, currentReferenceSpace);
                    const frameContext = Object.freeze({
                        time,
                        frame,
                        referenceSpace: currentReferenceSpace
                    });

                    experience.update?.(context, frameContext);
                    if (typeof experience.render === "function") {
                        experience.render(context, frameContext);
                    } else {
                        renderer.render(scene, camera);
                    }
                } catch (error) {
                    this.logger?.error?.(`XR experience '${experience.id}' failed during a frame.`, error);
                    this.dispatchEvent(createRuntimeEvent("error", Object.freeze({
                        experienceId: experience.id,
                        error
                    })));
                    renderer.setAnimationLoop(null);
                    void this.stop();
                }
            });

            this.dispatchEvent(createRuntimeEvent("started", context));
            return context;
        } catch (error) {
            inputs?.dispose?.();
            renderer?.setAnimationLoop?.(null);
            renderer?.dispose?.();
            canvas?.remove?.();
            try {
                await session.end();
            } catch {
                // The session may already have ended while initialization was failing.
            }
            throw error;
        }
    }

    async stop() {
        const active = this._active;
        if (!active) {
            return;
        }

        try {
            await active.session.end();
        } catch {
            // Cleanup still needs to run if the runtime reports an already-ended session.
        }
        await this._cleanup(active, "runtime-stop");
    }

    async _loadThree() {
        if (!this._threePromise) {
            this._threePromise = import(this.threeModuleUrl)
                .catch(error => {
                    this._threePromise = null;
                    throw error;
                });
        }
        return this._threePromise;
    }

    async _cleanup(active, reason) {
        if (!active || active.cleanupPromise) {
            return active?.cleanupPromise;
        }

        active.cleanupPromise = (async () => {
            active.renderer?.setAnimationLoop?.(null);
            try {
                await active.experience.dispose?.(active.context);
            } catch (error) {
                this.logger?.error?.(`XR experience '${active.experience.id}' failed during disposal.`, error);
            }

            active.inputs?.dispose?.();
            active.renderer?.dispose?.();
            active.canvas?.remove?.();

            if (this._active === active) {
                this._active = null;
            }
            this.dispatchEvent(createRuntimeEvent("stopped", Object.freeze({
                experienceId: active.experience.id,
                reason
            })));
        })();

        return active.cleanupPromise;
    }
}

export function createXrRuntime(options) {
    return new XrRuntime(options);
}

export {
    DEFAULT_OPTIONAL_FEATURES,
    DEFAULT_REFERENCE_SPACES
};
