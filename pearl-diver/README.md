# Pearl Diver — prototype

The first playable "grey-box" version of Pearl Diver. Everything is built from simple shapes so we can test one question before spending time on art: **is the dive fun?**

## What's in this version

- Hold to dive, let go to float up (mouse, touch or Space bar)
- Oxygen bar: it drops underwater and refills at the surface
- Pearls: white, pink (below 10 m), golden (below 25 m) and one rare black pearl (below 45 m)
- Combos: grab pearls quickly in a row for ×2 and ×3
- Jellyfish sting (−20 air) and sharks below 45 m, with a red “!” warning first
- 4 depth zones with changing water colour and +25% pearl value per zone
- Results screen, with placeholder “watch ad” buttons (no real ads yet)
- Two upgrades: oxygen tank and fins; progress is saved

## How to run it (first time, about 20 minutes)

1. Install **Unity Hub** from unity.com, then install the latest **Unity 6** version it offers (choose the one marked LTS). Tick **Android Build Support** (and **iOS Build Support** if you have a Mac).
2. In Unity Hub click **New project**, choose the **Universal 3D** template, name it `PearlDiver`, and create it.
3. Copy the folder `pearl-diver/Assets/PearlDiver` from this repository into the new project's `Assets` folder.
4. Back in Unity, wait for it to finish loading, then press the **▶ Play** button at the top.
5. In the **Game** view, set the size to a phone shape (for example 1080×1920 Portrait) using the menu at the top left of the Game view.

There is no scene to set up: the game builds itself when you press Play.

## Tuning while you play

While the game is running, click **Pearl Diver** in the **Hierarchy** panel. The **Tuning** component in the Inspector lists every number (dive force, oxygen, spawn rates, upgrade costs). Change a value and feel the difference straight away. Changes made during Play are lost when you press Stop, so write down the values you like.

Testing helpers in the same panel:
- **Reset Save** wipes all progress
- **Add 1000 Pearls** gives pearls to test upgrades

## What to report back

Play 10 dives and tell Claude:
- Does diving feel too fast, too slow or just right?
- Is oxygen too strict or too easy?
- Are the jellyfish fair? Do you see sharks coming in time?
- Did you want “one more dive”?

## Files

| File | What it does |
| --- | --- |
| `Scripts/Bootstrap.cs` | Starts the game automatically when you press Play |
| `Scripts/PearlDiverGame.cs` | The whole prototype: diver, oxygen, pearls, hazards, upgrades, screens |
| `Scripts/Tuning.cs` | All the numbers you can tweak |
| `Scripts/Shapes.cs` | Makes the simple coloured shapes used instead of real art |
