# Asset streaming

Assets from the Asset Manager are streamed using the [Unity Cloud Data Streaming package](https://docs.unity3d.com/Packages/com.unity.cloud.data-streaming@1.10/manual/index.html). The package enables you to stream and render only the specific parts of a model to the user cameras. 

To understand how assets can be streamed, refer to the **StreamingModelController** GameObject located in **Assets** > **Features** > **Streaming** > **Scripts**. The **StreamingModelController** GameObject provides methods and actions to start the streaming process and dynamically add additional models. This streamlined approach enhances performance while maintaining flexibility for complex 3D scenes.

## Start asset streaming

You can start asset streaming by passing in an **IAsset** or an **IDataSet** with a streamable tag. A **Streaming Stage** is created when streaming begins, allowing multiple assets to be added into a single stage. Each asset within the stage can be independently moved, rotated, and scaled. 

## Additional resources

* [Feature management](feature-management.md)