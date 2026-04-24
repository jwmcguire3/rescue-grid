# Capture Build

`scripts/build-capture.sh` builds the special capture variant with the `CAPTURE_BUILD` define enabled. In that build:

- the debug panel is stripped
- the committed [`L15` solve](../Assets/Resources/Levels/L15.solve.json) is the source of truth
- telemetry session writes are avoided because the debug telemetry host is not present
- the capture runner writes a small capture report instead: `persistentDataPath/capture/L15.capture.json`

## Exact L15 solve

The committed deterministic Level 15 path is:

1. tap `(5, 2)`
2. tap `(3, 5)`

Current solve metadata:

- level: `L15`
- seed: `5`
- alternate seed: `1005`
- expected outcome: `Win`

The capture runner verifies those taps through the real pipeline. If the sequence diverges, it logs a loud `[CaptureRunner] FAILURE ...` error and exits with failure on desktop platforms.

## Build

Desktop Windows capture build:

```bash
scripts/build-capture.sh
```

Optional target override:

```bash
RESCUE_CAPTURE_TARGET=android scripts/build-capture.sh
RESCUE_CAPTURE_TARGET=ios scripts/build-capture.sh
RESCUE_CAPTURE_TARGET=webgl scripts/build-capture.sh
```

Outputs land under `Build/Capture/`.

## Replay Repeatedly

Desktop:

```bash
scripts/record-l15.sh
```

That launches the capture app with `-capture-l15`, which runs the exact committed solve inside the player.

Mobile:

- build the capture variant for `android` or `ios`
- install it normally
- launch the app

On mobile capture builds, `CaptureRunner` auto-starts `L15` on app launch because command-line launch args are not practical on-device.

## Recording

Desktop:

- start your screen recorder first
- run `scripts/record-l15.sh`
- let the app play the two-action solve
- stop recording after the frozen win state is visible

Android:

- build with `RESCUE_CAPTURE_TARGET=android scripts/build-capture.sh`
- install the APK
- start Android screen recording or `adb shell screenrecord`
- launch the app and let the auto-run capture solve finish

iOS:

- build with `RESCUE_CAPTURE_TARGET=ios scripts/build-capture.sh`
- sign and install from Xcode
- start iOS screen recording or record from QuickTime on macOS
- launch the app and let the auto-run capture solve finish

## Marketing Reference

After a successful run, the capture runner writes:

- `persistentDataPath/capture/L15.capture.json`

That report includes:

- seed
- ordered tap list
- per-step event type list
- extracted target order
- final outcome

The player log also includes `[CaptureRunner]` lines for:

- the exact action path
- per-step event summaries
- success or failure
