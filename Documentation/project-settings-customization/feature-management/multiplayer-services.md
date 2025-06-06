# Enable multiplayer services

To use the multiplayer features to explore assets together in your project, you must configure and connect to the multiplayer services. 

To connect to multiplayer services, you must first [Configure a project for Unity Cloud](https://docs.unity.com/cloud/en-us/projects/configure-project-for-unity-cloud). Once complete, enable or disable the use of Multiplay in your project.

To enable or disable the use of Multiplay in your project:

1. In the Editor, navigate to **Tools** > **Multiplay.**  
2. Select one of the following options:  
   1. **Enable Multiplay for all platforms**  
   2. **Enable Multiplay for the current platform**

When selecting **Enable Multiplay for all platforms** the tool adds the `ENABLE_MULTIPLAY` scripting define symbol to the platform Player settings of all available target platforms in your project. Selecting **Enable Multiplay for all platforms** or **Enable Multiplay for the current platform** adds the `ENABLE_MULTIPLAY` scripting define symbol to the currently active target platform.  
	  
To disable multiplayer features, navigate to **Tools** > **Multiplay** and use one of the disable options. Alternatively, you can deactivate the **Network Manager** in the **Main** scene, and the **Multiplay Controller** and **Vivox Controller** GameObjects in the **Streaming** scene.

## Additional resources

* [Feature management](feature-management.md)