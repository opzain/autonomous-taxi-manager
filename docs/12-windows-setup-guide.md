# 12 — Windows Setup Guide (from zero to the 3D BKC city running)

This guide is for a fresh Windows 10 or 11 laptop that doesn't have Unity yet. It takes about 45–90 minutes, and most of that is downloading. You need about **20 GB of free disk space** and a stable internet connection for the installs.

---

## Part A — Install Unity (one time only)

1. **Download Unity Hub.** Open https://unity.com/download and click **Download for Windows**. Run `UnityHubSetup.exe` and accept the defaults.
2. **Create or sign in to a Unity account.** Open Unity Hub and choose **Sign in**, or **Create account**, which is free. Verify your email if Unity asks.
3. **Activate a free licence.** In Unity Hub, click the ⚙ (Settings) icon, then **Licenses → Add license → Get a free personal license**. Students can also apply for the free Unity Student plan, but Personal is enough.
4. **Install the editor.**
   1. In Unity Hub, go to **Installs → Install Editor**.
   2. On the **Official releases** tab, pick the newest **Unity 6** version marked **LTS** (6000.x.x LTS).
   3. When asked for modules, tick:
      - ✅ **Microsoft Visual Studio Community 2022**, the code editor. Its installer will ask for workloads: tick **Game development with Unity**.
      - ✅ **Windows Build Support (IL2CPP)**, which is optional and only needed later to make a .exe.
      - ❌ Android, iOS and WebGL: not needed.
   4. Click **Install** and wait until it finishes. This takes about 10–30 minutes.

## Part B — Create the project

5. **Create the project.** In Unity Hub, go to **Projects → New project**.
   - Editor version: the **Unity 6 LTS** you just installed (selected at the top).
   - Template: **Universal 3D**. If it shows a download icon, click **Download template** first.
   - Project name: `TaxiManager`.
   - Location: `C:\Unity\Projects`. Use a short path with no spaces to avoid Windows path-length problems.
   - Leave **Connect to Unity Cloud** off, then click **Create project**.
6. **Wait for Unity to open.** The first time takes 2–5 minutes. You'll see a sample scene with a few objects. **Close any welcome or tutorial windows.**

## Part C — Add the Autonomous Taxi Manager files

7. **Extract the project zip.** Right-click `autonomous-taxi-manager-v0.4.zip (or the newest version)`, choose **Extract All…**, and extract to `C:\TaxiManagerSource`.
8. **Find the `_Project` folder.** In File Explorer, open:
   `C:\TaxiManagerSource\autonomous-taxi-manager\unity\TaxiManager\Assets\`
   It contains a folder called **`_Project`**.
9. **Copy `_Project` into the Unity project.** Copy it into `C:\Unity\Projects\TaxiManager\Assets\`, so you end up with `C:\Unity\Projects\TaxiManager\Assets\_Project\Scripts`, `...\Shaders` and so on.
   - If a newer version arrives later: close Unity, delete the old `_Project` folder, copy in the new one, then reopen the project.
10. **Let Unity import and compile.** Click back into Unity. It imports the files and compiles; there's a spinner at the bottom right. Wait until it stops.
11. **Check the Console.** Open **Window → General → Console**.
    - **No red errors:** continue to Part D.
    - **Red errors:** click the **first** red line, copy the text (Ctrl+C), and send it to Claude or the next agent along with `HANDOFF-PLAN.md`. Yellow warnings are fine.
    - If Unity asks *"enable the new Input System and restart?"*, click **Yes**.

## Part D — Build and play the BKC city

12. **Run the setup.** From the top menu bar, choose **Taxi Manager → Setup BKC 3D World (Ultra)**. Click **Let's go** in the dialog. The scene `BKC_World` opens.
    - If the **Taxi Manager** menu is missing, the scripts didn't compile; go back to step 11.
13. **Press ▶ Play** (top centre). The city is generated in about 1–15 seconds. On the title screen choose **START DISPATCH SHIFT** to play the game (see `docs/16-dispatch-game.md`), or **EXPLORE THE CITY** for free-roam.
    - The ESP32 console is found automatically over USB. Upload firmware v0.4 first, and **close Arduino's Serial Monitor**: only one program can use the COM port at a time.
14. **Give the Game view keyboard focus.** Click once inside it so the keyboard works there. For a bigger view, use the **Play Maximized** option in the Game view toolbar (it may be labelled *Play Focused / Play Maximized* depending on the version). Set it before pressing Play.
15. **Press I** to show the controls. Try these:
    - **Tab:** fly to landmarks (Jio World Centre, Equinox / ATLAS SkillTech University, Kurla Station…)
    - **4:** night; **M:** monsoon
    - **P:** a route; **Shift+P:** close a road on it
    - **C:** cinematic tour
16. **Press ▶ again to stop.** Changes you make in Play mode are not kept.

### If it's slow

Press **F3** (High) or **F2** (Medium). Windows laptops with Intel/AMD integrated graphics usually prefer F2. Plug in the charger and choose Windows **Best performance** power mode.

### Screenshots for the report

Press **F9**. Screenshots are saved in:
`C:\Users\<your-name>\AppData\LocalLow\DefaultCompany\TaxiManager\Screenshots`

## Part D2 — Optional: real textures for the photoreal look

See `docs/13-textures-guide.md`. Download about 6 free textures and an HDRI sky, then drop them into `Assets/_Project/Resources/TaxiSimTextures/`.

## Part E — Optional: run the simulation tests

17. Install the **.NET 8 SDK** from https://dotnet.microsoft.com/download (choose **SDK**, not Runtime).
18. Open **PowerShell** in `C:\TaxiManagerSource\autonomous-taxi-manager` (Shift + right-click inside the folder, then "Open PowerShell window here") and run:
    ```
    dotnet test sim-tests
    ```
    All tests should show as passed. If any fail, send the output to Claude.

## Part F — Recommended: back up with Git

19. Install **GitHub Desktop** (https://desktop.github.com).
20. Choose **File → Add local repository → C:\Unity\Projects\TaxiManager → create a repository**. The `.gitignore` in the zip lists the folders not to upload (Library, Temp, Logs).
21. Commit after every working change and **Publish** to a private GitHub repository. You can then share it with teammates and give it to Claude for future sessions.

---

### Common problems

| Problem | Fix |
|---|---|
| Objects are pink | A shader didn't compile. Select it in `Assets/_Project/Shaders` and read the error in the Inspector, then send it to Claude. |
| Error mentions `UnityEngine.Input` | **Edit → Project Settings → Player → Other Settings → Active Input Handling → Both**, then restart Unity. |
| Water looks flat | Run step 12 again; it turns on the Depth Texture. |
| Keys don't respond | Click inside the Game view first. |
| "Taxi Manager" menu is missing | There are compile errors; check the Console (step 11). |
