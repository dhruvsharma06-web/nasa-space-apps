namespace Astra.Core.Lifecycle
{
    /// <summary>Pause / recenter / restart / exit must be reachable from every mission (FR-APP-03).</summary>
    public interface IAppLifecycle
    {
        void Pause();
        void Resume();
        void Recenter();
        void RestartModule();
        void ExitToHub();
    }
}
