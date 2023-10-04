using UnityEngine;
using Zenject;

public class TileController : MonoBehaviour
{
    [SerializeField] private float m_bpm;

    [SerializeField] private BackgroundSceneClip m_backgroundSceneClip;

    private Tile[] _tiles;

    private int _tileCount = 0;

    private float _period;
    public float Period => _period;

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

        _period = 60f / m_bpm * 0.5f;

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
            _movementController.SetMovementState(false);

            _levelSecuenceController.Lose();

            return _tiles[0];
        }

        return _tiles[_tileCount];
    }
}
