function copyTransform(transform) {
    if (!transform) {
        return null;
    }

    return Object.freeze({
        matrix: Object.freeze(Array.from(transform.matrix ?? [])),
        position: Object.freeze({
            x: transform.position?.x ?? 0,
            y: transform.position?.y ?? 0,
            z: transform.position?.z ?? 0
        }),
        orientation: Object.freeze({
            x: transform.orientation?.x ?? 0,
            y: transform.orientation?.y ?? 0,
            z: transform.orientation?.z ?? 0,
            w: transform.orientation?.w ?? 1
        })
    });
}

function copyPose(pose) {
    if (!pose) {
        return null;
    }

    return Object.freeze({
        emulatedPosition: pose.emulatedPosition === true,
        transform: copyTransform(pose.transform)
    });
}

function copyGamepad(gamepad) {
    if (!gamepad) {
        return null;
    }

    return Object.freeze({
        id: gamepad.id ?? "",
        index: gamepad.index ?? -1,
        mapping: gamepad.mapping ?? "",
        axes: Object.freeze(Array.from(gamepad.axes ?? [])),
        buttons: Object.freeze(Array.from(gamepad.buttons ?? [], button => Object.freeze({
            pressed: button.pressed === true,
            touched: button.touched === true,
            value: Number(button.value ?? 0)
        }))),
        hasHaptics: Boolean(
            gamepad.hapticActuators?.length
            || gamepad.vibrationActuator
        )
    });
}

function createPublicState(state) {
    return Object.freeze({
        id: state.id,
        handedness: state.handedness,
        targetRayMode: state.targetRayMode,
        profiles: state.profiles,
        deviceProfile: state.deviceProfile,
        targetRayPose: state.targetRayPose,
        gripPose: state.gripPose,
        gamepad: state.gamepad,
        hasGripSpace: Boolean(state.nativeSource.gripSpace),
        hasHandTracking: Boolean(state.nativeSource.hand),
        hand: state.nativeSource.hand ?? null,
        nativeSource: state.nativeSource
    });
}

const INPUT_EVENT_NAMES = Object.freeze([
    "select",
    "selectstart",
    "selectend",
    "squeeze",
    "squeezestart",
    "squeezeend"
]);

export class XrInputManager extends EventTarget {
    constructor(session, { deviceProfiles = null } = {}) {
        super();
        if (!session) {
            throw new TypeError("An XRSession is required to create an XR input manager.");
        }

        this.session = session;
        this.deviceProfiles = deviceProfiles;
        this._states = new Map();
        this._nextId = 1;
        this._listeners = [];

        this._listen("inputsourceschange", event => this._handleInputSourcesChange(event));
        for (const eventName of INPUT_EVENT_NAMES) {
            this._listen(eventName, event => this._handleInputEvent(eventName, event));
        }

        for (const source of session.inputSources ?? []) {
            this._addSource(source, false);
        }
    }

    get sources() {
        return Object.freeze(Array.from(this._states.values(), createPublicState));
    }

    get left() {
        return this.getByHandedness("left");
    }

    get right() {
        return this.getByHandedness("right");
    }

    get none() {
        return this.getByHandedness("none");
    }

    getByHandedness(handedness) {
        const state = Array.from(this._states.values())
            .find(candidate => candidate.handedness === handedness);
        return state ? createPublicState(state) : null;
    }

    getById(id) {
        const state = Array.from(this._states.values())
            .find(candidate => candidate.id === id);
        return state ? createPublicState(state) : null;
    }

    update(frame, referenceSpace) {
        if (!frame || !referenceSpace) {
            return this.sources;
        }

        for (const state of this._states.values()) {
            const source = state.nativeSource;
            state.targetRayPose = this._readPose(frame, source.targetRaySpace, referenceSpace);
            state.gripPose = this._readPose(frame, source.gripSpace, referenceSpace);
            state.gamepad = copyGamepad(source.gamepad);
        }

        return this.sources;
    }

    getJointPose(id, jointName, frame, referenceSpace) {
        const state = Array.from(this._states.values())
            .find(candidate => candidate.id === id);
        const jointSpace = state?.nativeSource.hand?.get?.(jointName);
        if (!jointSpace || !frame || !referenceSpace || typeof frame.getJointPose !== "function") {
            return null;
        }

        try {
            const pose = frame.getJointPose(jointSpace, referenceSpace);
            if (!pose) {
                return null;
            }

            return Object.freeze({
                radius: pose.radius ?? null,
                ...copyPose(pose)
            });
        } catch {
            return null;
        }
    }

    async pulse(id, intensity = 0.5, durationMs = 50) {
        const state = Array.from(this._states.values())
            .find(candidate => candidate.id === id);
        const gamepad = state?.nativeSource.gamepad;
        if (!gamepad) {
            return false;
        }

        const normalizedIntensity = Math.max(0, Math.min(1, Number(intensity) || 0));
        const normalizedDuration = Math.max(0, Number(durationMs) || 0);
        const actuator = gamepad.hapticActuators?.[0] ?? gamepad.vibrationActuator ?? null;
        if (!actuator) {
            return false;
        }

        try {
            if (typeof actuator.pulse === "function") {
                await actuator.pulse(normalizedIntensity, normalizedDuration);
                return true;
            }
            if (typeof actuator.playEffect === "function") {
                await actuator.playEffect("dual-rumble", {
                    duration: normalizedDuration,
                    strongMagnitude: normalizedIntensity,
                    weakMagnitude: normalizedIntensity
                });
                return true;
            }
        } catch {
            return false;
        }

        return false;
    }

    dispose() {
        for (const { type, listener } of this._listeners) {
            this.session.removeEventListener(type, listener);
        }
        this._listeners.length = 0;
        this._states.clear();
    }

    _listen(type, listener) {
        this.session.addEventListener(type, listener);
        this._listeners.push({ type, listener });
    }

    _addSource(source, dispatchEvent = true) {
        if (!source || this._states.has(source)) {
            return this._states.get(source) ?? null;
        }

        const state = {
            id: `xr-input-${this._nextId++}`,
            nativeSource: source,
            handedness: source.handedness ?? "none",
            targetRayMode: source.targetRayMode ?? "gaze",
            profiles: Object.freeze(Array.from(source.profiles ?? [])),
            deviceProfile: this.deviceProfiles?.resolveInput?.(source) ?? null,
            targetRayPose: null,
            gripPose: null,
            gamepad: copyGamepad(source.gamepad)
        };
        this._states.set(source, state);

        if (dispatchEvent) {
            this.dispatchEvent(new CustomEvent("connected", {
                detail: createPublicState(state)
            }));
        }
        return state;
    }

    _removeSource(source) {
        const state = this._states.get(source);
        if (!state) {
            return;
        }

        this.dispatchEvent(new CustomEvent("disconnected", {
            detail: createPublicState(state)
        }));
        this._states.delete(source);
    }

    _handleInputSourcesChange(event) {
        for (const source of event.removed ?? []) {
            this._removeSource(source);
        }
        for (const source of event.added ?? []) {
            this._addSource(source);
        }
    }

    _handleInputEvent(type, event) {
        const state = this._addSource(event.inputSource, false);
        this.dispatchEvent(new CustomEvent(type, {
            detail: Object.freeze({
                input: state ? createPublicState(state) : null,
                nativeEvent: event
            })
        }));
    }

    _readPose(frame, space, referenceSpace) {
        if (!space) {
            return null;
        }

        try {
            return copyPose(frame.getPose(space, referenceSpace));
        } catch {
            return null;
        }
    }
}
