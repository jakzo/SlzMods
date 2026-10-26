# Isolated button capture

BONEWORKS uses SteamVR actions. In that client, the legacy `GetControllerState`
API can return an unchanged packet even while the action buttons work. This
small Windows x64 background OpenVR client reads the legacy physical states
independently, without calling `UpdateActionState` or changing the game's action
manifest, bindings, or focus.

The mod builds and embeds the executable, extracts it into a content-hashed
`UserData/BoneworksFrameFix/native` directory, and starts it without a window.
It polls at the configured tracking sample rate using the same cancellable,
high-resolution timer as head tracking. It stops when the parent exits or signals
shutdown. Failures leave the game's action-timestamp fallback available.

The shared-memory ring stores 4096 timestamped left/right physical states and
validity flags. Publication sequence checks prevent torn reads. The game drains
the history before applying fixed-tick button events, so a rendering stall does
not discard quick presses or double-clicks. Scene and controller changes discard
old queued inputs. Custom SteamVR action remaps still need validation.

The helper uses the fixed `IVRSystem_019` C function table from Valve's OpenVR
1.0.17 header: controller role index 18, controller state index 34, Windows x64
controller state size 64 bytes, pressed mask offset 8. This is a versioned ABI,
not offsets into an arbitrary current runtime interface.

Windows native QPC timestamps have a common epoch across processes, but Unity's
Mono `Stopwatch` has a different epoch from the helper's .NET Framework runtime.
`NativeInputClock` calibrates the conversion in the game. Recorded edges keep
their original time through buffering and fixed-tick replay.

Reference: [Valve's OpenVR 1.0.17 C API](https://github.com/ValveSoftware/openvr/blob/v1.0.17/headers/openvr_capi.h).
