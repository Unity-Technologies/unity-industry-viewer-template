# Changelog

All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](http://keepachangelog.com/en/1.0.0/)
and this project adheres to [Semantic Versioning](http://semver.org/spec/v2.0.0.html).

## [2.4.0] - 2026-07-28

### Added
- Metadata is now shown in a second tab within the Hierarchy tool panel, driven by the current hierarchy/3D selection (desktop and VR).
- Double-click a Hierarchy entry to zoom-to-focus the selected object (when the asset provides per-instance geometry bounds).
- The streaming tool panel can now be resized in VR by dragging its edge handle (works with controllers and hand-tracking), matching the desktop/tablet resize.
- Clicking a collaboration comment or its 3D annotation marker now focuses the camera to the viewpoint saved with the annotation (position, orientation, and field of view), matching the Unity Asset Manager web viewer. Works in orbit/fly/walk navigation and VR.
- In multi-model layouts, a spatial annotation (its 3D marker and saved camera viewpoint) now anchors to the specific model it was placed on and follows that model when it is moved, including across saved-layout reloads and multiplayer sessions; annotations on the primary model, and existing annotations, continue to behave as before.
- Annotation comments now support @-mentions: type `@` in the comment, reply, or edit composer to search your organization's members and tag them. Mentions are highlighted in the composer and stored in the Unity Cloud mention format, so the tagged member is notified and the mention renders correctly in the Asset Manager web viewer. Works with mouse and touch, hardware keyboards (arrow keys + Enter to pick a suggestion, including iPad keyboard covers), on-screen keyboards on tablets, and the system keyboard on VR headsets.

### Changed
- Retired the standalone Metadata tool; its functionality is now integrated into the Hierarchy tool.
- Annotation viewpoints are now stored and restored in the model's local space (consistent with the annotation marker and the web viewer) instead of world space, so saved camera views stay correct when the model is moved, rotated, or scaled.

### Fixed
- Opening a resolved annotation thread from the streaming scene's annotation tool now shows the full conversation (the thread's resolution entry silently aborted the list population).

## [2.3.1] - 2026-06-29

### Changed
- Refactored the Mobile AR and VR camera passthrough controllers to share a common `ARPlacementNavigationOption` base class, removing duplicated transform and surface-placement logic (internal refactor, no behavioral change).
- Refactored the Fly, Orbit and Walk camera navigation options to share a common `StandardCameraNavigationOption` base class, centralising their shared navigation-option lifecycle (internal refactor, no behavioral change).
- Reduced duplicated UI code by sharing the streaming tool-panel update logic, the navigation home-button and sensitivity-slider wiring, and the Vivox microphone-button setup across their respective controllers (internal refactor, no behavioral change).
- Reduced duplicated logic across the in-experience streaming tools by sharing the world-space-UI raycast occlusion check and the transform-gizmo handler wiring on `StreamToolControllerBase`, and added shared `UIUtility` helpers for stylesheet add/remove and the EventSystem-refresh workaround (internal refactor, no behavioral change).

### Fixed
- Fixed the VR transform-gizmo free-rotate handle using the controller's world position instead of its pointing direction, which caused erratic rotation.
- Various bug fixes across asset browsing, streaming tools, collaboration, multiplayer, VR, and deep linking (mainly null-reference and runtime-exception guards).
- Fixed several event-subscription and resource leaks.
- General stability and memory improvements.

## [2.3.0] - 2026-05-15
### Added
- Ability to pin certificate in a VPC environment.
- Log Console UI panel in In-App Settings.

### Changed
- Migrated Collaboration feature to `com.unity.cloud.collaboration` v0.5.0: updated all API calls to use reference-based overloads.
- Updated Unity Cloud packages.

### Fixed
- `NullReferenceException` in `PlatformServices.IsUserLoggedIn` caused by destruction order when stopping the editor.
- `NullReferenceException` in `AddModelToolUIController.OnDestroy` when `SharedUIManager` singleton was already destroyed.
- Faulted-task `AggregateException` risk in `CollaborationController.HandleRequest` by guarding with `IsCompletedSuccessfully` before accessing `.Result`.
- Null annotation list crash in `CollaborationUIBase.OnAnnotationLoaded`.
- AR Placement bugs.
- Various VR/XR UI fixes.

## [2.2.1] - 2026-04-02
### Fixes
- Fixed on the duplication of the sample folders

## [2.2.0] - 2026-03-31
### Added
- Support Android XR deployment.
- Interaction tool for interact object with 3D data streaming.

## [2.1.0] - 2026-02-06
### Fixes
- Addressed many minor bugs and applied general performance improvements across the application.

### Changed
- Support for WebGL platform
- Update Unity Cloud Packages
- Update Unity Editor Version

## [2.0.0] - 2025-11-17
### Fixes
- Addressed many minor bugs and applied general performance improvements across the application.

### Added
- Asset Creation for 3D data streaming.
- Deep Linking to share current asset or viewing sessions.
- Collaboration: Added comments on assets and 3D annotation.
- Sample Environments for viewing assets in pre-configured 3D settings.
- Support for Standalone VR (e.g., Meta Quest 3) with a new UI/UX design.
- Camera Passthrough support for Meta Quest 3 devices.
- Measurement tool for distance between two points.
- Cross-Section Cut tool for viewing model cross-sections.

## [1.0.2] - 2025-08-07

### Changed
- Fixed Resource Limiter only apply in WebGL

## [1.0.1] - 2025-06-12

### Changed
- Removed Git LFS


## [1.0.0] - 2025-06-03

### Added
- First version of the Industry Viewer Template
