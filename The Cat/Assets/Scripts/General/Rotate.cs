using UnityEngine;

namespace SpaceShooter
{
    public class Rotate : MonoBehaviour
    {
        [SerializeField] private Vector3 _directionRotate; //Напрвление вращения.

        [SerializeField] private float _speed; //Скорость вращения.

        [SerializeField] private Transform _target; //Объект вращения.

        void Update()
        {
            _target.Rotate(_directionRotate * _speed * Time.deltaTime);
        }
    }
}
