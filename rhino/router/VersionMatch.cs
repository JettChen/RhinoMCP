namespace RhMcp.Router;

public static class VersionMatch
{
    public static bool IsCompatible(string actual, string required)
    {
        if (actual == required)
            return true;
        return (actual, required) switch
        {
            ("9", "WIP") => true,
            ("WIP", "9") => true,
            _ => false,
        };
    }
}
