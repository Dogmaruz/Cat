using System;
using DancingCats.Clone;
using RhythmGame.Services;
using RhythmGame.Sessions;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using VContainer;
using VContainer.Unity;

namespace RhythmGame.Composition
{
    [DisallowMultipleComponent]
    public sealed class GameLifetimeScope : LifetimeScope
    {
        [SerializeField] private OriginalPlayController controller;
        [SerializeField] private OriginalUIPresenter presenter;
        [SerializeField] private EventSystem eventSystem;
        [SerializeField] private Volume[] volumes = Array.Empty<Volume>();

        public void SetBindings(OriginalPlayController game, OriginalUIPresenter ui, EventSystem input, Volume[] sceneVolumes)
        {
            controller = game;
            presenter = ui;
            eventSystem = input;
            volumes = sceneVolumes ?? Array.Empty<Volume>();
        }

        protected override void Configure(IContainerBuilder builder)
        {
            if (controller == null || presenter == null || eventSystem == null || controller.Assets == null)
                throw new InvalidOperationException("Game scope requires the controller, UI, input and content library.");
            // Create the default actions at runtime; serializing AssignDefaultActions in a
            // scene would embed a duplicate input asset and every action reference.
            if (!eventSystem.TryGetComponent<InputSystemUIInputModule>(out _))
                eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
            GameServices.Register(builder, controller.Assets);
            builder.RegisterInstance(new SceneRenderSettings(volumes));
            builder.RegisterComponent(controller);
            builder.RegisterComponent(presenter);
            builder.RegisterComponent(eventSystem);
            builder.RegisterEntryPoint<GameStartup>();
        }
    }

    // This single-scene game hosts application services here. A multi-scene bootstrap
    // can own their parent scope; scene components and the run factory remain scoped.
    public static class GameServices
    {
        public static void Register(IContainerBuilder builder, OriginalAssets assets)
        {
            if (assets == null) throw new ArgumentNullException(nameof(assets));
            builder.RegisterInstance(assets);
            builder.Register<IGameResources, UnityGameResources>(Lifetime.Singleton);
            builder.Register<IAudioClock, UnityAudioClock>(Lifetime.Singleton);
            builder.Register<ISongCatalog, ResourceSongCatalog>(Lifetime.Singleton);
            builder.Register<PlayerPrefsProfileStore>(Lifetime.Singleton).As<IProfileStore>().As<IPlayerPreferences>();
            builder.Register<ICalendar, SystemCalendar>(Lifetime.Singleton);
            builder.Register(_ => new ApplicationSession(), Lifetime.Singleton);
            builder.Register(c => OriginalProgressionConfig.LoadFrom(c.Resolve<IGameResources>()), Lifetime.Singleton);
            builder.Register<ProfileServiceFactory>(Lifetime.Singleton);
            builder.Register(c => c.Resolve<ProfileServiceFactory>().Create(), Lifetime.Singleton);
            builder.Register<UiLocalization>(Lifetime.Singleton);
            builder.Register<RoleMotionCatalog>(Lifetime.Singleton);
            builder.Register<ThemeCatalog>(Lifetime.Singleton);
            builder.Register<RunSessionFactory>(Lifetime.Scoped);
        }
    }

    public sealed class GameStartup : IStartable
    {
        private readonly OriginalPlayController controller;
        private readonly OriginalUIPresenter presenter;

        public GameStartup(OriginalPlayController controller, OriginalUIPresenter presenter)
        {
            this.controller = controller;
            this.presenter = presenter;
        }

        public void Start()
        {
            controller.AttachPresenter(presenter);
            controller.Initialize();
            presenter.Initialize();
        }
    }
}
