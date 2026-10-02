# Stranded Deep Mod Settings API v0.2

This is the shared UI host for our custom Stranded Deep BepInEx mods.

Core rule
---------

The Mod Settings plugin owns UI only.

Each mod owns its own data/configuration.

Example:

UI Scaler
  owns ConfigEntry<float> Scale

StrandedDeepModSettings
  renders a slider
  calls getter/setter delegates supplied by UI Scaler

If StrandedDeepModSettings is absent:
  UI Scaler still loads
  UI Scaler still applies its BepInEx config
  only the in-game MODS page is unavailable

There is no hard assembly reference from client mods to the host API.
Client mods compile ModSettingsClient.cs into their own DLL.
That helper discovers the API at runtime through BepInEx Chainloader and
reflection.

This keeps the dependency soft and reversible.

Public host API
---------------

RegisterMod(
    string modId,
    string displayName,
    int order)

AddSlider(
    string modId,
    string settingId,
    string label,
    int order,
    float min,
    float max,
    float step,
    float displayMultiplier,
    string suffix,
    int decimals,
    Func<float> getter,
    Action<float> setter)

AddToggle(
    string modId,
    string settingId,
    string label,
    int order,
    string onText,
    string offText,
    Func<bool> getter,
    Action<bool> setter)

AddChoice(
    string modId,
    string settingId,
    string label,
    int order,
    string[] choices,
    Func<int> getter,
    Action<int> setter)

AddButton(
    string modId,
    string settingId,
    string label,
    int order,
    string actionText,
    Action action)

RemoveMod(
    string modId)

Client integration
------------------

Copy:

SDK\ModSettingsClient.cs

into the source set of the client mod.

Add a soft BepInEx dependency:

[BepInDependency(
    "com.bamex.strandeddeep.modsettings",
    BepInDependency.DependencyFlags.SoftDependency)]

Then register settings from Awake() or shortly after.

Example slider
--------------

ModSettingsClient.RegisterMod(
    "example",
    "Пример",
    500);

ModSettingsClient.AddSlider(
    "example",
    "scale",
    "Масштаб",
    100,
    1.0f,
    2.0f,
    0.1f,
    100.0f,
    "%",
    0,
    GetScale,
    SetScale);

Example toggle
--------------

ModSettingsClient.AddToggle(
    "example",
    "enabled",
    "Включено",
    200,
    "Вкл.",
    "Выкл.",
    GetEnabled,
    SetEnabled);

Example choice
--------------

string[] choices = new string[]
{
    "Низко",
    "Средне",
    "Высоко"
};

ModSettingsClient.AddChoice(
    "example",
    "quality",
    "Качество",
    300,
    choices,
    GetQualityIndex,
    SetQualityIndex);

Example action button
---------------------

ModSettingsClient.AddButton(
    "example",
    "reset",
    "Сбросить настройки",
    900,
    "Сбросить",
    ResetSettings);

Rendering model
---------------

Each registered mod currently becomes one category/header inside:

Settings -> MODS

For example:

MODS

ИНТЕРФЕЙС
  Масштаб интерфейса          150%

КАРТА
  Размер стрелки              120%
  Метки                       Вкл.

BETTER MEAT
  ...

The page uses native Stranded Deep UI assets:
- existing Options Canvas
- existing ScrollRect
- native TMP fonts
- native category header
- native option slider
- native option button
- native animations/layout

The API registry can change at runtime.
The host watches RegistryVersion and rebuilds the MODS page when a mod
registers or replaces settings.

v0.2 first client
-----------------

StrandedDeepUIScaler v0.3.0 is included in this package and is the first
real API client.

The old direct reflection from the menu host into UI Scaler._scale is gone.

Now ownership is correct:

UI Scaler -> registers itself -> menu host displays it.

The UI Scaler F6/F7/F8 shortcuts are removed in v0.3.0.
The BepInEx config remains the fallback when the menu host is absent.
