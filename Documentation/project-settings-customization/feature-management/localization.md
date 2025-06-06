# Localize your project

The Industry Viewer Template uses the [Unity Localization Package](https://docs.unity3d.com/Packages/com.unity.localization@latest) to manage runtime translations. The package allows you to configure multiple languages that users can select when using the built project.

## Add a language

To add a new language to your project:

1. In the Assets folder of the [Project window](https://docs.unity3d.com/Manual/ProjectView.html), navigate to **Features > Localization**.  
2. Select the **Localization Settings** asset.  
3. Select **Add Locale** in the Inspector window.  
   1.  A panel will appear, allowing you to add the new language option. 

Once a new locale is added, you must ensure all assets have the corresponding string tables created. To create string tables:

1. Navigate to **Features > Assets > Localization.**   
2. Select the **`Assets.asset`** file.   
3. In the Inspector window, in the Missing Tables section, select **Create** for your new language.  
4. Select **Open In Table Editor.**

You can now input or edit translations for the new language to ensure the runtime experience is fully localized.

## Additional resources

* [Feature management](feature-management.md)