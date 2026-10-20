using System;
using System.Reflection;
using UnityEngine;

namespace DancingCat.Editor
{
    public static class CatSimulationValidation
    {
        public static void Run()
        {
            var type = typeof(CatSong).Assembly.GetType("DancingCat.CatRunState");
            if (type == null) throw new Exception("CAT_SIMULATION RED: rhythm simulation is not implemented");
            var song = ScriptableObject.CreateInstance<CatSong>();
            try
            {
                song.speed = 6; song.duration = 2;
                song.notes.Add(new CatNote { time = 0 });
                song.notes.Add(new CatNote { time = .5f });
                song.notes.Add(new CatNote { time = 1 });
                dynamic state = Activator.CreateInstance(type, new object[] { song });
                state.Advance(0f, 0f); state.Advance(1.2f, 0f); state.Advance(1.2f, 0f);
                Expect(!state.Failed && state.Landings == 3, "crossed notes land once, including a long frame");
                state = Activator.CreateInstance(type, new object[] { song });
                state.Advance(0f, 0f); state.Advance(.5f, 1.5f);
                Expect(state.Failed, "a missed tile fails the run");

                song.notes.Clear();
                song.notes.Add(new CatNote { time = 0, duration = 1, kind = CatNoteKind.Hold });
                song.notes.Add(new CatNote { time = 1.5f });
                state = Activator.CreateInstance(type, new object[] { song });
                state.Advance(0f, 0f); state.Advance(.4f, 1.5f);
                Expect(state.Failed, "leaving a long tile before its end fails");

                song.notes.Clear();
                song.notes.Add(new CatNote { time = 0, duration = 1, lane = -1, endLane = 1, kind = CatNoteKind.Slide });
                song.notes.Add(new CatNote { time = 1.5f, lane = 1 });
                state = Activator.CreateInstance(type, new object[] { song });
                state.Advance(0f, -1f); state.Advance(.5f, 0f); state.Advance(1f, 1f);
                Expect(!state.Failed, "following a moving path stays supported");

                song.notes.Clear();
                song.notes.Add(new CatNote { time = 0 });
                song.notes.Add(new CatNote { time = 1 });
                state = Activator.CreateInstance(type, new object[] { song });
                state.Advance(0f, 0f); state.Advance(.5f, 0f);
                Expect(Mathf.Abs((float)state.Height - 1.6f) < .001f, "jump midpoint is an apex on the song clock");
                state.Advance(1f, 0f);
                Expect(Mathf.Abs((float)state.Height) < .001f, "landing ends the arc exactly");
                state.Advance(2f, 0f);
                Expect(state.Finished, "last tile finishes without wrapping to the first");
                song.duration = 10;
                state = Activator.CreateInstance(type, new object[] { song });
                state.Advance(0f, 0f); state.Advance(2f, 0f);
                Expect(state.Finished, "trailing silence ends on the finish platform instead of gliding through empty space");
                song.duration = 2;
                song.notes.Clear();
                var hold = new CatNote { time = 0, duration = 1, kind = CatNoteKind.Hold };
                hold.checkpoints.Add(.25f); hold.checkpoints.Add(.75f);
                song.notes.Add(hold); song.notes.Add(new CatNote { time = 1.5f });
                state = Activator.CreateInstance(type, new object[] { song });
                state.Advance(0f, 0f); state.Advance(.9f, 0f); state.Advance(.9f, 0f);
                Expect(state.Score == 14 && state.Landings == 1, "long tile checkpoints reward each accent once");
                dynamic coarse = Activator.CreateInstance(type, new object[] { song });
                coarse.Advance(0f, 0f); coarse.Advance(.2f, 0f); coarse.Advance(.4f, 1f);
                dynamic fine = Activator.CreateInstance(type, new object[] { song });
                fine.Advance(0f, 0f); fine.Advance(.2f, 0f); fine.Advance(.25f, .25f); fine.Advance(.4f, 1f);
                Expect(coarse.Failed && fine.Failed && coarse.Score == 12 && fine.Score == 12 &&
                    Mathf.Abs((float)coarse.Time - (float)fine.Time) < .001f,
                    "a hitch preserves hold accents earned before the first unsupported position");
                hold.kind = CatNoteKind.Slide;
                hold.path.Add(new CatPathPoint { time = .5f, lane = 1.5f });
                state = Activator.CreateInstance(type, new object[] { song });
                state.Advance(0f, 0f); state.Advance(1f, 0f);
                Expect(state.Failed, "a long frame cannot cut across the middle of a follow path");
                Debug.Log("CAT_SIMULATION PASS: 11 behavioral checks");
            }
            finally { UnityEngine.Object.DestroyImmediate(song); }
        }

        private static void Expect(bool condition, string message)
        {
            if (!condition) throw new Exception("CAT_SIMULATION FAIL: " + message);
            Debug.Log("CAT_SIMULATION PASS: " + message);
        }
    }
}
