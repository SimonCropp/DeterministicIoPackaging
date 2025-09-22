public static class ModuleInitializer
{
    [ModuleInitializer]
    public static void InitializeOther()
    {
        VerifierSettings.UniqueForTargetFramework();
        VerifierSettings.InitializePlugins();
    }
}