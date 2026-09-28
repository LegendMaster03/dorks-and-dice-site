function normalizeIdentityHint(hint, fallback = {}) {
    if (!hint || typeof hint !== "object") {
        return Object.freeze({ ...fallback });
    }

    return Object.freeze({
        ...fallback,
        ...hint,
        confidence: hint.confidence ?? fallback.confidence ?? "unknown"
    });
}

function runResolvers(resolvers, context, fallback, logger) {
    for (const entry of resolvers) {
        try {
            const result = entry.resolve(context);
            if (result) {
                return normalizeIdentityHint(result, {
                    ...fallback,
                    resolver: entry.id
                });
            }
        } catch (error) {
            logger?.warn?.(`XR device profile resolver '${entry.id}' failed.`, error);
        }
    }

    return normalizeIdentityHint(null, fallback);
}

export class XrDeviceProfileRegistry {
    constructor({ logger = globalThis.console } = {}) {
        this._logger = logger;
        this._headsetResolvers = [];
        this._inputResolvers = [];
    }

    registerHeadsetResolver(id, resolve) {
        return this._register(this._headsetResolvers, id, resolve);
    }

    registerInputResolver(id, resolve) {
        return this._register(this._inputResolvers, id, resolve);
    }

    resolveHeadset({ session, navigatorLike = globalThis.navigator } = {}) {
        return runResolvers(
            this._headsetResolvers,
            { session, navigatorLike },
            {
                family: null,
                vendor: null,
                model: null,
                confidence: "unknown",
                runtime: Object.freeze({
                    environmentBlendMode: session?.environmentBlendMode ?? null,
                    interactionMode: session?.interactionMode ?? null
                })
            },
            this._logger
        );
    }

    resolveInput(inputSource) {
        const profiles = Object.freeze(Array.from(inputSource?.profiles ?? []));
        return runResolvers(
            this._inputResolvers,
            { inputSource, profiles },
            {
                family: null,
                vendor: null,
                model: null,
                confidence: "unknown",
                profiles
            },
            this._logger
        );
    }

    _register(collection, id, resolve) {
        if (typeof id !== "string" || id.trim().length === 0) {
            throw new TypeError("An XR device profile resolver must have a non-empty id.");
        }
        if (typeof resolve !== "function") {
            throw new TypeError(`XR device profile resolver '${id}' must be a function.`);
        }
        if (collection.some(entry => entry.id === id)) {
            throw new Error(`XR device profile resolver '${id}' is already registered.`);
        }

        const entry = Object.freeze({ id, resolve });
        collection.push(entry);
        return () => {
            const index = collection.indexOf(entry);
            if (index >= 0) {
                collection.splice(index, 1);
            }
        };
    }
}

export function createDefaultXrDeviceProfileRegistry(options) {
    return new XrDeviceProfileRegistry(options);
}
