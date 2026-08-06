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
		var beamObject = new GameObject("PrismBeamAnimation");
		beamObject.transform.SetParent(transform, false);
		var material = new Material(Shader.Find("Sprites/Default"));
		var glow = CreatePrismLine(beamObject, material, 198,
			new Color(0.2f, 0.75f, 1f, 0.14f), new Color(0.65f, 0.9f, 1f, 0.5f));
		var core = CreatePrismLine(beamObject, material, 200,
			new Color(1f, 0.98f, 0.75f, 1f), new Color(0.55f, 0.95f, 1f, 1f));
		var headFlare = CreatePrismLine(beamObject, material, 201,
			new Color(1f, 1f, 0.8f, 1f), new Color(0.55f, 0.95f, 1f, 0.9f));
		headFlare.numCapVertices = 16;

		var source = _starData.GetPosition(sourceStarId) * DistanceBetweenStars;
		var target = _starData.GetPosition(targetStarId) * DistanceBetweenStars;
		var travelDirection = (target - source).normalized;
		const float duration = 0.82f;
		var elapsed = 0f;
		Boundary.Hide();
		while (elapsed < duration)
		{
			elapsed += Time.unscaledDeltaTime;
			var progress = Mathf.Clamp01(elapsed / duration);
			var head = Vector2.Lerp(source, target, Mathf.SmoothStep(0f, 1f, progress));
			var tail = Vector2.Lerp(source, target, Mathf.Clamp01(progress - 0.36f));
			glow.SetPosition(0, tail);
			glow.SetPosition(1, head);
			core.SetPosition(0, tail);
			core.SetPosition(1, head);
			glow.widthMultiplier = Mathf.Lerp(1.65f, 0.42f, progress);
			core.widthMultiplier = Mathf.Lerp(0.58f, 0.14f, progress);
			headFlare.SetPosition(0, head - travelDirection * 0.025f);
			headFlare.SetPosition(1, head + travelDirection * 0.025f);
			headFlare.widthMultiplier = Mathf.Lerp(2.4f, 0.8f, progress);
			_mapNavigator.SetFocus(head);
			yield return null;
		}

		_mapNavigator.SetFocus(target);
		Destroy(material);
		Destroy(beamObject);
	}

	private static LineRenderer CreatePrismLine(GameObject owner, Material material, int sortingOrder,
		Color startColor, Color endColor)
	{
		var line = owner.AddComponent<LineRenderer>();
		line.useWorldSpace = false;
		line.positionCount = 2;
		line.numCapVertices = 8;
		line.textureMode = LineTextureMode.Stretch;
		line.sortingOrder = sortingOrder;
		line.sharedMaterial = material;
		line.startColor = startColor;
		line.endColor = endColor;
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
