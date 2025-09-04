# SPU HUD Mod

A Stationeers mod that displays programmable HUD information from IC10 chip memory through sensor lenses and enhances Logic Transmitter functionality.

## Description

This experimental mod allows you to create custom HUD displays using IC10 programmable chips. When wearing sensor lenses connected to a suit with an IC10 chip, you can display formatted data directly on your screen. The mod also enhances Logic Transmitter functionality by allowing direct memory access to connected devices.

**⚠️ EXPERIMENTAL**: This mod is experimental and represents a Proof of Concept implementation. The code is not fully optimized or cleaned up. Use at your own risk. Expect potential bugs, performance issues, and incomplete features. This is a development version intended for testing and feedback.

## Features

- **HUD Display**: Shows formatted data from IC10 chip memory on sensor lenses
- **Logic Transmitter Enhancement**: Allows put/get commands to connected devices through Logic Transmitter
- **Multiple Format Codes**: Support for various data formats (temperature, pressure, percentages, etc.)
- **Color System**: 12-color palette for HUD text coloring
- **Define-based System**: Uses IC10 defines with "HUD" prefix for easy configuration
- **Save Safe**: Does not affect save file integrity - can be disabled at any moment

## Requirements

**IMPORTANT**: This mod requires [StationeersLaunchPad](https://github.com/StationeersLaunchPad/StationeersLaunchPad) to run properly. It will NOT work as a standalone BepInEx plugin.

- [StationeersLaunchPad](https://github.com/StationeersLaunchPad/StationeersLaunchPad) (includes BepInEx)

## Installation

### Prerequisites
1. Install [StationeersLaunchPad](https://github.com/StationeersLaunchPad/StationeersLaunchPad)
2. Make sure Stationeers is closed before installation

### Build from Source
1. Clone this repository: `git clone https://github.com/Allexin/Stationeers_SPUHUD.git`
2. Open the project in your preferred C# IDE (Visual Studio, Rider, etc.)
3. Add references to required Stationeers assemblies:
   - `Assembly-CSharp.dll` (from Stationeers game directory)
   - BepInEx libraries (included with StationeersLaunchPad)
4. Build the project to generate `SPUHUDMod.dll`
5. Copy the compiled `.dll` file to your Stationeers mods directory:
   - StationeersLaunchPad mods path: `%USERPROFILE%\AppData\Documents\My Games\Stationeers\mods\SPUHUDMod`
6. Copy About folder in same place

## Usage

### HUD Display Setup
1. Wear a suit with an IC10 chip in the chip slot
2. Wear sensor lenses and turn them on
3. Use defines with "HUD" prefix in IC10 code to set HUD data addresses

**IMPORTANT**: Only defines that start with "HUD" prefix will be displayed in the HUD.
Examples: `HUDTemperature`, `HUDPressure`, `HUDOxygen`, `HUDFuel`, etc.

### IC10 Code Example
```ic10
# Define HUD variables - MUST start with "HUD" prefix!
define HUDTemperature 100
define HUDPressure 110
define HUDOxygen 120

# Set temperature display
move r0 HUDTemperature
put db r0 25.5    # value: 25.5
add r0 HUDTemperature 1
put db r0 1       # show flag: 1 (show)
add r0 HUDTemperature 2 
put db r0 6       # format: 6 (Celsius from Kelvin)
add r0 HUDTemperature 3
put db r0 4       # color: 4 (Red)

# Note: Variables without "HUD" prefix will be ignored
```

### Memory Structure
Each HUD item uses 10 consecutive memory cells:
- **Offset 0**: Value to display
- **Offset 1**: Show flag (0=hide, any other=show)
- **Offset 2**: Format code (0-8)
- **Offset 3**: Color code (0-11)
- **Offset 4-9**: Reserved for future use

### Format Codes
- **0**: Float with 2 decimals (25.50)
- **1**: Integer (25)
- **2**: Normalized percent 0.0-1.0 → 0%-100% (75%)
- **3**: Percent with 2 decimals (98.25%)
- **4**: Kelvin with °K (298.15°K)
- **5**: Celsius with °C (25.00°C)
- **6**: Kelvin to Celsius conversion (25.00°C)
- **7**: Pressure Pa/kPa/MPa auto-scaling
- **8**: Integer with L suffix (25L)

### Color Codes
- **0**: Blue, **1**: Gray, **2**: Green, **3**: Orange
- **4**: Red, **5**: Yellow, **6**: White, **7**: Black
- **8**: Brown, **9**: Khaki, **10**: Pink, **11**: Purple

### Logic Transmitter Enhancement
- Connect Logic Transmitter to any device
- Use put/get commands on Logic Transmitter to access connected device's memory
- Example: `put d0 0 100` (writes 100 to memory address 0 of connected device)

## Configuration

The mod includes several configurable options:
- Font size
- Text color
- HUD position (X, Y coordinates)
- Display settings

## Known Issues

- Performance optimization is ongoing
- Some edge cases in memory access may cause errors
- This is an experimental build - expect bugs

## Author

@!!ex

## License

This project is provided as-is for educational and modding purposes. Use at your own risk.