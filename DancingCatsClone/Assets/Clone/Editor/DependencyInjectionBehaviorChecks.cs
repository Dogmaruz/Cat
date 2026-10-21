using System;
using DancingCats.Clone;
using RhythmGame.Composition;
using RhythmGame.Services;
using RhythmGame.Sessions;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace DancingCats.Clone.Editor
{
    /// <summary>Exercises the production registrations with isolated in-memory content and persistence.</summary>
    public static class DependencyInjectionBehaviorChecks
    {
        public static void Run()
        {
            var assets = ScriptableObject.CreateInstance<OriginalAssets>();
            using var firstResources = new FixtureResources("First");
            using var secondResources = new FixtureResources("Second");
            var firstCatalog = new FixtureCatalog("first-song");
            var secondCatalog = new FixtureCatalog("second-song");
            var firstStore = new MemoryPreferences();
            var secondStore = new MemoryPreferences();
            try
            {
                using var first = CreateRoot(assets, firstResources, firstCatalog, firstStore);
                using var second = CreateRoot(assets, secondResources, secondCatalog, secondStore);

                // Check overrides before resolving the profile: no test may touch the user's preferences.
                Require(ReferenceEquals(first.Resolve<IProfileStore>(), firstStore) &&
                    ReferenceEquals(first.Resolve<IPlayerPreferences>(), firstStore) &&
                    ReferenceEquals(first.Resolve<ISongCatalog>(), firstCatalog) &&
                    ReferenceEquals(first.Resolve<IGameResources>(), firstResources),
                    "last registrations replace persistence, catalog and resource adapters");
                Require(ReferenceEquals(second.Resolve<IProfileStore>(), secondStore) &&
                    ReferenceEquals(second.Resolve<IPlayerPreferences>(), secondStore),
                    "second root uses its own in-memory persistence adapters");

                CheckServiceLifetimes(first, second);
                CheckIndependentProfiles(first, second, firstStore, secondStore);
                CheckLocalization(first, second, firstResources, secondResources);
                CheckMutableMapIsolation();
                CheckRunLifetimes(first);
                CheckMissingDependency();
                Debug.Log("DEPENDENCY_INJECTION_BEHAVIOR PASS: isolated roots, explicit dependencies, scoped runs and localization");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(assets);
            }
        }

        private static IObjectResolver CreateRoot(OriginalAssets assets, FixtureResources resources,
            FixtureCatalog catalog, MemoryPreferences preferences)
        {
            var builder = new ContainerBuilder();
            GameServices.Register(builder, assets);
            builder.RegisterInstance<IGameResources>(resources);
            builder.RegisterInstance<ISongCatalog>(catalog);
            builder.RegisterInstance<IProfileStore>(preferences);
            builder.RegisterInstance<IPlayerPreferences>(preferences);
            builder.RegisterInstance<ICalendar>(new FixtureCalendar());
            return builder.Build();
        }

        private static void CheckServiceLifetimes(IObjectResolver first, IObjectResolver second)
        {
            Require(ReferenceEquals(first.Resolve<ISongCatalog>(), first.Resolve<ISongCatalog>()),
                "one root shares its song catalog");
            Require(ReferenceEquals(first.Resolve<UiLocalization>(), first.Resolve<UiLocalization>()),
                "one root shares its localization service");
            Require(ReferenceEquals(first.Resolve<ApplicationSession>(), first.Resolve<ApplicationSession>()),
                "one application root retains one session identity");
            Require(!ReferenceEquals(first.Resolve<ApplicationSession>(), second.Resolve<ApplicationSession>()) &&
                first.Resolve<ApplicationSession>().Id != second.Resolve<ApplicationSession>().Id,
                "independent roots own different application sessions");
            Require(!ReferenceEquals(first.Resolve<ISongCatalog>(), second.Resolve<ISongCatalog>()) &&
                !ReferenceEquals(first.Resolve<UiLocalization>(), second.Resolve<UiLocalization>()) &&
                !ReferenceEquals(first.Resolve<RoleMotionCatalog>(), second.Resolve<RoleMotionCatalog>()) &&
                !ReferenceEquals(first.Resolve<ThemeCatalog>(), second.Resolve<ThemeCatalog>()),
                "independent roots do not share content services or mutable caches");
        }

        private static void CheckIndependentProfiles(IObjectResolver first, IObjectResolver second,
            MemoryPreferences firstStore, MemoryPreferences secondStore)
        {
            var firstProfile = first.Resolve<OriginalProgression>();
            var secondProfile = second.Resolve<OriginalProgression>();
            Require(ReferenceEquals(firstProfile, first.Resolve<OriginalProgression>()) &&
                !ReferenceEquals(firstProfile, secondProfile), "profiles have application ownership");
            firstProfile.RecordSongStart("first-song");
            firstProfile.RecordSongStart("second-song");
            Require(firstProfile.SongProgress("first-song")?.starts == 1 &&
                firstProfile.SongProgress("second-song") == null &&
                secondProfile.SongProgress("first-song") == null,
                "injected catalogs constrain progress and profile state cannot leak across roots");
            Require(firstStore.Json.Contains("first-song") && !secondStore.Json.Contains("first-song"),
                "automatic saves write only to the owning profile store");
            first.Resolve<IPlayerPreferences>().SetInt("tutorial-fixture", 1);
            Require(second.Resolve<IPlayerPreferences>().GetInt("tutorial-fixture", 0) == 0,
                "tutorial preferences are isolated from the other application root");
            Require(firstProfile.State.diceStartedDay == "2026-10-20",
                "profile initialization resolves the injected calendar");
        }

        private static void CheckLocalization(IObjectResolver first, IObjectResolver second,
            FixtureResources firstResources, FixtureResources secondResources)
        {
            var firstRoot = CreateLabel(out var firstText);
            var secondRoot = CreateLabel(out var secondText);
            try
            {
                var firstLocalization = first.Resolve<UiLocalization>();
                var secondLocalization = second.Resolve<UiLocalization>();
                firstLocalization.Bind(firstRoot, "BuildLoading", 0);
                secondLocalization.Bind(secondRoot, "BuildLoading", 0);
                Require(firstText.text == "First English" && secondText.text == "Second English",
                    "localization reads the owning resource catalog rather than another root's cache");
                firstLocalization.Apply(firstRoot, 1);
                Require(firstText.text == "First Russian" && secondText.text == "Second English",
                    "changing one root's language leaves the other label unchanged");
                firstLocalization.SetText(firstText, "12345");
                firstLocalization.Apply(firstRoot, 0);
                Require(firstText.text == "12345", "language changes preserve dynamic numeric labels");
                Require(firstResources.LanguageLoads == 1 && secondResources.LanguageLoads == 1,
                    "each localization scope parses its own catalog only once");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(firstRoot);
                UnityEngine.Object.DestroyImmediate(secondRoot);
            }
        }

        private static GameObject CreateLabel(out Text text)
        {
            var root = new GameObject("DI localization fixture", typeof(RectTransform));
            var background = new GameObject("BG", typeof(RectTransform));
            background.transform.SetParent(root.transform, false);
            var label = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            label.transform.SetParent(background.transform, false);
            text = label.GetComponent<Text>();
            text.text = "Loading";
            return root;
        }

        private static void CheckRunLifetimes(IObjectResolver root)
        {
            var factory = root.Resolve<RunSessionFactory>();
            Require(ReferenceEquals(factory, root.Resolve<RunSessionFactory>()), "one game scope owns one run factory");
            var previous = factory.Begin();
            previous.Score.Apply(true);
            previous.Score.Apply(true);
            var current = factory.Begin();
            Require(previous.IsDisposed && current.Id != previous.Id &&
                current.Score.Value == 0 && current.Score.Streak == 0 && ReferenceEquals(factory.Current, current),
                "starting another run disposes its predecessor and starts independent score state");

            RunSession childRun;
            using (var child = root.CreateScope(_ => { }))
            {
                var childFactory = child.Resolve<RunSessionFactory>();
                Require(!ReferenceEquals(factory, childFactory) &&
                    ReferenceEquals(root.Resolve<ISongCatalog>(), child.Resolve<ISongCatalog>()) &&
                    ReferenceEquals(root.Resolve<OriginalProgression>(), child.Resolve<OriginalProgression>()),
                    "a child game scope shares application services but owns its run factory");
                childRun = childFactory.Begin();
            }
            Require(childRun.IsDisposed && !current.IsDisposed,
                "disposing a child scope cleans up its run without ending the parent run");
            factory.Dispose();
            factory.Dispose();
            Require(current.IsDisposed && factory.Current == null,
                "run factory disposal is idempotent and releases its current run");
            RequireThrows<ObjectDisposedException>(() => factory.Begin(),
                "a disposed run factory cannot create another run");
        }

        private static void CheckMutableMapIsolation()
        {
            using var resources = new MapFixtureResources();
            var catalog = new ResourceSongCatalog(resources);
            var first = catalog.LoadMap("fixture-map");
            first.elements[0].pos = new Vector3(50, 60, 70);
            first.elements[0].gameTime = 999;
            var second = catalog.LoadMap("fixture-map");
            Require(!ReferenceEquals(first, second) && !ReferenceEquals(first.elements, second.elements) &&
                !ReferenceEquals(first.elements[0], second.elements[0]) &&
                second.elements[0].pos == new Vector3(1, 2, 3) && second.elements[0].gameTime == 1.5,
                "each map load returns fresh mutable run data and cannot retain a previous attempt's offsets");
        }

        private static void CheckMissingDependency()
        {
            var builder = new ContainerBuilder();
            builder.Register<UiLocalization>(Lifetime.Singleton);
            using var incomplete = builder.Build();
            RequireThrows<VContainerException>(() => incomplete.Resolve<UiLocalization>(),
                "resolving localization without its resource dependency fails explicitly");
        }

        private static void RequireThrows<T>(Action action, string message) where T : Exception
        {
            try { action(); }
            catch (T) { return; }
            throw new InvalidOperationException("DEPENDENCY_INJECTION_BEHAVIOR FAIL: " + message);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("DEPENDENCY_INJECTION_BEHAVIOR FAIL: " + message);
        }

        private sealed class FixtureCatalog : ISongCatalog
        {
            public FixtureCatalog(string id)
            {
                Songs = new[] { new SongRecord { id = id, catalogId = 1,
                    catalog = new OriginalSongMetadata { id = 1, musicUnlockType = 0 } } };
            }
            public SongRecord[] Songs { get; }
            public MapTimeline LoadMap(string id) => throw new NotSupportedException();
            public AudioClip LoadAudio(SongRecord song, bool preview = false) => throw new NotSupportedException();
            public Sprite LoadIcon(SongRecord song) => throw new NotSupportedException();
        }

        private sealed class MemoryPreferences : IProfileStore, IPlayerPreferences
        {
            private readonly System.Collections.Generic.Dictionary<string, int> integers = new System.Collections.Generic.Dictionary<string, int>();
            public string Json { get; private set; } = string.Empty;
            public string Read() => Json;
            public void Write(string json) => Json = json;
            public int GetInt(string key, int defaultValue = 0) => integers.TryGetValue(key, out var value) ? value : defaultValue;
            public void SetInt(string key, int value) => integers[key] = value;
        }

        private sealed class FixtureCalendar : ICalendar
        {
            public DateTime Today => Now.Date;
            public DateTime Now => new DateTime(2026, 10, 20, 12, 0, 0);
        }

        private sealed class FixtureResources : IGameResources, IDisposable
        {
            private readonly TextAsset languageTable;
            private readonly TextAsset progressionConfig;
            public int LanguageLoads { get; private set; }
            public FixtureResources(string prefix)
            {
                languageTable = new TextAsset("{\"124\":{\"id\":124,\"English\":\"" + prefix +
                    " English\",\"Russian\":\"" + prefix + " Russian\"}}");
                progressionConfig = new TextAsset(JsonUtility.ToJson(new OriginalProgressionConfig
                {
                    schemaVersion = 1,
                    roles = new[] { new OriginalRoleDefinition { Name = "1" } },
                    trails = new[] { new OriginalRoleDefinition { Name = "1" } },
                    houseRows = Array.Empty<OriginalHouseDefinition>(),
                    diceSpaces = Array.Empty<OriginalDiceSpace>()
                }));
            }
            public T Load<T>(string path) where T : UnityEngine.Object
            {
                if (typeof(T) != typeof(TextAsset))
                    throw new InvalidOperationException("Unexpected fixture resource request: " + path);
                if (path == "OriginalData/progression.catalog") return (T)(UnityEngine.Object)progressionConfig;
                if (path == "json/language_table")
                {
                    LanguageLoads++;
                    return (T)(UnityEngine.Object)languageTable;
                }
                throw new InvalidOperationException("Unexpected fixture resource request: " + path);
            }
            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(languageTable);
                UnityEngine.Object.DestroyImmediate(progressionConfig);
            }
        }

        private sealed class MapFixtureResources : IGameResources, IDisposable
        {
            private readonly TextAsset songs;
            private readonly TextAsset map;

            public MapFixtureResources()
            {
                var song = new SongRecord { id = "fixture-map", catalogId = 1,
                    catalog = new OriginalSongMetadata { id = 1 } };
                songs = new TextAsset("[" + JsonUtility.ToJson(song) + "]");
                map = new TextAsset(JsonUtility.ToJson(new MapTimeline
                {
                    schemaVersion = 1,
                    id = song.id,
                    elements = new[] { new MapElement { pos = new Vector3(1, 2, 3), gameTime = 1.5 } },
                    segments = Array.Empty<MapSegment>(),
                    events = Array.Empty<KoreographyEvent>()
                }));
            }

            public T Load<T>(string path) where T : UnityEngine.Object
            {
                if (typeof(T) != typeof(TextAsset)) throw new InvalidOperationException("Unexpected fixture resource type.");
                if (path == "CustomData/songs") return null;
                if (path == "OriginalData/songs") return (T)(UnityEngine.Object)songs;
                if (path == "OriginalData/Maps/fixture-map.timeline") return (T)(UnityEngine.Object)map;
                throw new InvalidOperationException("Unexpected map fixture resource request: " + path);
            }

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(songs);
                UnityEngine.Object.DestroyImmediate(map);
            }
        }
    }
}
