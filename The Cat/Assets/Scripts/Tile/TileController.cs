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

    private float maxDistance = 15f;

    private Sequence _sequence;

    private MovementController _cat;

    [Inject]
    public void Construct(MovementController cat)
    {
        _cat = cat;
    }

    void Start()
    {
        DOTween.SetTweensCapacity(500, 150);

        float bounds = 2f;

        _tiles = GetComponentsInChildren<Tile>();

        _secPerBeat = m_backgroundSceneClip.BackgroundClip.length / m_bpm;

        foreach (Tile tile in _tiles)
        {
            var dist = Vector3.Distance(_cat.Cat.transform.position, tile.transform.position);

            if (dist > maxDistance)
            {
                tile.IsMoved = true;

                float offsetX = Random.Range(-bounds, bounds);

                float offsetY = 4f;

                var startPosition = new Vector3(tile.transform.position.x + offsetX, tile.transform.position.y + offsetY, tile.transform.position.z);

                _sequence = DOTween.Sequence();

                _sequence.Append(tile.transform.DOMove(startPosition, 0.01f));

                var meshRenders = tile.GetComponentsInChildren<MeshRenderer>();

                foreach (var mesh in meshRenders)
                {
                    _sequence.Join(mesh.material.DOFade(0f, 0.01f).SetEase(Ease.InOutQuad));
                }
            }
        }
    }

    private void Update()
    {
        foreach (Tile tile in _tiles)
        {
            var dist = Vector3.Distance(_cat.Cat.transform.position, tile.transform.position);

            if (dist < maxDistance && tile.IsMoved)
            {
                tile.IsMoved = false;

                _sequence = DOTween.Sequence();

                _sequence.Append(tile.transform.DOMove(tile.BasePosition, 1f));

                var meshRenders = tile.GetComponentsInChildren<MeshRenderer>();

                foreach (var mesh in meshRenders)
                {
                    _sequence.Join(mesh.material.DOFade(1f, 0.3f).SetEase(Ease.InOutQuad));
                }
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

    private void OnDestroy()
    {
        _sequence.Kill();
    }
}
