using UnityEngine;

public class SpectrumAnalyzer : MonoBehaviour
{
    AudioSource source;
    GameObject[] cubes;

    public float SpectrumRefreshTime;
    private float lastUpdate = 0;
    private float[] spectrum = new float[1024];
    public float scaleFactor = 10000;

    void Start()
    {
        source = GetComponent<AudioSource>();
        cubes = new GameObject[1024];
        createDisplayObjects();
    }

    void Update()
    {
        if (Time.time - lastUpdate > SpectrumRefreshTime)
        {
            source.GetSpectrumData(spectrum, 0, FFTWindow.Rectangular);
            for (int i = 0; i < spectrum.Length; i++)
            {
                cubes[i].transform.localScale = new Vector3(1, spectrum[i] * scaleFactor, 1);
            }
            lastUpdate = Time.time;
        }
    }

    void createDisplayObjects()
    {
        for (int i = 0; i < 1024; i++)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.position = new Vector3(i, 0, 0);
            cubes[i] = cube;
        }
    }
}
