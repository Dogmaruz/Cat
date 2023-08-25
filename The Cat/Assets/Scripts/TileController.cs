using UnityEngine;

public class TileController : MonoBehaviour
{
    [SerializeField] private float m_bpm;

    private Tile[] _tiles;

    private int _tileCount = 0;

    private float _secPerBeat;
    public float SecPerBeat => _secPerBeat;

    void Awake()
    {
        _tiles = GetComponentsInChildren<Tile>();

        _secPerBeat = 59.912f / m_bpm;
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
