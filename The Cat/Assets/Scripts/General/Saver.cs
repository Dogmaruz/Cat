using System;
using System.IO;
using UnityEngine;


[Serializable]
public class Saver<T>
{
    public T dataSaver;

    Saver(T data)
    {
        dataSaver = data;
    }

    //Загрузка данных из файла.
    public static void TryLoad(string filename, ref T data)
    {
        var path = FileHandler.Path(filename);

        if (File.Exists(path))
        {
            var dataString = File.ReadAllText(path);

            var saver = JsonUtility.FromJson<Saver<T>>(dataString);

            data = saver.dataSaver;
        }
    }

    //Сохранение данных в файл.
    public static void Save(string filename, T data)
    {
        var wrapper = new Saver<T>(data);

        var dataString = JsonUtility.ToJson(wrapper, true);

        File.WriteAllText(FileHandler.Path(filename), dataString);
    }
}

public static class FileHandler
{
    //Возврашает полный путь до файла.
    public static string Path(string fileName)
    {
        //Debug.Log($"{Application.persistentDataPath}/{fileName}");

        return $"{Application.persistentDataPath}/{fileName}";
    }

    //Удаляет файл данных.
    public static void Reset(string filename)
    {
        var path = FileHandler.Path(filename);

        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    //Проверка на наличии файла на диске по заданному пути.
    public static bool HasFile(string filename)
    {
        return File.Exists(Path(filename));
    }
}

