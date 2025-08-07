using CommunityToolkit.Mvvm.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using VRChatOSCLeash.Models;
using VRChatOSCLeash.Services.VRChatOSC;

// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace VRChatOSCLeash;

public sealed partial class ValueStatistics : UserControl
{
    public ApplicationSettings ApplicationSettings { get; } = Ioc.Default.GetRequiredService<ApplicationSettings>();
    public OSCParameters OscParameters { get; } = Ioc.Default.GetRequiredService<OSCParameters>();
    private LeashData LeashData { get; } = Ioc.Default.GetRequiredService<LeashData>();
    private TimerData TimerData { get; } = Ioc.Default.GetRequiredService<TimerData>();

    public ValueStatistics() => InitializeComponent();
}
