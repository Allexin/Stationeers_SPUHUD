using HarmonyLib;
using StationeersMods.Interface;
using UnityEngine;
using BepInEx.Configuration;

[StationeersMod("basovav.spuhud", "SPU HUD Mod", "1.0.0")]
public class SPUHUDMod : ModBehaviour
{
    public static ConfigEntry<int> FontSize;
    public static ConfigEntry<string> TextColor;
    public static ConfigEntry<int> PositionX;
    public static ConfigEntry<int> PositionY;
    private static bool _initialized = false;
    
    public override void OnLoaded(ContentHandler contentHandler)
    {
                if (_initialized)
        {
            return;
        }
        
        // Initialize configuration settings
        FontSize = Config.Bind("Display", "FontSize", 40, 
            new ConfigDescription("Font size for HUD text", new AcceptableValueRange<int>(10, 50)));
            
        TextColor = Config.Bind("Display", "TextColor", "yellow", 
            new ConfigDescription("Color of HUD text (red, green, blue, yellow, white, black, cyan, magenta)"));
            
        PositionX = Config.Bind("Position", "PositionX", 50, 
            new ConfigDescription("Horizontal position of HUD from left edge", new AcceptableValueRange<int>(0, 1920)));
            
        PositionY = Config.Bind("Position", "PositionY", 50, 
            new ConfigDescription("Vertical position of HUD from top edge", new AcceptableValueRange<int>(0, 1080)));
        
                        Harmony harmony = new Harmony("SPUHUDMod");
                harmony.PatchAll();

                _initialized = true;
    }
}
