using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DancingCats.Clone;
using RadioArchipelago.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RadioArchipelago.Editor
{
    /// <summary>Editor-only scheduling of the existing runtime motions; never changes their implementation.</summary>
    public sealed class CourierShowcaseTimeline
    {
        public sealed class Clip
        {
            public readonly string id, label, description;
            public readonly float duration;
            public Clip(string key, string title, float seconds, string note)
            { id = key; label = title; duration = seconds; description = note; }
        }
        // Preserve the original seven preview indices, then append the remaining runtime states.
        public static readonly Clip[] Clips =
        {
            new Clip("idle", "Ожидание и приветствие", 7.2f, "Переступание, дыхание, моргание и короткое приветствие рукой. Полный цикл показан без ускорения."),
            new Clip("short-hops", "Короткие прыжки — левая и правая ножка", 2.8f, "Чередование опорной ноги, махи от плеч, изменение формы в полёте и пружина при касании."),
            new Clip("long-jump", "Долгий перелёт и оборот", 3.3f, "Два длинных перелёта с разными направлениями оборота, зеркальными махами рук и инерцией подвесных деталей."),
            new Clip("landing", "Приземление и пружина", 1.05f, "Сжатие после касания и упругое восстановление. Шкала времени позволяет рассмотреть короткую фазу."),
            new Clip("ride-right", "Прокат — оборот вправо", 2.4f, "Один полный поворот на длинной опоре. Верхний механизм свободно реагирует на вращение героя."),
            new Clip("celebrate", "Радость после финиша", 2.8f, "Праздничные подскоки, движения коротких ног и рук."),
            new Clip("blink", "Моргание крупно", .7f, "Настоящий Blink-морф: закрытие и открытие глаз. Это фрагмент обычного цикла ожидания."),
            new Clip("ride-left", "Прокат — оборот влево", 2.4f, "Тот же прокат с противоположным направлением полного оборота."),
            new Clip("supported-rides", "Две подвижные плитки подряд", 4f, "Настоящий PlaySupportedRide сам чередует направление оборота: вправо, затем влево."),
            new Clip("steering", "Повороты и инерция деталей", 2.8f, "Поворот вправо-влево и небольшое смещение демонстрационной сцены показывают запаздывание антенны, чёлки и шарфа."),
            new Clip("finish-hover", "Сбор Искр на финише", 2.6f, "Спокойное парение без постоянного вращения. Руки и ноги продолжают мягко двигаться."),
            new Clip("departure-pose", "Махи в финальном прыжке", 1.8f, "Крупный вид настоящей позы FinishDeparture. Перемещение вдаль показано отдельным пунктом ниже."),
            new Clip("finish-departure", "Финальный улёт вдаль", 2f, "Полный IslandFinishDeparture: подъём, движение вдаль, уменьшение и исчезновение за 1,55 секунды. Камера здесь шире.")
        };

        readonly Transform stage;
        readonly GameObject actor;
        readonly CourierMotion motion;
        readonly Vector3 actorScale;
        readonly Quaternion actorRotation;
        readonly IslandFinishDeparture departure = new IslandFinishDeparture();
        int index, segment;
        bool landed;
        float clock;
        public float Time => clock;
        public int ClipIndex => index;
        public bool WideDeparture => index == 12;

        public CourierShowcaseTimeline(Transform disposableStage, GameObject courier, CourierMotion presentation)
        {
            stage = disposableStage; actor = courier; motion = presentation;
            actorScale = actor.transform.localScale; actorRotation = actor.transform.localRotation;
        }

        public void Reset(int clipIndex)
        {
            index = Mathf.Clamp(clipIndex, 0, Clips.Length - 1); clock = 0; segment = 0; landed = false;
            actor.SetActive(true); stage.localPosition = Vector3.zero;
            actor.transform.localScale = actorScale; actor.transform.localRotation = actorRotation;
            motion.ManualSimulation = true; motion.SetPaused(false); motion.ResetMotion(); departure.Reset();
            switch (index)
            {
                case 1: motion.PlayJump(.45f); motion.SetFlightProgress(0); break;
                case 2: motion.PlayJump(1.3f); motion.SetFlightProgress(0); break;
                case 3: motion.PlayIdle(); motion.PlayLanding(1); break;
                case 4: motion.PlayHold(false, 2.4f, true); break;
                case 5: motion.PlayEnd(); break;
                case 6:
                    motion.PlayIdle();
                    for (int step = 0; step < 162; step++) motion.SimulateSprings(1f / 120);
                    break;
                case 7: motion.PlayHold(true, 2.4f, true); break;
                case 8: motion.PlaySupportedRide(1.65f); break;
                case 10: motion.PlayFinishHover(); break;
                case 11: motion.PlayFinishDeparture(); break;
                case 12:
                    actor.transform.localRotation = Quaternion.identity;
                    motion.PlayFinishDeparture();
                    departure.TryBegin(true, 20, Vector3.zero, Quaternion.identity);
                    break;
                default: motion.PlayIdle(); break;
            }
        }

        public void Advance(float seconds)
        {
            motion.SetPaused(false);
            while (seconds > .000001f)
            {
                float dt = Mathf.Min(seconds, 1f / 120), next = clock + dt;
                float cycle = index == 1 ? .70f : index == 2 ? 1.65f : index == 8 ? 2 : 0;
                float flight = index == 1 ? .45f : index == 2 ? 1.3f : 0;
                int nextSegment = cycle > 0 ? Mathf.FloorToInt((next + .000001f) / cycle) : 0;
                if (nextSegment != segment)
                {
                    segment = nextSegment; landed = false;
                    if (flight > 0) { motion.PlayJump(flight); motion.SetFlightProgress(0); }
                    else if (index == 8) motion.PlaySupportedRide(1.65f);
                }
                float local = cycle > 0 ? next - segment * cycle : next;
                Vector3 position = Vector3.zero;
                if (flight > 0)
                {
                    float p = Mathf.Clamp01(local / flight);
                    if (!landed && local >= flight)
                    { motion.PlayLanding(1); motion.PlayIdle(); landed = true; }
                    if (!landed)
                    {
                        motion.SetFlightProgress(p);
                        // Root translation is an illustrative arc using the production scalar rule.
                        // The real song/route remains authoritative in gameplay.
                        position.y = NativeRhythmRules.JumpY(0, index == 1 ? .35f : .57f, p);
                        position.x = (segment % 2 == 0 ? -1 : 1) * Mathf.Sin(p * Mathf.PI) * .12f;
                    }
                    motion.SetFacing(0);
                }
                else if (index == 8)
                {
                    if (!landed && local >= 1.65f) { motion.PlayIdle(); landed = true; }
                }
                else if (index == 9)
                {
                    float swing = Mathf.Sin(next * Mathf.PI * 2 / 2.8f);
                    position.x = swing * .22f; motion.SetFacing(swing * 36);
                }
                else if (index == 12)
                {
                    departure.Tick(dt); position = departure.Offset;
                    actor.transform.localScale = actorScale * departure.VisualScale;
                }
                stage.localPosition = position;
                motion.SimulateSprings(dt);
                clock = next; seconds -= dt;
                if (index == 12 && departure.Complete) actor.SetActive(false);
            }
        }
    }

    /// <summary>Asynchronous native frame capture in an isolated preview scene, for all ten actual couriers.</summary>
    [InitializeOnLoad]
    public static class CourierMotionShowcase
    {
        const string CatalogPath = "Assets/RadioArchipelago/Content/CourierCatalog.asset";
        public const int CellWidth = 384, CellHeight = 600, Columns = 5, Rows = 2, Fps = 20;
        static string Root => Path.GetFullPath("../Docs/Design/RadioArchipelago/HatsAndMotion20261105/Motion");
        static CaptureJob current;
        [Serializable] public sealed class HeroRecord
        { public string id, assetId, label; public int column, row, cropX, cropY, cropWidth, cropHeight; }
        [Serializable] public sealed class ClipRecord
        { public string id, label, description, frameFolder, projection; public int index, frames; public float duration, orthographicSize; }
        [Serializable] public sealed class Manifest
        {
            public string status, message, phase, startedUtc, completedUtc, unityVersion, folder;
            public int fps = Fps, width = CellWidth * Columns, height = CellHeight * Rows;
            public int completedFrames, totalFrames, clipIndex, frameIndex;
            public bool activeScenePreserved;
            public HeroRecord[] heroes; public ClipRecord[] clips;
            public string method = "Native Unity Camera.Render and CPU-baked real skins at fixed 120 Hz simulation; existing CourierMotion only. Each hero is rendered in its own 384x600 viewport, then read unchanged into one 5x2 PNG. Tall antennas are intentionally cropped in close limb views and cannot overlap another hero. Hats are omitted. Default backpacks are equipped; default scarves appear only in the steering/secondary-motion clip so short arms remain visible elsewhere. A deterministic body-envelope preflight chooses one fixed camera size for each clip and all ten heroes, with a ten-percent margin. Jump root arcs use the production scalar curve with demonstration heights; final departure uses the complete production IslandFinishDeparture including its 1.55s movement/scale/disappearance. No game scenes, assets, profile or accepted motion code are changed.";
        }

        static CourierMotionShowcase()
        {
            AssemblyReloadEvents.beforeAssemblyReload += () => CancelWithReason("Capture interrupted by script reload; run it again after compilation.");
            EditorApplication.quitting += () => CancelWithReason("Editor closed before capture completed.");
        }
        [MenuItem("Radio Archipelago/Animations/Capture all ten heroes")]
        public static void Capture() => Begin();
        public static void Begin() => Start(false);
        public static void Quick() => Start(true);
        public static void Cancel() => CancelWithReason("Capture cancelled.");

        static void Start(bool quick)
        {
            if (current != null) throw new InvalidOperationException("A motion capture is already running.");
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("Start motion capture in compiled Edit Mode.");
            current = new CaptureJob(quick);
            try { current.Initialize(); EditorApplication.update += Tick; }
            catch (Exception error) { Finish(error.ToString()); throw; }
        }
        static void Tick()
        {
            if (current == null) { EditorApplication.update -= Tick; return; }
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Finish("Play Mode started during isolated capture."); return; }
            try { if (current.CaptureOneFrame()) Finish(null); }
            catch (Exception error) { Finish(error.ToString()); Debug.LogException(error); }
        }
        static void CancelWithReason(string reason) { if (current != null) Finish(reason); }
        static void Finish(string problem)
        {
            EditorApplication.update -= Tick;
            var job = current; current = null;
            if (job == null) return;
            try { job.Complete(problem); }
            finally { job.Dispose(); }
        }

        sealed class CaptureJob : IDisposable
        {
            readonly bool quick;
            readonly Scene originalScene;
            readonly bool originalDirty;
            CourierCatalog catalog;
            Scene scene;
            Camera camera;
            RenderTexture target;
            Texture2D grid;
            readonly List<GameObject> actors = new List<GameObject>();
            readonly List<CourierShowcaseTimeline> timelines = new List<CourierShowcaseTimeline>();
            readonly List<CourierPreviewSkinBake> bakes = new List<CourierPreviewSkinBake>();
            readonly List<Renderer[]> bodyRenderers = new List<Renderer[]>();
            readonly Manifest report = new Manifest();
            int clip, frame;
            int framingSample;
            float requiredOrtho;
            bool framing;
            string statusPath;

            public CaptureJob(bool pilot)
            { quick = pilot; originalScene = SceneManager.GetActiveScene(); originalDirty = originalScene.isDirty; }
            public void Initialize()
            {
                catalog = AssetDatabase.LoadAssetAtPath<CourierCatalog>(CatalogPath);
                if (catalog == null || catalog.entries.Length != 10) throw new InvalidDataException("The ten-hero catalog is required.");
                report.folder = Path.Combine(Root, quick ? "Quick" : "Native");
                Directory.CreateDirectory(report.folder); statusPath = Path.Combine(Root, quick ? "quick-status.json" : "capture-status.json");
                report.startedUtc = DateTime.UtcNow.ToString("O"); report.unityVersion = Application.unityVersion;
                report.status = "running";
                int[] indices = quick ? new[] { 1 } : Enumerable.Range(0, CourierShowcaseTimeline.Clips.Length).ToArray();
                report.clips = indices.Select(i =>
                {
                    var definition = CourierShowcaseTimeline.Clips[i];
                    float duration = quick ? .6f : definition.duration;
                    return new ClipRecord { id = definition.id, index = i, label = definition.label, description = definition.description,
                        duration = duration, frames = Mathf.CeilToInt(duration * Fps), frameFolder = definition.id,
                        projection = i == 12 ? "wide-perspective-production-departure" : "close-orthographic-limbs" };
                }).ToArray();
                report.totalFrames = report.clips.Sum(c => c.frames);
                scene = EditorSceneManager.NewPreviewScene();
                var cameraObject = Node("Owned motion capture camera"); camera = cameraObject.AddComponent<Camera>();
                camera.enabled = false; camera.scene = scene; camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.94f, .925f, .875f); camera.allowHDR = false; camera.allowMSAA = false;
                camera.nearClipPlane = .01f; camera.farClipPlane = 180;
                target = new RenderTexture(CellWidth, CellHeight, 24, RenderTextureFormat.ARGB32) { name = "Owned motion viewport" };
                target.Create(); camera.targetTexture = target;
                grid = new Texture2D(report.width, report.height, TextureFormat.RGB24, false);
                AddLight("Warm key", new Vector3(35, 145, 0), 1.5f, new Color(1, .95f, .87f));
                AddLight("Cool fill", new Vector3(25, -25, 0), 1.05f, new Color(.82f, .91f, 1));
                AddLight("Front fill", new Vector3(-12, 0, 0), .3f, new Color(1, .97f, .92f));
                var records = new List<HeroRecord>();
                for (int i = 0; i < catalog.entries.Length; i++)
                {
                    var hero = catalog.entries[i];
                    var stage = Node("Owned motion stage " + hero.assetId);
                    var actor = UnityEngine.Object.Instantiate(hero.prefab); actors.Add(actor);
                    SceneManager.MoveGameObjectToScene(actor, scene); actor.transform.SetParent(stage.transform, false);
                    actor.transform.localRotation = Quaternion.Euler(0, -20, 0);
                    var wardrobe = actor.GetComponent<CourierWardrobeRig>();
                    wardrobe.Apply(catalog.FindEquipment(hero.defaultBackpackId, CourierEquipmentKind.Backpack), null, null);
                    foreach (var node in actor.GetComponentsInChildren<Transform>(true)) node.gameObject.layer = 20 + i;
                    foreach (var source in actor.GetComponentsInChildren<AudioSource>(true)) source.enabled = false;
                    var motion = actor.GetComponent<CourierMotion>(); motion.ManualSimulation = true;
                    var timeline = new CourierShowcaseTimeline(stage.transform, actor, motion); timelines.Add(timeline);
                    var bake = new CourierPreviewSkinBake(actor); bakes.Add(bake);
                    bodyRenderers.Add(bake.Renderers.Where(r => r.GetComponentInParent<CourierEquipmentRig>() == null).Cast<Renderer>().ToArray());
                    records.Add(new HeroRecord { id = hero.id, assetId = hero.assetId, label = hero.displayName,
                        column = i % Columns, row = i / Columns, cropX = i % Columns * CellWidth, cropY = i / Columns * CellHeight,
                        cropWidth = CellWidth, cropHeight = CellHeight });
                }
                report.heroes = records.ToArray(); BeginClip(); WriteStatus();
            }
            void BeginClip()
            {
                frame = 0; var definition = report.clips[clip];
                Directory.CreateDirectory(Path.Combine(report.folder, definition.frameFolder));
                for (int i = 0; i < actors.Count; i++)
                {
                    bakes[i].Dispose(); actors[i].SetActive(true);
                    var hero = catalog.entries[i];
                    actors[i].GetComponent<CourierWardrobeRig>().Apply(
                        catalog.FindEquipment(hero.defaultBackpackId, CourierEquipmentKind.Backpack), null,
                        definition.index == 9 ? catalog.FindEquipment(hero.defaultScarfId, CourierEquipmentKind.Scarf) : null);
                    bakes[i] = new CourierPreviewSkinBake(actors[i]);
                    bodyRenderers[i] = bakes[i].Renderers.Where(r => r.GetComponentInParent<CourierEquipmentRig>() == null).Cast<Renderer>().ToArray();
                    timelines[i].Reset(definition.index);
                }
                camera.aspect = (float)CellWidth / CellHeight;
                framing = definition.index != 6 && definition.index != 12;
                framingSample = 0; requiredOrtho = 1.9f;
                report.clipIndex = clip; report.frameIndex = 0;
                report.phase = framing ? "framing" : "capturing";
                if (definition.index == 12)
                {
                    camera.orthographic = false; camera.fieldOfView = 60;
                    camera.transform.position = new Vector3(0, 5, -10); camera.transform.LookAt(new Vector3(0, 2.9f, 0));
                }
                else
                {
                    camera.orthographic = true; camera.orthographicSize = definition.index == 6 ? 1.4f : 1.9f;
                    camera.transform.position = new Vector3(0, definition.index == 6 ? 1.45f : 1.22f, 8);
                    camera.transform.LookAt(new Vector3(0, definition.index == 6 ? 1.45f : 1.22f, 0));
                }
            }
            public bool CaptureOneFrame()
            {
                var definition = report.clips[clip];
                if (framing)
                {
                    float sampleTime = Mathf.Min(definition.duration, framingSample * .1f);
                    for (int i = 0; i < actors.Count; i++)
                    {
                        timelines[i].Advance(Mathf.Max(0, sampleTime - timelines[i].Time)); bakes[i].Refresh();
                        foreach (var renderer in bodyRenderers[i])
                        {
                            var bounds = renderer.bounds;
                            for (int corner = 0; corner < 8; corner++)
                            {
                                var point = bounds.center + Vector3.Scale(bounds.extents, new Vector3((corner & 1) == 0 ? -1 : 1,
                                    (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1));
                                var local = camera.transform.InverseTransformPoint(point);
                                requiredOrtho = Mathf.Max(requiredOrtho, Mathf.Abs(local.y), Mathf.Abs(local.x) / camera.aspect);
                            }
                        }
                    }
                    framingSample++;
                    if (sampleTime >= definition.duration - .000001f)
                    {
                        camera.orthographicSize = requiredOrtho * 1.10f;
                        definition.orthographicSize = camera.orthographicSize;
                        foreach (var timeline in timelines) timeline.Reset(definition.index);
                        framing = false; report.phase = "capturing"; WriteStatus();
                    }
                    return false;
                }
                float time = (float)frame / Fps;
                var previous = RenderTexture.active;
                try
                {
                    for (int i = 0; i < actors.Count; i++)
                    {
                        timelines[i].Advance(Mathf.Max(0, time - timelines[i].Time));
                        bakes[i].Refresh(); camera.cullingMask = 1 << (20 + i);
                        if (definition.index != 12 && definition.index != 6) CheckBodyViewport(i, definition.id, frame);
                        camera.Render(); RenderTexture.active = target;
                        grid.ReadPixels(new Rect(0, 0, CellWidth, CellHeight), i % Columns * CellWidth,
                            (Rows - 1 - i / Columns) * CellHeight, false);
                    }
                    grid.Apply(false);
                    File.WriteAllBytes(Path.Combine(report.folder, definition.frameFolder, "frame-" + frame.ToString("0000") + ".png"), grid.EncodeToPNG());
                }
                finally { RenderTexture.active = previous; }
                frame++; report.completedFrames++; report.clipIndex = clip; report.frameIndex = frame;
                if (frame % 5 == 0 || frame == definition.frames) WriteStatus();
                if (frame < definition.frames) return false;
                clip++;
                if (clip == report.clips.Length) return true;
                BeginClip(); return false;
            }
            void CheckBodyViewport(int hero, string state, int image)
            {
                foreach (var renderer in bodyRenderers[hero])
                {
                    var bounds = renderer.bounds;
                    for (int corner = 0; corner < 8; corner++)
                    {
                        var point = bounds.center + Vector3.Scale(bounds.extents, new Vector3((corner & 1) == 0 ? -1 : 1,
                            (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1));
                        var view = camera.WorldToViewportPoint(point);
                        if (view.x < .005f || view.x > .995f || view.y < .005f || view.y > .995f || view.z <= camera.nearClipPlane)
                            throw new InvalidDataException("Motion body would clip: " + report.heroes[hero].assetId + "/" + state + " frame " + image
                                + ", viewport=" + view + ", ortho=" + camera.orthographicSize + ".");
                    }
                }
            }
            public void Complete(string problem)
            {
                if (string.IsNullOrEmpty(statusPath))
                {
                    report.folder = Path.Combine(Root, quick ? "Quick" : "Native");
                    Directory.CreateDirectory(report.folder);
                    statusPath = Path.Combine(Root, quick ? "quick-status.json" : "capture-status.json");
                }
                report.activeScenePreserved = SceneManager.GetActiveScene() == originalScene && originalScene.isDirty == originalDirty;
                report.completedUtc = DateTime.UtcNow.ToString("O");
                report.status = problem == null && report.activeScenePreserved ? "complete" : "failed";
                report.message = problem ?? (report.activeScenePreserved ? "Captured actual motions for all ten heroes." : "The user's active scene changed during capture.");
                WriteStatus(); File.WriteAllText(Path.Combine(report.folder, "manifest.json"), JsonUtility.ToJson(report, true));
                if (report.status == "complete") Debug.Log("COURIER_MOTION_SHOWCASE PASS: " + report.completedFrames + " native grid frames; all ten heroes, scene preserved. " + report.folder);
                else Debug.LogWarning("COURIER_MOTION_SHOWCASE " + report.message);
            }
            void WriteStatus() => File.WriteAllText(statusPath, JsonUtility.ToJson(report, true));
            GameObject Node(string name)
            { var node = new GameObject(name); SceneManager.MoveGameObjectToScene(node, scene); return node; }
            void AddLight(string name, Vector3 euler, float intensity, Color color)
            {
                var light = Node(name).AddComponent<Light>(); light.type = LightType.Directional;
                light.transform.rotation = Quaternion.Euler(euler); light.intensity = intensity; light.color = color;
            }
            public void Dispose()
            {
                foreach (var bake in bakes) bake.Dispose();
                if (camera != null) camera.targetTexture = null;
                if (target != null) { target.Release(); UnityEngine.Object.DestroyImmediate(target); }
                if (grid != null) UnityEngine.Object.DestroyImmediate(grid);
                if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
            }
        }
    }
}
