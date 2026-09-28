import { createXrRuntime } from "./xr-runtime.js";

const DIAGNOSTIC_EXPERIENCE_ID = "framework-diagnostics";

function createDiagnosticExperience() {
    return {
        id: DIAGNOSTIC_EXPERIENCE_ID,
        initialize(context) {
            const { THREE, scene } = context;
            scene.background = new THREE.Color(0x111827);

            const ambient = new THREE.HemisphereLight(0xffffff, 0x334155, 2.5);
            scene.add(ambient);

            const cubeGeometry = new THREE.BoxGeometry(0.3, 0.3, 0.3);
            const cubeMaterial = new THREE.MeshStandardMaterial({ color: 0x60a5fa });
            const cube = new THREE.Mesh(cubeGeometry, cubeMaterial);
            cube.position.set(0, 1.45, -1.5);
            scene.add(cube);

            const grid = new THREE.GridHelper(6, 12, 0x94a3b8, 0x334155);
            scene.add(grid);

            const markerGeometry = new THREE.SphereGeometry(0.035, 12, 8);
            const markerMaterial = new THREE.MeshStandardMaterial({ color: 0xf8fafc });
            const rayGeometry = new THREE.BufferGeometry().setFromPoints([
                new THREE.Vector3(0, 0, 0),
                new THREE.Vector3(0, 0, -1)
            ]);
            const rayMaterial = new THREE.LineBasicMaterial({ color: 0x22d3ee });

            return {
                cube,
                inputVisuals: new Map(),
                markerGeometry,
                markerMaterial,
                rayGeometry,
                rayMaterial,
                disposables: [
                    cubeGeometry,
                    cubeMaterial,
                    markerGeometry,
                    markerMaterial,
                    rayGeometry,
                    rayMaterial
                ]
            };
        },
        update(context, frameContext) {
            const { THREE, scene, inputs, state } = context;
            state.cube.rotation.x = frameContext.time / 1800;
            state.cube.rotation.y = frameContext.time / 1200;

            const activeIds = new Set();
            for (const input of inputs.sources) {
                activeIds.add(input.id);
                let group = state.inputVisuals.get(input.id);
                if (!group) {
                    group = new THREE.Group();
                    group.add(new THREE.Mesh(state.markerGeometry, state.markerMaterial));
                    group.add(new THREE.Line(state.rayGeometry, state.rayMaterial));
                    scene.add(group);
                    state.inputVisuals.set(input.id, group);
                }

                const transform = input.targetRayPose?.transform;
                group.visible = Boolean(transform);
                if (transform) {
                    group.position.set(
                        transform.position.x,
                        transform.position.y,
                        transform.position.z
                    );
                    group.quaternion.set(
                        transform.orientation.x,
                        transform.orientation.y,
                        transform.orientation.z,
                        transform.orientation.w
                    );
                }
            }

            for (const [id, group] of state.inputVisuals) {
                if (!activeIds.has(id)) {
                    scene.remove(group);
                    state.inputVisuals.delete(id);
                }
            }
        },
        dispose(context) {
            const { scene, state } = context;
            for (const group of state.inputVisuals.values()) {
                scene.remove(group);
            }
            state.inputVisuals.clear();
            for (const disposable of state.disposables) {
                disposable.dispose?.();
            }
        }
    };
}

export function installXrDiagnosticsButton({
    parent = globalThis.document?.body,
    runtime = createXrRuntime()
} = {}) {
    if (!parent) {
        throw new Error("XR diagnostics require a document body or explicit parent element.");
    }

    const existing = globalThis.document?.getElementById("xr-framework-diagnostics");
    if (existing) {
        return { element: existing, runtime };
    }

    runtime.registerExperience(createDiagnosticExperience());

    const wrapper = globalThis.document.createElement("div");
    wrapper.id = "xr-framework-diagnostics";
    wrapper.className = "position-fixed bottom-0 end-0 m-3 p-2 bg-body border rounded shadow-sm";
    wrapper.style.zIndex = "1080";

    const button = globalThis.document.createElement("button");
    button.type = "button";
    button.className = "btn btn-sm btn-outline-primary";
    button.textContent = "Checking WebXR…";
    button.disabled = true;

    const status = globalThis.document.createElement("span");
    status.className = "small text-muted ms-2";
    status.textContent = "Detecting capabilities";

    wrapper.append(button, status);
    parent.appendChild(wrapper);

    runtime.addEventListener("started", () => {
        button.textContent = "Exit XR diagnostics";
        status.textContent = "XR session active";
    });
    runtime.addEventListener("stopped", () => {
        button.textContent = "Enter XR diagnostics";
        status.textContent = "Ready";
    });
    runtime.addEventListener("error", event => {
        status.textContent = event.detail?.error?.message ?? "XR frame failed";
    });

    button.addEventListener("click", async () => {
        button.disabled = true;
        try {
            if (runtime.isPresenting) {
                await runtime.stop();
            } else {
                status.textContent = "Starting XR session";
                await runtime.start(DIAGNOSTIC_EXPERIENCE_ID);
            }
        } catch (error) {
            status.textContent = error?.message ?? "XR session failed";
        } finally {
            button.disabled = false;
        }
    });

    void runtime.detectCapabilities().then(capabilities => {
        if (!capabilities.webXR) {
            button.textContent = "WebXR unavailable";
            status.textContent = capabilities.secureContext
                ? "Browser does not expose WebXR"
                : "Secure context required";
            return;
        }
        if (!capabilities.sessionModes.immersiveVr) {
            button.textContent = "Immersive VR unavailable";
            status.textContent = "WebXR is present, but immersive-vr is unsupported";
            return;
        }

        button.textContent = "Enter XR diagnostics";
        button.disabled = false;
        status.textContent = "Ready";
    }).catch(error => {
        button.textContent = "WebXR check failed";
        status.textContent = error?.message ?? "Capability detection failed";
    });

    return { element: wrapper, runtime };
}

export { DIAGNOSTIC_EXPERIENCE_ID };
