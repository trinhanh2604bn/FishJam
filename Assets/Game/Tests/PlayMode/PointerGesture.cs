using FishPuzzle.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;

namespace FishPuzzle.Tests.PlayMode
{
    internal static class PointerGesture
    {
        public static void Click(FishView fish)
        {
            var point = Center(fish);
            Down(fish, point);
            Up(fish, point);
        }

        public static void Press(FishView fish)
        {
            Down(fish, Center(fish));
        }

        public static void ReleaseOver(FishView fish)
        {
            Up(fish, Center(fish));
        }

        public static void Cancel(FishView fish)
        {
            var point = Center(fish);
            Down(fish, point);
            Up(fish, point + new Vector2(900f, 900f));
        }

        private static void Down(FishView fish, Vector2 screen)
        {
            var data = new PointerEventData(EventSystem.current);
            data.position = screen;
            ExecuteEvents.Execute(fish.gameObject, data, ExecuteEvents.pointerDownHandler);
        }

        private static void Up(FishView fish, Vector2 screen)
        {
            var data = new PointerEventData(EventSystem.current);
            data.position = screen;
            ExecuteEvents.Execute(fish.gameObject, data, ExecuteEvents.pointerUpHandler);
        }

        private static Vector2 Center(FishView fish)
        {
            var rect = fish != null ? fish.transform as RectTransform : null;
            if (rect == null)
            {
                return new Vector2(200f, 200f);
            }

            var canvas = fish.GetComponentInParent<Canvas>();
            Camera camera = null;
            if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                camera = canvas.worldCamera;
            }

            return RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center));
        }
    }
}
