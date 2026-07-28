# Understand user controls, settings, and navigation

The 3D Scene displays the selected asset, allowing you to explore and move around the asset as required.

**Note**: When streaming the same asset under the same organization and project, you can view other users in the same 3D scene with 3D avatars and voice chat. Multiplayer services must be enabled to use this service. Refer to [Enable multiplayer services](../project-settings-customization/feature-management/multiplayer-services.md) for more information.

The following options and settings are available:

## Navigation

### Camera Control

There are three standard camera control modes: **Orbit**,  **Fly**, and **Walk** that are optimized for desktop and tablet platforms.
Users can toggle between these modes by clicking the **Navigate Mode** button when running a built Player. 
Settings are provided to the user to customize each mode. For example, customizing move sensitivity or enabling joystick controls. 
The navigation controls use the [Input System](https://docs.unity3d.com/Packages/com.unity.inputsystem@latest), which allows you to rebind or remap buttons to fully customize the user experience.

### Mobile AR Mode

The template also includes a **Mobile AR Mode**, available exclusively on iOS and Android. In this mode, users can scan their surroundings to place objects on a horizontal plane. Once placed, fine adjustments can be made using on-screen UI controls or finger gestures.

**Note**: If developing for iOS, it's recommended that you update the **Camera Usage Description** [Player setting](https://docs.unity3d.com/Manual/class-PlayerSettingsiOS.html) to display a message to the user when the application attempts to access the device camera.

### XR Support

If running on an XR device, users can navigate the 3D scene using standard XR controls. Pass-through mode is also supported on compatible devices, allowing users to look into their real-world surroundings and interact with a 3D asset. Users can scan their environment to place the asset on a horizontal plane, then use standard XR controls to move around and interact with the asset.

## Asset management

Use the following options to manage and communicate information about the asset in the 3D scene.

### Metadata

The metadata option allows you to check the asset’s properties, such as BIM properties, from the original source file if available. You can also search by text for certain items. Refer to [Asset metadata](../project-settings-customization/feature-management/asset-metadata.md) for more information.

### Hierarchy

Selecting the hierarchy option allows you to check the original asset structure and toggle the visibility of certain parts. When turning on the hierarchy option, you can move or rotate the selected asset by clicking the 3D parts or tabs inside the hierarchy panel. The degree of movement can also be adjusted through the level of the moving grid size.

### Annotation

The annotation option allows you to attach comments directly to the asset in the 3D scene. You can add text and attach images or files to the annotation if required.

You can also mention other members of your organization in a comment: type `@` followed by a name to open a suggestion list, then pick a member with the mouse, with the arrow keys and **Enter**, or by tapping the entry. Mentions are highlighted in the comment text and are stored in the `:user[name]{#id}` format that Unity Cloud uses to notify the mentioned member.

### Tool

The tool options, represented by a briefcase icon, allows you to access various tools for asset manipulation and interaction.

For more information, refer to [Explore the Tool Options menu](tool-options.md).

## Additional settings

### Add additional assets

By clicking the **Folder** icon, you can add more assets to the 3D scene. You can also save the layout with these additional assets back to the Unity Asset Manager for future review or visualization purposes.

### Share

Select the **Share** icon to generate a shareable link to the current asset view. Share this link with other users, allowing them to access the same asset view in their own instance of the Industry Viewer Template.

### Settings menu

Use the **Settings** (Cog) menu to customize various aspects of the user experience. The settings menu is available in both the viewer and the main asset selection screen.

The following settings are available:

| **Setting** | **Description** |
| :---- | :---- |
| **Version** | Displays the current version of the Industry Viewer Template. |
| **Refresh Rate** | Adjusts the refresh rate to optimize performance. You can adjust the refresh rate from 30 fps to 60 fps. |
| **Show FPS** | Toggles a display of the current frames per second (FPS). |
| **Language** | Allows you to select the language for the user interface. For more information on customizing the language settings, refer to the [Localize your project](../project-settings-customization/feature-management/localization.md). |
| **Offline Mode** | Enable offline mode to view previously downloaded assets. |
| **Camera control settings** | Customize the camera control settings for **Orbit**, **Fly**, and **Walk** modes. These options change depending on the selected mode. |
| **Wireframe** | Toggles the wireframe view of the asset in the 3D scene. For more information, refer to [Wireframe mode](../project-settings-customization/feature-management/wireframe-mode.md).|


