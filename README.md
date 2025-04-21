# Burn Dissolve Shader (URP)

A simple Burn Dissolve shader created using Shader Graph in Unity's Universal Render Pipeline (URP). This repository also includes a work-in-progress (WIP) version that allows burning to spread dynamically where the user drags with the mouse.

<div align="center">
    <img src="https://raw.githubusercontent.com/caliber44/BurnDissolveUnityBootCamp/main/old.gif" width="505" alt="Basic Burn Dissolve">
    <img src="https://raw.githubusercontent.com/caliber44/BurnDissolveUnityBootCamp/main/wip.gif" width="500" alt="Interactive Burn (WIP)">
</div>

## Features
- **Burn Dissolve Shader**: A basic shader that creates a burn effect, dissolving an object over time.
- **Interactive Burn (WIP)**: A version of the shader that spreads burning dynamically based on user input. Recently improved with a custom compute shader, allowing for more control over the burn speed and spread radius. The shader now supports efficient GPU-based calculations for burn spreading, which can be tweaked dynamically for smoother transitions and performance.
- **URP Compatibility**: Designed specifically for Unity's Universal Render Pipeline.
- **Fully Node-Based**: Created using Unity's Shader Graph for easy modification.

## Roadmap
- ✅ Basic Burn Dissolve Shader
- 🚧 Interactive Burn Shader (Work in Progress)

## License
This project is open-source under the MIT License.
