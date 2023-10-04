using UnityEngine;

public class RhythmObjectGenerator : MonoBehaviour
{
    public GameObject objectPrefab; // Префаб объекта, который будет создаваться на ритмических событиях
    public AudioSource musicSource; // Ссылка на источник музыки
    public float rhythmThreshold = 0.5f; // Пороговое значение для определения ритмических событий
    public float spawnDistance = 1.0f; // Расстояние между созданными объектами
    public float beatsPerMinute = 120; // Темп музыки в BPM

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

        // Получаем сэмплы аудио
        musicSource.clip.GetData(audioSamples, 0);

        timePerBeat = 60f / beatsPerMinute / 2f;

        // Начинаем анализ сразу
        nextSpawnTime = Time.time + timePerBeat;
    }

    void Update()
    {
        // Проверяем, наступило ли время для следующего анализа
        if (Time.time >= nextSpawnTime)
        {
            // Вычисляем среднее значение амплитуды в текущем интервале
            float averageAmplitude = CalculateAverageAmplitude();

            // Сравниваем с пороговым значением
            if (averageAmplitude > rhythmThreshold)
            {
                // Если это первое событие, задаем начальную позицию
                if (isFirstSpawn)
                {
                    spawnPosition = transform.position;
                    isFirstSpawn = false;
                }
                else
                {
                    // Смещаем позицию создания объектов на заданное расстояние
                    spawnPosition = Vector3.forward * perBeatsCount;
                }

                // Создаем объект на текущей позиции
                Instantiate(objectPrefab, spawnPosition, Quaternion.identity);
            }

            // Обновляем время следующего анализа на следующую восьмую долю
            nextSpawnTime += timePerBeat;

            perBeatsCount++;
        }
    }

    float CalculateAverageAmplitude()
    {
        int currentSample = musicSource.timeSamples;
        int samplesInInterval = Mathf.RoundToInt(sampleRate * timePerBeat);
        float sumAmplitude = 0f;

        // Суммируем амплитуду в текущем интервале
        for (int i = currentSample; i < currentSample + samplesInInterval; i++)
        {
            // Обеспечиваем, чтобы индекс не вышел за границы массива
            if (i < audioSamples.Length)
            {
                sumAmplitude += Mathf.Abs(audioSamples[i]);
            }
        }

        // Вычисляем среднее значение амплитуды
        float averageAmplitude = sumAmplitude / samplesInInterval;

        return averageAmplitude;
    }
}