# London Streets

A GTA-style, semi-open-world game set in London, built with **Godot 4**.

**Step 1 (this version):** a simple London street where you can walk a character around
with a third-person camera.

| Key | Action |
|-----|--------|
| W A S D / arrow keys | Walk |
| Mouse | Turn the camera |
| Shift (hold) | Run |
| Space | Jump |
| Esc | Free the mouse (click the game window to capture it again) |

---

## How to get it running (no coding needed)

### 1. Install Godot 4
1. Go to **https://godotengine.org/download**.
2. Click the big **Godot Engine** download button. Get the normal version, not ".NET".
3. Windows: unzip the download and double-click `Godot_v4.x-stable_win64.exe`.
   Mac: open the `.dmg` and drag Godot into Applications.
   You don't need to install anything else. Godot is a single program.

### 2. Download this project to your computer
1. On this repository's GitHub page, switch the branch dropdown (top left) to
   `claude/godot-london-game-setup-bowxe5`.
2. Click the green **Code** button, then **Download ZIP**.
3. Unzip it somewhere easy to find, for example `Desktop/LondonStreets`.
   (Don't put it inside your `GAMEPRO` folder. Keep the two separate.)

### 3. Open the project in Godot
1. Start Godot. The **Project Manager** window opens.
2. Click **Import** (top left).
3. Browse to the folder you unzipped and select the file **`project.godot`**.
4. Click **Import & Edit** (some versions say **Open**).
5. If Godot says the project was made for an older version and asks to convert or
   upgrade it, click **OK / Convert**. That's normal.

Godot spends a few seconds importing files the first time.

### 4. Play it
- Press **F5**, or click the ▶ **Play** button in the top-right corner.
- Walk around with WASD and the mouse. Press **Esc**, then close the game window
  (or press **F8**) to stop.

---

## A quick tour of the Godot editor

- **FileSystem** (bottom-left): the project's files. `res://` means "the project folder".
- **Scene** tree (top-left): the objects ("nodes") in the scene you have open.
- **Viewport** (middle): the 3D view. Hold the **right mouse button** and use **WASD**
  to fly around, the same as in a game.
- **Inspector** (right): the settings of whichever node you clicked.

### What's in the project

```
project.godot        Project settings, including the controls (Project > Project Settings > Input Map)
icon.svg             The game's icon
scenes/main.tscn     The London street: road, pavements, buildings, phone box, bus, sun, sky
scenes/player.tscn   The player: a blue capsule with a camera behind it
scripts/player.gd    The code that makes the player walk, run, jump and steer the camera
assets/              Put your GAMEPRO files here
```

### Things to try (no code)
- **Change walking speed:** double-click `scenes/main.tscn`, click **Player** in the
  Scene tree, and change **Walk Speed** / **Run Speed** in the Inspector.
- **Recolour or resize a building:** expand **Buildings**, click one, and change its
  **Size** in the Inspector. To recolour it, open **Material** and change **Albedo > Color**.
- **Add another building:** click a building and press **Ctrl+D** to duplicate it,
  then drag the coloured arrows in the viewport to move it.
- Press **Ctrl+S** to save, then **F5** to play.

---

## Adding your GAMEPRO assets

1. Copy the files from `Desktop/GAMEPRO` into this project's `assets/` folder
   (e.g. models into `assets/models/`, textures into `assets/textures/`).
   You can drag them straight onto the **FileSystem** panel in Godot.
2. Godot imports them automatically. 3D models work best as **.glb / .gltf**.
   **.fbx** and **.obj** work too.
3. To place a model in the street, drag it from the FileSystem panel into the 3D viewport.
