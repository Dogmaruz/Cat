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

    private float _offsetY = 4f;

    private float maxDistance = 15;

    private Tween _tween;

    MovementController _cat;

    [Inject]
    public void Construct(MovementController cat)
    {
        _cat = cat;
    }

    void Start()
    {
        _tiles = GetComponentsInChildren<Tile>();

        _secPerBeat = m_backgroundSceneClip.BackgroundClip.length / m_bpm;

        foreach (Tile tile in _tiles)
        {
            var dist = Vector3.Distance(_cat.Cat.transform.position, tile.transform.position);

            if (dist > maxDistance)
            {
                tile.IsMoved = true;

                var offsetX = Random.Range(-2f, 2f);

                tile.transform.position = new Vector3(tile.transform.position.x + offsetX, tile.transform.position.y + _offsetY, tile.transform.position.z);

                var meshRenders = tile.GetComponentsInChildren<MeshRenderer>();

                foreach (var mesh in meshRenders)
                {
                    _tween = mesh.material.DOFade(0f, 0.01f)
                   .SetEase(Ease.InOutQuad)
                   .OnComplete(KillTween);
                }
            }
        }
    }

    private void KillTween()
    {
        _tween.Kill();
    }

    private void Update()
    {
        foreach (Tile tile in _tiles)
        {
            var dist = Vector3.Distance(_cat.Cat.transform.position, tile.transform.position);

            if (dist < maxDistance && tile.IsMoved)
            {
                tile.IsMoved = false;

                tile.transform.DOMove(tile.BasePosition, 1f);

                var meshRenders = tile.GetComponentsInChildren<MeshRenderer>();

                foreach (var mesh in meshRenders)
                {
                    _tween = mesh.material.DOFade(1f, 0.3f)
                   .SetEase(Ease.InOutQuad)
                   .OnComplete(KillTween);
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
        _tween.Kill();
    }
}
