namespace GamepadMapperGUI.Models.Automation;

public static class AutomationPortCompatibility
{
    public static bool TypesMatch(AutomationPortType source, AutomationPortType target)
    {
        if (source == AutomationPortType.Any || target == AutomationPortType.Any)
            return true;

        return source == target;
    }
}
