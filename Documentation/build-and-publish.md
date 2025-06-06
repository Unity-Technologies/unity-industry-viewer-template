# Build and publish

Use [build profiles](https://docs.unity3d.com/Documentation/Manual/build-profiles.html) to create a build of your configured Industry Viewer Template to share with users across multiple platforms.

You can publish a build of the Industry Viewer Template to the following platforms:

* [Windows](https://docs.unity3d.com/Documentation/Manual/Windows.html)
* [macOS](https://docs.unity3d.com/Manual/AppleMac.html)
* [iOS](https://docs.unity3d.com/Manual/iphone.html)
* [Android](https://docs.unity3d.com/Manual/android.html)

Note that [Unity Cloud Identity](https://docs.unity3d.com/Packages/com.unity.cloud.identity@latest) SDK uses a Unity Cloud application namespace to allow redirection from the webpage back to the runtime after successful login and log out. The default namespace is `com.unity.industry-viewer`. Because the URL redirection uses the namespace to relaunch the runtime, having multiple runtimes that use the same namespace on your local machine might affect the redirection from the URL. The recommended best practice is to [create a new namespace](https://docs.unity3d.com/Packages/com.unity.cloud.common@latest?subfolder=/manual/unity-cloud-app-namespace.html) each time you build.

This template also includes a script called `PreBuildNamespace.cs` that creates a unique namespace in each build based on the App bundle ID and the current time of the build process to avoid using the same namespace during testing and development.
