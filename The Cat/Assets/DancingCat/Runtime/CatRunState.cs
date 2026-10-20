using System;
using UnityEngine;

namespace DancingCat
{
    // Rhythm rules consume song time. Physics, rendering and frame rate cannot move a hit.
    public sealed class CatRunState
    {
        public readonly CatSong Song;
        public int NextIndex { get; private set; }
        public int Landings { get; private set; }
        public int Score { get; private set; }
        public int Combo { get; private set; }
        public int BestCombo { get; private set; }
        public int Coins { get; private set; }
        public bool Failed { get; private set; }
        public bool Finished { get; private set; }
        public float Time { get; private set; }
        public float X { get; private set; }
        public string Feedback { get; private set; } = "";
        public float InvincibleUntil { get; private set; } = -1;
        private int _checkpointIndex;
        public event Action<int> Landed;
        public CatNote Current => NextIndex > 0 ? Song.notes[NextIndex - 1] : null;
        public CatNote Next => NextIndex < Song.notes.Count ? Song.notes[NextIndex] : null;
        public float FinishTime => Song.notes.Count == 0 ? 0 : Mathf.Min(Song.duration,
            Song.notes[Song.notes.Count - 1].time + Song.notes[Song.notes.Count - 1].duration + .6f);

        public CatRunState(CatSong song)
        {
            Song = song ?? throw new ArgumentNullException(nameof(song));
            X = song.notes.Count > 0 ? song.notes[0].lane : 0;
        }

        public float Height
        {
            get
            {
                if (Current == null || Next == null) return 0;
                float departure = Current.time + Current.duration;
                float gap = Next.time - departure;
                if (gap <= 0 || Time <= departure || Time >= Next.time) return 0;
                float phase = Mathf.Clamp01((Time - departure) / gap);
                return Mathf.Sin(Mathf.PI * phase) * Mathf.Clamp(gap * Song.speed * .32f, .65f, 1.6f);
            }
        }

        public float SupportLane(float time)
        {
            var note = Current;
            if (note == null) return 0;
            if (note.kind != CatNoteKind.Slide || note.duration <= 0) return note.lane;
            float elapsed = Mathf.Clamp(time - note.time, 0, note.duration);
            float previousTime = 0, previousLane = note.lane;
            if (note.path != null)
            {
                foreach (var point in note.path)
                {
                    if (elapsed <= point.time)
                        return Mathf.Lerp(previousLane, point.lane, Mathf.InverseLerp(previousTime, point.time, elapsed));
                    previousTime = point.time; previousLane = point.lane;
                }
            }
            return Mathf.Lerp(previousLane, note.endLane, Mathf.InverseLerp(previousTime, note.duration, elapsed));
        }

        public void Advance(float songTime, float x)
        {
            if (Failed || Finished || songTime < Time) return;
            float from = Time;
            float fromX = X;
            float to = Mathf.Max(0, songTime);
            x = Mathf.Clamp(x, -1.7f, 1.7f);
            float cursor = from;
            while (Next != null && Next.time <= to)
            {
                float hitTime = Next.time;
                if (!CheckSupport(cursor, hitTime, from, to, fromX, x)) return;
                ProcessCheckpoints(hitTime);
                float hitX = InputAt(hitTime, from, to, fromX, x);
                if (Mathf.Abs(hitX - Next.lane) > .64f && hitTime > InvincibleUntil)
                {
                    Fail(hitTime, hitX);
                    return;
                }
                var note = Next;
                int hitIndex = NextIndex++;
                _checkpointIndex = 0;
                Landings++;
                Combo++;
                BestCombo = Mathf.Max(Combo, BestCombo);
                Score += 10;
                if (note.coin) Coins++;
                Feedback = Mathf.Abs(hitX - note.lane) <= .19f ? "PERFECT" : "GREAT";
                Time = hitTime;
                X = hitX;
                Landed?.Invoke(hitIndex);
                cursor = hitTime;
            }
            if (!CheckSupport(cursor, to, from, to, fromX, x)) return;
            ProcessCheckpoints(to);
            Time = to;
            X = x;
            Finished = NextIndex == Song.notes.Count && to >= FinishTime;
        }

        private bool CheckSupport(float start, float end, float from, float to, float fromX, float toX)
        {
            var support = Current;
            if (support == null || support.duration <= 0) return true;
            float a = Mathf.Max(start, support.time);
            float b = Mathf.Min(end, support.time + support.duration);
            if (b < a) return true;
            float cursor = a;
            if (support.kind == CatNoteKind.Slide && support.path != null)
            {
                foreach (var point in support.path)
                {
                    float t = support.time + point.time;
                    if (t <= a || t >= b) continue;
                    if (!CheckSupportSegment(cursor, t, from, to, fromX, toX)) return false;
                    cursor = t;
                }
            }
            return CheckSupportSegment(cursor, b, from, to, fromX, toX);
        }

        private bool CheckSupportSegment(float a, float b, float from, float to, float fromX, float toX)
        {
            // Input and the support path are linear between their knots. Find the first
            // boundary crossing so earlier earned accents survive a later miss in one frame.
            if (b <= InvincibleUntil) { ProcessCheckpoints(b); return true; }
            a = Mathf.Max(a, InvincibleUntil);
            float errorA = InputAt(a, from, to, fromX, toX) - SupportLane(a);
            float errorB = InputAt(b, from, to, fromX, toX) - SupportLane(b);
            if (Mathf.Abs(errorA) > .64f || Mathf.Abs(errorB) > .64f)
            {
                float crossing = a;
                if (Mathf.Abs(errorA) <= .64f)
                    crossing = Mathf.Lerp(a, b, Mathf.Clamp01((Mathf.Sign(errorB) * .64f - errorA) / (errorB - errorA)));
                ProcessCheckpoints(crossing);
                Fail(crossing, InputAt(crossing, from, to, fromX, toX));
                return false;
            }
            ProcessCheckpoints(b);
            return true;
        }

        private static float InputAt(float time, float from, float to, float fromX, float toX)
        {
            return to > from ? Mathf.Lerp(fromX, toX, Mathf.Clamp01((time - from) / (to - from))) : toX;
        }

        private void ProcessCheckpoints(float time)
        {
            var note = Current;
            if (note == null || note.duration <= 0 || note.checkpoints == null) return;
            while (_checkpointIndex < note.checkpoints.Count && note.time + note.checkpoints[_checkpointIndex] <= time)
            {
                float accent = note.checkpoints[_checkpointIndex++];
                if (accent > 0 && accent < note.duration) { Score += 2; Feedback = "HOLD +2"; }
            }
        }

        private void Fail(float time, float x)
        {
            Time = time;
            X = x;
            Combo = 0;
            Failed = true;
            Feedback = "MISSED";
        }

        public void Revive()
        {
            if (!Failed) return;
            Time = Current != null ? Current.time : 0;
            X = Current != null ? Current.lane : 0;
            Failed = false;
            Combo = 0;
            Feedback = "LET'S GO!";
            InvincibleUntil = Time + 3;
        }
    }
}
