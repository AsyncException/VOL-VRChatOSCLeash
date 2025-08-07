using FastOSC;

namespace VRChatOSCLeash.Services.VRChatOSC;

public record VRChatOscMessage(string Address, object?[] Arguments, string ParameterName, OscMessageType MessageType) : OSCMessage(Address, Arguments)
{
    public static VRChatOscMessage Create(OSCMessage data) {

        OscMessageType messageType = data.Address.StartsWith(VRChatOscConstants.PARAMETER_PREFIX) ? OscMessageType.AvatarParameter : OscMessageType.Other;
        string parameterName = messageType is OscMessageType.AvatarParameter ? data.Address[VRChatOscConstants.PARAMETER_PREFIX.Length..] : string.Empty;
        return new VRChatOscMessage(data.Address, data.Arguments, parameterName, messageType);
    }

    public object ParameterValue => Arguments[0]!;
}

public enum OscMessageType
{
    AvatarParameter,
    Other
}