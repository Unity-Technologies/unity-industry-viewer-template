# User login

The [Unity Cloud Identity](https://docs.unity3d.com/Packages/com.unity.cloud.identity@latest) package powers the Login functionality. The Identity package integrates a single sign-on (SSO) service to the project to ensure that only authorized users with the correct account permissions can view your assets. 

The feature includes two key components: the **IdentityController** and the **IdentityUIController**. These controllers are located in **Assets** > **Features** > **Identity** > **Scripts**.

The **IdentityController** provides C# actions that the **IdentityUIController** uses to facilitate the login process. Additionally, an event listener is included to monitor and update the login status in real-time. Once a user successfully logs in, the controller retrieves their name and displays it in the top-right corner of the interface, offering a seamless and user-friendly login experience.

## Additional resources

* [Feature management](feature-management.md)