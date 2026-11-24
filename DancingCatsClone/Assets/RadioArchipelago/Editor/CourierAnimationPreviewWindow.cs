using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace RadioArchipelago.Editor
{
    /// <summary>Owns an isolated PreviewRenderUtility scene; never edits the active game or its profile.</summary>
    public sealed class CourierAnimationPreviewWindow : EditorWindow
    {
        private const string CatalogPath = "Assets/RadioArchipelago/Content/CourierCatalog.asset";
        private static readonly string[] States = CourierShowcaseTimeline.Clips.Select(c => c.label).ToArray();
        private static readonly string[] Views = { "Спереди", "Справа", "Сзади", "Слева", "¾" };
        private static readonly float[] ViewAngles = { 0, 90, 180, 270, 24 };
        private static readonly float[] Durations = CourierShowcaseTimeline.Clips.Select(c => c.duration).ToArray();
        private static readonly string[] Descriptions = CourierShowcaseTimeline.Clips.Select(c => c.description).ToArray();

        [SerializeField] private int courierIndex, stateIndex;
        [SerializeField] private bool playing = true, loop = true;
        [SerializeField] private float speed = 1, yaw = 24, elevation = 7, zoom = 1;
        [SerializeField] private bool fullEquipmentFrame;
        [SerializeField] private bool showScarf;
        private CourierCatalog catalog;
        private string[] courierNames = Array.Empty<string>();
        private PreviewRenderUtility preview;
        private CourierPreviewSkinBake skinBake;
        private GameObject stage, actor;
        private CourierMotion motion;
        private CourierShowcaseTimeline timeline;
        private Bounds modelBounds, equipmentBounds;
        private double lastUpdate;
        private float stateTime;
        private bool dragging;
        private string problem;
        private GUIStyle titleStyle, noteStyle;
        private int dragControl;

        [MenuItem("Radio Archipelago/Animation Preview")]
        public static void Open()
        {
            var window = GetWindow<CourierAnimationPreviewWindow>();
            window.titleContent = new GUIContent("Анимации курьеров");
            window.minSize = new Vector2(560, 580);
            window.Show(); window.Focus();
        }

        public static void OpenFlight()
        {
            Open();
            var window = GetWindow<CourierAnimationPreviewWindow>();
            window.stateIndex = 2; window.playing = true; window.loop = true; window.Rebuild();
        }

        private void OnEnable()
        {
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged -= OnPlayMode;
            EditorApplication.playModeStateChanged += OnPlayMode;
            lastUpdate = EditorApplication.timeSinceStartup;
            LoadCatalog();
            if (!EditorApplication.isPlayingOrWillChangePlaymode) Rebuild();
        }

        private void OnDisable()
        {
            EditorApplication.update -= Tick;
            EditorApplication.playModeStateChanged -= OnPlayMode;
            DisposePreview();
        }
        private void OnDestroy() => DisposePreview();

        private void OnPlayMode(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode) DisposePreview();
            else if (state == PlayModeStateChange.EnteredEditMode) { LoadCatalog(); Rebuild(); }
            lastUpdate = EditorApplication.timeSinceStartup;
            Repaint();
        }

        private void LoadCatalog()
        {
            catalog = AssetDatabase.LoadAssetAtPath<CourierCatalog>(CatalogPath);
            if (catalog == null || catalog.entries == null || catalog.entries.Length == 0)
            {
                courierNames = Array.Empty<string>();
                problem = "Каталог курьеров ещё не импортирован. После импорта нажми «Обновить модели».";
                return;
            }
            courierNames = new string[catalog.entries.Length];
            for (int i = 0; i < courierNames.Length; i++)
                courierNames[i] = catalog.entries[i] != null ? catalog.entries[i].displayName : "Не назначен";
            courierIndex = Mathf.Clamp(courierIndex, 0, courierNames.Length - 1);
            stateIndex = Mathf.Clamp(stateIndex, 0, States.Length - 1);
        }

        private void Rebuild()
        {
            DisposePreview();
            if (EditorApplication.isPlayingOrWillChangePlaymode || catalog == null || courierNames.Length == 0) return;
            try
            {
                var definition = catalog.entries[courierIndex];
                if (definition == null || definition.prefab == null)
                    throw new InvalidOperationException("Для этого персонажа не назначен prefab.");
                preview = new PreviewRenderUtility();
                var camera = preview.camera;
                camera.orthographic = true; camera.nearClipPlane = .05f; camera.farClipPlane = 100;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.965f, .949f, .906f, 1);
                camera.allowHDR = false; camera.allowMSAA = true;
                preview.ambientColor = new Color(.42f, .43f, .45f, 1);
                var lights = preview.lights;
                lights[0].intensity = 1.2f; lights[0].color = new Color(1, .95f, .87f);
                lights[0].transform.rotation = Quaternion.Euler(28, 145, 0);
                lights[1].intensity = .6f; lights[1].color = new Color(.82f, .91f, 1);
                lights[1].transform.rotation = Quaternion.Euler(18, -25, 0);
                stage = new GameObject("Editor courier preview stage") { hideFlags = HideFlags.HideAndDontSave };
                preview.AddSingleGO(stage);
                actor = Instantiate(definition.prefab, stage.transform, false);
                actor.name = "Preview_" + definition.id;
                var wardrobe = actor.GetComponent<CourierWardrobeRig>();
                if (wardrobe != null)
                    wardrobe.Apply(catalog.FindEquipment(definition.defaultBackpackId, CourierEquipmentKind.Backpack), null,
                        showScarf ? catalog.FindEquipment(definition.defaultScarfId, CourierEquipmentKind.Scarf) : null);
                foreach (var node in actor.GetComponentsInChildren<Transform>(true)) node.gameObject.hideFlags = HideFlags.HideAndDontSave;
                foreach (var source in actor.GetComponentsInChildren<AudioSource>(true)) source.enabled = false;
                foreach (var animator in actor.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
                foreach (var collider in actor.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
                foreach (var body in actor.GetComponentsInChildren<Rigidbody>(true)) { body.isKinematic = true; body.detectCollisions = false; }
                foreach (var skin in actor.GetComponentsInChildren<SkinnedMeshRenderer>(true)) skin.updateWhenOffscreen = true;
                motion = actor.GetComponentInChildren<CourierMotion>(true);
                if (motion == null) throw new InvalidOperationException("У персонажа не найден CourierMotion.");
                motion.ManualSimulation = true; motion.ResetMotion();
                var renderers = actor.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length == 0) throw new InvalidOperationException("У персонажа нет видимой модели.");
                equipmentBounds = CourierVisualBounds.Measure(renderers);
                var bodySkins = actor.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                    .Where(r => r.GetComponentInParent<CourierEquipmentRig>() == null).Cast<Renderer>().ToArray();
                modelBounds = CourierVisualBounds.Measure(bodySkins.Length > 0 ? bodySkins : renderers);
                skinBake = new CourierPreviewSkinBake(actor);
                timeline = new CourierShowcaseTimeline(stage.transform, actor, motion);
                problem = null;
                RestartState();
            }
            catch (Exception error) { problem = error.Message; playing = false; DisposePreview(); }
            Repaint();
        }

        private void DisposePreview()
        {
            skinBake?.Dispose(); skinBake = null;
            motion = null; timeline = null; actor = null; stage = null;
            if (preview != null) { preview.Cleanup(); preview = null; }
            if (dragging && GUIUtility.hotControl == dragControl) GUIUtility.hotControl = 0;
            dragging = false;
        }

        private void RestartState()
        {
            stateTime = 0;
            if (motion == null || timeline == null) return;
            timeline.Reset(stateIndex);
            motion.SetPaused(!playing);
            lastUpdate = EditorApplication.timeSinceStartup;
        }

        private void Tick()
        {
            double now = EditorApplication.timeSinceStartup;
            float dt = Mathf.Clamp((float)(now - lastUpdate), 0, .1f) * speed;
            lastUpdate = now;
            if (!playing || motion == null || EditorApplication.isCompiling || EditorApplication.isUpdating
                || EditorApplication.isPlayingOrWillChangePlaymode) return;
            float duration = Durations[stateIndex];
            float toEnd = duration - stateTime;
            Advance(Mathf.Min(dt, Mathf.Max(0, toEnd)));
            if (stateTime >= duration - .00001f)
            {
                if (loop) { float remainder = Mathf.Max(0, dt - toEnd); RestartState(); Advance(remainder); }
                else { playing = false; motion.SetPaused(true); }
            }
            Repaint();
        }

        private void Advance(float seconds)
        {
            if (motion == null || timeline == null) return;
            motion.SetPaused(false);
            timeline.Advance(seconds); stateTime = timeline.Time;
            motion.SetPaused(!playing);
        }

        private void Seek(float value)
        {
            bool wasPlaying = playing;
            RestartState();
            motion?.SetPaused(false); Advance(Mathf.Clamp(value, 0, Durations[stateIndex]));
            playing = wasPlaying; motion?.SetPaused(!playing);
            lastUpdate = EditorApplication.timeSinceStartup;
            Repaint();
        }

        private void OnGUI()
        {
            if (titleStyle == null) titleStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 17 };
            if (noteStyle == null) noteStyle = new GUIStyle(EditorStyles.wordWrappedMiniLabel) { padding = new RectOffset(4, 4, 3, 3) };
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Радиоархипелаг · движения курьеров", titleStyle);
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(courierNames.Length == 0))
                {
                    int next = EditorGUILayout.Popup("Персонаж", courierIndex, courierNames);
                    if (next != courierIndex) { courierIndex = next; Rebuild(); }
                }
                if (GUILayout.Button("Обновить модели", GUILayout.Width(138))) { LoadCatalog(); Rebuild(); }
            }
            int nextState = EditorGUILayout.Popup("Движение", stateIndex, States);
            if (nextState != stateIndex) { stateIndex = nextState; RestartState(); }
            EditorGUILayout.LabelField(Descriptions[stateIndex], noteStyle, GUILayout.MinHeight(30));

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorGUILayout.HelpBox("Останови Play Mode для просмотра анимаций. Окно использует отдельную временную сцену.", MessageType.Info);
                return;
            }
            if (!string.IsNullOrEmpty(problem)) EditorGUILayout.HelpBox(problem, MessageType.Warning);

            using (new EditorGUI.DisabledScope(motion == null))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(playing ? "Пауза" : "Воспроизвести", GUILayout.Width(118)))
                    {
                        if (!playing && stateTime >= Durations[stateIndex] - .00001f) RestartState();
                        playing = !playing; motion?.SetPaused(!playing); lastUpdate = EditorApplication.timeSinceStartup;
                    }
                    if (GUILayout.Button("Сначала", GUILayout.Width(86))) RestartState();
                    loop = GUILayout.Toggle(loop, "По кругу", GUILayout.Width(90));
                    GUILayout.FlexibleSpace();
                    GUILayout.Label("Скорость", GUILayout.Width(60));
                    speed = EditorGUILayout.Slider(speed, .25f, 1.5f, GUILayout.MinWidth(100));
                }
                float nextTime = EditorGUILayout.Slider("Время, с", stateTime, 0, Durations[stateIndex]);
                if (Mathf.Abs(nextTime - stateTime) > .0001f) Seek(nextTime);
                using (new EditorGUILayout.HorizontalScope())
                {
                    for (int i = 0; i < Views.Length; i++)
                        if (GUILayout.Button(Views[i], EditorStyles.miniButton)) { yaw = ViewAngles[i]; elevation = i == 4 ? 7 : 0; zoom = 1; Repaint(); }
                }
                fullEquipmentFrame = EditorGUILayout.ToggleLeft("Показывать рюкзак и антенну целиком", fullEquipmentFrame);
                bool nextScarf = EditorGUILayout.ToggleLeft("Показать шарф персонажа", showScarf);
                if (nextScarf != showScarf) { showScarf = nextScarf; Rebuild(); }
            }
            Rect area = GUILayoutUtility.GetRect(100, 100, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            DrawPreview(area);
            EditorGUILayout.LabelField("Потяни мышью для поворота · колесо — масштаб. В крупном виде верх антенны может выходить из кадра. Музыка и игровая сцена не меняются.", noteStyle);
        }

        private void DrawPreview(Rect area)
        {
            if (area.width < 2 || area.height < 2) return;
            HandleOrbit(area);
            if (Event.current.type != EventType.Repaint) return;
            EditorGUI.DrawRect(area, new Color(.965f, .949f, .906f, 1));
            if (preview == null || actor == null) return;
            float headroom = stateIndex == 1 || stateIndex == 2 ? .7f : .15f;
            var bounds = fullEquipmentFrame ? equipmentBounds : modelBounds;
            var center = bounds.center + Vector3.up * headroom * .45f;
            float aspect = area.width / area.height;
            preview.camera.aspect = aspect;
            if (stateIndex == 12)
            {
                preview.camera.orthographic = false; preview.camera.fieldOfView = 60;
                preview.camera.farClipPlane = 180;
                preview.camera.transform.position = new Vector3(0, 5, -10);
                preview.camera.transform.LookAt(new Vector3(0, 2.9f, 0));
            }
            else
            {
                preview.camera.orthographic = true;
                preview.camera.orthographicSize = Mathf.Max(bounds.size.y * .78f + headroom * .5f,
                    Mathf.Max(bounds.size.x, bounds.size.z) * .86f / aspect) * zoom;
                preview.camera.transform.position = center + Quaternion.Euler(-elevation, yaw, 0) * Vector3.forward * 8;
                preview.camera.transform.LookAt(center);
            }
            bool opened = false;
            bool failed = false;
            Texture rendered = null;
            try
            {
                skinBake?.Refresh();
                preview.BeginPreview(area, GUIStyle.none); opened = true;
                preview.Render(true, false);
            }
            catch (Exception error) { problem = "Не удалось отрисовать модель: " + error.Message; playing = false; failed = true; }
            finally { if (opened) rendered = preview.EndPreview(); }
            if (failed) DisposePreview();
            else if (rendered != null) GUI.DrawTexture(area, rendered, ScaleMode.StretchToFill, false);
        }

        private void HandleOrbit(Rect area)
        {
            var current = Event.current;
            dragControl = GUIUtility.GetControlID("RadioCourierOrbit".GetHashCode(), FocusType.Passive, area);
            if (current.type == EventType.MouseDown && current.button == 0 && area.Contains(current.mousePosition))
            { GUIUtility.hotControl = dragControl; dragging = true; current.Use(); }
            else if (current.type == EventType.MouseDrag && dragging && GUIUtility.hotControl == dragControl)
            {
                yaw -= current.delta.x * .55f; elevation = Mathf.Clamp(elevation + current.delta.y * .35f, -35, 50);
                current.Use(); Repaint();
            }
            else if (current.type == EventType.MouseUp && dragging)
            { dragging = false; if (GUIUtility.hotControl == dragControl) GUIUtility.hotControl = 0; current.Use(); }
            else if (current.type == EventType.ScrollWheel && area.Contains(current.mousePosition))
            { zoom = Mathf.Clamp(zoom * (1 + current.delta.y * .04f), .65f, 1.8f); current.Use(); Repaint(); }
        }
    }
}
