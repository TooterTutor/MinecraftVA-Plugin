# Minecraft VoiceAttackAPI Plugin

## Overview

The Minecraft VoiceAttack Integration consists of two components: a **Minecraft Mod** and a **VoiceAttack Plugin**. This is the plugin for VoiceAttack. 

## Components

The VoiceAttack Plugin acts as the intermediary between VoiceAttack and the Minecraft Mod. It is responsible for sending commands to the mod based on user-defined parameters. Key features include:

- **Command Execution**: Sends keybind commands to the Minecraft Mod using two parameters:
  1. **Translation Key**: A text variable named `keybind` that corresponds to the action to be triggered in Minecraft.
  2. **Execution Context**: Executes the Minecraft plugin with the context `execute_keybind`, passing the `keybind` variable.

## VoiceAttack Plugin Installation

1. **Download the Plugin**: Clone or download the plugin repository from GitHub.
2. **Install the Plugin**: Place the plugin file into the VoiceAttack apps directory.

## Configuration

### VoiceAttack Configuration

To set up VoiceAttack for use with the plugin, you need to configure each command with the following parameters:

1. **Translation Key**: Create a text variable named `keybind` that corresponds to the action you want to trigger in Minecraft. This variable should hold the translation key for the desired keybind.

2. **Execute Command**: Use the Minecraft plugin for VoiceAttack to execute the command. Pass the context `execute_keybind` along with the `keybind` variable you created earlier.

### Example Command Setup

1. Create a new command in VoiceAttack.
2. Add a text variable named `keybind` with the value of the desired Minecraft action (e.g., `key.inventory`).
3. Add an action to execute the Minecraft plugin with the context `execute_keybind` and reference the `keybind` variable.

## Limitations

- The integration does not support all keybind methods, including:
  - Player movement controls (e.g., walking, running).
  - Mouse look functionality.
  - Keybinds that require holding down a key (e.g., zooming into a world map).

## Contribution

Contributions are welcome! If you would like to contribute to the VoiceAttackAPI projects, please fork the respective repositories and submit a pull request. For any issues or feature requests, please open an issue in the appropriate GitHub repository.

## License

This project is licensed under the MIT License.
