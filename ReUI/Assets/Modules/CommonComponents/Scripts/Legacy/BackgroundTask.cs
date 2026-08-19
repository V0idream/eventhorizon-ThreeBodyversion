#if UNITY_WEBGL

public abstract class BackgroundTask
{
    protected abstract bool DoWork();
    protected abstract void OnIdle();
    public void StartTask(object owner = null) {}
    public void StopTask() {}
    public bool Paused { set { } }
}

#else

using System;
using System.Threading;

public abstract class BackgroundTask
{
    protected abstract bool DoWork();
    protected abstract void OnIdle();

    public void StartTask(object owner = null)
    {
        if (!_started)
        {
            _ownerReference = owner != null ? new WeakReference<object>(owner) : null;

            var thread = CreateThread();
            while (thread.ManagedThreadId == Thread.CurrentThread.ManagedThreadId)
            {
                UnityEngine.Debug.Log("Invalid thread id (" + thread.ManagedThreadId + ")");
                thread = CreateThread();
            }

            thread.Start(this);
            _started = true;
        }
    }

    private Thread CreateThread()
    {
        return new Thread(ThreadFunc)
        {
            Name = GetType().Name,
            // AiManager is currently the only BackgroundTask user in the game.
            // Keeping its coordinator slightly above normal helps Android avoid
            // parking the thread on a slow core while its parallel workers run.
            Priority = ThreadPriority.AboveNormal,
            IsBackground = true,
        };
    }

    public void StopTask() { _cancelled = true; }
    public bool Paused { set { _paused = value; } }

    private void ThreadFunc(object data)
    {
        var context = (BackgroundTask)data;

        UnityEngine.Debug.Log("background task started: " + GetType() + " (" + Thread.CurrentThread.ManagedThreadId + ")");

        while (!context._cancelled && (context._ownerReference == null || context._ownerReference.IsAlive))
        {
            while (context._paused)
            {
                Thread.Sleep(100);
            }

            try
            {
                if (!DoWork())
                {
                    OnIdle();
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogException(e);
            }
        }

        UnityEngine.Debug.Log("background task finished: " + GetType());
    }

    private WeakReference<object> _ownerReference;
    private bool _started = false;
    private bool _paused = false;
    private bool _cancelled = false;
}
#endif
