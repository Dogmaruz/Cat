using UnityEngine;
using Zenject;

public class TileController : MonoBehaviour
{
    [SerializeField] private float m_bpm;

    [SerializeField] private BackgroundSceneClip m_backgroundSceneClip;

    private Tile[] _tiles;

    private int _tileCount = 0;

    private float _secPerBeat;
    public float SecPerBeat => _secPerBeat;

    private float _maxVisibleDistanceOfTiles = 15f;

    private MovementController _movementController;

    private LevelSecuenceController _levelSecuenceController;

    [Inject]
    public void Construct(MovementController movementController, LevelSecuenceController levelSecuenceController)
    {
        _movementController = movementController;

        _levelSecuenceController = levelSecuenceController;
    }

    void Start()
    {
        enabled = false;

        _tiles = GetComponentsInChildren<Tile>();

        _secPerBeat = m_backgroundSceneClip.BackgroundClip.length / m_bpm;

        HidenTilesBasedOnDistance();
    }

    private void Update()
    {
        TryShowHideTilesDependendingOnDistance();
    }

    private void HidenTilesBasedOnDistance()
    {
        foreach (Tile tile in _tiles)
        {
            var dist = Vector3.Distance(_movementController.PlayerTransform.transform.position, tile.transform.position);

            if (dist > _maxVisibleDistanceOfTiles)
            {
                tile.Hide();
            }
        }

        enabled = true;
    }

    private void TryShowHideTilesDependendingOnDistance()
    {
        foreach (Tile tile in _tiles)
        {
            var dist = Vector3.Distance(_movementController.PlayerTransform.transform.position, tile.transform.position);

            if (dist < _maxVisibleDistanceOfTiles && tile.IsMoved)
            {
                tile.Show();
            }
        }
    }

    public Tile NextTile()
    {
        _tileCount++;

        if (_tileCount >= _tiles.Length)
        {
            _movementController.StopMovement();

            _levelSecuenceController.Lose();

            return _tiles[0];
        }

        return _tiles[_tileCount];
    }
}
