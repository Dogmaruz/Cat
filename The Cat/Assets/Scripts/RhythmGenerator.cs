using UnityEngine;

public class RhythmObjectGenerator : MonoBehaviour
{
    public GameObject objectPrefab; // ������ �������, ������� ����� ����������� �� ����������� ��������

    public AudioSource musicSource; // ������ �� �������� ������

    public float rhythmThreshold = 0.5f; // ��������� �������� ��� ����������� ����������� �������

    public float spawnDistance = 1.0f; // ���������� ����� ���������� ���������

    public float beatsPerMinute = 120; // ���� ������ � BPM

    private float[] audioSamples;

    private int sampleRate;

    private float timePerBeat;

    private float nextSpawnTime;

    private Vector3 spawnPosition;

    private bool isFirstSpawn = true;

    public int perBeatsCount = 0;

    void Start()
    {
        sampleRate = AudioSettings.outputSampleRate;

        audioSamples = new float[musicSource.clip.samples * musicSource.clip.channels];

        // �������� ������ �����
        musicSource.clip.GetData(audioSamples, 0);

        timePerBeat = 60f / beatsPerMinute;

        // �������� ������ �����
        nextSpawnTime = Time.time + timePerBeat;
    }

    void Update()
    {
        // ���������, ��������� �� ����� ��� ���������� �������
        if (Time.time >= nextSpawnTime)
        {
            // ��������� ������� �������� ��������� � ������� ���������
            float averageAmplitude = CalculateAverageAmplitude();

            // ���������� � ��������� ���������
            if (averageAmplitude > rhythmThreshold || isFirstSpawn)
            {
                // ���� ��� ������ �������, ������ ��������� �������
                if (isFirstSpawn)
                {
                    spawnPosition = transform.position;
                    isFirstSpawn = false;
                }
                else
                {
                    // ������� ������� �������� �������� �� �������� ����������
                    spawnPosition += Vector3.forward * spawnDistance;
                }

                // ������� ������ �� ������� �������
                Instantiate(objectPrefab, spawnPosition, Quaternion.identity);
            }
            else
            {
                spawnPosition += Vector3.forward * spawnDistance;
            }

            // ��������� ����� ���������� ������� �� ��������� ����
            nextSpawnTime += timePerBeat;

            perBeatsCount++;
        }
    }

    float CalculateAverageAmplitude()
    {
        int currentSample = musicSource.timeSamples;

        int samplesInInterval = Mathf.RoundToInt(sampleRate * timePerBeat);

        float sumAmplitude = 0f;

        // ��������� ��������� � ������� ���������
        for (int i = currentSample; i < currentSample + samplesInInterval; i++)
        {
            // ������������, ����� ������ �� ����� �� ������� �������
            if (i < audioSamples.Length)
            {
                sumAmplitude += Mathf.Abs(audioSamples[i]);
            }
        }

        // ��������� ������� �������� ���������
        float averageAmplitude = sumAmplitude / samplesInInterval;

        return averageAmplitude;
    }
}