# Scene management

Two scenes control the user experience of the Industry Viewer Template. To access them, open the **Project** window and navigate to **Assets** > **Scenes**. 

**Note**: VR versions of each scene are available if you plan to run the viewer on an XR device. These are named Main VR and Streaming VR.

## Main scene

The **Main** scene handles the user login and allows users to select the assets they want to view. The scene contains multiple configurable GameObjects that control the main features of the Industry Viewer Template. The following table lists the GameObjects in the **Main** scene and their functions:

| **GameObject name** | **Description** |
| :---- | :---- |
| **UIDocument** | Controls the UI elements in the scene.  |
| **Platform Service** | Initializes all the required communication utilities  between the application and Unity Cloud services. |
| **Manager** | The Manager GameObject collects different controllers to manage services such as Identity, Assets, and Network Detector. |
| **Scene Controller** | Controls the transition between the Main scene and the Streaming scene. |
| **Network Manager** | Use the Network Manager GameObject to configure your network settings if using Unity Multiplayer services. |
| **In App Settings** | Use the In App Settings GameObject to control additional UI settings, such as displaying a frames per second indicator to the user. |

## Streaming scene

The **Streaming** scene allows users to navigate and explore selected assets in a 3D environment. The scene contains configurable GameObjects that control the navigation of assets in 3D space and Multiplay functionality.

| **GameObject name** | **Description** |
| :---- | :---- |
| **Navigation Controller** | The main controller that handles user navigation. Contains the controls for Orbit, Walk, and Fly navigation in the streaming scene. |
| **Stream Tool Controller** | Adds controls to manipulate the model, view metadata, and view the asset hierarchy.  |
| **Multiplay Controller** | Controls the connected multiplay services. **Note**: The **Multiplay Controller** GameObject is available only when [Multiplayer services](feature-management/multiplayer-services.md) are enabled. |
| **Vivox Controller** | Controls voice functionality through the [Vivox](https://docs.unity.com/ugs/en-us/manual/vivox-unity/manual/Unity/Unity) service. **Note**:The **Vivox Controller** GameObject is available only when [Multiplayer Services](feature-management/multiplayer-services.md) are enabled. |
