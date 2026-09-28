# WebXR framework boundary

The Site provides a thin, mode-agnostic WebXR foundation for future spatial experiences. It does not spatialize the normal Razor/Bootstrap interface and it does not define Dorks & Dice game interactions.

## Scope

The framework owns shared browser/runtime concerns that otherwise would be reimplemented by every XR consumer:

- WebXR and WebGL capability detection;
- immersive session lifecycle;
- Three.js renderer lifecycle;
- reference-space acquisition and fallback;
- normalized XR input-source state;
- select/squeeze input events;
- controller target-ray and grip poses;
- Gamepad button/axis snapshots and best-effort haptics;
- access to standard `XRHand` data and joint-pose lookup when exposed by the browser;
- an optional device-profile resolver layer for headset/controller-specific hints;
- experience registration, frame updates, rendering, disposal, and failure cleanup.

The framework deliberately does not own:

- spatial menus or application UI;
- locomotion conventions;
- grabbing or pointing semantics;
- gesture interpretation;
- eye-gaze semantics;
- game rules, campaigns, characters, maps, or combat behavior;
- multiplayer/avatar systems;
- vendor SDKs or vendor-specific runtime dependencies.

Those concerns belong to a mode, Tool, experience, or a later reusable interaction package when multiple consumers actually share the same behavior.

## Capability-first hardware model

XR operation must not depend on recognizing a headset or controller model. Standard WebXR capabilities and input data are authoritative.

`XrDeviceProfileRegistry` is a secondary optimization layer. Consumers may register resolvers that identify known hardware from standards-visible information such as `XRInputSource.profiles` or from other browser/runtime hints. Resolver failure is non-fatal and unresolved hardware is represented as unknown.

WebXR input profile strings describe compatible input configurations. They are useful hints, but they are not guaranteed to identify one physical device uniquely. A device resolver must therefore assign an appropriate confidence level and must not turn a profile match into a core compatibility requirement. For example, a runtime may intentionally expose one controller as compatible with another controller profile.

No vendor-specific resolver ships with the framework initially. Steam Frame-, Rift-, or other hardware-specific resolvers should be added only when the available standards-visible signals are reliable enough to justify the distinction.

This permits later hardware-specific tuning without making Steam Frame, Oculus Rift, Meta hardware, or any other vendor an architectural prerequisite.

## Modules

The framework is exposed as native browser ES modules under `/js/xr/`:

- `xr-capabilities.js` — capability detection;
- `xr-device-profiles.js` — optional hardware/profile resolver registry;
- `xr-input.js` — normalized input-source state and events;
- `xr-runtime.js` — session, renderer, reference-space, experience, and cleanup lifecycle;
- `xr-diagnostics.js` — opt-in manual hardware diagnostic experience.

Nothing is imported by the shared page layout. Ordinary non-XR page loads therefore pay no JavaScript or Three.js cost for this capability.

## Three.js

`xr-runtime.js` defaults to the pinned Three.js ES module at:

`https://cdn.jsdelivr.net/npm/three@0.186.1/build/three.module.js`

The module is loaded lazily only after an immersive session has been granted. The runtime constructor accepts `threeModuleUrl`, so the exact same framework can use a self-hosted module later without changing experience code.

The WebXR session request occurs before the dynamic Three.js import. This ordering is intentional because immersive `requestSession()` calls must remain in the browser's transient user-activation path.

A caller that wants to remove the first-session module-fetch delay may call `runtime.preloadRenderer()` before the user starts XR. Preloading does not request an XR session and remains optional; the shared Site does not do it automatically.

No Meta SDK, Horizon SDK, IWSDK, A-Frame, or headset-vendor package is part of the framework.

## Basic usage

```javascript
import { createXrRuntime } from "/js/xr/xr-runtime.js";

const xr = createXrRuntime();

xr.registerExperience({
    id: "example",
    initialize(context) {
        const geometry = new context.THREE.BoxGeometry(0.25, 0.25, 0.25);
        const material = new context.THREE.MeshNormalMaterial();
        const cube = new context.THREE.Mesh(geometry, material);
        cube.position.set(0, 1.4, -1.5);
        context.scene.add(cube);
        return { cube, geometry, material };
    },
    update(context, frame) {
        context.state.cube.rotation.y = frame.time / 1000;
    },
    dispose(context) {
        context.state.geometry.dispose();
        context.state.material.dispose();
    }
});

const capabilities = await xr.detectCapabilities();
```

A user-initiated click or equivalent activation should call:

```javascript
await xr.start("example", { mode: "immersive-vr" });
```

To end the active session:

```javascript
await xr.stop();
```

The runtime rejects overlapping starts, tracks a session that is still initializing, and performs cleanup if experience initialization or frame processing fails.

## Input contract

`context.inputs.sources` contains normalized snapshots for the currently connected WebXR input sources. Each snapshot includes:

- stable runtime-local `id`;
- `handedness`;
- `targetRayMode`;
- WebXR input `profiles`;
- optional device-profile hint;
- target-ray pose;
- grip pose when available;
- Gamepad axes/buttons when available;
- hand-tracking availability;
- the standard `XRHand` object when available;
- the native `XRInputSource` as an escape hatch for future standard features not yet normalized.

The manager emits `connected`, `disconnected`, `select`, `selectstart`, `selectend`, `squeeze`, `squeezestart`, and `squeezeend` events. It also exposes best-effort `pulse()` haptics and `getJointPose()` for standard WebXR hand joints.

The framework does not assign gameplay meaning to any of those inputs.

## Reference spaces

The renderer initially attaches with the WebXR-guaranteed `viewer` reference space, then requests the best available space in this order by default:

1. `local-floor`;
2. `bounded-floor`;
3. `local`;
4. `viewer`.

Experiences may provide a different preference order when starting a session. The selected type is available as `context.referenceSpaceType`.

## Permissions policy

The Site response policy explicitly permits `xr-spatial-tracking` for the same origin only. Camera, microphone, and geolocation remain disabled by the existing policy. This matches the current trusted same-origin Embedded Module model without granting XR tracking to arbitrary embedded origins.

## Manual hardware diagnostic

The diagnostic module is intentionally not added to normal navigation. On a page served by the Site, use the developer console to install a temporary button:

```javascript
const diagnostics = await import("/js/xr/xr-diagnostics.js");
diagnostics.installXrDiagnosticsButton();
```

The injected button performs capability detection. Clicking **Enter XR diagnostics** from the page provides the required user activation and starts a small scene with a rotating cube, floor grid, and tracked-pointer visualizations. The experience uses only the framework APIs and is suitable for smoke testing Rift, Frame, or another standards-compatible runtime.

## Evolution rule

Add behavior to the framework when it is genuinely shared XR infrastructure. Add hardware-specific behavior through device-profile hints or capability checks. Keep domain and interaction semantics in the experience that owns them until multiple consumers demonstrate a stable common abstraction.
