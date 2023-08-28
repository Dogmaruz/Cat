using DG.Tweening;
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

    private float maxVisibleDistanceOfTiles = 15f;

    private MovementController _cat;

    [Inject]
    public void Construct(MovementController cat)
    {
        _cat = cat;
    }

    void Start()
    {
        enabled = false;

        DOTween.SetTweensCapacity(500, 150);

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
            var dist = Vector3.Distance(_cat.Cat.transform.position, tile.transform.position);

            if (dist > maxVisibleDistanceOfTiles)
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
            var dist = Vector3.Distance(_cat.Cat.transform.position, tile.transform.position);

            if (dist < maxVisibleDistanceOfTiles && tile.IsMoved)
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
            return _tiles[0];
        }

        return _tiles[_tileCount];
    }
}
