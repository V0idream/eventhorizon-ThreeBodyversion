using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using UnityEngine;

namespace ReUI.Runtime
{
    internal static class AndroidPerformanceBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            ConfigurePhysics2DJobs();
            ConfigureUnityJobWorkers();

#if UNITY_ANDROID && !UNITY_EDITOR
            TryRaiseMainThreadPriority();
            CreatePerformanceDriver();
#endif
        }

        private static void ConfigurePhysics2DJobs()
        {
            try
            {
                var options = Physics2D.jobOptions;
                options.useMultithreading = true;
                options.useConsistencySorting = false;

                // Keep the Unity 6-style batch sizes coarse. The previous
                // tuning split large combats into too many tiny jobs, which
                // raised scheduling/barrier overhead and produced hitching even
                // while aggregate CPU utilisation stayed below 100%.
                options.interpolationPosesPerJob = 100;
                options.newContactsPerJob = 30;
                options.collideContactsPerJob = 100;
                options.clearFlagsPerJob = 200;
                options.clearBodyForcesPerJob = 200;
                options.syncDiscreteFixturesPerJob = 50;
                options.syncContinuousFixturesPerJob = 50;
                options.findNearestContactsPerJob = 100;
                options.updateTriggerContactsPerJob = 100;
                options.islandSolverCostThreshold = 100;
                options.islandSolverBodiesPerJob = 50;
                options.islandSolverContactsPerJob = 50;

                Physics2D.jobOptions = options;

                Debug.Log(
                    "[Performance] Physics2D jobs enabled=" + Physics2D.jobOptions.useMultithreading +
                    $", newContacts/job={options.newContactsPerJob}, collide/job={options.collideContactsPerJob}, " +
                    $"islandBodies/job={options.islandSolverBodiesPerJob}");
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[Performance] Unable to configure Physics2D jobs: " + exception.Message);
            }
        }

        private static void ConfigureUnityJobWorkers()
        {
            try
            {
                var jobsUtility = Type.GetType(
                    "Unity.Jobs.LowLevel.Unsafe.JobsUtility, UnityEngine.CoreModule");
                jobsUtility ??= AppDomain.CurrentDomain.GetAssemblies()
                    .Select(assembly => assembly.GetType("Unity.Jobs.LowLevel.Unsafe.JobsUtility", false))
                    .FirstOrDefault(type => type != null);
                if (jobsUtility == null)
                    return;

                var flags = BindingFlags.Public | BindingFlags.Static;
                var workerCountProperty = jobsUtility.GetProperty("JobWorkerCount", flags);
                var maximumCountProperty = jobsUtility.GetProperty("JobWorkerMaximumCount", flags);
                if (workerCountProperty?.CanRead != true || maximumCountProperty?.CanRead != true)
                    return;

                var maximum = (int)maximumCountProperty.GetValue(null);
                var current = (int)workerCountProperty.GetValue(null);
                if (maximum <= 0)
                    return;

                // Do not override Unity's platform-selected worker count. With
                // Physics2D jobs, render threads and the AI ThreadPool active at
                // the same time, forcing processorCount-1 workers oversubscribed
                // mobile CPUs and increased context-switching/frame barriers.
                Debug.Log($"[Performance] Job workers platform-selected={current}, max={maximum}");
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[Performance] Unable to configure Unity job workers: " + exception.Message);
            }
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private static void TryRaiseMainThreadPriority()
        {
            try
            {
                using var process = new AndroidJavaClass("android.os.Process");
                // Android THREAD_PRIORITY_DISPLAY. This is less aggressive than
                // urgent-display/audio priorities and remains scheduler-managed.
                process.CallStatic("setThreadPriority", -4);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[Performance] Unable to raise main-thread priority: " + exception.Message);
            }
        }

        private static void TryCreatePerformanceHintSession()
        {
            try
            {
                ++_hintSessionAttempts;
                using var version = new AndroidJavaClass("android.os.Build$VERSION");
                _androidSdk = version.GetStatic<int>("SDK_INT");
                if (_androidSdk < 31)
                {
                    _hintSessionAttempts = MaxHintSessionAttempts;
                    return;
                }

                var threadIds = FindUnityPerformanceThreadIds();
                if (threadIds.Length == 0)
                    return;

                using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                using var manager = activity?.Call<AndroidJavaObject>("getSystemService", "performance_hint");
                if (manager == null)
                {
                    ScheduleHintSessionRetry();
                    return;
                }

                _targetWorkDurationNanos = CalculateTargetWorkDurationNanos();
                _preferredHintUpdateRateNanos = Math.Max(1_000_000L,
                    manager.Call<long>("getPreferredUpdateRateNanos"));
                _performanceHintSession = manager.Call<AndroidJavaObject>("createHintSession",
                    (object)threadIds, _targetWorkDurationNanos);
                if (_performanceHintSession == null)
                {
                    // PerformanceHintManager is present from API 31 onward,
                    // but individual devices may not support hint sessions.
                    // Do not retry every frame on such devices.
                    ScheduleHintSessionRetry();
                    return;
                }

                _performanceThreadIds = threadIds;

                if (_androidSdk >= 35)
                {
                    // Explicitly tell Android that this frame-critical workload
                    // should not prefer power-efficient placement. The scheduler
                    // remains free to pick cores/frequencies; we only provide the
                    // performance intent instead of pinning Unity to a core mask.
                    _performanceHintSession.Call("setPreferPowerEfficiency", false);
                }

                Debug.Log($"[Performance] ADPF hint session threads={threadIds.Length}, " +
                          $"target={_targetWorkDurationNanos / 1_000_000.0:F2}ms, " +
                          $"update={_preferredHintUpdateRateNanos / 1_000_000.0:F2}ms");
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[Performance] Unable to create ADPF performance hint session: " +
                                 exception.Message);
                DisposePerformanceHintSession();
                ScheduleHintSessionRetry();
            }
        }

        private static void CreatePerformanceDriver()
        {
            if (_driverCreated)
                return;

            _driverCreated = true;
            var gameObject = new GameObject("ReUI Android Performance Driver")
            {
                hideFlags = HideFlags.HideAndDontSave,
            };
            UnityEngine.Object.DontDestroyOnLoad(gameObject);
            gameObject.AddComponent<AndroidPerformanceDriver>();
        }

        internal static void TickPerformanceHints()
        {
            _performanceFrameCounter++;
            if (_performanceHintSession == null)
            {
                if (_hintSessionAttempts < MaxHintSessionAttempts &&
                    Time.realtimeSinceStartupAsDouble >= _nextHintSessionAttemptTime)
                    TryCreatePerformanceHintSession();
            }
            else if (_androidSdk >= 34 && _performanceFrameCounter % ThreadRefreshIntervalFrames == 0)
                RefreshPerformanceThreadGroup();

            if (_performanceHintSession == null)
                return;

            var now = Time.realtimeSinceStartupAsDouble;
            if (now < _nextHintReportTime)
                return;

            try
            {
                var targetDuration = CalculateTargetWorkDurationNanos();
                if (Math.Abs(targetDuration - _targetWorkDurationNanos) > 250_000L)
                {
                    _targetWorkDurationNanos = targetDuration;
                    _performanceHintSession.Call("updateTargetWorkDuration", _targetWorkDurationNanos);
                }

                FrameTimingManager.CaptureFrameTimings();
                var count = FrameTimingManager.GetLatestTimings(1u, FrameTimings);
                var cpuFrameTimeMs = count > 0 && FrameTimings[0].cpuFrameTime > 0.0
                    ? FrameTimings[0].cpuFrameTime
                    : Math.Max(0.1, Time.unscaledDeltaTime * 1000.0);
                var actualDurationNanos = Math.Max(100_000L,
                    Math.Min(100_000_000L, (long)(cpuFrameTimeMs * 1_000_000.0)));
                _performanceHintSession.Call("reportActualWorkDuration", actualDurationNanos);
                _nextHintReportTime = now + _preferredHintUpdateRateNanos / 1_000_000_000.0;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[Performance] ADPF performance hint disabled after runtime error: " +
                                 exception.Message);
                DisposePerformanceHintSession();
                ScheduleHintSessionRetry();
            }
        }

        internal static void ShutdownPerformanceHints()
        {
            DisposePerformanceHintSession();
        }

        private static long CalculateTargetWorkDurationNanos()
        {
            var displayRefreshRate = Mathf.RoundToInt((float)Screen.currentResolution.refreshRateRatio.value);
            if (displayRefreshRate <= 0)
                displayRefreshRate = 60;

            var requestedFrameRate = Application.targetFrameRate > 0
                ? Application.targetFrameRate
                : displayRefreshRate;
            var effectiveFrameRate = Mathf.Clamp(Mathf.Min(requestedFrameRate, displayRefreshRate), 30, 120);

            // Keep a small scheduling margin instead of asking Android merely
            // to meet the exact vsync deadline. This gives the scheduler room
            // to boost before a heavy combat frame becomes a visible hitch.
            return Math.Max(1_000_000L,
                (long)(1_000_000_000.0 / effectiveFrameRate * 0.90));
        }

        private static void ScheduleHintSessionRetry()
        {
            _nextHintSessionAttemptTime = Time.realtimeSinceStartupAsDouble + HintSessionRetrySeconds;
        }

        private static void RefreshPerformanceThreadGroup()
        {
            try
            {
                var threadIds = FindUnityPerformanceThreadIds();
                if (threadIds.Length == 0)
                    return;

                if (_performanceHintSession != null && _androidSdk >= 34 &&
                    !_performanceThreadIds.SequenceEqual(threadIds))
                {
                    _performanceHintSession.Call("setThreads", (object)threadIds);
                    _performanceThreadIds = threadIds;
                    Debug.Log($"[Performance] ADPF thread group refreshed: {threadIds.Length} threads");
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[Performance] Unable to refresh Unity performance threads: " +
                                 exception.Message);
            }
        }

        private static int[] FindUnityPerformanceThreadIds()
        {
            var threadIds = new HashSet<int> { gettid() };
            const string taskRoot = "/proc/self/task";
            if (!Directory.Exists(taskRoot))
                return threadIds.ToArray();

            foreach (var directory in Directory.GetDirectories(taskRoot))
            {
                if (!int.TryParse(Path.GetFileName(directory), out var threadId))
                    continue;

                try
                {
                    var threadName = File.ReadAllText(Path.Combine(directory, "comm")).Trim();
                    if (IsUnityPerformanceThread(threadName))
                        threadIds.Add(threadId);
                }
                catch (IOException)
                {
                    // A worker can disappear between enumerating /proc and
                    // reading its name. It will be reconsidered next refresh.
                }
            }

            return threadIds.OrderBy(id => id).Take(MaxPerformanceThreads).ToArray();
        }

        private static bool IsUnityPerformanceThread(string threadName)
        {
            if (string.IsNullOrEmpty(threadName))
                return false;

            return threadName.IndexOf("UnityMain", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   threadName.IndexOf("UnityGfx", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   threadName.IndexOf("RenderThread", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   threadName.IndexOf("Job.Worker", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   threadName.IndexOf("AiManager", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void DisposePerformanceHintSession()
        {
            if (_performanceHintSession == null)
                return;

            try
            {
                _performanceHintSession.Call("close");
            }
            catch (Exception)
            {
                // Best-effort shutdown only.
            }
            finally
            {
                _performanceHintSession.Dispose();
                _performanceHintSession = null;
            }
        }

        private const int ThreadRefreshIntervalFrames = 600;
        private const int MaxPerformanceThreads = 24;
        private const int MaxHintSessionAttempts = 3;
        private const double HintSessionRetrySeconds = 5.0;
        private static readonly FrameTiming[] FrameTimings = new FrameTiming[1];
        private static AndroidJavaObject _performanceHintSession;
        private static int[] _performanceThreadIds = Array.Empty<int>();
        private static int _androidSdk;
        private static int _performanceFrameCounter;
        private static int _hintSessionAttempts;
        private static bool _driverCreated;
        private static long _targetWorkDurationNanos = 15_000_000L;
        private static long _preferredHintUpdateRateNanos = 16_666_667L;
        private static double _nextHintReportTime;
        private static double _nextHintSessionAttemptTime;

        [DllImport("libc")]
        private static extern int gettid();
#endif
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    internal sealed class AndroidPerformanceDriver : MonoBehaviour
    {
        private void LateUpdate()
        {
            AndroidPerformanceBootstrap.TickPerformanceHints();
        }

        private void OnDestroy()
        {
            AndroidPerformanceBootstrap.ShutdownPerformanceHints();
        }
    }
#endif
}
