using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CommonComponents.Signals;
using Zenject;

namespace Combat.Ai
{
	public sealed class AiManager : BackgroundTask, IAiManager, IInitializable, IDisposable, IFixedTickable
	{
	    public AiManager(CeasefireSignal ceasefireSignal, PlayerInputSignal playerInputSignal)
	    {
	        _ceasefireSignal = ceasefireSignal;
            _playerInputSignal = playerInputSignal;
	    }

		public void Add(IController item)
		{
			lock (_lockObject) 
			{
				_recentlyAddedControllers.Add(item);
			}
        }

        public void Initialize()
		{
            _ceasefireSignal.Event += OnCeasefire;
            _playerInputSignal.Event += OnPlayerInput;
            
            _currentFrame = 0;
			_lastFrame = -1;
			_fixedDeltaTime = UnityEngine.Time.fixedDeltaTime;

			StartTask();
		}

		public void Dispose()
		{
            _ceasefireSignal.Event -= OnCeasefire;
            _playerInputSignal.Event -= OnPlayerInput;
            
            UnityEngine.Debug.Log("AiManager.Dispose");
			StopTask();
		}

		public void FixedTick()
		{
#if UNITY_WEBGL
			_currentFrame++;
			DoWork();
#else
			System.Threading.Interlocked.Increment(ref _currentFrame);
#endif
		}

		protected override bool DoWork()
		{
			if (_currentFrame == _lastFrame)
				return false;

            lock (_lockObject) 
			{
				if (_recentlyAddedControllers.Count > 0) 
				{
					_controllers.AddRange (_recentlyAddedControllers);
					_recentlyAddedControllers.Clear();
				}
			}

			var needCleanup = false;
			var count = _controllers.Count;
			var deltaTime = _fixedDeltaTime * (_currentFrame - _lastFrame);            
            _options.DisableThreats = count > _maxControllersBeforeOptimization;
            _options.TimeSinceLastPlayerInput = _timeSinceLastPlayerInput;
            _timeSinceLastPlayerInput += deltaTime;

			#if !UNITY_WEBGL
			if (!_parallelAiDisabled && count >= ParallelControllerThreshold && MaxParallelism > 1)
			{
				// The original implementation moved all AI off Unity's main thread,
				// but still processed every ship sequentially on that single AI thread.
				// Controllers own their own behavior/context/control state, so large
				// battles can safely fan controller computation across a bounded number
				// of ThreadPool workers. Unity/Physics object lifetime remains on the
				// main simulation path; this only parallelizes the already-background AI.
				var cleanupFlag = 0;
				var options = _options;
				try
				{
					Parallel.For(0, count, ControllerParallelOptions, i =>
					{
						var controller = _controllers[i];
						if (!IsDead(controller))
							controller.Update(deltaTime, options);
						else
							Interlocked.Exchange(ref cleanupFlag, 1);
					});
				}
				catch (AggregateException exception)
				{
					// A legacy/custom AI node may still hide shared mutable state that
					// was harmless while every controller ran on one background thread.
					// Fail closed for the rest of this battle instead of throwing once
					// per fixed frame. Do not replay this frame serially because some
					// controllers may already have completed their update.
					_parallelAiDisabled = true;
					UnityEngine.Debug.LogWarning(
						"[Performance] Parallel AI disabled after worker failure; falling back to serial AI. " +
						exception.Flatten().InnerException?.Message);
				}
				needCleanup = cleanupFlag != 0;
			}
			else
			#endif
			{
				for (var i = 0; i < count; ++i)
				{
					var controller = _controllers[i];
					if (!IsDead(controller))
						controller.Update(deltaTime, _options);
					else
						needCleanup = true;
				}
			}

			if (needCleanup)
				_controllers.RemoveAll(IsDead);

			_lastFrame = _currentFrame;
			return true;
		}

		private static bool IsDead(IController controller) => controller.Status == ControllerStatus.Dead;

		protected override void OnIdle ()
		{
			System.Threading.Thread.Sleep((int)(_fixedDeltaTime*1000));
		}

		private void OnCeasefire(bool value)
		{
			_options.CeaseFire = value;
		}

        private void OnPlayerInput()
        {
            _timeSinceLastPlayerInput = 0;
        }

        private float _timeSinceLastPlayerInput;
		private bool _parallelAiDisabled;
		private int _currentFrame;
		private int _lastFrame;
		private float _fixedDeltaTime;
        private Options _options;

        private readonly object _lockObject = new object();
	    private readonly CeasefireSignal _ceasefireSignal;
        private readonly PlayerInputSignal _playerInputSignal;
		private readonly List<IController> _recentlyAddedControllers = new List<IController>();
		private readonly List<IController> _controllers = new List<IController>();

		private const int _maxControllersBeforeOptimization = 30;
		private const int ParallelControllerThreshold = 24;
		// Physics2D now uses Unity's worker pool as well. Keep AI fan-out bounded
		// to one coordinator + one helper worker so it does not oversubscribe the
		// same mobile CPU during physics barriers.
		private static readonly int MaxParallelism = Math.Max(1, Math.Min(2, Environment.ProcessorCount - 2));
		private static readonly ParallelOptions ControllerParallelOptions = new ParallelOptions
		{
			MaxDegreeOfParallelism = MaxParallelism,
		};

        public struct Options
        {
            public bool CeaseFire;
            public bool DisableThreats;
            public float TimeSinceLastPlayerInput;
        }
    }

    public class CeasefireSignal : Signal<CeasefireSignal, bool> { }
    public class PlayerInputSignal : Signal<PlayerInputSignal> { }
}
