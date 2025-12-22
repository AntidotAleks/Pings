using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using I2.Loc;
using UnityEngine;
using UnityEngine.UI;

namespace pings
{
    public class Setup : MonoBehaviour
    {
        
        
        private static Canvas _canvas;
        
        internal static Canvas CreateCanvas()
        {
            var canvasObj = new GameObject("PingCanvas");
            _canvas = canvasObj.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceCamera;

            _canvas.worldCamera = GameObject.Find("UICamera")?.GetComponent<Camera>();
            
            _canvas.sortingLayerName = "Default";
            _canvas.sortingOrder = -10;

            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            canvasObj.AddComponent<GraphicRaycaster>();
            DontDestroyOnLoad(canvasObj);
            
            return _canvas;
        }

        internal static GameObject CreatePingPrefab()
        {
            var pingPrefab = new GameObject("PingIcon");
            var textObj = new GameObject("PingText");
            pingPrefab.transform.SetParent(_canvas.transform);
            textObj.transform.SetParent(pingPrefab.transform);
            
            var diamond = pingPrefab.AddComponent<DiamondShape>();
            
            var arrowObj = new GameObject("ArrowShape"); // Create a new GameObject for the arrow shape, since Ping Object already has a diamond shape
            arrowObj.transform.SetParent(pingPrefab.transform);
            var arrow = arrowObj.AddComponent<ArrowShape>();
            
            var rt = pingPrefab.GetComponent<RectTransform>();
            var text = textObj.AddComponent<Text>();
            
            var subObjects = pingPrefab.AddComponent<PingSubObjects>();
            subObjects.rectTransform = rt;
            subObjects.diamondShape = diamond;
            subObjects.arrowShape = arrow;
            subObjects.textObject = text;
            
            // Shapes
            
            diamond.color = Color.white;
            arrow.color = Color.clear;
            var arrowRt = arrowObj.GetComponent<RectTransform>();
            arrowRt.sizeDelta = rt.sizeDelta = new Vector2(20, 20);
            pingPrefab.SetActive(false);
        
            // Text
            
            var gameFont = Resources.FindObjectsOfTypeAll<Font>().FirstOrDefault(f => f.name == "ChineseRocks");
            if (!gameFont) gameFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
            
            text.font = gameFont;
            text.fontSize = 48;
            text.transform.localScale = Vector3.one * 0.3f; 
            text.alignment = TextAnchor.UpperCenter;
            text.color = Color.white;

            var textRect = textObj.GetComponent<RectTransform>();
            textRect.sizeDelta = new Vector2(6000, 60);
            textRect.anchoredPosition = new Vector2(0, -20); // Position below the diamond

            
            
            return pingPrefab;
        }
        
        public class PingSubObjects : MonoBehaviour
        {
            public RectTransform rectTransform;
            public DiamondShape diamondShape;
            public ArrowShape arrowShape;
            public Text textObject;
        }

        public class DiamondShape : MaskableGraphic
        {
            protected override void OnPopulateMesh(VertexHelper vh)
            {
                vh.Clear();
                var w = rectTransform.rect.width * 0.5f;
                var h = rectTransform.rect.height * 0.5f;

                // Diamond points: top, right, bottom, left
                vh.AddVert(new Vector3(0, h), color, Vector2.zero);    // Top
                vh.AddVert(new Vector3(w, 0), color, Vector2.zero);    // Right
                vh.AddVert(new Vector3(0, -h), color, Vector2.zero);   // Bottom
                vh.AddVert(new Vector3(-w, 0), color, Vector2.zero);   // Left

                // Two triangles to form a diamond
                vh.AddTriangle(0, 1, 2);
                vh.AddTriangle(2, 3, 0);
            }
        }
        
        public class ArrowShape : MaskableGraphic
        {
            protected override void OnPopulateMesh(VertexHelper vh)
            {
                vh.Clear();
                var w = rectTransform.rect.width * 0.5f;
                var h = rectTransform.rect.height * 0.5f;

                // Arrow points: top, right, bottom, left
                vh.AddVert(new Vector3(0, h), color, Vector2.zero);    // Top
                vh.AddVert(new Vector3(w, -h), color, Vector2.zero);   // Right
                vh.AddVert(new Vector3(0, -h/2), color, Vector2.zero);    // Bottom
                vh.AddVert(new Vector3(-w, -h), color, Vector2.zero);  // Left

                // Two triangles to form an arrow
                vh.AddTriangle(0, 1, 2);
                vh.AddTriangle(2, 3, 0);
            }
        }

        internal static void LoadLocalizations()
        {
            var source = LocalizationManager.Sources?[0];
            if (source == null)
            {
                Debug.LogError("No language sources found. Should not happen. If happened, skill issue.");
                return;
            }
            
            var langCsv = Encoding.UTF8.GetString(Pings.mod.GetEmbeddedFileBytes("lang.csv"));
            source.Import_CSV(null, langCsv, eSpreadsheetUpdateMode.Merge, ';');
        }

        
        private static AssetBundle _assetQuick, _assetFancy;
        internal static IEnumerator LoadOutlines()
        {
            #region Quick Outline
            // https://assetstore.unity.com/packages/tools/particles-effects/quick-outline-115488
            
            var request = AssetBundle.LoadFromMemoryAsync(Pings.mod.GetEmbeddedFileBytes("outlines/quick/outline.assets"));
            yield return request;
            
            _assetQuick = request.assetBundle;
            Pings.OutlineMaterial = _assetQuick.LoadAsset<Material>("OutlineMask");
            Pings.FillMaterial = _assetQuick.LoadAsset<Material>("OutlineFill");

            #endregion

            #region Fancy Outline
            // https://assetstore.unity.com/packages/vfx/shaders/fullscreen-camera-effects/outline-effect-78608
            
            var request2 = AssetBundle.LoadFromMemoryAsync(Pings.mod.GetEmbeddedFileBytes("outlines/fancy/betteroutline.assets"));
            yield return request2;

            _assetFancy = request2.assetBundle;
            Pings.OutlineShader = _assetFancy.LoadAsset<Shader>("OutlineShader");
            Pings.OutlineBufferShader = _assetFancy.LoadAsset<Shader>("OutlineBufferShader");

            #endregion
        }

        internal static void UnloadOutlines()
        {
            _assetQuick?.Unload(true);
            Destroy(Pings.OutlineMaterial);
            Destroy(Pings.FillMaterial);
            
            _assetFancy?.Unload(true);
            Destroy(Pings.OutlineShader);
            Destroy(Pings.OutlineBufferShader);
        }
    }
}