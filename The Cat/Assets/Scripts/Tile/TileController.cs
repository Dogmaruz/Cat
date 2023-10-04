using UnityEngine;
using Zenject;

public class TileController : MonoBehaviour
{
    [SerializeField] private float m_bpm;

    [SerializeField] private BackgroundSceneClip m_backgroundSceneClip;

    [SerializeField] private bool m_isRandomX; // переменная выставлена для удобства дебага. 

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

    private void Awake()
    {
        _tiles = GetComponentsInChildren<Tile>();

        MoveTilesAlongAxisX();

        _period = 60f / m_bpm * 0.5f;
    }

    void Start()
    {
        enabled = false;

        HidenTilesBasedOnDistance();
    }

    private void Update()
    {
        TryShowHideTilesDependendingOnDistance();
    }

    private void MoveTilesAlongAxisX()
    {
        if (m_isRandomX == true)
        {
            for (int i = 4; i < _tiles.Length; i++)
            {
                int rnd = Random.Range(-1, 2);

                float posX = Mathf.Clamp((int)_tiles[i - 1].StartPosition.x + rnd, -2, 2);

                Vector3 newStartPosition = new Vector3(posX, _tiles[i].transform.position.y, _tiles[i].transform.position.z);

                _tiles[i].SetStartPosition(newStartPosition);
            }
        }
        else
        {
            foreach(var tile in _tiles)
            {
                tile.SetStartPosition(tile.transform.position);
            }
        }

        /*  TODO 
         *  1. Убрать переменную m_isRandomX;
         *  2. Удалить всё из метода выше;
         *  3. Убрать комментарий.
          
            for (int i = 4; i < _tiles.Length; i++)
            {
                int rnd = Random.Range(-1, 2);

                float posX = Mathf.Clamp((int)_tiles[i - 1].StartPosition.x + rnd, -2, 2);

                Vector3 newStartPosition = new Vector3(posX, _tiles[i].transform.position.y, _tiles[i].transform.position.z);

                _tiles[i].SetStartPosition(newStartPosition);
            }
        */
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
