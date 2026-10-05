using UnityEngine;

namespace FishPuzzle.Presentation
{
    /// <summary>
    /// Removes presentation objects without calling DestroyImmediate while the game is running.
    /// Edit Mode scene authoring still destroys immediately, because Object.Destroy is not legal there.
    /// </summary>
    internal static class SceneObjectCleanup
    {
        public static void DestroyObject(GameObject target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                target.SetActive(false);
                if (target.transform.parent != null)
                {
                    target.transform.SetParent(null, false);
                }

                Object.Destroy(target);
                return;
            }

#if UNITY_EDITOR
            Object.DestroyImmediate(target);
#else
            Object.Destroy(target);
#endif
        }
    }
}
