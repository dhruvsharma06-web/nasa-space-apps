using System;
using UnityEngine;

namespace Astra.XR.Input
{
    /// <summary>
    /// The interactor performing the UI action currently being invoked, so parameterless UI bindings
    /// (UnityEvent → PauseMenu.Pause) can report the modality that actually acted (CONTRACT.md §3).
    /// Null means a desktop EventSystem click; callers then use their configured default actor.
    /// </summary>
    public static class UiActionActor
    {
        public static Component Current { get; private set; }

        public static Scope Push(Component actor) => new Scope(actor);

        public readonly struct Scope : IDisposable
        {
            private readonly Component previous;

            internal Scope(Component actor)
            {
                previous = Current;
                Current = actor;
            }

            public void Dispose() => Current = previous;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Current = null;
    }
}
