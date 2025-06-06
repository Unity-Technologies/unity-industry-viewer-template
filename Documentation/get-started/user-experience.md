# Understand the user experience

The following sections explain a standard user flow when opening a build of the Industry Viewer Template.

### Login

When opening the built-in Player for the first time, a login screen appears. Select **Log in** to open the SSO page and sign in to your Unity ID.

**Note**: This Unity ID must be associated with the same Unity Organization that's used to manage your assets.

### Asset explorer

The asset explorer displays an interactive list of available assets that you can stream. You can search for a particular asset name or sort by asset type. Choose an asset and select **Stream** to view your asset in the 3D scene.

**Note**: If the Unity ID is assigned to multiple organizations, you can select a specific organization using the **Organization** drop-down in the top-left corner of the Asset explorer.

### 3D Scene

The 3D Scene displays the selected asset, allowing you to explore and move around the asset as required. The following options and settings are available:

* **Camera Control**: There are three standard camera control modes: **Orbit**,  **Fly**, and **Walk** that are optimized for desktop and tablet platforms. Users can toggle between these modes by clicking the **Navigate Mode** button when running a built Player. Settings are provided to the user to customize each mode. For example, customizing move sensitivity or enabling joystick controls. The navigation controls use the [Input System](https://docs.unity3d.com/Packages/com.unity.inputsystem@latest), which allows you to rebind or remap buttons to fully customize the user experience.

  The template also includes a **Mobile AR Mode**, available exclusively on iOS and Android. In this mode, users can scan their surroundings to place objects on a horizontal plane. Once placed, fine adjustments can be made using on-screen UI controls or finger gestures.

  **Note**: If developing for iOS, it's recommended that you update the **Camera Usage Description** [Player setting](https://docs.unity3d.com/Manual/class-PlayerSettingsiOS.html) to display a message to the user when the application attempts to access the device camera.

* **Hierarchy and Asset Position:** Selecting the hierarchy option allows you to check the original asset structure and toggle the visibility of certain parts. When turning on the hierarchy option, you can move or rotate the selected asset by simply clicking the 3D parts or tabs inside the hierarchy panel. The degree of movement can also be adjusted through the level of the moving grid size.

* **Metadata**: The metadata option allows you to check the asset’s properties, such as BIM properties, from the original source file if available. You can also search by text for certain items. 

* **Adding Assets and Saving Layout**: By clicking the add asset button (**\+**), you can add more assets to the 3D scene. You can also save the layout with these additional assets back to the Unity Asset Manager for future review or visualization purposes. 

* **Multiplayer**: By streaming the same asset under the same organization and project, you can view other users in the same 3D scene with 3D avatars and voice chat.   
    
* **Settings**: A settings option is always visible, allowing you to customize options such as updating the build's refresh rate or changing the user language. Additional settings for voice input and output are shown if using multiplayer functionality. 