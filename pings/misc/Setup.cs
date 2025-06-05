using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using HMLLibrary;
using I2.Loc;
using UnityEngine;
using UnityEngine.UI;

namespace pings
{
    public class Setup : MonoBehaviour
    {
        private static readonly Dictionary<string, Dictionary<string, string>> Translations = new Dictionary<string, Dictionary<string, string>>
        {
            {
                "pings/ping",
                new Dictionary<string, string>
                {
                    {"en", "Ping"},
                    {"sv", "Ping"},
                    {"fr", "Ping"},
                    {"it", "Ping"},
                    {"de", "Ping"},
                    {"es", "Ping"},
                    {"pl", "Ping"},
                    {"pt-BR", "Ping"},
                    {"zh-CN", "Ping"},
                    {"ja", "Ping"},
                    {"ko", "Ping"},
                    {"uk", "Пінг"}
                }
            }
        };
        
        
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
            var diamond = pingPrefab.AddComponent<DiamondShape>();
            diamond.color = Color.white;
            var rect = pingPrefab.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(20, 20);
            pingPrefab.transform.SetParent(_canvas.transform);
            pingPrefab.SetActive(false);
        
            // Add text as child
            var textObj = new GameObject("PingText");
            textObj.transform.SetParent(pingPrefab.transform);
            var text = textObj.AddComponent<Text>();
            
            var gameFont = Resources.FindObjectsOfTypeAll<Font>().FirstOrDefault(f => f.name == "ChineseRocks");
            if (!gameFont)
                gameFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
            
            
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
        
        private class DiamondShape : MaskableGraphic
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

        internal static LanguageSourceData LoadLocalizations()
        {
            var source = LocalizationManager.Sources?[0];
            if (source == null)
            {
                Debug.LogWarning("No language sources found. Should not happen. If happened, skill issue.");
                return null;
            }
            
            Debug.Log(source.GetLanguagesCode().Aggregate("Languages: ", (current, lang) => current + $"{lang}, ").TrimEnd(',', ' '));
            // foreach (var (term, entries) in Translations)
            //     AddEntry(term, entries);
            // source.UpdateDictionary();
            source.Import_CSV(null, Pings.langCsv, eSpreadsheetUpdateMode.Merge, ';');
            return source;
        }
        

        private static void AddEntry(string term, Dictionary<string, string> entries)
        {
            var source = LocalizationManager.Sources[0];
            var termData = source.AddTerm(term, eTermType.Text);
            
            foreach (var (langCode, translate) in entries)
            {
                var langIndex = source.GetLanguageIndexFromCode(langCode);
                if (langIndex < 0)
                {
                    if (Pings.DebugMode)
                        Debug.LogWarning($"Language '{langCode}' not found in source. Skipping entry for this language.");
                    continue;
                }
                termData.SetTranslation(langIndex, translate);
            }
        }
    }
}