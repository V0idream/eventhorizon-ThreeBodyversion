using System;
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
            ConfigureUnityJobWorkers();

#if UNITY_ANDROID && !UNITY_EDITOR
            TryRaiseMainThreadPriority();
            TryApplyMainThreadAffinity();
#endif
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
                if (workerCountProperty?.CanRead != true || workerCountProperty.CanWrite != true ||
                    maximumCountProperty?.CanRead != true)
                    return;

                var maximum = (int)maximumCountProperty.GetValue(null);
                var current = (int)workerCountProperty.GetValue(null);
                if (maximum <= 0)
                    return;
                var desired = Mathf.Clamp(SystemInfo.processorCount - 2, 1, maximum);
                if (desired > current)
                    workerCountProperty.SetValue(null, desired);

                Debug.Log($"[Performance] Job workers current={current}, desired={desired}, max={maximum}");
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

        private static void TryApplyMainThreadAffinity()
        {
            try
            {
                var mask = FindPerformanceCoreMask();
                if (mask == 0UL)
                    return;

                var threadId = gettid();
                var result = sched_setaffinity(threadId, (UIntPtr)sizeof(ulong), ref mask);
                if (result == 0)
                    Debug.Log($"[Performance] Main thread {threadId} affinity mask=0x{mask:X}");
                else
                    Debug.LogWarning($"[Performance] sched_setaffinity failed: {Marshal.GetLastWin32Error()}");
            }
            catch (Exception exception)
            {
                // Affinity is strictly best-effort: vendors may hide cpufreq
                // data or reject masks. Never make startup depend on it.
                Debug.LogWarning("[Performance] Unable to apply main-thread affinity: " + exception.Message);
            }
        }

        private static ulong FindPerformanceCoreMask()
        {
            var coreCount = Mathf.Clamp(SystemInfo.processorCount, 1, 64);
            var frequencies = new long[coreCount];
            var maximumFrequency = 0L;
            for (var core = 0; core < coreCount; ++core)
            {
                frequencies[core] = ReadMaximumFrequency(core);
                maximumFrequency = Math.Max(maximumFrequency, frequencies[core]);
            }

            if (maximumFrequency <= 0L)
                return 0UL;

            // Heterogeneous Android SoCs normally expose a cluster of big
            // cores within 80% of the highest advertised frequency. Pin only
            // the latency-sensitive Unity main thread; worker/render threads
            // remain free for Android and Unity to schedule independently.
            var threshold = maximumFrequency * 0.80;
            var mask = 0UL;
            var selected = 0;
            for (var core = 0; core < coreCount; ++core)
            {
                if (frequencies[core] <= 0L || frequencies[core] < threshold)
                    continue;
                mask |= 1UL << core;
                ++selected;
            }

            return selected >= 2 ? mask : 0UL;
        }

        private static long ReadMaximumFrequency(int core)
        {
            foreach (var name in new[] { "cpuinfo_max_freq", "scaling_max_freq" })
            {
                var path = $"/sys/devices/system/cpu/cpu{core}/cpufreq/{name}";
                if (File.Exists(path) && long.TryParse(File.ReadAllText(path).Trim(), out var frequency))
                    return frequency;
            }

            return 0L;
        }

        [DllImport("libc", SetLastError = true)]
        private static extern int sched_setaffinity(int pid, UIntPtr cpusetsize, ref ulong mask);

        [DllImport("libc")]
        private static extern int gettid();
#endif
    }
}
