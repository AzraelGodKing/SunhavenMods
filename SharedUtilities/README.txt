SharedUtilities for Sun Haven Mods
===================================

This folder contains shared utility code that can be linked into each mod project.
Each mod remains independently buildable while sharing common patterns.

FILES
-----
- PersistentRunnerBase.cs : Base class for MonoBehaviours that survive scene loads
- SceneRootSurvivor.cs    : Optional Harmony postfix on Scene.GetRootGameObjects so registered DontDestroyOnLoad runners are omitted from unload lists (no third-party Keep Alive mod)
- SceneHelpers.cs         : Scene detection utilities (menu vs game detection)
- GUIStyleHelper.cs       : Cached solid Texture2D helpers
- ReflectionHelper.cs     : Common reflection patterns with Harmony integration
- TextInputFocusGuard.cs  : Shared “player is typing in chat/console/UI text field” detection so mod hotkeys can defer (CheatEnabler / Quantum Console friendly). Throttled (~0.25s shared cache), reflection cached after first lookup, no per-frame GetComponents. Link UnityEngine.UI and UnityEngine.IMGUIModule; uses 0Harmony for type resolution.

NAMESPACE
---------
All utilities use: SunhavenMods.Shared

HOW TO LINK IN VISUAL STUDIO
----------------------------
1. Right-click your mod project in Solution Explorer
2. Add > Existing Item...
3. Navigate to SharedUtilities folder
4. Select the file(s) you need
5. Click the dropdown arrow on "Add" button
6. Select "Add as Link"

This creates a link to the shared file rather than copying it.
Changes to the shared file will affect all mods that link to it.

HOW TO LINK IN .CSPROJ (Manual Method)
--------------------------------------
Add to your .csproj file:

  <ItemGroup>
    <Compile Include="..\SharedUtilities\PersistentRunnerBase.cs" Link="Shared\PersistentRunnerBase.cs" />
    <Compile Include="..\SharedUtilities\SceneHelpers.cs" Link="Shared\SceneHelpers.cs" />
    <Compile Include="..\SharedUtilities\GUIStyleHelper.cs" Link="Shared\GUIStyleHelper.cs" />
    <Compile Include="..\SharedUtilities\ReflectionHelper.cs" Link="Shared\ReflectionHelper.cs" />
    <Compile Include="..\SharedUtilities\SceneRootSurvivor.cs" Link="Shared\SceneRootSurvivor.cs" />
    <Compile Include="..\SharedUtilities\TextInputFocusGuard.cs" Link="Shared\TextInputFocusGuard.cs" />
  </ItemGroup>

USAGE EXAMPLES
--------------

1. PersistentRunnerBase - Create a mod-specific runner:

   public class MyModRunner : PersistentRunnerBase
   {
       protected override string RunnerName => "MyMod";
       protected override float HeartbeatInterval => 30f; // Enable for dev tools

       protected override void Log(string msg) => Plugin.Log?.LogInfo(msg);
       protected override void LogWarning(string msg) => Plugin.Log?.LogWarning(msg);

       protected override void OnUpdate()
       {
           // Check hotkeys, update UI, etc.
           if (Input.GetKeyDown(KeyCode.F5))
               ToggleWindow();
       }

       protected override void OnMenuTransition()
       {
           // Clean up when returning to menu
           CloseWindow();
           ResetState();
       }
   }

   // In Plugin.Awake():
   _runner = PersistentRunnerBase.CreateRunner<MyModRunner>();

2. SceneHelpers - Check game state:

   if (SceneHelpers.IsInGame())
   {
       // Safe to access Player.Instance
   }

3. GUIStyleHelper - Cache a solid texture:

   private Texture2D _windowBg;
   private bool _stylesInit = false;

   void InitStyles()
   {
       if (_stylesInit) return;

       _windowBg = GUIStyleHelper.MakeSolidTexture(new Color(0.96f, 0.93f, 0.85f));

       _stylesInit = true;
   }

4. ReflectionHelper - Access game internals:

   // Find and access a singleton
   var dbType = ReflectionHelper.FindWishType("ItemDatabase");
   var db = ReflectionHelper.GetSingletonInstance(dbType);

   // Get a property value
   var items = ReflectionHelper.GetInstanceValue(db, "Items");

   // Safe value access with default
   string name = ReflectionHelper.TryGetValue<string>(item, "Name", "Unknown");

DEPENDENCIES
------------
- UnityEngine.dll (Unity runtime)
- 0Harmony.dll (Harmony library from BepInEx)
- UnityEngine.CoreModule.dll
- UnityEngine.IMGUIModule.dll

These are standard BepInEx/Unity dependencies that all mods already reference.

NOTES
-----
- Each mod can link only the files it needs
- Files are compiled into each mod's DLL (no runtime dependency)
- Mods remain fully standalone and independently buildable
- Updates to shared files require rebuilding mods that use them
