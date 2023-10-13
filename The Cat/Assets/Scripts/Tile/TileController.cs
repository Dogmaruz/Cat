using UnityEngine;
using Zenject;

public class TileController : MonoBehaviour
{
    [SerializeField] private float m_bpm;

    [Range(0f, 1f)]
    [SerializeField] private float m_coinSpawnChance;

    [SerializeField] private Coin m_coinPrefab;

    [SerializeField] private bool m_isRandomX; // переменная выставлена для удобства дебага. 

    private Tile[] _tiles;

    private int _tileCount = 0;

    private float _period;
    public float Period => _period;

    private float _maxVisibleDistanceOfTiles = 15f;

    private Vector3 _coinUpVector = new Vector3(0f, 0.5f, 0f);

    private DiContainer _diContainer;

    private MovementController _movementController;

    private LevelSecuenceController _levelSecuenceController;

    [Inject]
    public void Construct(DiContainer diContainer, MovementController movementController, LevelSecuenceController levelSecuenceController)
    {
        _diContainer = diContainer;

        _movementController = movementController;

        _levelSecuenceController = levelSecuenceController;
    }

    private void Awake()
    {
        _tiles = GetComponentsInChildren<Tile>();

        TrySetCoinToTile();

        MoveTilesAlongAxisX();

        _period = 60f / m_bpm;
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

    private void TrySetCoinToTile()
    {
        foreach (Tile tile in _tiles)
        {
            float rnd = Random.Range(0f, 1f);

            if (rnd > m_coinSpawnChance)
            {
                continue;
            }
            else
            {
                //var coin = Instantiate(m_coinPrefab, tile.transform);
                var coin = _diContainer.InstantiatePrefab(m_coinPrefab, tile.transform);

                coin.transform.localPosition += _coinUpVector;
            }
        }
    }

    private void MoveTilesAlongAxisX()
    {
        if (m_isRandomX == true)
        {
            for (int i = 4; i < _tiles.Length; i++)
            {
                if(_tiles[i].TileType == TileType.Move)
                {
                    _tiles[i].SetStartPosition(_tiles[i].transform.position);
                }
                else
                {
                    float rnd = Random.Range(-1, 2);

                    if (i < _tiles.Length / 2)
                    {
                        rnd *= 0.5f;
                    }

                    float posX = Mathf.Clamp((int)_tiles[i - 1].StartPosition.x + rnd, -2, 2);

                    Vector3 newStartPosition = new Vector3(posX, _tiles[i].transform.position.y, _tiles[i].transform.position.z);

                    _tiles[i].SetStartPosition(newStartPosition);
                }
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
            //TODO: Заменить эту реализацию на окно результатов с переходом на новый уровень.

            _movementController.SetMovementState(false);

            _levelSecuenceController.Lose();

            return _tiles[0];
        }

        return _tiles[_tileCount];
    }
}
