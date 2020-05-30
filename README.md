# Stationeers Mod Language loading fix

This fixes the errors that are logged in the console window when loading Mods containing language files.

## Building from source

You need to set your Reference Paths for the project to your Stationeers install
 - \<steam install path\>/steamapps/common/Stationeers/rocketstation_Data/Managed
 - \<steam install path\>/steamapps/common/Stationeers/BepInEx/core

## Releases

To download, get it from the [Releases](https://github.com/ICanHazCode/ModLanguageFix/releases) page.

## Requirements
This is made with a modified version of [BepInEx 5.0.1](https://github.com/BepInEx/BepInEx/releases) that uses the HarmonyX code at [HarmonyX](https://github.com/BepInEx/HarmonyX).
The reason is that later versions of Unity (around 2017.x to now) cut out many of the code building functions needed by transpiler mods.
I've tried with the original BepInEx with Harmony and couldn't get it to work.

This version is compatible with BepInEx 5.0.1 so you can just drop it in place of the original.
[BepInEx with HarmonyX](https://github.com/ICanHazCode/BepInEx/releases)

## Installation
1. Install [BepInEx](https://github.com/ICanHazCode/BepInEx/releases) in the Stationeers steam folder.
2. Launch the game, reach the main menu, then quit back out.
3. In the steam folder, there should now be a folder BepInEx/Plugins
4. Copy the [stationeers.ModLanguageFix]() folder from this mod into BepInEx/Plugins


## Version
- v0.1.0 Initail version
