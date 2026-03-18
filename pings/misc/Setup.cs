using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using I2.Loc;
using pings.outlines;
using pings.outlines.fancy;
using pings.outlines.quick;
using Sirenix.Utilities;
using UltimateWater;
using UnityEngine;
using UnityEngine.UI;
using Outline = pings.outlines.Outline;

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
                vh.AddVert(new Vector3(0, -h/2), color, Vector2.zero); // Bottom
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
                Pings.LogError("No language sources found. Should not happen. If happened, skill issue.");
                return;
            }
            
            var langCsv = Encoding.UTF8.GetString(Pings.mod.GetEmbeddedFileBytes("misc/lang.csv"));
            source.Import_CSV(null, langCsv, eSpreadsheetUpdateMode.Merge, ';');
        }

        
        private static AssetBundle _assetQuick, _assetFancy;
        internal static IEnumerator LoadAssets()
        {
            // Quick Outline
            // https://assetstore.unity.com/packages/tools/particles-effects/quick-outline-115488
            
            var request = AssetBundle.LoadFromMemoryAsync(Pings.mod.GetEmbeddedFileBytes("outlines/quick/QuickOutline.assets"));
            yield return request;
            
            _assetQuick = request.assetBundle;
            try {
                QuickOutlineBundle.OutlineMaterial = _assetQuick.LoadAsset<Material>("OutlineMask");
                QuickOutlineBundle.FillMaterial = _assetQuick.LoadAsset<Material>("OutlineFill");
            } catch (Exception ex) { Pings.LogError($"Failed to load Quick Outline Materials: {ex}", sub:"Setup"); }
            _assetQuick?.Unload(false);

            if (!QuickOutlineBundle.OutlineMaterial) Pings.LogError("Failed to load Quick Outline Material", sub:"Setup");
            if (!QuickOutlineBundle.FillMaterial) Pings.LogError("ailed to load Quick Outline Fill Material", sub:"Setup");
            
            // Fancy Outline
            // https://github.com/cakeslice/Outline-Effect
            
            request = AssetBundle.LoadFromMemoryAsync(Pings.mod.GetEmbeddedFileBytes("outlines/fancy/FancyOutline.assets"));
            yield return request;
            
            _assetFancy = request.assetBundle;
            try {
                FancyOutlineBundle.BufferShader = _assetFancy.LoadAsset<Shader>("BufferShader");
                FancyOutlineBundle.OutlineShader = _assetFancy.LoadAsset<ComputeShader>("OutlineShader");
            } catch (Exception ex) { Pings.LogError($"Failed to load Fancy Outline Materials: {ex}", sub:"Setup"); }
            _assetFancy?.Unload(false);
        
            if (!FancyOutlineBundle.BufferShader) Pings.LogError("Failed to load Fancy Outline Buffer Shader", sub:"Setup");
            if (!FancyOutlineBundle.OutlineShader) Pings.LogError("Failed to load Fancy Outline Compute Shader", sub:"Setup");
        }

        internal static void UnloadAssets()
        {
            
            var allOutlines = FindObjectsOfType<Outline>(); // In case any outlines are left behind
            if (allOutlines.Length > 0) Pings.Log($"Destroying {allOutlines.Length} outline(s)", sub:"Unloading");
            foreach (var outline in allOutlines) if (outline)
            {
                outline.enabled = false;
                DestroyImmediate(outline);
            }
            Pings.Camera.gameObject.GetComponents<FancyOutlineOnCamera>()?.ForEach(DestroyImmediate);
            
            Destroy(QuickOutlineBundle.OutlineMaterial);
            Destroy(QuickOutlineBundle.FillMaterial);
            
            Destroy(FancyOutlineBundle.BufferShader);
            Destroy(FancyOutlineBundle.OutlineShader);
        }
    }
}