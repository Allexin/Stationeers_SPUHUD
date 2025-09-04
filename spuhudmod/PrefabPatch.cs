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
    // HUD Manager component that gets attached to SensorLenses GameObject
    public class HUDManager : MonoBehaviour
    {
        public GameObject hudCanvas;
        public List<UnityEngine.UI.Text> hudTextComponents = new List<UnityEngine.UI.Text>();
        
        public void CreateHUDCanvas()
        {
            if (hudCanvas != null) return;
            
            hudCanvas = new GameObject("SPUHUD_Canvas");
            var canvas = hudCanvas.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = -100; // Render under everything else
            
            // Don't add CanvasScaler to avoid font size inconsistencies
        }
        
        public void UpdateHUDLines(string[] lines)
        {
            if (lines == null || lines.Length == 0) return;
            
            if (hudCanvas == null) CreateHUDCanvas();
            
            // Update text components or create if needed
            for (int i = 0; i < lines.Length; i++)
            {
                if (i >= hudTextComponents.Count)
                {
                    CreateTextComponent(i);
                }
                
                if (i < hudTextComponents.Count && hudTextComponents[i] != null)
                {
                    hudTextComponents[i].text = lines[i];
                    hudTextComponents[i].gameObject.SetActive(true);
                }
            }

            // Hide extra text components
            for (int i = lines.Length; i < hudTextComponents.Count; i++)
            {
                if (hudTextComponents[i] != null)
                {
                    hudTextComponents[i].gameObject.SetActive(false);
                }
            }
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
                string[] dynamicHUDLines = GetSuitChipData(InventoryManager.ParentHuman);
                hudManager.UpdateHUDLines(dynamicHUDLines);
                hudManager.ShowHUD();
            }
            else
            {
                hudManager.HideHUD();
            }
        }
        
        private static string[] GetSuitChipData(Human human)
        {
            var hudLinesList = new List<string>();
            
            try
            {
                // Check what's in the suit slot
                var suitSlot = human.SuitSlot;
                if (suitSlot == null)
                {
                    hudLinesList.Add("HUD Disabled: No suit slot");
                    return hudLinesList.ToArray();
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
                    hudLinesList.Add("HUD Disabled: Wrong suit type");
                    return hudLinesList.ToArray();
                }
                else
                {
                    hudLinesList.Add("HUD Disabled: No suit");
                    return hudLinesList.ToArray();
                }
                
                if (chipSlot == null)
                {
                    hudLinesList.Add("HUD Disabled: No chip slot");
                    return hudLinesList.ToArray();
                }
                
                var chip = chipSlot.Get<ProgrammableChip>();
                if (chip == null)
                {
                    hudLinesList.Add("HUD Disabled: No chip");
                    return hudLinesList.ToArray();
                }
                
                // All conditions met - chip is available
                hudLinesList.Add("HUD: Active");
                
                // Get aliases and registers using reflection
                var aliasesField = typeof(ProgrammableChip).GetField("_Aliases", BindingFlags.NonPublic | BindingFlags.Instance);
                var registersField = typeof(ProgrammableChip).GetField("_Registers", BindingFlags.NonPublic | BindingFlags.Instance);
                
                if (aliasesField != null && registersField != null)
                {
                    var aliases = aliasesField.GetValue(chip);
                    var registers = (double[])registersField.GetValue(chip);
                    
                    if (aliases != null && registers != null)
                    {
                        // Process HUD aliases
                        ProcessHUDAliases(aliases, registers, hudLinesList);
                    }
                }
                
            }
            catch (System.Exception ex)
            {
                hudLinesList.Clear();
                hudLinesList.Add($"HUD Error: {ex.Message}");
                Debug.LogError($"SPUHUD: Error reading chip data: {ex.Message}");
            }
            
            return hudLinesList.ToArray();
        }
        
        private static void ProcessHUDAliases(object aliases, double[] registers, List<string> hudLinesList)
        {
            try
            {
                // aliases is Dictionary<string, _AliasValue> but _AliasValue is private
                // We need to use reflection to iterate through it
                var aliasesDict = aliases as System.Collections.IDictionary;
                if (aliasesDict == null) return;

                const double INACTIVE_VALUE = -500.0;
                const double EPSILON = 0.00001;

                foreach (System.Collections.DictionaryEntry entry in aliasesDict)
                {
                    string aliasKey = entry.Key as string;
                    if (aliasKey != null && aliasKey.StartsWith("HUD"))
                    {
                        // Get _AliasValue using reflection
                        var aliasValue = entry.Value;
                        var targetField = aliasValue.GetType().GetField("Target");
                        var indexField = aliasValue.GetType().GetField("Index");
                        
                        if (targetField != null && indexField != null)
                        {
                            int target = (int)targetField.GetValue(aliasValue);
                            int index = (int)indexField.GetValue(aliasValue);
                            
                            // Check if it's a register alias (Target == 1 for Register)
                            if (target == 1 && index >= 0 && index < registers.Length)
                            {
                                double value = registers[index];
                                
                                // Check if value is not the inactive marker
                                if (Math.Abs(value - INACTIVE_VALUE) > EPSILON)
                                {
                                    // Remove "HUD" prefix and format display key
                                    string displayKey = FormatAliasKey(aliasKey.Substring(3)); // Remove "HUD"
                                    hudLinesList.Add($"{displayKey}: {value:F2}");
                                }
                            }
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                hudLinesList.Add($"Alias Error: {ex.Message}");
                Debug.LogError($"SPUHUD: Error processing aliases: {ex.Message}");
            }
        }
        
        private static string FormatAliasKey(string aliasKey)
        {
            if (string.IsNullOrEmpty(aliasKey))
                return aliasKey;
            
            var result = new StringBuilder();
            
            // Add first character as-is
            result.Append(aliasKey[0]);
            
            // Add spaces before uppercase letters (except the first character)
            for (int i = 1; i < aliasKey.Length; i++)
            {
                if (char.IsUpper(aliasKey[i]))
                {
                    result.Append(' ');
                }
                result.Append(aliasKey[i]);
            }
            
            return result.ToString();
        }
    }
}
