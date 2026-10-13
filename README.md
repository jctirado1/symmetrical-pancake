# London Streets

A GTA-style, semi-open-world game set in London, built with **Godot 4**.

**So far:** a city of 6 × 6 blocks of London streets (about 450 m across) with zebra
crossings, street lamps, parks, a suburban outer ring and an industrial edge. Traffic (cars,
black cabs and red buses) drives on the left, turns at junctions and queues. Pedestrians walk
the pavements and use the zebra crossings. A minimap sits in the corner. You walk around in
third person and can get into cars and drive them.

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
scenes/main.tscn     The game world: the City, traffic, props (phone box, postbox), cars, player, sun, sky
scenes/player.tscn   The player: a blue capsule with a camera behind it
scenes/car.tscn      A drivable car (a blue box car until you give it a model)
scenes/model_prop.tscn  A slot that shows any model file you give it (and makes it solid)
scripts/player.gd    The code that makes the player walk, run, jump and steer the camera
scripts/city_grid.gd The city generator: builds the roads, pavements, buildings, lamps and parks
scripts/traffic.gd   Traffic: routes, junction queues, and spotting what's in front
scripts/traffic_car.gd  One computer-driven car or bus
scripts/pedestrians.gd, pedestrian.gd  The people on the pavements
scripts/minimap.gd   The minimap
scripts/asset_finder.gd  Searches the project for character and car models
scripts/car.gd       The code for driving, the chase camera and the speedometer
scripts/model_prop.gd, model_loader.gd  The code behind the model slots
assets/characters/   Put your player character here. It's used automatically.
assets/models/cars/  Put car models here. Traffic uses them automatically.
assets/models/       Put your other GAMEPRO .glb / .fbx / .obj files here
assets/textures/     Put your GAMEPRO .png files here
```

### Things to try (no code)
- **Change walking speed:** double-click `scenes/main.tscn`, click **Player** in the
  Scene tree, and change **Walk Speed** / **Run Speed** in the Inspector.
- **Change the city:** click **City** in the Scene tree and change the settings in the
  Inspector (see "The city" below).
- Press **Ctrl+S** to save, then **F5** to play.

---

## Adding your GAMEPRO assets

### The quickest way: copy the whole GAMEPRO folder in
Drag your entire **`GAMEPRO`** folder from your Desktop onto the **`res://`** folder at the
top of Godot's **FileSystem** panel. Wait for the import progress bar to finish, then
press **F5**. The game searches the whole project and works out which models are which:

- **Characters** are recognised by names like *character, man, woman, player, npc* or *people*,
  or because they have a skeleton (rigged for animation). The one named like a player or hero
  (or the first one) becomes **you**, and the others become **pedestrians**.
- **Cars** are recognised by names like *car, vehicle, sedan, taxi, van, truck* or *police*.
  Wheels, doors and other loose car parts are ignored, and so are buildings like a
  *Police_Station*. Cars are used for the **parked cars you can drive** and for the **traffic**.
  Each one is turned to face along the road and resized to car length.

After you press F5, open **`assets/found_assets.txt`** in the FileSystem panel to see exactly
what was found and what each model is used for. If something is missing or in the wrong
list, rename the file or put it in a folder called `characters` or `cars`. The first run
with a big GAMEPRO folder can take a little while, because every model is checked once.
After that it's remembered.

> Models dropped into `assets/characters` or `assets/models/cars`, or set by hand in the
> Inspector (below), always win over the automatic search.

### 1. Copy the files in (to choose for yourself)
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
**The easy way:** drag your character's `.glb` or `.fbx` file into the **`assets/characters`**
folder. The player uses it automatically: the blue capsule disappears, and your character
is resized to 1.8 m tall and stood on the ground. If several are in there, the first one
alphabetically is used. With nothing in that folder, the automatic search (above) picks one.

**To choose a specific one:**
1. Open `scenes/main.tscn` (double-click it in FileSystem).
2. Click **Player** in the Scene tree (top left).
3. In the Inspector (right), open the **Character Model** section.
4. Drag your character's `.glb`/`.fbx` file from FileSystem onto the **Character Model** box.
5. Adjust it if needed:
   - **Wrong size?** Characters are fitted to 1.8 m automatically. Use **Model Scale**
     to make them a bit bigger or smaller, or untick **Fit To Height** to turn this off.
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

To fill the whole city with your buildings at once, see **The city** below.

### 4. Use your car models
**For traffic:** drag your car models into **`assets/models/cars`**. Every traffic car
picks one at random and is resized to about 4.3 m long. If they drive backwards,
click **Traffic** in the Scene tree and tick **Flip Car Models**.

**For the cars you can drive:** there are two parked cars at the start, under **Vehicles** in the Scene tree.
1. Click **Vehicles → Car**.
2. In the Inspector, open **Car Model** and drag a car file onto the **Car Model** box.
   The blue box car disappears and your model takes its place. If you leave it empty, a car
   found by the automatic search is used.
3. Adjust it if needed:
   - **Wrong size?** Cars are resized to 4.3 m long automatically. To use your own size,
     untick **Fit To Car Size** and change **Model Scale**.
   - **Facing backwards?** Tick **Flip Model Direction**. The car drives towards the blue
     arrow (Z) you see when you click it.
   - **Floating or sunk into the road?** Change **Model Height Offset**.
   - **No wheels showing?** Tick **Show Box Wheels**.
4. **More cars:** click a car and press **Ctrl+D**, or drag `scenes/car.tscn` into the 3D view.
5. **Tweak the handling:** open the **Driving** section (engine force, brakes, steering, top speed).

> The car's model doesn't change how it handles. The solid part is the invisible box shown in
> blue lines. For a much bigger or smaller vehicle, click its **CollisionShape3D** and change
> **Size**. You can also move the four **Wheel** nodes to sit where your model's wheels are.

---

## Traffic

Click **Traffic** in the Scene tree to change it in the Inspector:
| Setting | What it does |
|---------|--------------|
| Car Count / Bus Count | How many cars and buses drive around (default 30 + 3) |
| Cruise Speed Kmh | How fast traffic drives (default 40 km/h, about 25 mph) |
| Car Models Folder | Where traffic finds car models (default `assets/models/cars`) |
| Flip Car Models | Tick if your traffic cars drive backwards |

What the traffic does:
- It drives on the **left**, takes a random turn at each junction and takes turns to cross.
- It slows down and queues behind other cars, and stops if **you** step into the road
  or block it with a car.
- If a car is stuck for a long time somewhere you can't see, it quietly reappears
  somewhere else, so jams clear themselves.
- Traffic only appears when you **play**, not in the editor.
- You can't get into traffic cars yet. Crashing into them is like hitting a wall: they
  carry on and don't get damaged.

---

## Pedestrians

People walk round the blocks on the pavements and cross the roads on the zebra crossings.
Click **Pedestrians** in the Scene tree to change them:
| Setting | What it does |
|---------|--------------|
| Count | How many people are out and about (default 80) |
| Keep Within | People further away than this (metres) quietly reappear closer to you, so the streets around you always feel busy |
| Characters Folder | A folder of character models to use. If empty, the characters the automatic search found (except yours) are used, and if there are none, simple coloured people |
| Flip Models | Tick if they walk backwards |

What they do:
- They keep to the left, so people walking opposite ways pass each other.
- At a crossing they wait until the road is clear. If they've waited a long time, they
  step out anyway and **traffic stops for them**, like on a real zebra.
- They stop rather than walk into you, and **jump out of the way** of a car you drive at
  them. Cars can't knock them over.
- If their model has a *walk* animation it plays. Otherwise they bob along.

---

## Minimap

The map in the bottom-left corner shows the roads, blocks (green for parks), traffic as
white dots, buses as red dots and cars you can drive as blue dots. You're the yellow arrow.
It turns with the camera so up is always the way you're looking, the **N** shows where north
is, and it zooms out while you're driving.

To change it, click **Minimap** in the Scene tree. You can change how many metres it shows on
foot and when driving, or untick **Rotate With Camera** to keep north at the top. To move or
resize it, drag it in the **2D** view (click **2D** at the top of the editor).

---

## The city

The whole map is made by one node called **City**. Click it in the Scene tree and all its
settings appear in the Inspector. Change any of them and the city rebuilds itself
immediately.

**Layout**
| Setting | What it does |
|---------|--------------|
| Blocks X / Blocks Z | How many blocks across and deep. 6 × 6 is about 450 m square. 10 × 10 is about 740 m. |
| Block Size | Size of each block in metres |
| Road Width / Pavement Width | Width of the roads and pavements |
| Random Seed | Type any number to get a different random city |
| Park Chance | How often a block in the middle of town becomes a park |
| Industrial Edge | Puts warehouses and factories along the east side |
| Street Lamps | Turns the lamp posts on or off |

The middle of town has brick, stone and glass buildings. The outer ring has smaller
houses with gaps between them. The east edge is industrial. There's always a crossroads
at the very centre of the map.

### Use your city packs for the buildings
1. Put each pack in its own folder inside `assets/models`, for example
   `assets/models/city`, `assets/models/suburban` and `assets/models/industrial`.
   **Only include buildings.** Move cars, trees, benches and other small props into a
   different folder, or they'll be used as buildings and blown up to building size.
2. Click **City**, then open **Building Models** in the Inspector.
3. Click the folder icon next to **City Models Folder** and choose your city/normal pack
   folder. Do the same for **Suburban Models Folder** and **Industrial Models Folder**.
   Any folder you leave empty keeps using the plain box buildings.
4. The plots fill with random buildings from each folder, including models in sub-folders.
   They are resized to fit their plot, sit on the pavement and are solid.
5. **Facing the wrong way** (backs to the road)? Tick **Flip Models**.
6. **Prefer the models' own sizes?** Untick **Fit Models To Plots** and set **Model Scale**
   (for example `0.01` if they come out 100 times too big).

> Your London buildings and the store are best placed by hand, so they go exactly where you
> want them. Use a model slot (section 3 above) for each one. Turn on snapping by
> clicking the **magnet icon** above the 3D view, so things move in 1 m steps.
>
> The generated buildings can't be clicked or edited one at a time. To make space for a
> hand-placed landmark, change **Random Seed** until a park lands where you want it.
> Otherwise, place the landmark on the edge of the map.

Big packs can make the editor slow while they import the first time, which is normal.
