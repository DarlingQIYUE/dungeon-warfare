using UnityEngine;

namespace DungeonWarfare
{
    /// <summary>
    /// Locks the view to a fixed 16:9 (black bars when the window differs) and
    /// splits that area into a left play region and a right sidebar, Red Alert
    /// style. Assigns the resulting viewport rects to the world camera and an
    /// optional sidebar camera so the play area renders left and the sidebar
    /// (an empty colored panel for now) sits on the right.
    /// </summary>
    public class GameViewportLayout : MonoBehaviour
    {
        [SerializeField] private Camera worldCamera;
        [SerializeField] private Camera sidebarCamera;

        [Header("Target frame")]
        [SerializeField] private float aspectWidth = 16f;
        [SerializeField] private float aspectHeight = 9f;
        [Tooltip("How many of the 16 horizontal units the right sidebar takes.")]
        [SerializeField] private float sidebarUnits = 2f;

        private int lastScreenW;
        private int lastScreenH;

        private void OnEnable() => Apply();

        private void Update()
        {
            if (Screen.width != lastScreenW || Screen.height != lastScreenH)
                Apply();
        }

        private void Apply()
        {
            lastScreenW = Screen.width;
            lastScreenH = Screen.height;

            Rect frame = Fit();                          // the 16:9 area inside the window
            float playFrac = (aspectWidth - sidebarUnits) / aspectWidth; // e.g. 14/16
            float sideFrac = sidebarUnits / aspectWidth;                 // e.g. 2/16

            if (worldCamera != null)
                worldCamera.rect = new Rect(
                    frame.x, frame.y, frame.width * playFrac, frame.height);

            if (sidebarCamera != null)
                sidebarCamera.rect = new Rect(
                    frame.x + frame.width * playFrac, frame.y, frame.width * sideFrac, frame.height);
        }

        private Rect Fit()
        {
            float targetAspect = aspectWidth / aspectHeight;
            float windowAspect = (float)Screen.width / Screen.height;
            float scale = windowAspect / targetAspect;

            if (scale < 1f) // window too tall -> letterbox
                return new Rect(0f, (1f - scale) * 0.5f, 1f, scale);

            float inverse = 1f / scale; // window too wide -> pillarbox
            return new Rect((1f - inverse) * 0.5f, 0f, inverse, 1f);
        }
    }
}
