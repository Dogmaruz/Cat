using UnityEngine;

public class TouchScreenController : MonoBehaviour
{
    [SerializeField] private GameObject m_touchZone;

    private void Awake()
    {
#if UNITY_ANDROID
        m_touchZone.SetActive(true);
#endif
    }
}
