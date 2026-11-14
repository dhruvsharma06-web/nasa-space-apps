using UnityEngine;

namespace Astra.Integrations.Coach
{
    /// <summary>
    /// Scene component the LevelController asks for a coach (CONTRACT.md §9).
    /// OWNER: Content & AI Coach. Stub returns null (no coach text) until implemented:
    /// then it returns OfflineCoach, or HttpCoachClient when a proxy URL is configured.
    /// </summary>
    public class CoachProvider : MonoBehaviour
    {
        public virtual ICoachClient Client => null;
    }
}
