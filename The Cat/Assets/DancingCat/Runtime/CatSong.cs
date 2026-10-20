using System;
using System.Collections.Generic;
using UnityEngine;

namespace DancingCat
{
    public enum CatNoteKind { Short, Hold, Slide, BigJump }
    public enum CatGamePhase { MainMenu, Countdown, Playing, Paused, Failed, Complete }

    [Serializable]
    public class CatPathPoint
    {
        public float time;
        public float lane;
    }

    [Serializable]
    public class CatNote
    {
        public float time;
        public float duration;
        public float lane;
        public float endLane;
        public CatNoteKind kind;
        public bool coin;
        public List<float> checkpoints = new List<float>();
        public List<CatPathPoint> path = new List<CatPathPoint>();
    }

    [CreateAssetMenu(menuName = "Dancing Cat/Song")]
    public class CatSong : ScriptableObject
    {
        public string id;
        public string title;
        public string artist = "Cat sessions";
        public AudioClip clip;
        public float bpm = 120;
        public float offset;
        public float speed = 6;
        public float duration = 45;
        public int difficulty = 1;
        public Color theme = new Color(.38f, .25f, .9f);
        public List<CatNote> notes = new List<CatNote>();
    }
}
