namespace Astra.XR.Interactables
{
    /// <summary>P1 emitter IDs follow content-style-guide.md §5; mission validation remains in Core.</summary>
    public static class InteractionValidation
    {
        public static bool IsTargetId(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length > 64)
                return false;

            for (var i = 0; i < value.Length; i++)
            {
                var character = value[i];
                if ((character < 'a' || character > 'z') &&
                    (character < '0' || character > '9') && character != '_')
                    return false;
            }

            return true;
        }
    }
}
