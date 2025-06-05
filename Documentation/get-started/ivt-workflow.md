# Industry Viewer Template workflow

Use the following workflow to get started with the Industry Viewer Template:

1. [Prepare your assets for streaming](#prepare-your-assets-for-streaming)  
2. [Create a Unity Cloud project](#create-a-unity-cloud-project)  
3. [Open the Industry Viewer Template](#open-the-industry-viewer-template)  
4. [Explore assets in Play mode](#explore-assets-in-play-mode)

## Prepare your assets for streaming

To allow a user to discover and explore assets of any size and preserve the original  metadata and hierarchy, the Industry Viewer Template uses [3D Data Streaming](https://docs.unity3d.com/Packages/com.unity.cloud.data-streaming@1.7/manual/index.html). 3D Data Streaming allows for both large and small assets to be streamed  together in a single 3D scene, while managing performance and visual fidelity. For detailed steps on how to enable your asset to be ready for streaming, refer to [Asset preparation and management on Unity Cloud](../prepare-your-assets.md).

**Note**: To use 3D Data Streaming in your project, you must be either a Unity Industry or Cloud customer. For more information, refer to [Prerequisites - Unity Cloud](../prerequisites.md). You must also have an Asset Manager Project Contributor project role or higher assigned to your account to be able to manage and upload assets. For more information on user roles, refer to [Manage access to your project](https://docs.unity.com/cloud/en-us/asset-manager/manage-project-access).

## Create a Unity Cloud project

Create a project in Unity Cloud to ensure they're connected to the required services when you open them from the Unity Editor. For more information, refer to [Create a Unity Cloud project](https://docs.unity.com/cloud/en-us/projects/create-project).

## Open the Industry Viewer Template

To open the Industry Viewer Template:

1. Open the Unity Hub.  
2. Select **Add**.  
3. Select the folder where the Industry Viewer Template files are located. The template project is added to the Unity Hub and opens automatically.  
4. Ensure the project is connected to your Unity Cloud project. For more information, refer to [Configure a project for Unity Cloud](https://docs.unity.com/cloud/en-us/projects/configure-project-for-unity-cloud).

## Explore assets in Play mode

The Industry Viewer Template is ready to use once you open the project and enter Play mode. 

To quickly explore an asset:

1. From the **Project** window, navigate to **Assets** > **Scenes.**  
2. Open the **Main** scene.  
3. Use the **Play** button to enter Play mode. For information about Play mode, refer to [The Game view](https://docs.unity3d.com/Documentation/Manual/GameView.html).  
4. Select **Login**. A browser window will open.  
5. Login to your Unity Account and select **Allow login request**.   
6. From the Unity Editor, click **Select Organization** and select the cloud organization you want to use.  
7. Select an asset from the asset browser.  
8. Select **Stream.**

The Streaming scene loads, allowing you to navigate the asset in the  3D scene.

## Additional resources

* [Understand the user experience](user-experience.md)