using System;
using DancingCats.Clone;
using UnityEngine;

namespace RhythmGame.Services
{
    public interface IProfileStore
    {
        string Read();
        void Write(string json);
    }

    public interface IPlayerPreferences
    {
        int GetInt(string key, int defaultValue = 0);
        void SetInt(string key, int value);
    }

    /// <summary>Retains the existing save key and JSON while isolating Unity persistence from progression rules.</summary>
    public sealed class PlayerPrefsProfileStore : IProfileStore, IPlayerPreferences
    {
        public string Read() => PlayerPrefs.GetString(OriginalProgression.SaveKey, string.Empty);

        public void Write(string json)
        {
            PlayerPrefs.SetString(OriginalProgression.SaveKey, json);
            PlayerPrefs.Save();
        }

        public int GetInt(string key, int defaultValue = 0) => PlayerPrefs.GetInt(key, defaultValue);

        public void SetInt(string key, int value)
        {
            PlayerPrefs.SetInt(key, value);
            PlayerPrefs.Save();
        }
    }

    /// <summary>Owned by the application scope; another scope receives an independent session.</summary>
    public sealed class ApplicationSession
    {
        public string Id { get; }

        public ApplicationSession() : this(Guid.NewGuid().ToString("N")) { }

        public ApplicationSession(string id)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("A session identifier is required.", nameof(id));
            Id = id;
        }
    }

    public interface ICalendar
    {
        DateTime Today { get; }
        DateTime Now { get; }
    }

    public sealed class SystemCalendar : ICalendar
    {
        public DateTime Today => DateTime.Today;
        public DateTime Now => DateTime.Now;
    }

    /// <summary>Creates one profile from explicit scope dependencies without a global service lookup.</summary>
    public sealed class ProfileServiceFactory
    {
        private readonly OriginalProgressionConfig config;
        private readonly ISongCatalog catalog;
        private readonly IProfileStore store;
        private readonly ApplicationSession session;
        private readonly ICalendar calendar;

        public ProfileServiceFactory(OriginalProgressionConfig config, ISongCatalog catalog, IProfileStore store,
            ApplicationSession session, ICalendar calendar)
        {
            this.config = config ?? throw new ArgumentNullException(nameof(config));
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.calendar = calendar ?? throw new ArgumentNullException(nameof(calendar));
        }

        public OriginalProgression Create()
        {
            var profile = OriginalProgression.FromJson(config, store.Read(), catalog, store, true, session);
            profile.BeginDailySession(session.Id);
            profile.StartDiceEvent(calendar.Today);
            return profile;
        }
    }
}
