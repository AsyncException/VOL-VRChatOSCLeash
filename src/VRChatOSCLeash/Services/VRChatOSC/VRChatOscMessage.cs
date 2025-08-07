using FastOSC;

namespace VRChatOSCLeash.Services.VRChatOSC;

public record VRChatOscMessage(string Address, object?[] Arguments, string ParameterName, OscMessageType MessageType) : OSCMessage(Address, Arguments)
{
    public static VRChatOscMessage Create(OSCMessage data) {
        OscMessageType messageType = data.Address switch {
            VRChatOscConstants.ADDRESS_AVATAR_CHANGE => OscMessageType.AvatarChange,
            VRChatOscConstants.ADDRESS_CHATBOX_INPUT => OscMessageType.ChatboxInput,
            _ when data.Address.StartsWith(VRChatOscConstants.ADDRESS_AVATAR_PARAMETERS_PREFIX) => OscMessageType.AvatarParameter,
            _ => OscMessageType.Other
        };

        string parameterName = messageType is OscMessageType.AvatarParameter ? data.Address[VRChatOscConstants.ADDRESS_AVATAR_PARAMETERS_PREFIX.Length..] : string.Empty;

        return new VRChatOscMessage(data.Address, data.Arguments, parameterName, messageType);
    }

    public bool IsAvatarChangeEvent => MessageType is OscMessageType.AvatarChange;
    public bool IsAvatarParameter => MessageType is OscMessageType.AvatarParameter;
    public bool IsChatboxInput => MessageType is OscMessageType.ChatboxInput;

    public object ParameterValue => Arguments[0]!;
}

public enum OscMessageType
{
    AvatarChange,
    AvatarParameter,
    ChatboxInput,
    Other
}