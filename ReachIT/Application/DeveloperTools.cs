namespace ReachIT.Application;

public static class DeveloperTools
{
#if ENABLE_DEVELOPER_TOOLS
    public static bool IsEnabled => true;
#else
    public static bool IsEnabled => false;
#endif
}
