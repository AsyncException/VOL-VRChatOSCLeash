using CommunityToolkit.Mvvm.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using VRChatOSCLeash.Utilities;
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VRChatOSCLeash;
/// <summary>
/// An empty window that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class DebugWindow : Window
{
    public DebugLoggerContext DebugLoggerContext { get; }

    public DebugWindow() {
        InitializeComponent();
        ExtendsContentIntoTitleBar = true;
        DebugLoggerContext = Ioc.Default.GetRequiredService<DebugLoggerContext>();
        DebugLoggerContext.LogReceived += ScrollIntoView;
    }

    public void OnClose() {
        DebugLoggerContext.LogReceived -= ScrollIntoView;
    }

    private void ScrollIntoView(object? sender, (string latestMessage, string LogName)context) {
        (context.LogName switch {
            "AppLogs" => AppLogList,
            "ReceiveLogs" => ReceiveLogList,
            "SendLogs" => SendLogList,
            _ => throw new ArgumentException("Invalid log name", nameof(context))
        }).ScrollIntoView(context.latestMessage);
    }
}

public class DebugLogMessage(Guid id, string message) {
    public Guid Id { get; } = id;
    public string Message { get; } = message;
}