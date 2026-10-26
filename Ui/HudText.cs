namespace Cards.Ui;

/// <summary>Centre-screen messages for the local player.</summary>
internal static class HudText
{
    private static System.Reflection.MethodInfo? _message;

    /// <summary>
    /// Character.Message without binding to one signature: Valheim 1.0 added a parameter, and a direct call compiled
    /// against the older API stubs throws at runtime.
    /// </summary>
    public static void Tell(Character? character, string text)
    {
        if (character == null || string.IsNullOrEmpty(text))
            return;
        _message ??= System.Array.Find(typeof(Character).GetMethods(), m => m.Name == "Message"
            && m.GetParameters().Length >= 2 && m.GetParameters()[0].ParameterType == typeof(MessageHud.MessageType));
        if (_message == null)
            return;
        var parameters = _message.GetParameters();
        var args = new object?[parameters.Length];
        args[0] = MessageHud.MessageType.Center;
        args[1] = text;
        for (var i = 2; i < parameters.Length; i++)
            args[i] = parameters[i].HasDefaultValue ? parameters[i].DefaultValue
                : parameters[i].ParameterType.IsValueType ? System.Activator.CreateInstance(parameters[i].ParameterType) : null;
        _message.Invoke(character, args);
    }

    public static void Say(string text) => Tell(Player.m_localPlayer, text);
}
