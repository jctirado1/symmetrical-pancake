# London Streets for Unity

This is the Unity version of the London Streets game (the Godot version is in the rest of
this repository). It has the same things in it:

- a city of 6 × 6 blocks of London streets (about 450 m across) with zebra crossings, street
  lamps, parks, a suburban outer ring and an industrial edge
- traffic (cars, black cabs and red buses) that drives on the left, turns at junctions and queues
- pedestrians who walk the pavements, use the zebra crossings and jump out of your way
- two parked cars you can get into and drive
- a minimap in the corner

| Key | Action |
|-----|--------|
| W A S D / arrow keys | Walk |
| Mouse | Turn the camera |
| Shift (hold) | Run |
| Space | Jump |
| E (or F) | Get into a nearby car / get out |
| Esc | Free the mouse (click the game window to capture it again) |

**In a car:** W = accelerate, S = brake / reverse, A / D = steer, Space = handbrake.

It works with Unity 2022.3 LTS and Unity 6, with any of the 3D templates (Built-in, URP or
HDRP), and with either the old or the new input system. You don't need to set any of that up.

---

## How to get it running (no coding needed)

### 1. Install Unity
1. Download and install **Unity Hub** from **https://unity.com/download**.
2. Open Unity Hub, go to **Installs** → **Install Editor**, and install the latest
   **Unity 6** (or **2022.3 LTS**). You don't need any of the extra modules.

### 2. Make a new project
1. In Unity Hub, go to **Projects** → **New project**.
2. Choose the **Universal 3D** template (**3D (Built-In Render Pipeline)** works too).
3. Give it a name, for example `LondonStreets`, and click **Create project**.
   Unity takes a minute or two to open the first time.

### 3. Download this game and copy it in
1. On this repository's GitHub page, switch the branch dropdown (top left) to
   `claude/inspiring-volta-7isjnz`, then click the green **Code** button → **Download ZIP**.
2. Unzip it. Inside, open the `unity` folder. You'll see a folder called **`LondonStreets`**.
3. Drag that **`LondonStreets`** folder into Unity's **Project** window (bottom of the
   screen), onto the **Assets** folder. It must end up as `Assets/LondonStreets`.

Unity compiles the scripts (a few seconds). Then a box pops up asking
**"Build the London Streets scene now?"** Click **Build it**.

> Missed the box? Use the menu at the top of Unity: **London Streets → Build London Scene**.

### 4. Play it
- Press the ▶ **Play** button at the top of the editor.
- Click inside the **Game** view so the mouse is captured, then walk around with WASD and the mouse.
- Press **Esc** to get the mouse back, and ▶ again to stop.

> If Unity asks whether to enable the new input system's backends and restart, click **Yes**.
> It's harmless either way.

---

## A quick tour of the Unity editor

- **Project** window (bottom): the project's files. Everything for this game is in `Assets/LondonStreets`.
- **Hierarchy** (left): the objects in the scene you have open: City, Traffic, Pedestrians,
  Props, Vehicles, Player, Minimap and Sun.
- **Scene** view (middle): the 3D view. Hold the **right mouse button** and use **WASD**
  to fly around, the same as in a game.
- **Inspector** (right): the settings of whichever object you clicked.

The city appears in the Scene view straight away. The traffic, pedestrians and your character
only appear when you press Play.

Changes you make to settings **while playing** are thrown away when you stop. Stop first,
change the settings, then press Play again. Press **Ctrl+S** to save the scene.

### What's in the folder

```
LondonStreets/
  Scenes/London.unity    The game world (made by London Streets > Build London Scene)
  Characters/            Put your player character here. It's used automatically.
  Cars/                  Put car models here. Traffic uses them automatically.
  Models/                Put your other models here (for example building packs)
  found_assets.txt       Which characters and cars the game found in your project
  Materials/, Resources/ Made automatically: the sky, the red paint, and the game's lists
  Scripts/
    PlayerController.cs  Walking, running, jumping and the camera
    Car.cs               Driving, the chase camera and the speedometer
    CityGrid.cs          The city generator: roads, pavements, buildings, lamps and parks
    Traffic.cs           Traffic: routes, junction queues, and spotting what's in front
    TrafficCar.cs        One computer-driven car or bus
    Pedestrians.cs, Pedestrian.cs  The people on the pavements
    Minimap.cs           The minimap
    ModelProp.cs         A slot that shows any model you give it (and makes it solid)
    LondonSky.cs         The sky, haze and light
    ...                  Small helpers used by the scripts above
  Editor/
    LondonSetup.cs       Builds the scene and adds the "London Streets" menu
    AssetFinder.cs       Searches the project for character and car models
```

### Things to try (no code)
- **Change walking speed:** click **Player** in the Hierarchy and change **Walk Speed** /
  **Run Speed** in the Inspector.
- **Change the city:** click **City** and change the settings in the Inspector
  (see "The city" below). The Scene view updates as you type.
- **Change the weather:** click **Sun** and change the haze and light colours.

---

## Adding your GAMEPRO assets

### The quickest way: copy the whole GAMEPRO folder in
Drag your entire **`GAMEPRO`** folder from your Desktop onto the **Assets** folder in Unity's
**Project** window. Wait for the import to finish (big packs take a while), then press ▶.
The game searches the whole project and works out which models are which:

- **Characters** are recognised by names like *character, man, woman, player, npc* or *people*,
  or because they have a skeleton (rigged for animation). The one named like a player or hero
  (or the first one) becomes **you**, and the others become **pedestrians**.
- **Cars** are recognised by names like *car, vehicle, sedan, taxi, van, truck* or *police*.
  Wheels, doors and other loose car parts are ignored, and so are buildings like a
  *Police_Station*. Cars are used for the **parked cars you can drive** and for the **traffic**.
  Each one is turned to face along the road and resized to car length.
- If a pack has both a model file and a prefab with the same name, the **prefab** is used,
  because it usually has the materials set up.

Open **`Assets/LondonStreets/found_assets.txt`** to see exactly what was found and what each
model is used for. If something is missing or in the wrong list, rename the file or put it
in a folder called `characters` or `cars`, then use **London Streets → Find Characters and
Cars Again**.

> Models dropped into `LondonStreets/Characters` or `LondonStreets/Cars`, or set by hand in
> the Inspector (below), always win over the automatic search.

| File type | What Unity does with it |
|-----------|-------------------------|
| `.fbx` | Best format for Unity. Comes in with animations. |
| `.obj` | Shape only, with no animations. Often shows up white, so give it a texture (see below). |
| `.glb` / `.gltf` | Needs the free **glTFast** package: **Window → Package Manager → + → Add package by name** → `com.unity.cloud.gltfast`. |
| `.png` / `.jpg` | Textures (the "paint" for a model). |
| `.prefab` | Ready-made Unity objects from asset packs. Work as they are. |

> **Pink models?** The pack was made for a different render pipeline. Select its materials,
> then use **Edit → Rendering → Materials → Convert Selected Built-in Materials to URP**
> (in a URP project).

### 1. Use your character as the player
**The easy way:** drag your character's model into the **`LondonStreets/Characters`** folder.
The player uses it automatically: the blue capsule disappears, and your character is resized
to 1.8 m tall and stood on the ground. If several are in there, the first one alphabetically
is used.

**To choose a specific one:**
1. Click **Player** in the Hierarchy.
2. In the Inspector, drag your character's model or prefab onto **Character Model**.
3. Adjust it if needed:
   - **Wrong size?** Characters are fitted to 1.8 m automatically. Use **Model Scale**
     to make them a bit bigger or smaller, or untick **Fit To Height** to turn this off.
   - **Walks backwards?** Tick **Flip Model Direction**.
   - **Looks white?** Drag its texture onto **Character Texture**.
4. Press ▶. If the model has animations with names containing *idle*, *walk* or *run*,
   they play automatically. The **Console** window lists the animations it found. To use other
   ones, drag the clips onto **Idle/Walk/Run Animation** in the Inspector (click the little
   arrow next to a model in the Project window to see the clips inside it). Clips from other
   files work too, for example Mixamo animations downloaded separately.

> **Animations not moving the character?** Select the model in the Project window, open the
> **Rig** tab in the Inspector, and make sure the character and its animations use the same
> **Animation Type** (both *Humanoid* is the easiest), then click **Apply**.

### 2. Place buildings, cars and props
The easiest way is the usual Unity way: drag a model from the Project window into the Scene
view. Move it with the arrows (**W**), turn it (**E**) and resize it (**R**). To make it
solid, click **Add Component** in the Inspector and add a **Mesh Collider**.

There's also a slot called **YourModelHere** (under **Props**). Drag a model onto its
**Model** setting and it appears in the street, already solid. Press **Ctrl+D** to duplicate
it for more.

To fill the whole city with your buildings at once, see **The city** below.

### 3. Use your car models
**For traffic:** drag your car models into **`LondonStreets/Cars`**. Every traffic car
picks one at random and is resized to about 4.3 m long. If they drive backwards,
click **Traffic** in the Hierarchy and tick **Flip Car Models**.

**For the cars you can drive:** there are two parked cars, under **Vehicles** in the Hierarchy.
1. Click **Vehicles → Car**.
2. In the Inspector, drag a car model onto **Car Model**. If you leave it empty, a car
   found by the automatic search is used, and if there are none, the blue box car.
3. Adjust it if needed:
   - **Wrong size?** Cars are resized to 4.3 m long automatically. To use your own size,
     untick **Fit To Car Size** and change **Model Scale**.
   - **Facing backwards?** Tick **Flip Model Direction**. The car drives towards the blue
     arrow (Z) you see when you click it.
   - **Floating or sunk into the road?** Change **Model Height Offset**.
   - **No wheels showing?** Tick **Show Box Wheels**.
4. **More cars:** click a car and press **Ctrl+D**.
5. **Tweak the handling:** change the settings under **Driving** (engine torque, brakes,
   steering, top speed).

> The car's model doesn't change how it handles. The solid part is an invisible 1.8 × 1.1 × 4.2 m
> box with four wheels underneath.

---

## Traffic

Click **Traffic** in the Hierarchy to change it in the Inspector:

| Setting | What it does |
|---------|--------------|
| Car Count / Bus Count | How many cars and buses drive around (default 30 + 3) |
| Cruise Speed Kmh | How fast traffic drives (default 40 km/h, about 25 mph) |
| Car Models Folder | Where traffic finds car models (default `Assets/LondonStreets/Cars`) |
| Flip Car Models | Tick if your traffic cars drive backwards |

What the traffic does:
- It drives on the **left**, takes a random turn at each junction and takes turns to cross.
- It slows down and queues behind other cars, and stops if **you** step into the road
  or block it with a car.
- If a car is stuck for a long time somewhere you can't see, it quietly reappears
  somewhere else, so jams clear themselves.
- You can't get into traffic cars. Crashing into them is like hitting a wall: they
  carry on and don't get damaged.

## Pedestrians

People walk round the blocks on the pavements and cross the roads on the zebra crossings.
Click **Pedestrians** in the Hierarchy to change them:

| Setting | What it does |
|---------|--------------|
| Count | How many people are out and about (default 80) |
| Keep Within | People further away than this (metres) quietly reappear closer to you |
| Characters Folder | A folder of character models to use. If empty, the characters the automatic search found (except yours) are used, and if there are none, simple coloured people |
| Flip Models | Tick if they walk backwards |
| Walk / Idle Animation | Optional clips for everyone, if the models don't have their own |

What they do:
- They keep to the left, so people walking opposite ways pass each other.
- At a crossing they wait until the road is clear. If they've waited a long time, they
  step out anyway and **traffic stops for them**, like on a real zebra.
- They stop rather than walk into you, and **jump out of the way** of a car you drive
  at them. Cars can't knock them over.

## Minimap

The map in the bottom-left corner shows the roads, blocks (green for parks), traffic as
white dots, buses as red dots and cars you can drive as blue dots. You're the yellow arrow.
It turns with the camera so up is always the way you're looking, the **N** shows where north
is, and it zooms out while you're driving. Click **Minimap** to change how many metres it
shows, its size, or to keep north at the top.

---

## The city

The whole map is made by the object called **City**. Click it in the Hierarchy and all its
settings appear in the Inspector. Change any of them and the city rebuilds itself.

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
at the very centre of the map. Keep the City object at position (0, 0, 0).

### Use your city packs for the buildings
1. Put each pack in its own folder, for example `LondonStreets/Models/City`,
   `LondonStreets/Models/Suburban` and `LondonStreets/Models/Industrial`.
   **Only include buildings.** Move cars, trees, benches and other small props into a
   different folder, or they'll be used as buildings and blown up to building size.
2. Click **City**. Under **Building Models**, drag the city pack's folder from the Project
   window onto **City Models Folder** (or click **Choose**). Do the same for **Suburban
   Models Folder** and **Industrial Models Folder**. Any folder you leave empty keeps using
   the plain box buildings. You can also drag single models into the lists below them.
3. The plots fill with random buildings from each folder, including sub-folders.
   They are resized to fit their plot, sit on the pavement and are solid.
4. **Facing the wrong way** (backs to the road)? Tick **Flip Models**.
5. **Prefer the models' own sizes?** Untick **Fit Models To Plots** and set **Model Scale**
   (for example `0.01` if they come out 100 times too big).

> Your London landmarks are best placed by hand, so they go exactly where you want them.
> Hold **Ctrl** (**Cmd** on a Mac) while dragging to move in fixed steps.
>
> The generated buildings can't be clicked or edited one at a time. To make space for a
> hand-placed landmark, change **Random Seed** until a park lands where you want it.
> Otherwise, place the landmark on the edge of the map.

---

## Making a standalone game (optional)

**File → Build Profiles** (Unity 6) or **File → Build Settings** (2022.3), then **Build**.
The London scene is already in the list. Models found automatically and models in the
`Characters` and `Cars` folders are included. If you pointed a setting at another folder,
drag those models into the matching list in the Inspector as well (for example the City's
**City Models** list), because a built game can't search folders.

## If something goes wrong
- **Cars bump into pedestrians, or something else odd with collisions:** the game uses
  physics layers 29 and 30. If your project already uses those, change the numbers at the
  top of `Scripts/GameLayers.cs`.
- **The scene is empty when you open the project:** open `LondonStreets/Scenes/London.unity`,
  or use **London Streets → Open London Scene**.
- **Everything is pink:** use **London Streets → Build London Scene** again after
  changing render pipeline, so the game's materials are remade for it.
