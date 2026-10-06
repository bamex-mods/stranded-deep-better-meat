# Better Meat v0.3.3 — RU/EN runtime HUD and Mod Settings

База: стабильный Better Meat v0.2.9.

Игровая механика мяса и исправленный strict crosshair targeting не менялись.
Добавлена интеграция с общей системой:

```text
StrandedDeepModSettings
GUID: com.bamex.strandeddeep.modsettings
```

Интеграция является soft dependency.

Если StrandedDeepModSettings установлен:
- появляется раздел `BETTER MEAT` в `Настройки -> МОДЫ`.

Если StrandedDeepModSettings отсутствует:
- Better Meat продолжает работать;
- все значения по-прежнему хранятся в собственном BepInEx config.

## Пользовательские настройки

```text
BETTER MEAT

Таймер приготовления          Вкл./Выкл.
Размер таймера                75–175%
Положение таймера             Низко / Средне / Высоко
Запретить сырое мясо          Вкл./Выкл.
Запретить протухшее мясо      Вкл./Выкл.
Разделять мясо по состоянию   Вкл./Выкл.
```

### Связь с существующим config

`Таймер приготовления`
→ `Cooking HUD / Enabled`

`Размер таймера`
→ `Cooking HUD / Scale`

`Положение таймера`
→ `Cooking HUD / BottomMargin`

Значения:
- Низко = 25
- Средне = 70
- Высоко = 120

`Запретить сырое мясо`
→ `Eating / BlockRawMeat`

`Запретить протухшее мясо`
→ `Eating / BlockSpoiledMeat`

`Разделять мясо по состоянию`
→ `Stacks / SeparateMeatByState`

## Намеренно не выведены в меню

- SecondsPerGameHour
- StrongTextOutline
- DebugLogging
- legacy VerticalPosition
- legacy/experimental menu-hiding settings

Это технические или незавершённые параметры.

## Архитектура

Better Meat остаётся владельцем `ConfigEntry`.

`ModSettingsClient.cs`:
- ищет host по BepInEx GUID;
- получает его assembly;
- reflection-ом вызывает `StrandedDeepModSettings.ModSettingsApi`;
- не требует compile-time reference на `StrandedDeepModSettings.dll`.

В `build.ps1` нет ссылки на `StrandedDeepModSettings.dll`.

## Acceptance test

1. Игра запускается.
2. Настройки -> МОДЫ.
3. Один раздел `BETTER MEAT`.
4. Все 6 контролов присутствуют.
5. HUD toggle сразу скрывает/возвращает таймер.
6. Scale сразу меняет размер.
7. Position меняет положение.
8. RAW/SPOILED toggles реально меняют возможность еды.
9. Stack toggle меняет разделение состояний.
10. После restart значения сохраняются через BepInEx config.
11. Better Meat продолжает работать без host-мода настроек.

## v0.3.3

- Добавлена русско-английская локализация собственного runtime HUD Better Meat:
  названия костра/коптильни, количество мяса, статусы приготовления и копчения.
- Добавлена русско-английская локализация названий мяса, формируемых самим Better Meat.
- Язык определяется через штатный язык Stranded Deep; Russian использует русские строки,
  остальные языки используют английский fallback.
- Runtime-локализация не зависит от наличия StrandedDeepModSettings.
- Stacking, cooking/smoking calculations, eating protection, inventory,
  Mod Settings и split-screen HUD ownership не менялись.

## v0.3.2

- Добавлена русско-английская локализация шести настроек в `Settings -> MODS`.
- Vendored bridge синхронизирован с authoritative bilingual `SDK\ModSettingsClient.cs`.
- Gameplay, ConfigEntry, сохранение настроек и split-screen поведение не менялись.
- Русская/английская регистрация Mod Settings и игровое поведение проверены вручную перед публикацией.

## v0.3.1

Интеграция Mod Settings теперь использует дословно официальный/рабочий
`SDK\ModSettingsClient.cs`, переданный вместе с `API.md`.

Изменения относительно v0.3.0:
- заменён реконструированный reflection bridge;
- используется точный lookup методов по полной сигнатуре;
- доступны штатные `IsAvailable()` и `RemoveMod()`;
- public API helper совпадает с SDK;
- BetterMeat gameplay и набор из 6 настроек не менялись.
