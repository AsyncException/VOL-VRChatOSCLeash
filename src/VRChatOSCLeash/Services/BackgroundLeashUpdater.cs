using CommunityToolkit.Mvvm.Messaging;
using Serilog;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using VRChatOSCLeash.Messages;
using VRChatOSCLeash.Models;
using VRChatOSCLeash.Services.VRChatOSC;
using VRChatOSCLeash.Utilities;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace VRChatOSCLeash.Services;

public interface IBackgroundLeashUpdater
{
    Task StopProcess();
}

public sealed class BackgroundLeashUpdater : IRecipient<EmergencyStopMessage>, IRecipient<StartLeashUpdater>, IRecipient<StopLeashUpdater>, IBackgroundLeashUpdater
{
    private IVRChatOscClient Client { get; }
    private OSCParameters Leash { get; }
    private ThresholdSettings Thresholds { get; }
    private LeashData LeashData { get; }
    private ApplicationSettings Settings { get; }
    private IDebugLogger DebugLogger { get; }

    private readonly CancellationTokenSource f_cancellationTokenSource = new();

    private Task f_leashUpdateTask;

    public BackgroundLeashUpdater(IVRChatOscClient client, OSCParameters leashContext, ThresholdSettings thresholds, LeashData leashData, ApplicationSettings settings, IDebugLogger debugLogger) {
        (Client, Leash, Thresholds, LeashData, Settings, DebugLogger) = (client, leashContext, thresholds, leashData, settings, debugLogger);

        StrongReferenceMessenger.Default.Register<EmergencyStopMessage>(this);
        StrongReferenceMessenger.Default.Register<StartLeashUpdater>(this);
        StrongReferenceMessenger.Default.Register<StopLeashUpdater>(this);

        if (Settings.GlobalEnableLeash) {
            f_leashUpdateTask = LeashTask(f_cancellationTokenSource.Token);
            Log.Information("Started Leash background service");
            DebugLogger.LogApp("Started Leash background service");
        }
        else {
            f_leashUpdateTask = Task.CompletedTask;
            Log.Information("Leash background service is disabled");
            DebugLogger.LogApp("Leash background service is disabled");
        }
    }

    private async Task LeashTask(CancellationToken stoppingToken) {
        try {
            using PeriodicTimer timer = new(TimeSpan.FromMilliseconds(50));
            while (await timer.WaitForNextTickAsync(stoppingToken)) {

                if (!Thresholds.LeashEnabled) {
                    continue;
                }

                if (Settings.EnableToggleOnNullInput && await IsLeashReset()) {
                    continue;
                }

                LeashData currentData = LeashCalculator.GetLeashData(Leash, Thresholds, LeashData);

                if (currentData.Equals(LeashData)) {
                    continue;
                }

                LeashData.CopyFrom(currentData);
                Client.SendMovement(LeashData);
            }
        }
        catch (TaskCanceledException) { } // Ignore cancellation exceptions
        catch (OperationCanceledException) { } // Ignore cancellation exceptions
        catch (Exception ex) {
            Log.Error(ex, "An error occurred in the Leash background service");
            throw;
        }
    }


    private int f_resetCounter = 0;
    private async ValueTask<bool> IsLeashReset() {
        if (LeashCalculator.IsZeroColliderDistance(Leash)) {

            if (f_resetCounter < 3) {
                Client.SendParameter(OSCParameters.ENABLED, false);
                await Task.Delay(TimeSpan.FromSeconds(1));
                Client.SendParameter(OSCParameters.ENABLED, true);
                await Task.Delay(TimeSpan.FromSeconds(1));

                f_resetCounter++;

                Log.Information("Leash reset attempt {attempt}", f_resetCounter);
                DebugLogger.LogApp($"Leash reset attempt {f_resetCounter}");
                return false;
            }
            else if (f_resetCounter == 3) {
                Log.Information("Unable to reset leash");
                DebugLogger.LogApp($"Unable to reset leash");
                f_resetCounter++;

                return false;
            }

            return true;
        }
        else {
            f_resetCounter = 0;
            return false;
        }
    }

    /// <summary>
    /// This method is called when an EmergencyStopMessage is received. Cancelling the current movemnt and disabling both the leash and timer.
    /// </summary>
    /// <param name="message"></param>
    async void IRecipient<EmergencyStopMessage>.Receive(EmergencyStopMessage message) {
        Thresholds.LeashEnabled = false;
        Thresholds.TimerEnabled = false;

        await Task.Delay(50);

        LeashData data = new(); // Empty movement
        Client.SendMovement(data);

        Log.Warning("Emergency stop received");
        DebugLogger.LogApp($"Emergency stop received");
    }

    private readonly SemaphoreSlim f_semaphore = new(1, 1);

    /// <summary>
    /// Starts the leash updater task when a StartLeashUpdater message is received.
    /// </summary>
    /// <param name="message"></param>
    void IRecipient<StartLeashUpdater>.Receive(StartLeashUpdater message) {
        f_semaphore.Wait();

        try {
            f_cancellationTokenSource.TryReset();
            f_leashUpdateTask = LeashTask(f_cancellationTokenSource.Token);

            Log.Information("Started Leash background service");
            DebugLogger.LogApp("Started Leash background service");
        }
        catch (Exception ex) {
            Log.Error(ex, "Failed to start Leash background service");
            DebugLogger.LogApp("Failed to start Leash background service");
        }
        finally {
            f_semaphore.Release();
        }
    }

    /// <summary>
    /// Stops the leash updater task when a StopLeashUpdater message is received.
    /// </summary>
    /// <param name="message"></param>
    async void IRecipient<StopLeashUpdater>.Receive(StopLeashUpdater message) {
        await f_semaphore.WaitAsync();

        try {
            f_cancellationTokenSource.Cancel();
            await f_leashUpdateTask;
            f_leashUpdateTask = Task.CompletedTask;

            Log.Information("Stopped Leash background service");
            DebugLogger.LogApp("Stopped Leash background service");
        }
        catch (Exception ex) {
            Log.Error(ex, "Failed to stop Leash background service");
            DebugLogger.LogApp("Failed to stop Leash background service");
        }
        finally {
            f_semaphore.Release();
        }
    }

    /// <summary>
    /// Stops the background process and unregisters all messages.
    /// </summary>
    /// <returns></returns>
    public async Task StopProcess() {
        try {
            await f_cancellationTokenSource.CancelAsync();
            await f_leashUpdateTask;

            StrongReferenceMessenger.Default.UnregisterAll(this);

            Log.Information("Shutdown Leash backgound service");
        }
        catch (Exception ex) {
            Log.Error(ex, "Failed to stop Leash background service");
        }
    }
}

file class LeashCalculator
{
    public static float GetVerticalOffset(OSCParameters leash) => Math.Clamp((leash.FrontDistance - leash.BackDistance) * leash.Stretch, -1f, 1f);
    public static float GetHorizontalOffset(OSCParameters leash) => Math.Clamp((leash.RightDistance - leash.LeftDistance) * leash.Stretch, -1f, 1f);
    public static float GetHorizontalLook(OSCParameters leash, ThresholdSettings thresholds, float horizontalOffset) {
        if (leash.Stretch <= thresholds.TurningThreshold || leash.FrontDistance >= thresholds.TurningGoal) {
            return 0f;
        }

        float turn = thresholds.TurningMultiplier * horizontalOffset;
        turn = leash.RightDistance > leash.LeftDistance ? (turn += leash.BackDistance) : (turn -= leash.BackDistance);
        return Math.Clamp(turn, -1f, 1f);
    }
    public static bool ShouldRun(OSCParameters leash, ThresholdSettings thresholds, LeashData leashData) {
        bool shouldRun = leash.Stretch > thresholds.RunningMaxThreshold;

        if (leashData.ShouldRun && !shouldRun && leash.Stretch > thresholds.RunningMinThreshold) {
            shouldRun = true;
        }

        return shouldRun;
    }

    public static bool LeashActive(OSCParameters leash, ThresholdSettings thresholds) => leash.IsGrabbed && leash.Stretch > thresholds.StretchThreshold;

    public static LeashData GetLeashData(OSCParameters leash, ThresholdSettings thresholds, LeashData previous) {
        if (!LeashActive(leash, thresholds)) {
            return new LeashData();
        }

        float verticalOffset = GetVerticalOffset(leash);
        float horizontalOffset = GetHorizontalOffset(leash);
        float horizontalLook = GetHorizontalLook(leash, thresholds, horizontalOffset);
        bool shouldRun = ShouldRun(leash, thresholds, previous);
        return new LeashData { HorizontalLook = horizontalLook, HorizontalOffset = horizontalOffset, VerticalOffset = verticalOffset, ShouldRun = shouldRun };
    }

    public static bool IsZeroColliderDistance(OSCParameters leash) => leash.RightDistance == 0 && leash.LeftDistance == 0 && leash.FrontDistance == 0 && leash.BackDistance == 0;
}