using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Galaxy;
using GameServices.Player;
using GameStateMachine.States;
using Services.Messenger;
using Services.ObjectPool;
using Zenject;

public class GalaxyMap : MonoBehaviour
{
    [Inject] private readonly StartTravelSignal.Trigger _startTravelTrigger;
    [Inject] private readonly IMessenger _messenger;
    [Inject] private readonly IObjectPool _objectPool;
    [Inject] private readonly StarMap _starMap;
    [Inject] private readonly StarData _starData;
    [Inject] private readonly MotherShip _motherShip;
    [Inject] private readonly PlayerSkills _playerSkills;

	[SerializeField] private Gui.Windows.AnimatedWindow _noFuelBalloon;
	[SerializeField] private Map.MapScaler _mapScaler;
	[SerializeField] private Map.MapNavigator _mapNavigator;
	//[SerializeField] private Map.ScreenCenter _screenCenter;

    public float DistanceBetweenStars = 5;
	public ShipRange Boundary;
	public GameObject StarPrefab;
	public ViewModel.HomeStarPanelViewModel HomeStarPanel;
	public ViewModel.FlightConfirmationViewModel ConfirmationDialog;
	public StarSystem.StarSystem StarSystem;
	public PlayerShipObject PlayerShipObject;

	private Dictionary<int, Star> _stars = new Dictionary<int, Star>();
	private Dictionary<int, Star> _starsOld = new Dictionary<int, Star>();

	private void Awake()
	{
		PlayerShipObject.MovedEvent += OnShipMoved;
		StarSystem.MovedEvent += OnShipMoved;
		Boundary.gameObject.SetActive(!ThreeBodySkillState.HyperspaceEngineUnlocked);
		if (Boundary.gameObject.activeSelf)
			Boundary.transform.localScale = Vector3.one * DistanceBetweenStars * _motherShip.FlightRange;
	}

	private void Start()
	{
		_messenger.AddListener<int>(EventType.PlayerPositionChanged, OnPlayerPositionChanged);
		_messenger.AddListener<int>(EventType.FocusedPositionChanged, OnFocusedPositionChanged);

		_messenger.AddListener<ViewMode>(EventType.ViewModeChanged, OnMapStateChanged);
		_messenger.AddListener<int>(EventType.StarContentChanged, OnStarContentChanged);
		_messenger.AddListener<int, int>(EventType.PrismBeamFired, OnPrismBeamFired);
		_messenger.AddListener(EventType.StarMapContentChanged, OnStarMapContentChanged);

		OnPlayerPositionChanged(_motherShip.Position);
		OnMapStateChanged(_motherShip.ViewMode);

		_mapNavigator.MoveImmediately();

		UpdateVisibleStars();
	}

	public void OnClick(Vector2 position)
	{
		if (_motherShip.ViewMode == ViewMode.StarSystem)
		{
			StarSystem.OnClick(position);
			return;
		}

		//TODO: if (_gameLogic.CurrentPlayerState != PlayerState.Idle)
		//	return;

		var currentStarId = _motherShip.Position;
		var radius = _motherShip.ViewMode == ViewMode.GalaxyMap ? 1.6f*DistanceBetweenStars : 0.4f*DistanceBetweenStars;

		foreach (Transform child in transform)
		{
			if (Vector2.Distance(child.position, position) < radius && child.tag == "Star")
			{
				var starId = System.Convert.ToInt32(child.name);
				if (starId == currentStarId)
					continue;

				if (_motherShip.IsOutOfFuel)
					_noFuelBalloon.Open();

				if (!_motherShip.IsStarReachable(starId))
					Boundary.Refresh();
				else
                    _startTravelTrigger.Fire(starId);

                break;
			}
		}
	}

    public void OnStarContentChanged(int starId)
    {
        if (starId == _motherShip.CurrentStar.Id)
            UpdateCurrentStar();
        else
            UpdateStar(starId, false);
    }

	public void UpdateCurrentStar()
	{
		var star = _motherShip.CurrentStar;
		UpdateStar(star.Id);
		if (StarSystem.IsActive)
		{
			StarSystem.Cleanup();
			StarSystem.Initialize(star, Star.GetStarColor(star.Id));
		}
	}

	public void OnPositionChanged()
    {
		UpdateVisibleStars();
    }

	private void OnMapStateChanged(ViewMode state)
	{
		if (state == ViewMode.StarSystem)
		{
			var star = _motherShip.CurrentStar;
			var color = Star.GetStarColor(star.Id);

			StarSystem.gameObject.Move(star.Position*DistanceBetweenStars);
			StarSystem.Initialize(star, color);
		}
		else if (StarSystem.gameObject.activeSelf)
		{
			StarSystem.Cleanup();
			_mapNavigator.SetFocus(_motherShip.CurrentStar.Position * DistanceBetweenStars);
		}

        PlayerShipObject.gameObject.SetActive(state == ViewMode.StarMap || state == ViewMode.GalaxyMap);
		_mapNavigator.Go();
		
		UpdateVisibleStars();
	}

    private void OnFocusedPositionChanged(int starId)
    {
		var position = starId >= 0 ? _starData.GetPosition(starId) : _motherShip.CurrentStar.Position;
		_mapNavigator.SetFocus(position * DistanceBetweenStars);
	}

	private void UpdateStar(int starId, bool createIfNotActive = true)
	{
		var currentStar = new Galaxy.Star(starId, _starData);
		
		Star star;
		if (_stars.TryGetValue(starId, out star))
		{
			star.Deinitialize();
			star.Initialize(currentStar);
		}
		else if (createIfNotActive)
		{
			_stars[starId] = CreateStar(currentStar);
		}
	}	

	private void OnPlayerPositionChanged(int starId)
	{
		UpdateStar(starId);
		var position = _starData.GetPosition(starId) * DistanceBetweenStars;

		Boundary.gameObject.Move(position);
		_mapNavigator.SetFocus(position);
	}

	private void OnShipMoved(Vector2 position)
	{
		Boundary.Hide();
		_mapNavigator.SetFocus(position);
    }

	private void OnPrismBeamFired(int sourceStarId, int targetStarId)
	{
		StartCoroutine(PlayPrismBeam(sourceStarId, targetStarId));
	}

	private IEnumerator PlayPrismBeam(int sourceStarId, int targetStarId)
	{
		// The facility panel can be opened from a zoomed galaxy view (or while
		// the star-system view is still active). Force the normal regional star
		// map before creating the projectile so the complete route is visible.
		_motherShip.ViewMode = ViewMode.StarMap;
		_mapScaler.ShowStarMap();
		yield return null;

		var beamObject = new GameObject("PrismBeamAnimation");
		beamObject.transform.SetParent(transform, false);
		var shader = Shader.Find("Sprites/Additive") ?? Shader.Find("Sprites/Default");
		if (shader == null)
		{
			Debug.LogError("[Prism] No compatible line shader is available.");
			Destroy(beamObject);
			yield break;
		}

		var material = new Material(shader)
		{
			name = "PrismBeamRuntimeMaterial"
		};
		var glow = CreatePrismLine(beamObject, material, 198,
			new Color(0.08f, 0.55f, 1f, 0.32f), new Color(0.35f, 0.9f, 1f, 0.75f), "Glow");
		var core = CreatePrismLine(beamObject, material, 200,
			new Color(1f, 1f, 0.92f, 1f), new Color(0.65f, 1f, 1f, 1f), "Core");
		var headFlare = CreatePrismLine(beamObject, material, 201,
			new Color(1f, 1f, 0.82f, 1f), new Color(0.45f, 0.95f, 1f, 1f), "HeadFlare");
		headFlare.numCapVertices = 16;

		var source = _starData.GetPosition(sourceStarId) * DistanceBetweenStars;
		var target = _starData.GetPosition(targetStarId) * DistanceBetweenStars;
		var travelDirection = (target - source).normalized;
		const float duration = 1.35f;
		var elapsed = 0f;
		Boundary.Hide();
		_mapNavigator.Go();
		_mapNavigator.SetFocus(source);
		_mapNavigator.MoveImmediately();
		UpdateVisibleStars(true);
		var nextMapRefresh = 0f;
		while (elapsed < duration)
		{
			elapsed += Time.unscaledDeltaTime;
			var progress = Mathf.Clamp01(elapsed / duration);
			var easedProgress = Mathf.SmoothStep(0f, 1f, progress);
			var head = Vector2.Lerp(source, target, easedProgress);
			var tailProgress = Mathf.Clamp01((progress - 0.28f) / 0.72f);
			var tail = Vector2.Lerp(source, target, Mathf.SmoothStep(0f, 1f, tailProgress));
			glow.SetPosition(0, tail);
			glow.SetPosition(1, head);
			core.SetPosition(0, tail);
			core.SetPosition(1, head);
			var pulse = 0.88f + 0.12f * Mathf.Sin(elapsed * 42f);
			glow.widthMultiplier = Mathf.Lerp(3.8f, 1.1f, progress) * pulse;
			core.widthMultiplier = Mathf.Lerp(1.15f, 0.34f, progress);
			headFlare.SetPosition(0, head - travelDirection * 0.055f);
			headFlare.SetPosition(1, head + travelDirection * 0.055f);
			headFlare.widthMultiplier = Mathf.Lerp(5.4f, 1.8f, progress) * pulse;
			_mapNavigator.SetFocus(head);
			_mapNavigator.MoveImmediately();
			if (elapsed >= nextMapRefresh)
			{
				nextMapRefresh = elapsed + 0.08f;
				UpdateVisibleStars(true);
			}
			yield return null;
		}

		_mapNavigator.SetFocus(target);
		_mapNavigator.MoveImmediately();
		UpdateVisibleStars(true);
		yield return PlayPrismImpactExplosion(beamObject, material, target);
		Destroy(material);
		Destroy(beamObject);
	}

	private IEnumerator PlayPrismImpactExplosion(GameObject owner, Material material, Vector2 target)
	{
		var outerRing = CreatePrismRing(owner, material, 205, "ImpactOuterRing", 48);
		var innerRing = CreatePrismRing(owner, material, 206, "ImpactInnerRing", 40);
		const int rayCount = 10;
		var rays = new LineRenderer[rayCount];
		for (var i = 0; i < rayCount; i++)
		{
			rays[i] = CreatePrismLine(owner, material, 204,
				new Color(0.7f, 0.95f, 1f, 1f), new Color(0.15f, 0.65f, 1f, 0f),
				"ImpactRay" + i);
			rays[i].numCapVertices = 8;
		}

		const float explosionDuration = 0.72f;
		var elapsed = 0f;
		while (elapsed < explosionDuration)
		{
			elapsed += Time.unscaledDeltaTime;
			var progress = Mathf.Clamp01(elapsed / explosionDuration);
			var expansion = 1f - (1f - progress) * (1f - progress);
			var alpha = 1f - progress;
			var pulse = 0.88f + 0.12f * Mathf.Sin(elapsed * 55f);

			UpdatePrismRing(outerRing, target, Mathf.Lerp(0.35f, 8.5f, expansion));
			UpdatePrismRing(innerRing, target, Mathf.Lerp(0.2f, 4.8f, expansion));
			outerRing.widthMultiplier = Mathf.Lerp(1.5f, 0.08f, progress) * pulse;
			innerRing.widthMultiplier = Mathf.Lerp(2.1f, 0.12f, progress) * pulse;
			outerRing.startColor = outerRing.endColor = new Color(0.2f, 0.8f, 1f, alpha * 0.9f);
			innerRing.startColor = innerRing.endColor = new Color(1f, 1f, 0.86f, alpha);

			for (var i = 0; i < rays.Length; i++)
			{
				var angle = (360f / rayCount * i + progress * 16f) * Mathf.Deg2Rad;
				var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
				var startRadius = Mathf.Lerp(0.08f, 1.6f, expansion);
				var endRadius = Mathf.Lerp(1.3f, 11f, expansion);
				rays[i].SetPosition(0, target + direction * startRadius);
				rays[i].SetPosition(1, target + direction * endRadius);
				rays[i].widthMultiplier = Mathf.Lerp(1.15f, 0.03f, progress) * pulse;
				rays[i].startColor = new Color(1f, 1f, 0.88f, alpha);
				rays[i].endColor = new Color(0.12f, 0.65f, 1f, 0f);
			}

			_mapNavigator.SetFocus(target);
			_mapNavigator.MoveImmediately();
			yield return null;
		}
	}

	private static LineRenderer CreatePrismRing(GameObject owner, Material material, int sortingOrder,
		string layerName, int points)
	{
		var layer = new GameObject(layerName);
		layer.transform.SetParent(owner.transform, false);
		var ring = layer.AddComponent<LineRenderer>();
		ring.useWorldSpace = false;
		ring.positionCount = points;
		ring.loop = true;
		ring.numCapVertices = 8;
		ring.numCornerVertices = 4;
		ring.sortingOrder = sortingOrder;
		ring.sharedMaterial = material;
		ring.alignment = LineAlignment.View;
		return ring;
	}

	private static void UpdatePrismRing(LineRenderer ring, Vector2 center, float radius)
	{
		for (var i = 0; i < ring.positionCount; i++)
		{
			var angle = Mathf.PI * 2f * i / ring.positionCount;
			ring.SetPosition(i, center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
		}
	}

	private static LineRenderer CreatePrismLine(GameObject owner, Material material, int sortingOrder,
		Color startColor, Color endColor, string layerName)
	{
		// LineRenderer does not reliably support multiple instances on one
		// GameObject on every Unity backend. Separate children keep all three
		// layers alive in Android player builds.
		var layer = new GameObject(layerName);
		layer.transform.SetParent(owner.transform, false);
		var line = layer.AddComponent<LineRenderer>();
		line.useWorldSpace = false;
		line.positionCount = 2;
		line.numCapVertices = 8;
		line.textureMode = LineTextureMode.Stretch;
		line.sortingOrder = sortingOrder;
		line.sharedMaterial = material;
		line.startColor = startColor;
		line.endColor = endColor;
		line.alignment = LineAlignment.View;
		return line;
	}

    private void OnStarMapContentChanged()
    {
        UpdateVisibleStars(true);
    }

	private void UpdateVisibleStars(bool forceUpdate = false)
	{
		var camera = Camera.main;
		var screenSize = new Vector2(camera.orthographicSize*camera.aspect, camera.orthographicSize);
		var topLeft = -(Vector2)transform.localPosition - screenSize;
		var bottomRight = -(Vector2)transform.localPosition + screenSize;

		var temp = _starsOld;
		_starsOld = _stars;
		_stars = temp;
		_stars.Clear();

		IEnumerable<Galaxy.Star> allStars;

		switch (_mapScaler.SuitableViewMode)
		{
			case ViewMode.StarMap:
				allStars = _starMap.GetVisibleStars(topLeft / DistanceBetweenStars, bottomRight / DistanceBetweenStars);
				break;
			case ViewMode.GalaxyMap:
				allStars = _starMap.GetGalaxyViewVisibleStars(topLeft / DistanceBetweenStars, bottomRight / DistanceBetweenStars);
				break;
			case ViewMode.StarSystem:
			default:
				allStars = Enumerable.Repeat(_motherShip.CurrentStar, 1);
				break;
		}

		foreach (var item in allStars)
		{
			Star star;
			if (_starsOld.TryGetValue(item.Id, out star))
			{
				_starsOld.Remove(item.Id);

				if (forceUpdate)
				{
					star.Deinitialize();
					star.Initialize(item);
				}
				
				_stars.Add(item.Id, star);
			}
			else
			{
				_stars.Add(item.Id, CreateStar(item));
			}
		};

		foreach (var item in _starsOld)
			DestroyStar(item.Value.GetComponent<Star>());

		var position = _starData.GetPosition(0)*DistanceBetweenStars;
		var homeStarVisible = topLeft.x > position.x != bottomRight.x > position.x && topLeft.y > position.y != bottomRight.y > position.y;
		
		var direction = _mapNavigator.Center - position;
		HomeStarPanel.SetDistance(direction/DistanceBetweenStars);
		HomeStarPanel.Visible = !homeStarVisible && _motherShip.ViewMode != ViewMode.StarSystem;
    }

    private Star CreateStar(Galaxy.Star star)
	{
		var starGameObject = _objectPool.GetObject(StarPrefab);
	    var position = star.Position;
		starGameObject.SetActive(true);
		starGameObject.transform.parent = transform;
		starGameObject.transform.localPosition = new Vector3(position.x*DistanceBetweenStars,position.y*DistanceBetweenStars,-1);
		var script = starGameObject.GetComponent<Star>();

		script.Initialize(star);

		return script;
	}

	private void DestroyStar(Star star)
	{
		star.Deinitialize();
		_objectPool.ReleaseObject(star.gameObject);
	}
}
