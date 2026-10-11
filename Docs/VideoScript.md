# Video plan for `H4ThodupunuriAnshul.mp4` (target: under 3 minutes)

Record with OBS: one display capture showing Unity and the diagrams (open the PNGs from `Docs/diagrams/` or the PDF). Talk in your own words; this script is a guide, not something to read aloud.

**Before recording:**
- Run **EcoSim → Build Everything**.
- Play one run of a few minutes so you know where the interesting moments are.
- Prepare these moments to show:
  - a fox stalking you ("You feel watched...")
  - a wolf chase into a burrow
  - a lineage transfer
  - an ability unlock toast

---

## 0:00 – 0:45 · From Homework 3 to a game
*Open the `EcoSimMain` scene and press Play for 5 seconds.*
- "This is my Homework 3: a predator and prey simulation. The agents were rich (herds, packs, stalking), but you could only watch."

*Show the review table (PDF page 4) and `H4_Octalysis_BeforeAfter.png`.*
- "I reviewed it with the game design components. It had space, agents and behaviours, but no player, no goal and no stakes. In Octalysis it only triggered curiosity."
- "So I put the player inside the system, on the weak side: you are one rabbit."
- "Octalysis told me *which* drives to target: loss and avoidance first, balanced with ownership and accomplishment. MDA told me *what* to build."

*Show `H4_GameLoop.png`.*
- "Core loop: forage, eat (and you are frozen while eating), mate, hide in burrows."
- "Time loop: nights get longer and predators sharper."
- "Progression: your warren unlocks abilities. When you die, you continue as your child."

## 0:45 – 1:10 · Research and preparation
*Show only the resources you actually used: browser tabs or a slide.* Suggestions to check before recording:
- Yu-kai Chou, *Octalysis framework* (yukaichou.com), and Hunicke, LeBlanc & Zubek, *MDA* (2004)
- Unity Manual: *Order of execution for event functions*, *Coroutines*, *Immediate Mode GUI (IMGUI)*
- Unity Scripting API: `SceneManager.LoadScene`, `JsonUtility`, `PlayerPrefs`, `Time.timeScale`, `SerializedObject`
- Microsoft C# docs: *events and delegates*, *generic type constraints*
- The three videos linked in the brief (game design process, basic principles, moving beyond gamification)

## 1:10 – 2:30 · Architecture and order of work
*Show the Project window, then `H4_Architecture.png`.*
- "The folders are Scripts, Prefabs, Visual Assets and Scenes."
- "Every script is in the namespace `ThodupunuriAnshul.EcoSim`, with one sub-namespace per folder."
- "The dependencies go one way: Core, then Agents, then Player, then Game, then UI. Events flow back."

*Show the code in your IDE, following the order of work:*
1. **Refactor first.** Show `Animal.Update`, which chooses between the AI brain and the player brain.
   - The possession API: `SetMoveInput`, `TryEatNearby`.
   - "The player obeys the same rules as every rabbit."
2. **Minimal loop.** Show `PlayerController` (stamina, camera-relative movement) and `FollowCamera`.
3. **`GameManager`.** Show the state machine and `LineageTransferRoutine` (a coroutine).
   - `FindHeir` uses my generic `FindBest<T>` from Homework 3.
4. **Burrows.** Show `Herbivore.CanBeEaten => isAlive && !IsHidden`: "predators needed zero new code".
5. **Abilities.** Show that the deer herd alarm moved up to `Herbivore`, and that `Thump` re-uses it.
6. **Day/night and balancing.** Show `DifficultySettings` and `ProgressionSettings` in the GameManager Inspector.

*Play mode (around 30 s):*
- the title screen
- eating with the danger banner
- sprinting into a burrow (the rabbit sinks in)
- the night falling
- an ability unlock
- a death and the next generation

## 2:30 – 2:55 · Challenges solved
- **Coupling.** The simulation must not depend on the game. I solved it with C# events (`Died`, `JoinedWarren`, `DayStarted`).
- **Static events and scene reload.** Restart reloads the scene, so static subscriptions are removed in `OnDestroy`. Otherwise destroyed managers would still receive events.
- **Fairness against stealth.** A stalking fox is only "felt", while a chasing predator is announced. This keeps the Homework 3 fox mechanic meaningful.
- **Player vs AI conflicts.** The AI could pick the player as a mate. `IsReadyToMate` now excludes possessed animals, and the shared rule lives in `MeetsMatingRequirements`.

---

### Submission checklist
- [ ] Run **EcoSim → Build Everything** once, so that `Assets/Prefabs`, `Assets/Visual Assets` and `Assets/Scenes` exist in the folder you submit.
- [ ] Folder `ThodupunuriAnshulHomework4/` contains:
  - `H4ThodupunuriAnshul.pdf`
  - `H4ThodupunuriAnshul/` (the Unity project: Assets, Packages, ProjectSettings, Docs; **without** `Library/`, `Temp/`, `Logs/`, `obj/`, `UserSettings/`)
  - `H4ThodupunuriAnshul.mp4`
- [ ] Zip the folder, upload it to Google Drive, and set sharing to **Anyone with the link → Viewer**.
