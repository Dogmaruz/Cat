using System;
using DancingCats.Clone;
using VContainer;

namespace RhythmGame.Sessions
{
    public sealed class RunSession : IDisposable
    {
        public string Id { get; } = Guid.NewGuid().ToString("N");
        public OriginalScore Score { get; } = new OriginalScore();
        public bool IsDisposed { get; private set; }
        public void Dispose() => IsDisposed = true;
    }

    // Container access is confined to this composition factory, never to a tile or Update.
    public sealed class RunSessionFactory : IDisposable
    {
        private readonly IObjectResolver parent;
        private IScopedObjectResolver scope;
        private bool disposed;
        public RunSession Current { get; private set; }

        public RunSessionFactory(IObjectResolver parent)
        {
            this.parent = parent ?? throw new ArgumentNullException(nameof(parent));
        }

        public RunSession Begin()
        {
            if (disposed) throw new ObjectDisposedException(nameof(RunSessionFactory));
            scope?.Dispose();
            scope = parent.CreateScope(builder => builder.Register<RunSession>(Lifetime.Scoped));
            Current = scope.Resolve<RunSession>();
            return Current;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            scope?.Dispose();
            scope = null;
            Current = null;
        }
    }
}
