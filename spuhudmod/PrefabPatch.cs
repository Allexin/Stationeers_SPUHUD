using Assets.Scripts;
using Assets.Scripts.GridSystem;
using Assets.Scripts.Inventory;
using Assets.Scripts.Objects.Items;
using Assets.Scripts.Objects.Clothing;
using Assets.Scripts.Objects.Electrical;
using Assets.Scripts.Objects.Entities;
using Assets.Scripts.Objects;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using System;
using System.Reflection;
using System.Collections.Generic;
using System.Text;

namespace spuhudmod
{
    // HUD formatting codes
    public enum HUDFormatCode
    {
        Float2Decimal = 0,      // float с выводом двух цифр после запятой
        Integer = 1,            // округление до целого
        NormalizedPercent = 2,  // 0.0-1.0 -> 0%-100%
        Percent = 3,            // процент с двумя цифрами после запятой
        Kelvin = 4,             // градусы в кельвинах с °К
        Celsius = 5,            // градусы в цельсии с °С
        CelsiusFromKelvin = 6,  // конвертация из кельвинов в цельсии
        Pressure = 7,           // давление в Pa/kPa/MPa
        Liters = 8              // литры с суффиксом L
    }

    // HUD color codes
    public static class HUDColors
    {
        public static readonly Color[] Colors = {
            new Color(0x21/255f, 0x2A/255f, 0xA5/255f), // 0 Blue
            new Color(0x7B/255f, 0x7B/255f, 0x7B/255f), // 1 Gray
            new Color(0x3F/255f, 0x9B/255f, 0x39/255f), // 2 Green
            new Color(0xFF/255f, 0x66/255f, 0x2B/255f), // 3 Orange
            new Color(0xE7/255f, 0x02/255f, 0x00/255f), // 4 Red
            new Color(0xFF/255f, 0xBC/255f, 0x1B/255f), // 5 Yellow
            new Color(0xE7/255f, 0xE7/255f, 0xE7/255f), // 6 White
            new Color(0x08/255f, 0x09/255f, 0x08/255f), // 7 Black
            new Color(0x63/255f, 0x3C/255f, 0x2B/255f), // 8 Brown
            new Color(0x63/255f, 0x63/255f, 0x3F/255f), // 9 Khaki
            new Color(0xE4/255f, 0x1C/255f, 0x99/255f), // 10 Pink
            new Color(0x73/255f, 0x2C/255f, 0xA7/255f)  // 11 Purple
        };
        
        public static Color GetColor(int colorCode)
        {
            if (colorCode < 0 || colorCode >= Colors.Length)
                return Colors[11]; // Purple as default
            return Colors[colorCode];
        }
    }

    // HUD data structure for memory cells
    public struct HUDMemoryData
    {
        public double Value;        // 0 - значение
        public double ShowFlag;     // 1 - флаг отображения
        public double Format;       // 2 - форматирование
        public double Color;        // 3 - цвет
        // 4-9 reserved
        
        public bool ShouldShow => Math.Abs(ShowFlag) > 0.001;
        public int FormatCode => Math.Max(0, Math.Min(8, (int)Math.Round(Format)));
        public int ColorCode => Math.Max(0, Math.Min(11, (int)Math.Round(Color)));
    }

    // HUD Manager component that gets attached to SensorLenses GameObject
    public class HUDManager : MonoBehaviour
    {
        public GameObject hudCanvas;
        public List<UnityEngine.UI.Text> hudTextComponents = new List<UnityEngine.UI.Text>();
        

        
        // Cache for previous frame data to avoid unnecessary UI updates
        private List<(string text, Color color)> _previousHudData = new List<(string text, Color color)>();
        
        public void CreateHUDCanvas()
        {
            if (hudCanvas != null) return;
            
            hudCanvas = new GameObject("SPUHUD_Canvas");
            var canvas = hudCanvas.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = -100; // Render under everything else
            
            // Don't add CanvasScaler to avoid font size inconsistencies
        }
        
        public void UpdateHUDLines(List<(string text, Color color)> hudData)
        {
            if (hudData == null || hudData.Count == 0) return;
            
            // Check if data has changed to avoid unnecessary UI updates
            if (HudDataEquals(hudData, _previousHudData)) return;
            
            if (hudCanvas == null) CreateHUDCanvas();
            
            // Update text components or create if needed
            for (int i = 0; i < hudData.Count; i++)
            {
                if (i >= hudTextComponents.Count)
                {
                    CreateTextComponent(i);
                }
                
                if (i < hudTextComponents.Count && hudTextComponents[i] != null)
                {
                    hudTextComponents[i].text = hudData[i].text;
                    hudTextComponents[i].color = hudData[i].color;
                    hudTextComponents[i].gameObject.SetActive(true);
                }
            }

            // Hide extra text components
            for (int i = hudData.Count; i < hudTextComponents.Count; i++)
            {
                if (hudTextComponents[i] != null)
                {
                    hudTextComponents[i].gameObject.SetActive(false);
                }
            }
            
            // Cache current data for next frame comparison
            _previousHudData.Clear();
            _previousHudData.AddRange(hudData);
        }
        
        private bool HudDataEquals(List<(string text, Color color)> data1, List<(string text, Color color)> data2)
        {
            if (data1.Count != data2.Count) return false;
            
            for (int i = 0; i < data1.Count; i++)
            {
                if (data1[i].text != data2[i].text || data1[i].color != data2[i].color)
                    return false;
            }
            
            return true;
        }
        
        private void CreateTextComponent(int index)
        {
            if (hudCanvas == null) return;

            int fontSize = SPUHUDMod.FontSize?.Value ?? 40;
            int lineHeight = fontSize + 4;

            var textGO = new GameObject($"SPUHUD_Line_{index}");
            textGO.transform.SetParent(hudCanvas.transform);
            
            var text = textGO.AddComponent<UnityEngine.UI.Text>();
            text.color = ParseColor(SPUHUDMod.TextColor?.Value ?? "yellow");
            text.fontSize = fontSize;
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.UpperLeft;
            
            var rectTransform = textGO.GetComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0, 1);
            rectTransform.anchorMax = new Vector2(0, 1);
            rectTransform.pivot = new Vector2(0, 1);
            
            int posX = SPUHUDMod.PositionX?.Value ?? 20;
            int posY = SPUHUDMod.PositionY?.Value ?? 50;
            
            float yPos = -posY - (index * lineHeight);
            rectTransform.anchoredPosition = new Vector2(posX, yPos);
            rectTransform.sizeDelta = new Vector2(4000, lineHeight);

            hudTextComponents.Add(text);
        }
        
        private Color ParseColor(string colorName)
        {
            switch (colorName.ToLower())
            {
                case "red": return Color.red;
                case "green": return Color.green;
                case "blue": return Color.blue;
                case "yellow": return Color.yellow;
                case "white": return Color.white;
                case "black": return Color.black;
                case "cyan": return Color.cyan;
                case "magenta": return Color.magenta;
                default: return Color.yellow;
            }
        }
        
        public void HideHUD()
        {
            if (hudCanvas != null)
            {
                hudCanvas.SetActive(false);
            }
        }
        
        public void ShowHUD()
        {
            if (hudCanvas != null)
            {
                hudCanvas.SetActive(true);
            }
        }
        
        private void OnDestroy()
        {
            // Canvas will be automatically destroyed when this component is destroyed
            if (hudCanvas != null)
            {
                Destroy(hudCanvas);
                hudCanvas = null;
            }
            hudTextComponents.Clear();
        }
    }

    [HarmonyPatch]
    public class SensorLensesPatch
    {
        // Cached list to avoid allocations every frame
        private static List<(string text, Color color)> _cachedHudDataList = new List<(string text, Color color)>();
                [HarmonyPatch(typeof(SensorLenses), "UpdateEachFrame")]
        [HarmonyPostfix]
        public static void ShowHUDText(SensorLenses __instance)
        {
            // Check if HUD should be displayed
            bool shouldShowHUD = GameManager.GameState == GameState.Running && 
                InventoryManager.ParentHuman != null && 
                InventoryManager.ParentHuman == __instance.RootParentHuman && 
                __instance.ParentSlot == InventoryManager.ParentHuman.GlassesSlot &&
                __instance.IsOperable && 
                __instance.InteractOnOff.State == 1;

            // Get or create HUD manager
            var hudManager = __instance.GetComponent<HUDManager>();
            if (hudManager == null)
            {
                hudManager = __instance.gameObject.AddComponent<HUDManager>();
            }

            if (shouldShowHUD)
            {
                var dynamicHUDData = GetSuitChipData(InventoryManager.ParentHuman);
                hudManager.UpdateHUDLines(dynamicHUDData);
                hudManager.ShowHUD();
            }
            else
            {
                hudManager.HideHUD();
            }
        }
        
        private static List<(string text, Color color)> GetSuitChipData(Human human)
        {
            // Clear and reuse cached list to avoid allocations
            _cachedHudDataList.Clear();
            var hudDataList = _cachedHudDataList;
            
            try
            {
                // Check what's in the suit slot
                var suitSlot = human.SuitSlot;
                if (suitSlot == null)
                {
                    hudDataList.Add(("HUD Disabled: No suit slot", Color.red));
                    return hudDataList;
                }
                
                // Check for different suit types and get chip slot
                Slot chipSlot = null;
                
                if (suitSlot.Contains<AdvancedSuit>())
                {
                    var advancedSuit = suitSlot.Get<AdvancedSuit>();
                    chipSlot = advancedSuit.ChipSlot;
                }
                else if (suitSlot.Contains<SuitBase>())
                {
                    var suitBase = suitSlot.Get<SuitBase>();
                    chipSlot = suitBase.ChipSlot;
                }
                else if (!suitSlot.IsEmpty())
                {
                    hudDataList.Add(("HUD Disabled: Wrong suit type", Color.red));
                    return hudDataList;
                }
                else
                {
                    hudDataList.Add(("HUD Disabled: No suit", Color.red));
                    return hudDataList;
                }
                
                if (chipSlot == null)
                {
                    hudDataList.Add(("HUD Disabled: No chip slot", Color.red));
                    return hudDataList;
                }
                
                var chip = chipSlot.Get<ProgrammableChip>();
                if (chip == null)
                {
                    hudDataList.Add(("HUD Disabled: No chip", Color.red));
                    return hudDataList;
                }
                
                // Get defines and memory using reflection
                var definesField = typeof(ProgrammableChip).GetField("_Defines", BindingFlags.NonPublic | BindingFlags.Instance);
                var stackField = typeof(ProgrammableChip).GetField("_Stack", BindingFlags.NonPublic | BindingFlags.Instance);
                
                if (definesField != null && stackField != null)
                {
                    var defines = definesField.GetValue(chip);
                    var memory = (double[])stackField.GetValue(chip);
                    
                    if (defines != null && memory != null)
                    {
                        // Add status line
                        hudDataList.Add(("HUD: Active", Color.white));
                        
                        // Process HUD defines
                        ProcessHUDDefines(defines, memory, hudDataList);
                    }
                    else
                    {
                        hudDataList.Add(("HUD Error: No defines or memory", Color.red));
                    }
                }
                else
                {
                    hudDataList.Add(("HUD Error: Cannot access defines/memory", Color.red));
                }
                
            }
            catch (System.Exception ex)
            {
                hudDataList.Clear();
                hudDataList.Add(($"HUD Error: {ex.Message}", Color.red));
                Debug.LogError($"SPUHUD: Error reading chip data: {ex.Message}");
            }
            
            return hudDataList;
        }
        
        private static void ProcessHUDDefines(object defines, double[] memory, List<(string text, Color color)> hudDataList)
        {
            try
            {
                // defines is Dictionary<string, double> 
                var definesDict = defines as System.Collections.IDictionary;
                if (definesDict == null) return;

                foreach (System.Collections.DictionaryEntry entry in definesDict)
                {
                    string defineKey = entry.Key as string;
                    if (defineKey != null && defineKey.StartsWith("HUD"))
                    {
                        double memoryAddress = (double)entry.Value;
                        int baseAddress = (int)Math.Round(memoryAddress);
                        
                        // Check if we have enough memory cells (need 10 cells: 0-9)
                        if (baseAddress >= 0 && baseAddress + 9 < memory.Length)
                        {
                            var hudData = new HUDMemoryData
                            {
                                Value = memory[baseAddress + 0],
                                ShowFlag = memory[baseAddress + 1],
                                Format = memory[baseAddress + 2],
                                Color = memory[baseAddress + 3]
                            };
                            
                            // Check if this line should be displayed
                            if (hudData.ShouldShow)
                            {
                                // Format the value according to format code
                                string formattedValue = FormatValue(hudData.Value, (HUDFormatCode)hudData.FormatCode);
                                
                                // Get color
                                Color lineColor = HUDColors.GetColor(hudData.ColorCode);
                                
                                // Remove "HUD" prefix and format display key
                                string displayKey = FormatDefineKey(defineKey.Substring(3)); // Remove "HUD"
                                
                                hudDataList.Add(($"{displayKey}: {formattedValue}", lineColor));
                            }
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                hudDataList.Add(($"Define Error: {ex.Message}", Color.red));
                Debug.LogError($"SPUHUD: Error processing defines: {ex.Message}");
            }
        }
        
        private static string FormatValue(double value, HUDFormatCode formatCode)
        {
            switch (formatCode)
            {
                case HUDFormatCode.Float2Decimal:
                    return value.ToString("F2");
                    
                case HUDFormatCode.Integer:
                    return Math.Round(value).ToString("F0");
                    
                case HUDFormatCode.NormalizedPercent:
                    return (value * 100).ToString("F0") + "%";
                    
                case HUDFormatCode.Percent:
                    return value.ToString("F2") + "%";
                    
                case HUDFormatCode.Kelvin:
                    return value.ToString("F2") + "°K";
                    
                case HUDFormatCode.Celsius:
                    return value.ToString("F2") + "°C";
                    
                case HUDFormatCode.CelsiusFromKelvin:
                    return (value - 273.15).ToString("F2") + "°C";
                    
                case HUDFormatCode.Pressure:
                    if (value < 1)
                        return (value * 1000).ToString("F0") + "Pa";
                    else if (value < 1000)
                        return value.ToString("F2") + "kPa";
                    else if (value < 10000)
                        return value.ToString("F0") + "kPa";
                    else
                        return (value / 1000).ToString("F2") + "MPa";
                        
                case HUDFormatCode.Liters:
                    return Math.Round(value).ToString("F0") + "L";
                    
                default:
                    return value.ToString("F2");
            }
        }
        
        private static string FormatDefineKey(string defineKey)
        {
            if (string.IsNullOrEmpty(defineKey))
                return defineKey;
            
            var result = new StringBuilder();
            
            // Add first character as-is
            result.Append(defineKey[0]);
            
            // Add spaces before uppercase letters (except the first character)
            for (int i = 1; i < defineKey.Length; i++)
            {
                if (char.IsUpper(defineKey[i]))
                {
                    result.Append(' ');
                }
                result.Append(defineKey[i]);
            }
            
            return result.ToString();
        }
    }
}
