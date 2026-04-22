# Distribution Builds

This repo includes three batch entry points for development builds:

- `scripts/build-ios.sh`
- `scripts/build-android.sh`
- `scripts/build-web.sh`

They all call `Assets/Editor/BuildScripts.cs`, build whatever scenes are currently enabled in Unity Build Settings, and write artifacts under `Build/` by default.

If no scenes are enabled in Build Settings, the build fails immediately with a clear error. That is intentional: the scripts do not guess which scene should ship.

## Shared expectations

- Unity 6.4 must be installed with the platform support you are targeting.
- Set `UNITY_PATH` to the Unity editor executable, or make `Unity` available on `PATH`.
- The scripts are idempotent for their target outputs:
  - iOS and WebGL clear the target output directory before rebuilding.
  - Android overwrites the target `.apk` or `.aab`.
- Default output root: `Build`
- Common optional env vars:
  - `RESCUE_BUILD_OUTPUT_ROOT`
  - `RESCUE_BUILD_APP_IDENTIFIER`
  - `RESCUE_DEVELOPMENT_BUILD`
  - `RESCUE_ALLOW_DEBUGGING`
  - `RESCUE_CONNECT_PROFILER`

Representative performance targets for these dev builds:

- Android: mid-range Pixel 5a class device
- iOS: a roughly 2-year-old iPhone class device

## iPhone dev install

`build-ios.sh` exports an Xcode project. Unity does not finish iOS signing on its own.

1. Run `scripts/build-ios.sh` on macOS.
2. Open `Build/iOS/XcodeProject` in Xcode.
3. In Xcode, choose a development team and a unique bundle identifier if needed.
4. Connect the iPhone, trust the machine on the device, and select the phone as the run target.
5. Build and run from Xcode.

Notes:

- `xcodebuild` must be installed and available before the script runs.
- The generated Unity build is only the Xcode project. Final signing, provisioning, and install still happen in Xcode.

## Android dev install

Use a Pixel 5a class Android phone as the representative validation device.

1. Build an APK for quickest sideload iteration:

```bash
RESCUE_ANDROID_FORMAT=apk scripts/build-android.sh
```

2. Enable Developer Options and USB debugging on the phone.
3. Connect the device and confirm it appears in `adb devices`.
4. Install the artifact:

```bash
adb install -r Build/Android/rescue-grid-android-dev.apk
```

If you need an app bundle instead, build with:

```bash
RESCUE_ANDROID_FORMAT=aab scripts/build-android.sh
```

Notes:

- Unity needs Android Build Support installed.
- Unity also needs access to Android SDK / NDK / OpenJDK, either from Unity Hub modules or local configuration.
- The build script warns if `ANDROID_SDK_ROOT` and `ANDROID_HOME` are both unset, but it does not hard-fail because Unity can use embedded tooling.

## WebGL rapid iteration

For fast browser iteration:

1. Run `scripts/build-web.sh`.
2. Serve `Build/WebGL` with a local static file server. Example:

```bash
cd Build/WebGL
python -m http.server 8080
```

3. Open `http://localhost:8080` in a desktop browser.

Why use WebGL here:

- quickest no-install loop for designers and playtest prep
- easy screen recording and note-taking during balance iteration
- good fit for validating level flow, rescue order readability, and dock pressure without a device deploy every time

WebGL is the convenience build, not the final mobile performance truth. Validate device feel on the representative Android and iPhone hardware before calling a change ready.

## Telemetry collection after a playtest

Telemetry sessions are written as JSONL files under the app's `persistentDataPath` in a `telemetry/` folder.

Expected file shape:

- session logs: `telemetry/<session-id>.jsonl`
- captured loss replays: `telemetry/losses/*.jsonl`

Platform collection tips:

- iPhone:
  - In Xcode, open `Window > Devices and Simulators`.
  - Select the installed app/device entry.
  - Download the app container.
  - Pull the JSONL files from the container's `Documents/telemetry/` directory.
- Android:
  - Use `adb`:

```bash
adb pull /sdcard/Android/data/<application-id>/files/telemetry ./telemetry-android
```

- WebGL:
  - Browser persistence uses the WebGL sandbox, so treat WebGL telemetry as secondary.
  - Prefer device builds when you need durable playtest log collection.

If you need to inspect or replay a captured loss locally, the repo already includes replay tooling built around these JSONL files.
