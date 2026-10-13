# London Streets

A GTA-style, semi-open-world game set in London, built with **Godot 4**.

**So far:** a London street where you walk around in third person, and cars you can
get into and drive.

| Key | Action |
|-----|--------|
| W A S D / arrow keys | Walk |
| Mouse | Turn the camera |
| Shift (hold) | Run |
| Space | Jump |
| E (or F) | Get into a nearby car / get out |
| Esc | Free the mouse (click the game window to capture it again) |

**In a car:** W = accelerate, S = brake / reverse, A / D = steer, Space = handbrake.

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
scenes/car.tscn      A drivable car (a blue box car until you give it a model)
scenes/model_prop.tscn  A slot that shows any model file you give it (and makes it solid)
scripts/player.gd    The code that makes the player walk, run, jump and steer the camera
scripts/car.gd       The code for driving, the chase camera and the speedometer
scripts/model_prop.gd, model_loader.gd  The code behind the model slots
assets/models/       Put your GAMEPRO .glb / .fbx / .obj files here
assets/textures/     Put your GAMEPRO .png files here
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

### 1. Copy the files in
In Godot's **FileSystem** panel (bottom left) you'll see `assets/models` and
`assets/textures`. Drag files from your `Desktop/GAMEPRO` window onto them:
- `.glb`, `.fbx`, `.obj` files go in **`assets/models`**
- `.png` files go in **`assets/textures`**

Godot imports them automatically. This takes a few seconds, and a progress bar appears.

> **If a model and its textures came in the same sub-folder** (e.g. `GAMEPRO/car/car.fbx`
> plus `GAMEPRO/car/car_color.png`), copy the **whole sub-folder** into `assets/models`
> instead. Models often look for their textures next to them.

| File type | What Godot does with it |
|-----------|-------------------------|
| `.glb` / `.gltf` | Best format. Comes in with textures and animations. |
| `.fbx` | Works well. Comes in with animations, but sometimes loses its textures. |
| `.obj` | Shape only, with no animations. Often shows up white, so give it a `.png` (see below). |
| `.png` | Textures (the "paint" for a model). |

### 2. Use your character as the player
1. Open `scenes/main.tscn` (double-click it in FileSystem).
2. Click **Player** in the Scene tree (top left).
3. In the Inspector (right), open the **Character Model** section.
4. Drag your character's `.glb`/`.fbx` file from FileSystem onto the **Character Model** box.
   The blue capsule is replaced by your model.
5. Adjust it if needed:
   - **Too big or too small?** Change **Model Scale** (try `0.01` if it's enormous, or `100` if it's tiny).
   - **Walks backwards?** Tick **Flip Model Direction**.
   - **Looks white?** Drag its `.png` onto **Character Texture**.
6. Press **F5**. If the model has animations with names containing *idle*, *walk* or *run*,
   they play automatically. If they don't play, look in the **Output** panel at the bottom.
   It lists the animation names it found. Type the right names into **Idle/Walk/Run
   Animation** in the Inspector.

### 3. Place buildings, cars and props
The street has an empty slot called **YourModelHere** (under **Props**).
1. Click **YourModelHere** in the Scene tree.
2. Drag a model file onto **Model** in the Inspector. It appears in the street.
3. Optionally drag a `.png` onto **Texture**.
4. Move it with the coloured arrows in the 3D view. Press **R** to switch to rotate,
   **S** for scale and **W** to go back to move.

**To add more:** right-click **YourModelHere** → **Duplicate** (or **Ctrl+D**), then give
the copy a different model. Another way is to drag `scenes/car.tscn      A drivable car (a blue box car until you give it a model)
scenes/model_prop.tscn` from FileSystem
into the 3D view.

Models placed this way are **solid**: the player bumps into them. Untick **Solid** for
things you should be able to walk through.

To swap a grey box building for one of your models, add a model slot where the box was.
Then click the box and press **Delete**.

### 4. Use your car models
There are two cars in the street, under **Vehicles** in the Scene tree.
1. Click **Vehicles → Car**.
2. In the Inspector, open **Car Model** and drag a car file onto the **Car Model** box.
   The blue box car disappears and your model takes its place.
3. Adjust it if needed:
   - **Wrong size?** Change **Model Scale**. A real car is about 4 m long, the same as the box car.
   - **Facing backwards?** Tick **Flip Model Direction**. The car drives towards the blue
     arrow (Z) you see when you click it.
   - **Floating or sunk into the road?** Change **Model Height Offset**.
   - **No wheels showing?** Tick **Show Box Wheels**.
4. **More cars:** click a car and press **Ctrl+D**, or drag `scenes/car.tscn` into the 3D view.
5. **Tweak the handling:** open the **Driving** section (engine force, brakes, steering, top speed).

> The car's model doesn't change how it handles. The solid part is the invisible box shown in
> blue lines. For a much bigger or smaller vehicle, click its **CollisionShape3D** and change
> **Size**. You can also move the four **Wheel** nodes to sit where your model's wheels are.

### Tips for the city packs
- Build blocks from the **suburban / normal / industrial** packs using model slots
  (section 3). Duplicate a slot (**Ctrl+D**) and swap the model to make a terrace of houses or shops.
- To make positioning tidy, turn on snapping: click the **magnet icon** in the toolbar above
  the 3D view. Objects then move in 1 m steps.
- Big packs can slow the editor down while they import. That's normal the first time.
