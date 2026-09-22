# Star Levels Expanded — аудит багов

**Версия:** 1.10.2 · **Коммит:** `f3d283b` · **Дата аудита:** 2026-09-21

Все находки проверены чтением исходников. Ссылки вида `файл:строка` соответствуют коммиту выше.

Отчёт разбит на четыре блока. Внутри блока пункты идут по убыванию влияния на пользователя. В конце — секция [С чего начинать](#с-чего-начинать) с порядком из восьми правок.

---

## Прогресс исправлений

Обновляется по ходу работы. Под каждым пунктом отчёта стоит строка **Статус**.

- ✅ исправлено — 36 из 50: U1–U10, L1–L9, L11–L13, C2–C5, C7, Y2–Y4, Y6, Y7, Y9–Y11, Y13
- 🟡 частично / сознательно ограничено — 6: U14, L10, C1, Y1, Y5, Y8
- ⬜ не исправлено — 8: U11, U12, U13, C6, C8, C9, C10, Y12

Блок 5 (разбор панели контрол за контролом) закрыт правками 817ed5d, 44f3091, 6c3c80b и f2f7607; отдельных статусов у его подпунктов нет.

---

## Блок 1. Меню настроек

Это то, из-за чего меню ощущается сломанным.

### U1 — Панель не блокирует игровой ввод (главная причина)

**Статус:** ✅ исправлено (817ed5d) — панель строится через `ConfigUI.CreatePanel`, страж ввода на месте

`StarLevelSystem/modules/UI/QuickConfigureTool.cs:186`

`BuildPanel` создаёт окно напрямую через `GUIManager.Instance.CreateWoodpanel`, минуя `ConfigUI.CreatePanel`:

```csharp
panel = GUIManager.Instance.CreateWoodpanel(
    parent: GUIManager.CustomGUIFront.transform, ...
```

Только `ConfigUI.CreatePanel` навешивает страж ввода — `ConfigUI.cs:144`:

```csharp
panel.AddComponent<ConfigUIInputGuard>().Hold();
```

Значит `GUIManager.BlockInput(true)` для этой панели не вызывается никогда. В панели около 25 полей ввода (`ConfigUI.cs:374-384`). Комментарий в самом UI-ките описывает последствие дословно (`ConfigUI.cs:31-32`):

> Every InputField in a Valheim UI leaks keystrokes into the game — typing a level name walks your character around.

**Эффект:** набор числа в любом поле водит персонажа, открывает инвентарь, переключает предметы.

**Что сделать:** строить панель через `ConfigUI.CreatePanel`. Сейчас её единственный потребитель — `QuickConfigBroker.cs:226`.

### U2 — Escape не закрывает панель, крестика нет

**Статус:** ✅ исправлено (817ed5d) — Escape закрывает верхнюю открытую панель, добавлен крестик

`StarLevelSystem/common/ConfigUI/QuickConfigBroker.cs:126-131`

```csharp
private static bool OnMenuUpdate() {
    if (Instance == null || Instance.listPanel == null) { return true; }
    if (ZInput.GetKeyDown(KeyCode.Escape) == false) { return true; }
    Instance.CloseList();
    return false;
}
```

Обработчик закрывает только `listPanel` (список модов брокера), не панель самого мода. При этом список вообще не создаётся, если зарегистрирован один мод — `QuickConfigBroker.cs:219-222`:

```csharp
if (order.Count == 1) { Invoke(order[0]); return; }
```

В обычной установке (только SLS) `listPanel` всегда `null`, обработчик выходит по первой строке, Escape проваливается в `Menu.Update` и открывает паузу **поверх** открытой панели. `ConfigUI.AddCloseX` к панели мода не применён. Единственный выход — кнопка `Cancel` (`QuickConfigureTool.cs:226`).

Побочно: `QuickConfigBroker.cs:128` читает Escape через `ZInput` (учитывает блокировку ввода), а `:138` — через `Input` (не учитывает). Если блокировка заработает (см. U1), `ZInput` может проглотить нажатие.

### U3 — Удалённый админ молча теряет все правки YAML

**Статус:** ✅ исправлено (6c3c80b) — подключён `ConfigNetwork.RequestEdit`, вердикты сервера показываются в панели

`StarLevelSystem/common/Config/YamlConfigManager.cs:106-109`

```csharp
if (ZNet.instance != null && ZNet.instance.IsServer() == false) {
    message = $"{file.FileName} belongs to the server; changes have to be sent to it.";
    return false;
}
```

`QuickConfigureTool` вызывает `ApplyEdited` для четырёх файлов (`:642`, `:660`, `:682`, `:706`) и на отказ реагирует только записью в лог:

```csharp
if (YamlConfigManager.ApplyEdited(YamlConfigManager.LevelSettings, yaml, out string levelMessage) == false) {
    Logger.LogWarning($"Level settings were not saved: {levelMessage}");
}
```

После всех четырёх — безусловно (`:715-716`):

```csharp
Logger.LogInfo("QuickConfigureTool applied and saved configuration.");
ClosePanel();
```

**Эффект:** админ на выделенном сервере выключает модификатор, отключает рейд, правит очки Nemesis — окно закрывается как при успехе, в логе предупреждение, на диске ничего.

Механизм для этого случая существует и не подключён: `ConfigNetwork.RequestEdit` (`ConfigNetwork.cs:99`) не имеет ни одного вызова, событие `EditResult` (`:33`) — ни одного подписчика. Комментарий в `YamlConfigManager` прямо ссылается на этот путь как на штатный.

### U4 — Сохранение затирает список генераторов уровней

**Статус:** ✅ исправлено (44f3091) — генератор стал опциональным, запись правит нулевой элемент списка на месте

`StarLevelSystem/modules/UI/QuickConfigureTool.cs:636`

```csharp
settings.DefaultLevelupGenerators = new List<LevelGenerator> { staged.generator };
```

При этом снимок читает только нулевой элемент (`:976-977`):

```csharp
if (settings?.DefaultLevelupGenerators != null && settings.DefaultLevelupGenerators.Count > 0) {
    src = settings.DefaultLevelupGenerators[0];
```

`DefaultLevelupGenerators` — полноценный список с записями по `PrefabName` (`DataObjects.cs:490-492`, `:352`).

**Сценарий:** админ вручную описал в `LevelSettings.yaml` три генератора, открыл панель, подвигал только ползунки рейдов, нажал `Apply & Save` → генераторы 2 и 3 удалены с диска без возможности восстановления.

**Что сделать:** править элемент `[0]` на месте, не пересоздавая список.

### U5 — Ошибки применения нигде не показываются

**Статус:** ✅ исправлено (6c3c80b) — добавлена строка статуса между кнопками навигации

`ConfigUI.SetMessages` (`ConfigUI.cs:231-241`) существует ровно для вывода ошибок в панель и не вызывается ниоткуда. `ConfigUIPrompt` (`ConfigUIPrompt.cs`, подтверждение несохранённых изменений) — файл целиком без вызовов.

Дополнительно: кнопка `Apply & Save` видна только на пятой странице (`QuickConfigureTool.cs:244`). Пользователь, изменивший что-то на первой странице и нажавший `Cancel`, теряет правки без предупреждения.

### U6 — Половинчатое применение

**Статус:** ✅ исправлено (6c3c80b) — все документы собираются и валидируются до первой записи

`QuickConfigureTool.cs:596-624` присваивает 26 значений BepInEx (каждое немедленно вызывает свой `SettingChanged`) **до** первой валидации YAML на `:642`. Отказ YAML оставляет половину настроек применённой и живой, без отката.

### U7 — `float.TryParse` без инвариантной культуры

**Статус:** ✅ исправлено (4c99ad4) — `ConfigUI` разбирает и форматирует числа через `InvariantCulture`

`StarLevelSystem/common/ConfigUI/ConfigUI.cs:396`

```csharp
box.onEndEdit.AddListener(str => {
    if (float.TryParse(str, out float v) == false) { v = slider.value; }
```

`Fmt` (`ConfigUI.cs:301-303`, `v.ToString("0.00")`) тоже зависит от культуры. Валидатор Unity `ContentType.DecimalNumber` принимает точку независимо от локали.

**Сценарий:** клиент на ru-RU / de-DE, пользователь вводит `1.5` в «HP за уровень». Точка трактуется как разделитель групп → `15` → `Mathf.Clamp(15, 0, 5)` → `5`. Тихая ошибка в 3.3 раза.

**Что сделать:** `float.TryParse(str, NumberStyles.Float, CultureInfo.InvariantCulture, out v)` и `ToString("0.00", CultureInfo.InvariantCulture)`.

### U8 — Показанное значение не равно сохранённому

**Статус:** ✅ исправлено (f2f7607) — диапазоны ползунков приведены к диапазонам `ConfigEntry`, очки Nemesis больше не целочисленные

`ConfigUI.cs:359` клампит значение в диапазон ползунка, а `:385` заполняет текстовое поле уже **из склампленного** ползунка, а не из исходного значения:

```csharp
slider.value = Mathf.Clamp(value, min, max);
...
box.SetTextWithoutNotify(Fmt(slider.value, wholeNumbers));
```

`onChange` при сборке не вызывается, поэтому `staged` хранит настоящее значение — расходится только то, что видит пользователь. Конкретные случаи:

| Поле | Диапазон ползунка | Реальный диапазон |
|---|---|---|
| «Raid frequency» | от `0.1` (`QuickConfigureTool.cs:383`) | `0.001`..`10` (`Config.cs:480`) |
| «Influence radius (m)» | до `1000` (`:461`) | в YAML не ограничен |
| «Min spawn distance» | до `500` (`:462`) | в YAML не ограничен |
| Все очки Nemesis | `wholeNumbers: true` (`:469-475`) | поля `float` в YAML |

Сервер, настроенный на `RaidEventRate = 0.005`, показывает `0.10`. `DecayPerUpdate = 0.5` рисуется как `0` или `1`. Стоит тронуть такой ползунок — настоящее значение потеряно.

### U9 — Защита «не показывать кнопку вне хоста» не работает

**Статус:** ✅ исправлено (f2f7607) — `ApplyRegistration` пересчитывается на смену админ-статуса и на синхронизацию конфига

`QuickConfigureTool.cs:106-127`. `ApplyRegistration()` вызывается один раз из `Awake` (`:98`), когда `ZNet.instance == null`. Поэтому:

```csharp
private static bool IsOwner() { return ZNet.instance == null || ZNet.instance.IsServer(); }
```

возвращает `true`, и ветка с `CanPushRemoteConfig()` (`:117-120`) не выполняется никогда. Ничто не перезапускает `ApplyRegistration` на `SynchronizationManager.OnAdminStatusChanged` или при входе в мир — единственный повторный вызов идёт из `OnShowButtonChanged` (`:102-104`).

Собственный комментарий кода (`:114-116`) описывает намерение, которое не реализовано:

> do not offer the button off-host at all … Better to be missing than to look like it worked

Это прямая причина U3: кнопка предлагается там, где сохранение не работает.

### U10 — Рассинхрон счётчика блокировки ввода

**Статус:** ✅ исправлено (817ed5d) — `PushInputBlock` сообщает, взялся ли блок; `Hold()` запоминает только реально взятый

`ConfigUI.cs:37-41` против `:57-60`:

```csharp
internal static void PushInputBlock() {
    if (Player.m_localPlayer == null) { return; }   // счётчик не увеличен
    inputBlockDepth++;
...
internal void Hold() {
    if (held) { return; }
    held = true;                                    // выставлен в любом случае
    PushInputBlock();
}
```

**Сценарий:** редактор открыт в мире (`depth == 1`). Игрок на мгновение уничтожается (смерть/респавн/телепорт, `Player.m_localPlayer == null`). Открывается вложенный picker → `Hold()` ставит `held = true`, но счётчик остаётся 1. Закрытие picker'а → `OnDestroy` → `PopInputBlock` опускает счётчик 1→0 → `BlockInput(false)` при всё ещё открытом редакторе. Ровно тот случай, ради которого счётчик и вводился (`ConfigUI.cs:32-34`).

**Что сделать:** `Hold()` должен запоминать, увеличился ли счётчик на самом деле.

### U11 — Панель почти не локализована

**Статус:** ⬜ не исправлено

Токены `$sls_cfg_*` используются только на первой странице (`QuickConfigureTool.cs:260`, `:268`, `:276`, `:278`, `:282-283`), и даже там `:261` — сырая английская строка. Хардкод на английском: заголовки страниц (`:238`), страница статистики (`:303-317`), генератор (`:344-358`), рейды (`:374-395`), Nemesis (`:452-475`), модификаторы (`:489-511`), все 28 описаний модификаторов (`:40-69`), кнопки навигации (`:225-228`).

Существующие токены заведены только в `Localization/English.json:2-10` — в остальных ~30 языковых файлах их нет.

### U12 — Нет скролла, жёсткие размеры

**Статус:** ⬜ не исправлено

Корни страниц — прямоугольники фиксированной высоты (`:209-215`): `PanelH - ContentTop - 70` = `690 - 92 - 70` = **528 px**. Скролл есть только у списка модификаторов (`:512-516`) и списка рейдов (`:396-400`).

Левая колонка страницы 2 (`:302-318`) — 15 строк: `13 × 34 + 12 + 4 + 14 × 4` = **514 px**, нижняя граница на **516 из 528**. Одна добавленная строка или другой `GuiScale` выносит содержимое за пределы панели без полосы прокрутки. Правая колонка в том же положении: `genStartY = 254` (`:341`) + 7 строк = 516.

Панель `900 × 690` (`:21-22`) при `GuiScale 1.5` не помещается на экран 1366×768.

Смещения вроде `StartY + 4 * RowPitch + bossShift` (`:337`) посчитаны вручную под текущий порядок строк в списке `:302-318` — вставка строки бесшумно разъедет правую колонку.

### U13 — Панель не реагирует на внешние изменения конфига

**Статус:** ⬜ не исправлено

`staged` снимается один раз при открытии (`:161`). `OnConfigurationSynchronized` доходит только до `QuickConfigBroker.RefreshVisibility` (`:112-114`); открытую панель ничто не перестраивает и не закрывает. Синхронизация с сервером или срабатывание file-watcher'а во время редактирования оставляет на экране устаревшие числа.

Хуже: `ModifiersChanged()` / `RaidsChanged()` / `NemesisChanged()` (`:726-773`) сравнивают с `staged.modifierSource` / `raidSource` / `nemesisSource`, а это **живые ссылки**, захваченные на `:929`, `:937`, `:952`. Они могут измениться под детектором, и тогда он отвечает на вопрос о данных, которых пользователь не видел.

### U14 — Остальное по UI

**Статус:** 🟡 частично (f2f7607) — предупреждение о `Table`, превью генератора и взаимный порядок Min/Max сделаны в 44f3091; `CurrentBossHuds` очищается (4c99ad4). Остальное открыто: локализация кнопок, утечки временных `GameObject`, дублирование скролла без `scrollSensitivity`, двойная отрисовка picker'а, `Next >` / `Apply & Save` в одном прямоугольнике

- **`Table` выбирается без редактора таблиц.** Стиль `LevelupCalculationStyle.Table` есть в переборе (`:349`), но панель не умеет редактировать `LevelupWeightTablesBySpan` (`DataObjects.cs:510-512`). После выбора видимые ползунки «Level-up chance» и «Gaussian offset» перестают на что-либо влиять, без предупреждения. См. также L13.
- **Два разных ползунка подписаны «Max level»** — `ValConfig.MaxLevel` (`:306`) и `generator.MaxLevel` (`:346`), рядом на одной странице, без взаимной привязки.
- **Нет проверки Min ≤ Max** для генератора (`:345-346`); `GetLevelUpDefinition` молча меняет их местами (`DataObjects.cs:376`), то есть UI позволяет собрать конфигурацию, которая означает не то, что показано.
- **Кнопки `Next >` и `Apply & Save` занимают один и тот же прямоугольник** (`:227-228`). Работает только потому, что `ShowPage` держит их взаимоисключающими, но `applyBtn` создаётся активной и гасится лишь в `ShowPage(0)` на `:175`.
- **Утечки временных объектов:** `GameObject.Instantiate(new GameObject(...))` — исходный объект не уничтожается (`UIPatches.cs:38`, `:67`; `UIHudControl.cs:182-183`, `:218-219`).
- **`characterExtendedHuds` и `CurrentBossHuds` не очищаются между мирами** (`UIHudControl.cs:90`, `:20`) — `Clear()` не вызывается нигде.
- **Дублирование в обход UI-кита.** `common/ConfigUI/README.md:127-129` требует не звать `CreateToggle` напрямую; три места зовут (`:420-425`, `:548-553`, `:803-827`), повторяя фикс вручную. Построение скролла продублировано дважды (`:396-403`, `:512-519`) **без** `scrollSensitivity = 200f` из `ConfigUI.CreateScroll` (`ConfigUI.cs:164`) — списки рейдов и модификаторов прокручиваются медленнее остальных.
- **Двойная отрисовка при перестроении picker'а** (`ConfigUIPicker.cs:78`): `Destroy` отложен до конца кадра, новые строки добавляются сразу — кадр с удвоенным содержимым на каждое нажатие клавиши.

---

## Блок 2. Логические баги

### L1 — `SLE_Level_Settings` инициализируется в `null`, а не дефолтами

**Статус:** ✅ исправлено (f8c807f) — инициализация перенесена в статический конструктор

`StarLevelSystem/Data/LevelSystemData.cs:22-24`

```csharp
public static DataObjects.CreatureLevelSettings SLE_Level_Settings = DefaultConfiguration;   // строка 22
public static readonly DataObjects.CreatureLevelSettings DefaultConfiguration = new ...      // строка 24
```

Статические инициализаторы в C# выполняются в порядке текста. На строке 22 `DefaultConfiguration` ещё `null`, поэтому `SLE_Level_Settings` остаётся `null` до вызова `ApplyLoaded` — вопреки замыслу.

Любой незащищённый читатель в этом окне падает: `LevelSelection.cs:168`, `:183`, `DistanceScaleSystem.cs:68`. Существующая заглушка `if (LevelSystemData.SLE_Level_Settings == null) { return; }` (`LevelSelection.cs:291`) — след от уже пойманного падения.

**Что сделать:** поменять объявления местами либо перенести инициализацию в статический конструктор.

### L2 — Бонус зоны инвертирован

**Статус:** ✅ исправлено (f8c807f) — `GetLevelBonus` возвращает `1 + (ZoneLevel - 1) * bonus`

`StarLevelSystem/common/DataObjects.cs:2121-2125`

```csharp
internal float GetLevelBonus() {
    if (ZoneLevel <= 1) { return 1f; }
    float bonus = (ZoneLevel - 1) * ValConfig.ZoneLevelBonusPerLevel.Value;
    return bonus;
}
```

Результат используется как **множитель** порога — `LevelSelection.cs:221`:

```csharp
float levelup_req = kvp.Value * nightBonus * zoneBonus;
```

Два дефекта:

1. **Разрыв.** При `ZoneLevel <= 1` возвращается нейтральная `1f`, а при уровне 2 — сырое `(ZoneLevel-1)*x`. Должно быть `1f + (ZoneLevel-1)*x`.
2. **Инверсия.** Диапазон `ZoneLevelBonusPerLevel` — `0.1`..`50` (`Config.cs:501`). Любое значение **меньше 1** на втором уровне зоны даёт `zoneBonus < 1`, что **понижает** все пороги и делает существ в прокачанной зоне **слабее**. Противоположность смыслу фичи.

Плюс расхождение с описанием (`Config.cs:501`):

> Bonus **added** to each level-up chance tier for each zone level above 1. E.g. 2.0 at zone level 3 **adds +4** to every tier.

Код умножает на 4. При пороге 20 описание обещает 24, код даёт 80.

### L3 — Выбранный уровень может превысить `maxLevel`

**Статус:** ✅ исправлено (f8c807f) — ключ таблицы клампится по `maxLevel`

`StarLevelSystem/modules/LevelSystem/LevelSelection.cs:231-232`

```csharp
if (roll >= levelup_req || kvp.Key >= maxLevel || index == LevelUpWithBonus.Count) {
    selected_level = kvp.Key;
```

`maxLevel` работает как условие выхода из цикла, но присваивается `kvp.Key` без ограничения. Если таблица шансов начинается выше или перескакивает через предел, возвращается значение больше `maxLevel`. Именно такие «переросшие» существа потом приходится исправлять в `CompositeLazyCache.StartZOwnerCreatureRoutines:218`.

**Что сделать:** `selected_level = Mathf.Min(kvp.Key, maxLevel);`

### L4 — Штатные `ConditionalCreatureLevelupChance` нерабочие

**Статус:** ✅ исправлено (f8c807f) — `resolvedByBiome` читается через `TryGetConditionalLevelRange`, работает фоллбэк на биом `All`

`Data/LevelSystemData.cs:341-348` — генератор для Meadows при `defeated_fader`:

```csharp
new LevelGenerator() { MinLevel = 6, MaxLevel = 30, LevelUpChance = 0.25f, ... }
```

`Data/LevelSystemData.cs:80-84` — штатный предел того же биома:

```csharp
{ Heightmap.Biome.Meadows, new DataObjects.BiomeSpecificSetting() { BiomeMaxLevelOverride = 4, } }
```

`GetMaxCreatureLevel` (`LevelSelection.cs:32`) возвращает `4 + 1 = 5`. Минимальный ключ условной таблицы — `6`. В `DetermineLevelRollResult` уже первая итерация удовлетворяет `kvp.Key(6) >= maxLevel(5)`.

**Эффект:** при `EnableConditionalCreatureLevelupChance: true` и убитом Фейдере **каждое** существо в Meadows детерминированно получает уровень 6 (5 звёзд). Тот же дефект в BlackForest (5 против предела 7) и далее по биомам — ни один штатный условный уровень не согласован с лимитами биомов.

Замысел виден в мёртвом коде: `resolvedByBiome` (`ConditionalScaleSystem.cs:11`, `:52`) заполняется и нигде не читается, кроме `.Count` в строке лога (`:59`) — то есть Min/Max генератора никуда не применяются (см. Y13).

### L5 — Кэш условного скейлинга не сбрасывается при убийстве босса

**Статус:** ✅ исправлено (f8c807f) — активный ключ выводится на каждый вызов, кэшируется только разворот генератора

`ConditionalScaleSystem.cs:37` читает **глобальные ключи мира**:

```csharp
if (entry.Key != null && ZoneSystem.instance.GetGlobalKey(entry.Key)) {
```

Ключи выставляются через `ZoneSystem.SetGlobalKey`. Единственные хуки сброса — `LevelScalingPatches.cs:92-104`, патчи `Player.AddUniqueKey` и `Player.RemoveUniqueKey`. `ZoneSystem.SetGlobalKey` не патчится нигде (проверено `grep`).

**Эффект:** на выделенном сервере локального `Player` нет вовсе, поэтому `cacheValid` остаётся `true` на всё время жизни процесса. Победа над боссом не меняет ничего до перезагрузки конфига (`LevelSystemData.cs:557`) или перезапуска.

### L6 — NullReferenceException при разведении прирученных

**Статус:** ✅ исправлено (f8c807f) — `cdc_parent` больше не разыменовывается в логе

`StarLevelSystem/modules/LevelSystem/LevelPatches.cs:345`

```csharp
Logger.LogDebug($"Parent level {inheritedLevel} being used for child from: proc-{proc.m_character.m_level} cdc-{cdc_parent.Level}.");
```

`cdc_parent` явно проверяется на `null` двумя местами выше по потоку (`:311`, `:322`), но здесь разыменовывается без проверки. Сам код документирует, что запись кэша может отсутствовать законно (`LevelPatches.cs:182-184`): «UpdateYamlConfig flushes the whole cache on a config sync».

**Эффект:** разведение любого прирученного существа после перезагрузки конфига бросает исключение внутри делегата транспайлера `Procreation.Procreate`.

### L7 — Off-by-one и противоречие в случайных уровнях потомства

**Статус:** ✅ исправлено (f8c807f) — `Random.Range(1, inherited + 1)`, в ZDO пишется выпавший уровень

`LevelPatches.cs:327`

```csharp
int level = UnityEngine.Random.Range(1, inheritedLevel);
```

`Random.Range(int, int)` исключает верхнюю границу: от родителя 2-го уровня всегда рождается 1-й, а `inheritedLevel` недостижим для любого родителя. Нужно `Random.Range(1, inheritedLevel + 1)`.

Тут же, `:335-337`:

```csharp
CharacterCacheEntry cce = CompositeLazyCache.GetAndSetLocalCache(chara, inheritedLevel, updateCache: true);
chara.m_nview.GetZDO().Set(ZDOVars.s_level, inheritedLevel);   // уровень родителя
CreatureSetupControl.CreatureSetup(chara, level, delay: 0.1f); // случайный уровень
```

В ZDO (реплицируемое, авторитетное значение) пишется `inheritedLevel`, а не выпавший `level`. После перезахода в мир `RandomizeTameChildrenLevels` фактически не работает.

### L8 — NRE на частично заполненном `Colorization.yaml`

**Статус:** ✅ исправлено (7842603) — пропущенная секция `DefaultLevelColorization` достраивается до слияния

`StarLevelSystem/modules/Colorization/Colorization.cs:64-68`

```csharp
creatureColorizationSettings = parsed;
foreach (var entry in defaultColorizationSettings.DefaultLevelColorization) {
    if (!creatureColorizationSettings.DefaultLevelColorization.Keys.Contains(entry.Key)) {
```

`DefaultLevelColorization` объявлен без инициализатора (`DataObjects.cs:674`), поэтому при отсутствии секции в файле он `null`. `ColorSettings` не регистрирует `Validate` (`StarLevelConfigFiles.cs:39-48`), так что документ не отклоняется.

Исключение проглатывается apply-хуком (`YamlConfigFile.cs:383-387`), но сломанный объект **уже присвоен** на строке 64 — мод доигрывает сессию с `null`-таблицей цветов. `ConfigFailurePolicy.RevertToDefaults` не срабатывает, потому что загрузка формально успешна.

Заголовок файла обещает обратное (`StarLevelConfigFiles.cs:272`):

> A partial file is fine: any star the file does not define falls back to the built-in defaults

Корневая причина — Y1: обещанного слияния с дефолтами не существует.

### L9 — Границы максимального уровня расходятся

**Статус:** ✅ исправлено (1255ec9) — оба оставшихся места в Nemesis (`NemesisRemoteSpawnControl`, `NemesisPatches`) переведены на `GetMaxCreatureLevel`; у метода появился параметр `asBoss` для существа, которое только повышается до босса

`modules/LevelSystem/UpdateLevelsOnChange.cs:51`

```csharp
if (chara.GetLevel() <= ValConfig.MaxLevel.Value) { continue; }
```

против `LevelSelection.cs:26-32`, где `GetMaxCreatureLevel` возвращает `max_level + 1` и учитывает `MaxBossLevel`, `BiomeMaxLevelOverride`, `CreatureMaxLevelOverride`.

Комментарий в `LevelSelection.cs:19-25` прямо требует совпадения всех границ:

> If they drift, a creature sitting at exactly the maximum re-rolls a fresh random level on every cache build … which turns the per-frame EnemyHud cache check into a permanent invalidate/rebuild loop.

Эта граница расходится. Существо, законно стоящее ровно на пределе (`MaxLevel + 1`), перестраивается при каждом `SettingChanged` для `MaxLevel`; существа под более высоким `BiomeMaxLevelOverride` — тоже.

То же в `modules/NemesisSystem/NemesisRemoteSpawnControl.cs:396`:

```csharp
cce.Level = Mathf.Min(levelBonus + cce.Level, ValConfig.MaxLevel.Value);
```

Здесь должен быть `GetMaxCreatureLevel(...)`: миниборсс Nemesis не может дойти до настроенного предела, а спавны с флагом босса ограничены небоссовой настройкой.

### L10 — `MaxBossLevel` недостижим при штатных дефолтах

**Статус:** 🟡 частично (4c99ad4) — описание `MaxBossLevel` теперь честно говорит о приоритете `BiomeMaxLevelOverride`; само поведение не изменено

`LevelSelection.cs:27-29`

```csharp
int max_level = (character != null && character.IsBoss()) ? ValConfig.MaxBossLevel.Value : ValConfig.MaxLevel.Value;
if (biome_settings != null && biome_settings.BiomeMaxLevelOverride != 0) { max_level = biome_settings.BiomeMaxLevelOverride; }
```

`BiomeMaxLevelOverride` перекрывает боссовый предел безусловно. При штатных дефолтах Эйктюр в Meadows ограничен уровнем 4, а не 10. Описание `MaxBossLevel` (`Config.cs:386`) о приоритете биома не упоминает.

### L11 — Несколько генераторов складываются аддитивно без ограничения

**Статус:** ✅ исправлено (1255ec9) — все три места слияния используют `LevelGeneratorResolver.MergeGeneratorCurve`, сумма клампится по 100 с предупреждением; аддитивность описана в заголовке YAML и в `[Description]`

`modules/LevelSystem/LevelGeneratorResolver.cs:42-44` → `common/SLSExtensions.cs:317-321`

```csharp
if (addative) { primaryDict[key] += otherDict[key]; }
```

Два генератора по 25 % на уровнях 1..8 дают порог 50, три — 75. Ничто не ограничивает сумму сотней, а выше 100 уровень становится **навсегда** недостижим: `roll` берётся из `Random.Range(0f, 100f)`, условие `roll >= levelup_req` не выполнится никогда.

Ни заголовок YAML (`StarLevelConfigFiles.cs:176-191`), ни атрибуты `[Description]` не упоминают, что генераторы складываются, а не накладываются.

### L12 — Подсчёт убийств для зон без проверки владельца

**Статус:** ✅ исправлено (f2f7607) — патч проверяет владельца, прирученных и игроков

`modules/LevelSystem/LevelScalingPatches.cs:10-13`

```csharp
static void TrackZoneDeath(Character __instance) {
    ZoneScaleSystem.OnCreatureKilled(__instance.transform.position);
}
```

`ZoneScaleSystem.cs:217-218` утверждает «Runs on the creature's owner peer», но патч не проверяет ни `m_nview.IsOwner()`, ни `IsTamed()`, ни `IsPlayer()`. Любой путь, вызывающий `Character.OnDeath` не у владельца (или у прирученного, или у тренировочного манекена), накручивает счётчик зоны, а зона от этого необратимо повышает уровень.

### L13 — Стиль `Table` непригоден в поставке

**Статус:** ✅ исправлено (44f3091) — короткая или отсутствующая таблица откатывается на `Exponential`, неубывающие пороги клампятся

`Data/LevelSystemData.cs:334-338` задаёт таблицы для спанов **4, 5, 6**:

```csharp
LevelupWeightTablesBySpan = new Dictionary<int, SortedDictionary<int, float>>() {
    { 4, ... }, { 5, ... }, { 6, ... },
},
```

При этом у всех штатных генераторов `ConditionalCreatureLevelupChance` спан (`MaxLevel - MinLevel + 1`) равен **8, 11, 14, 17, 20 или 25** (`:341-543`), и все они используют `LevelupCalculationStyle.Exponential`. То есть:

- ни один штатный генератор не использует `Table`, вопреки сообщению коммита `d439ecc` («ship Table style in the default ConditionalCreatureLevelupChance»);
- таблицы 4/5/6 — мёртвые данные;
- админ, переключивший штатный генератор на `Table`, попадает в ветку предупреждения (`DataObjects.cs:433`) и получает схлопывание в один уровень.

Дополнительно, в самой реализации (`DataObjects.cs:437-443`):

```csharp
int position = 0;
foreach (KeyValuePair<int, float> kvp in shape) {
    int lvl = min + position;
    if (lvl > max) { break; }
    chances.Add(lvl, kvp.Value);
    position++;
}
```

- **Длина таблицы не сверяется со спаном.** Таблица под спан 6, содержащая 4 записи, покрывает только `min..min+3`; `max` становится недостижим без предупреждения. Документация (`StarLevelConfigFiles.cs:199-207`) обещает сдвиг формы на весь диапазон `MinLevel..MaxLevel`.
- **Монотонность порогов не обеспечивается.** Остальные три ветки применяют `Mathf.Max(threshold, epsilon)` и принудительный `epsilon` на `max` (`:394`, `:419`, `:452`), `Table` пишет `kvp.Value` как есть. Контракт заявлен в `DataObjects.cs:370-371`: пороги обязаны строго убывать. `SortedDictionary<int,float>` сортирует по ключу, а не по значению, так что порядок записи не защищает.

Ни `Table`, ни изменение порядка боссовых тиров (поведенческое изменение для всех с включённым `EnableConditionalCreatureLevelupChance`) не попали в `Package/CHANGELOG.md` — последняя запись всё ещё 1.10.2, версия в `manifest.json` и `StarLevelSystem.cs:29` тоже.

---

## Блок 3. Расхождения между настройками и поведением

### C1 — Мёртвые настройки

**Статус:** 🟡 частично (4c99ad4, f2f7607) — `MiniMapRingGeneratorUpdatesPerFrame`, `EnemyHealthPerWorldLevel`, `RaidSpawnEntry.LevelMin` и `TolerantEnumConverter.SetFallback` подключены; `RequestEdit` / `EditResult` подключены в 6c3c80b. Открыто: `NeedsPrefabs`, `ConfigUI.SetMessages`, `ConfigUIPrompt`

Объявлены, забинжены, задокументированы — и не читаются нигде (проверено `grep` по всему дереву).

| Настройка | Объявление | Что на самом деле |
|---|---|---|
| `EnemyHealthPerWorldLevel` | `Config.cs:411` | Мировой уровень считается по ванильному множителю — `HealthModifications.cs:24`: `chealth *= (float)Game.m_worldLevel * Game.instance.m_worldLevelEnemyHPMultiplier;`. Настройка не влияет ни на что |
| `MiniMapRingGeneratorUpdatesPerFrame` | `Config.cs:403` | Потребителя нет. `DistanceScaleSystem` на неё не ссылается |
| `RaidSpawnEntry.LevelMin` | `DataObjects.cs:1363` | Задокументирована в заголовке (`StarLevelConfigFiles.cs:442`) и проставлена осмысленными значениями во всех штатных рейдах (`RaidsData.cs:41-413`). Единственный потребитель уровней рейда — `RaidRunner.cs:200` — передаёт только `LevelMax`. Рейд «Hildir demon» с `LevelMin = 25` спавнит скелетов 1 уровня |

Туда же — мёртвая инфраструктура: `ConfigNetwork.RequestEdit` и `EditResult` (см. Y8), флаг `NeedsPrefabs`, метод `TolerantEnumConverter.SetFallback`, `ConfigUI.SetMessages`, весь `ConfigUIPrompt`.

### C2 — Описания противоречат коду

**Статус:** ✅ исправлено (4c99ad4, 7842603) — все перечисленные описания переписаны под фактическое поведение

| Настройка | Написано | Код |
|---|---|---|
| `EnemyHealthMultiplier` (`Config.cs:410`) | «The amount of health that **each level** gives a creature» | Плоский множитель без `GetLevel()` — `HealthModifications.cs:44` |
| `BossEnemyHealthMultiplier` (`Config.cs:413`) | «**1 is 100% more health per level**», дефолт `0.3` | `HealthModifications.cs:41`: `chealth *= 0.3f` → босс без переопределения `HealthPerLevel` получает **30 % базового HP**, то есть слабее ванили на любом уровне. Урон при этом действительно умножается на уровень (`DamagePatches.cs:36,42`) — две половины фичи не согласованы |
| `MultiplayerEnemyHealthModifier` (`Config.cs:447`) | ключ говорит «health», описание — «take reduced damage» | `MultiplayerDamageMod.cs:38` патчит `Game.GetDifficultyDamageScaleEnemy` — это множитель **получаемого урона**. HP не меняется ни у кого |
| `EnableMultiplayerEnemyHealthScaling` (`Config.cs:450`) | «Creatures gain more health when players are grouped up» | То же: здоровье не растёт |
| `MultiplayerEnemyDamageModifier` (`Config.cs:446`) | «…**per player**. .2 = 20%» | `MultiplayerDamageMod.cs:17`: `(1f + playerDifficulty) * value`. При `playerDifficulty = 3` и дефолте `0.05` описание обещает +15 %, код даёт +20 % |
| `HealthDisplayFontSizeAdjustment` (`Config.cs:543`) | «**Percentage** modification», дефолт `0.8` | Доля 0..1 — `UIHudControl.cs:325`. Админ, прочитавший «percentage» и вписавший `80`, получит шрифт 640 pt |
| `MaxLevel` / `MaxBossLevel` (`Config.cs:384,386`) | «The Maximum number of **stars**» | Код сравнивает уровни (`UpdateLevelsOnChange.cs:51`), а заголовок YAML сам поясняет: «level 1 has no stars, level 2 = 1 star» (`StarLevelConfigFiles.cs:123`). `MaxLevel 20` — это 19 звёзд |
| `EnemyHealthbarScalarX` / `Y` (`Config.cs:540-541`) | Байт-в-байт одинаковые описания | Разные оси, разные дефолты (1.0 и 1.75) — по описанию не различить |
| `MaxZoneSize` (`Config.cs:510`) | Парность с `MinZoneSize` по названию | `MinZoneSize` — порог отнесения к острову (`ZoneScaleSystem.cs:124`), `MaxZoneSize` — сторона ячейки сетки (`ZoneScaleSystem.cs:115`). Диапазоном они не являются, соотношение не валидируется |
| `EnableDistanceLevelBonus` (`StarLevelConfigFiles.cs:170`) | «turns the whole system on/off» | Учитывается только в `LevelSelection.DetermineDistanceBonus` (`:183`); путь объектов/деревьев/птиц/рыб смотрит только на BepInEx-настройку (`LevelSelection.cs:141`, `DistanceScaleSystem.cs:68`). Формулировка `[Description]` в `DataObjects.cs:498` вдобавок инвертирована |

### C3 — Дефолт вне собственного диапазона

**Статус:** ✅ исправлено (4c99ad4) — настройка переименована и получила диапазон, в котором лежит её дефолт

`Config.cs:403`

```csharp
MiniMapRingGeneratorUpdatesPerFrame = BindServerConfig("LevelSystem", "MiniMapRingGeneratorUpdatesPerFrame", 1000, "...", true);
```

Перегрузка для `int` (`Config.cs:946`) имеет сигнатуру `(..., bool advanced = false, int valMin = 0, int valMax = 150)` и всегда навешивает `AcceptableValueRange`. Дефолт 1000 будет урезан BepInEx до 150 при первой записи, хотя описание обещает «Higher values make this go faster». (Практического эффекта нет только потому, что настройка мёртвая — см. C1.)

### C4 — Около 13 настроек молча наследуют диапазон `0..150`

**Статус:** ✅ исправлено (4c99ad4) — восемнадцати настройкам проставлены осмысленные min/max

Числовые перегрузки `BindServerConfig` (`Config.cs:946`, `:966`) навешивают `AcceptableValueRange` **всегда**, поэтому «без границ» не бывает — бывают неверные границы. Настройки, забинженные без явных min/max:

`FishSizeScalePerLevel` (`:434`), `TreeSizeScalePerLevel` (`:439`), `PerLevelTreeLootScale` / `PerLevelBirdLootScale` / `PerLevelMineRockLootScale` / `PerLevelDestructibleLootScale` (`:441-444`), `MultiplayerEnemyMinDamageTaken` (`:448`), `HealthDisplayFontSizeAdjustment` (`:543`), `InitialDelayBeforeSetup` (`:562`), `KillReportFlushIntervalSeconds` (`:511`), `MaxActiveRaids` (`:485`), `MaxMajorModifiersPerCreature` / `MaxMinorModifiersPerCreature` (`:517-518`), `MaxBossModifiersPerBoss` (`:528`), `LimitCreatureModifierPrefixes` (`:530`), `FallbackDelayBeforeCreatureSetup` (`:563`).

Среди них доли 0..1. Отдельно опасен `MultiplayerEnemyMinDamageTaken` — это **нижняя граница множителя получаемого урона** (`MultiplayerDamageMod.cs:38`); любое значение больше 1 инвертирует фичу, и сгруппировавшиеся игроки начинают наносить врагам **больше** урона. Описание при этом прямо говорит «0.2 = 20%».

### C5 — Ноль разрешён там, где он ломает систему

**Статус:** ✅ исправлено (4c99ad4) — нули запрещены там, где они ломают систему

- `KillReportFlushIntervalSeconds = 0` (`Config.cs:511`) → `ZoneScaleSystem.cs:234-235`: `WaitForSeconds(0)` пропускает один кадр, то есть слив очереди и (на клиенте) отправка RPC выполняются **каждый кадр**.
- `HealthDisplayFontSizeAdjustment = 0` или `EnemyHealthbarScalarY = 0` (`Config.cs:540-543`) → нулевой размер шрифта (`UIHudControl.cs:325`).
- `EnemyHealthMultiplier = 0` (`Config.cs:410`) → существа с 0 HP (`HealthModifications.cs:44`).

Для сравнения, места с делением защищены корректно: `DestructibleMaxLevel` и `RockMaxLevel` объявлены с минимумом 1 (`Config.cs:432-433`), `NumberOfCacheUpdatesPerFrame` тоже (`:560`).

### C6 — Ключ не совпадает с именем поля, опечатки в ключах

**Статус:** ⬜ не исправлено

| Поле | Ключ в `.cfg` | Где |
|---|---|---|
| `OverLevelCreaturesGetRerolledOnLoad` | `OverlevedCreaturesGetRerolledOnLoad` | `Config.cs:387` |
| `RandomizeTameChildrenLevels` | `RandomizeTameLevels` | `Config.cs:415` |
| `EnableJewelCraftingBossHudCompat` | `EnableJewelcraftingBossHudCompat` | `Config.cs:557` |
| — | поле `RaidPostion` в сетевом контракте | `Config.cs:827` |

Соседние ключи одной фичи оформлены по-разному: `OverlevedCreaturesGetRerolledOnLoad` против `OverLevelTamesGetRerolledOnLoad` (`:387-388`), `RandomizeTameLevels` против `RandomizeTameChildrenModifiers` (`:415-416`).

**Важно:** эти ключи уже лежат в пользовательских `.cfg` и в сериализованных данных. Переименование — ломающее изменение, требующее миграции.

### C7 — `SettingChanged` навешан непоследовательно

**Статус:** ✅ исправлено (4c99ad4) — обработчики добавлены `MaxBossLevel`, `EnableCreatureScalingPerLevel` и четырём настройкам полосок здоровья

Есть обработчик у: `MaxLevel` (`:385`), `PerLevelScaleBonus` / `MinimumCreatureScale` (`:405`, `:407`), `EnableTreeScaling` / `EnableScalingBirds` / `EnableScalingFish` (`:423-440`), четырёх боссовых настроек HUD (`:553-556`) и ещё около 25.

Нет обработчика у:

- `MaxBossLevel` (`:386`) — при том, что `MaxLevel` его имеет. Снижение боссового предела оставляет переросших боссов в мире, хотя `OverlevedCreaturesGetRerolledOnLoad` обещает «automatically clean up over leveled creatures».
- `EnableCreatureScalingPerLevel` (`:389`) — главный выключатель фичи не пересчитывает уже заспавненных существ, хотя два подчинённых ему ползунка пересчитывают.
- `EnableColorization` (`:409`), `EnableRockLevels` (`:428`), `EnableZoneMapOverlay` (`:506`).
- Все настройки обычных полосок здоровья: `EnemyHealthbarScalarX` / `Y` (`:540-541`), `HealthDisplayFontSizeAdjustment` (`:543`), `EnableEnemyHealthbarNumberDisplay` (`:544`), `UseCustomHealthFont` (`:542`) — читаются только при построении полоски (`UIHudControl.cs:312,325,327`), уже созданные сохраняют старые значения.

### C8 — Клиентские настройки сделаны серверными

**Статус:** ⬜ не исправлено

Все перечисленные идут через `BindServerConfig`, а значит `IsAdminOnly = true` и принудительная синхронизация с сервера:

- Косметика карты: `DistanceRingColorOptions` (`:401`), `MapRingsAboveFog` (`:394`), `EnableMapRingsForDistanceBonus` (`:392`), `ZoneOverlayColorOptions` (`:512`), `ZoneOverlayColorTransparency` (`:514`), `ZoneOverlayAboveFog` (`:507`), `EnableZoneMapOverlay` (`:506`).
- Косметика HUD: `EnemyHealthbarScalarX` / `Y` (`:540-541`), `HealthDisplayFontSizeAdjustment` (`:543`), `EnableEnemyHealthbarNumberDisplay` (`:544`), `StackMultipleBossHealthbars` (`:545`), `BossHealthbarSpacing` (`:546`), `ModifierIconDisplayStyle` (`:534`).
- Машинные настройки производительности и ввода-вывода: `ConfigPollIntervalSeconds` (`:564`), `ConfigApplyDelay` (`:567`), `NumberOfCacheUpdatesPerFrame` (`:560`), `InitialDelayBeforeSetup` (`:562`), `FallbackDelayBeforeCreatureSetup` (`:563`), `OutputColorizationGeneratorsData` (`:561`) — последняя заставляет **каждый клиент** писать файл `DebugGeneratedColorValues.yaml` по решению админа сервера (`Colorization.cs:79`).

Особенно заметен разрыв внутри одной фичи: `BossHudTopBuffer` (`:547`) и `BossHealthbarWidthPercent` (`:550`) — клиентские и помечены «[Client side Config]», а два их соседа, ведущих в тот же обработчик `UIHudControl.OnBossHudConfigChanged`, — серверные.

### C9 — Непоследовательная раскладка по секциям

**Статус:** ⬜ не исправлено

- Секция `"Client config"` — единственная с пробелом и строчной буквой (остальные: `LevelSystem`, `ObjectLevels`, `LootSystem`, `ZoneScaling`, `ModCompat`). В ней же лежат три **серверные** по смыслу настройки: `EnableLocationResetLog` (`:364`), `EnableDebugLocationResetDetails` (`:360`), `EnableDebugRaidDetails` (`:356`) — соответствующие подсистемы выполняются на сервере (`LocationResetLog.cs:29`, `ZoneResetReport.cs:146`).
- Настройки совместимости размазаны по трём секциям: `ModCompat` (`:570-571`), `Raids/EnableCustomRaidsCompat` (`:488`), `UI/EnableJewelcraftingBossHudCompat` (`:557`).
- `UseCustomHealthFont` (`:542`) забинжен через другую перегрузку, чем соседи, и не получает `ConfigurationManagerAttributes`.

### C10 — Клиент не может перечитать свои настройки, будучи в игре

**Статус:** ⬜ не исправлено

`Config.cs:576-585`

```csharp
private static void OnMainConfigFileChanged(string _) {
    if (ZNet.instance != null && ZNet.instance.IsServer() == false) { return; }
    ...
    cfg.Reload();
}
```

Ранний выход завязан на «я сервер», а не на «настройка синхронизируемая». Подключённый к серверу клиент, правящий в файле `Client config/EnableDebugMode`, `UI/BossHudTopBuffer` или `UI/BossHealthbarWidthPercent`, не увидит эффекта до отключения.

---

## Блок 4. YAML, сеть и надёжность

### Y1 — Обещанного слияния с дефолтами не существует

**Статус:** 🟡 сознательно ограничено (7842603) — слияние реализовано там, где оно обещано: `Colorization.yaml` достраивает пропущенную секцию из встроенной таблицы, и заголовок файла теперь говорит правду. Общего слияния «дозаполнить любую пропущенную секцию из дефолтов» намеренно нет: оно воскрешало бы секции, которые админ удалил осознанно. Вместо него — точечные защиты в `ApplyLoaded` (`LevelSystemData`, `RaidsData`, `LocationResetData`) и новый валидатор `LevelSettings`, который ловит единственный по-настоящему фатальный случай

`StarLevelConfigFiles.cs:44-45` при регистрации утверждает:

> Colours are cosmetic and the merge-in of missing default keys makes a partial file workable

Такого слияния нет ни в `YamlConfigFile.cs`, ни в `YamlConfigManager.cs`. Частично заполненный файл даёт `null`-коллекции по всем пропущенным секциям. Это корневая причина L8 и общий риск для всех семи конфигов.

### Y2 — Начальная синхронизация отправляет файл с диска, а не значения из памяти

**Статус:** ✅ исправлено (4c99ad4) — начальная синхронизация не отдаёт файл, который не разобрался на сервере

`common/Config/ConfigNetwork.cs:224`

```csharp
package.Write(File.Exists(file.Path) ? File.ReadAllText(file.Path) : file.SerializeCurrent());
```

`ReloadFromDisk` от этого защищается явно (`YamlConfigManager.cs:85-87`: «Only a clean load goes out to the peers»), а регистрация начального синка на `ConfigNetwork.cs:80` — нет.

**Сценарий:** политика по умолчанию `KeepLastGood` (у `LevelSettings`, `LootSettingsFile`, `LocationResetSettings`, `NemesisSettings`). Файл на диске повреждён, сервер продолжает работать на последних валидных значениях. Каждый входящий клиент получает **повреждённый файл**, не может его разобрать и молча откатывается на собственные дефолты. Сервер и клиенты расходятся в уровнях и луте без единого видимого признака.

`Broadcast` (`ConfigNetwork.cs:186`) читает с диска по той же схеме.

### Y3 — `ApplyEdited` возвращает `true`, даже если запись на диск упала

**Статус:** ✅ исправлено (4c99ad4) — `WriteRawToDisk` сообщает об ошибке, `ApplyEdited` возвращает `false`

`YamlConfigManager.cs:128` вызывает `WriteRawToDisk`, единственная обработка ошибки которого — `:170-172`:

```csharp
catch (Exception e) { Logger.LogError($"Could not write {file.FileName}: {e.Message}"); }
```

После этого `ApplyEdited` рассылает изменения и возвращает `true` (`:132-135`). Значения в памяти, у пиров и на диске расходятся, а админу сообщается об успехе. В сочетании с Y2 следующий рассыл отправит **старые** байты с диска.

### Y4 — Файл с упавшим `Prepare` остаётся без синхронизации на всю сессию

**Статус:** ✅ исправлено (d16b3dd) — `Prepare` разделён на три шага, `ConfigNetwork.RegisterFile` и регистрация watcher'а выполняются независимо от исхода загрузки

`YamlConfigManager.cs:220-224` оборачивает всё тело `Prepare` в `catch (Exception e) { Logger.LogError(...) }`. Если исключение произошло до строки 218, `ConfigNetwork.RegisterFile` не выполняется → `file.Rpc` остаётся `null` → `Broadcast` становится no-op (`ConfigNetwork.cs:184`), `AddInitialSynchronization` не регистрируется. Входящие клиенты не получают по этому файлу **ничего** и работают на своих дефолтах. Единственный признак — одна строка `LogError`.

### Y5 — Watcher основного `.cfg` не переставляет штамп

**Статус:** 🟡 частично (4c99ad4) — `RefreshOwnConfigStamp` переставляет штамп после записи собственного `.cfg`; удаление отслеживаемого файла по-прежнему не замечается

`Config.cs:287` регистрирует watcher для файла BepInEx, но `ConfigFileWatcher.RefreshStamp` вызывается ровно из одного места — `YamlConfigManager.cs:169`, и только для YAML-файлов.

При `cfg.SaveOnConfigSet = true` (`Config.cs:286`) `ApplyAndSave` выполняет около 25 присваиваний (`QuickConfigureTool.cs:596-690`), то есть около 25 перезаписей файла. Следующий опрос видит изменение и вызывает `cfg.Reload()` (`Config.cs:584`), заново поднимая `SettingChanged` для всего, что сдвинулось. Бесконечного цикла нет (`Reload` не пишет), но шторм перезагрузок и обработчиков мод устраивает себе сам. То же происходит, когда Jotunn откатывает синхронизированные значения при отключении.

Зеркальная проблема: `WriteCurrentToDisk` переставляет штамп и **не** вызывает `Broadcast` (в отличие от `ApplyEdited`), поэтому `ApplyNemesisBossAdd` / `Remove` (`Config.cs:616`, `:637`) полагаются на отдельную рассылку `SendNewNemesisBossRPC`. Комментарий на `Config.cs:614-615` описывает противоположное обоснование.

Удаление отслеживаемого файла не замечается вовсе — `ConfigFileWatcher.cs:106`: `if (File.Exists(path) == false) { continue; }`.

### Y6 — Опечатки в enum молча превращаются в нулевой член

**Статус:** ✅ исправлено (f2f7607, d16b3dd) — `SetFallback` подключён для `Character.Faction`; `TolerantEnumConverter` собирает плохие значения в текущую загрузку, и они попадают в `ValidationReport` и на пути `LoadFrom`, и на пути `DryRun` (заодно `DryRun` больше не засоряет лог предупреждениями об отклонённых кандидатах)

`common/Config/TolerantEnumConverter.cs:18-22` формулирует условие безопасности:

> Falling back to the zero member is safe for the enums this is aimed at … Check that holds for your own enums before relying on it, and use SetFallback for any where it does not.

`SetFallback` (`:28`) не вызывается **ни для одного** enum (проверено `grep`).

`DataObjects.cs:1361` подставляет в конфиг рейдов ванильный `Character.Faction`, нулевой член которого — `Players`.

**Эффект:** опечатка во фракции в `RaidSettings.yaml` даёт рейд из существ **на стороне игрока** — они не атакуют и по ним нельзя попасть. Одна строка предупреждения, затем тихо сломанный рейд.

Опечатки enum вдобавок не попадают в `ValidationReport` (`TolerantEnumConverter.cs:53-55` только логирует), поэтому `ApplyEdited` возвращает пустую строку предупреждений (`YamlConfigManager.cs:134`) и UI докладывает, что всё применилось чисто. Конвертер также срабатывает внутри `DryRun` (`YamlConfigFile.cs:147`), засоряя лог предупреждениями для кандидатов, которые затем отклоняются.

### Y7 — Второй, несогласованный YAML-конвейер

**Статус:** ✅ исправлено (44f3091, d16b3dd) — `yamlSerializer` получил `DisableAliases`; `yamlDeserializer` получил `IgnoreUnmatchedProperties` и `TolerantEnumConverter`, а RPC-обработчики разбирают полезную нагрузку через `DataObjects.TryDeserialize` (включая NRE на пустом `OnClientReceiveRaidStart`)

`common/DataObjects.cs:31-35` собирает собственные сериализатор и десериализатор независимо от `YamlFormat`:

```csharp
public static IDeserializer yamlDeserializer = new DeserializerBuilder().WithCaseInsensitivePropertyMatching().WithTypeConverter(new ProtectionRuleYamlConverter()).Build();
```

Следствия:

- Нет `TolerantEnumConverter` и `IgnoreUnmatchedProperties` — все RPC-обработчики разбирают полезную нагрузку **без `try/catch`** (`Config.cs:612`, `:797`, `:807`, `:825-827`, `:897`). Один неизвестный ключ или плохой enum от несовпавшей сборки выбрасывает исключение из корутины. `Config.cs:827` вдобавок разыменовывает результат сразу: `if (raidNetRequest.RaidPostion != Vector3.zero)` — NRE на пустой посылке.
- Нет `DisableAliases()`, в отличие от `YamlFormat.cs:85-86`, где это сделано с явным обоснованием: «an object reused by reference emits &a1 / *a1 anchors, which read as file corruption to an admin editing yaml by hand». Именно этим сериализатором Quick Configure готовит данные (`QuickConfigureTool.cs:638`, `:658`, `:679`, `:704`), и **эти самые байты** пишутся на диск через `WriteRawToDisk`. Сохранение из панели может записать якоря в `LevelSettings.yaml`, `Modifiers.yaml`, `RaidSettings.yaml`, `NemesisSettings.yaml`.

### Y8 — Мёртвый канал правок и мёртвые флаги

**Статус:** 🟡 частично (6c3c80b) — канал правок подключён и используется панелью; `NeedsPrefabs` и версия протокола у семи рабочих RPC остаются открытыми

- `ConfigNetwork.RequestEdit` (`:99`) — ноль вызовов, `EditResult` (`:33`) — ноль подписчиков. При этом все семь файлов ставят `AllowAdminEdit = true` (`StarLevelConfigFiles.cs:36,47,56,67,76,92,101`), регистрируя семь RPC-каналов, по которым не пойдёт ни один пакет.
- `NeedsPrefabs` выставляется у трёх файлов (`StarLevelConfigFiles.cs:55,66,100`) и не читается никем, кроме объявления (`YamlConfigFile.cs:53`). `RevalidateAll` (`YamlConfigManager.cs:178`) перепроверяет все файлы подряд, а `Revalidate` выходит сразу при отсутствии валидатора (`YamlConfigFile.cs:173`). `LootSettingsFile` и `LocationResetSettings` заявляют `NeedsPrefabs = true`, но валидатора не имеют — проход `PrefabManager.OnPrefabsRegistered` для них бесполезен, а их хук `AttachLootPrefabs` (`LootSystemData.cs:208`) отработал только на `Awake`, до появления таблицы префабов.
- Единственная проверка версии протокола — байт `EditProtocolVersion` (`ConfigNetwork.cs:29`, проверки на `:127` и `:157`) в мёртвом канале. Семь рабочих RPC версии не несут вообще (`:210-219`).

### Y9 — Об ошибках разбора сообщается только по первому ключу

**Статус:** ✅ исправлено (d16b3dd) — добавлен `UnknownKeyScan`: обход разобранного документа по типу через рефлексию, до 12 неизвестных ключей за проход, каждый с подсказкой из `ConfigValidation.SuggestKey`

`YamlConfigFile.cs:267-283`: единственный `catch (YamlException strictError)` логирует один `Describe(strictError)`, после чего прогоняет весь документ толерантным проходом. Файл с пятью опечатками покажет одну, четыре отбросит молча.

`ConfigValidation.SuggestKey` (`ConfigValidation.cs:57`), существующий ровно для подсказки «Did you mean 'X'?», из этого пути не вызывается.

### Y10 — `LevelSettings.yaml` — единственный файл без валидации

**Статус:** ✅ исправлено (44f3091, 1255ec9) — `ApplyLoaded` откатывается на встроенные дефолты, добавлен `LevelSystemData.ValidateLevelSettings`: пустой документ — ошибка; возрастающие пороги, `LevelUpChance` вне 0..1, `MinLevel > MaxLevel`, `Table` без таблицы и несуществующая ссылка на генератор — предупреждения

`StarLevelConfigFiles.cs:31-37` — ни `Validate`, ни политики отказа. Для сравнения: `ModifierSettings` (`:64-65`) имеет и валидатор, и `RevertToDefaults`; `ColorSettings` (`:46`) и `RaidSettings` (`:75`) — тоже.

Не проверяется ничего из того, на чём держится математика уровней: убывание порогов, попадание `LevelUpChance` в 0..1 (`DataObjects.cs:385` молча превращает `25` в `100`), `MinLevel <= MaxLevel`, наличие таблицы для спана при стиле `Table`.

Плюс `ApplyLoaded` присваивает разобранный объект без проверки (`LevelSystemData.cs:551-553`):

```csharp
internal static void ApplyLoaded(DataObjects.CreatureLevelSettings parsed) {
    SLE_Level_Settings = parsed;
```

Сравните с `RaidsData.cs:440-449`, который явно логирует и подставляет встроенные дефолты для структурно валидного, но пустого документа, и с `LocationResetData.cs:250` (`parsed ?? DefaultConfiguration`). Здесь `DefaultCreatureLevelUpChance: {}` молча делает всех существ первого уровня: `DetermineLevelRollResult` обходит пустой словарь, оставляет `selected_level = 0` (`LevelSelection.cs:205`), и кламп минимума (`:107`) превращает это в 1.

### Y11 — `RaidSpawnEntry.LevelMax` попадает в ловушку `OmitDefaults`

**Статус:** ✅ исправлено (f2f7607) — `LevelMax` стал константой с `[DefaultValue]`

`common/DataObjects.cs:1363-1364`

```csharp
[DefaultValue(1)]
public int LevelMin { get; set; } = 1;
public int LevelMax { get; set; } = ValConfig.MaxLevel.Value;   // нет [DefaultValue]
```

Это ровно та ловушка, что описана в `YamlFormat.cs:26-32`. `OmitDefaults` сравнивает с `default(int) == 0`, поэтому:

- админ, написавший `LevelMax: 0`, потеряет это при следующей перезаписи;
- значение `MaxLevel` замораживается в момент конструирования объекта и записывается в сгенерированные дефолты, так что последующая синхронизация `MaxLevel` до уже разобранных записей рейдов не доходит;
- если `RaidSpawnEntry` будет сконструирован до бинда `ValConfig` — NRE.

### Y12 — Атрибуты `[Description]` не доходят до пользователя

**Статус:** ⬜ не исправлено

Около 200 строк `[Description]` в `DataObjects.cs` читает единственный потребитель — `DocumentationUpdater.cs:49`, а его точка входа закомментирована (`StarLevelSystem.cs:78`):

```csharp
//DocumentationUpdater.UpdateDocumentation();
```

И даже будучи включённой, она только пишет markdown в `Logger.LogInfo`. В результате существуют два несинхронных источника правды — атрибуты в коде и написанные вручную заголовки в `StarLevelConfigFiles.cs`. Именно отсюда расхождения ниже.

### Y13 — Документация разошлась с кодом

**Статус:** ✅ исправлено (7842603) — описания условных тиров, биома `All`, Nemesis и `Table` приведены к коду; README переписан, корневой `README.MD` больше не заглушка

**Заявленное, но не реализованное поведение:**

| Заявлено | Реальность |
|---|---|
| «The **'All' biome** acts as a fallback within an entry» (`DataObjects.cs:507`, `StarLevelConfigFiles.cs:222`) | `ConditionalScaleSystem.cs:23` делает только точный поиск по биому. Запись, описывающая один `All:`, не применяется ни к чему |
| «Highest tier (**bottom-most listed**) defeated boss applies» (`DataObjects.cs:507`) | `ConditionalScaleSystem.cs:36-41` берёт **первый** совпавший ключ. Коммит `d439ecc` починил данные, переставив дефолты, но описание осталось противоположным |
| «replaces the biome default levelup curve **and Min/Max**» (`DataObjects.cs:507`) | `MinLevel`/`MaxLevel` генератора влияют только на форму кривой. Предел берётся исключительно из `GetMaxCreatureLevel` (`LevelSelection.cs:58`), минимум — из `BiomeMinLevelOverride`/`CreatureMinLevelOverride` (`:72-74`). Это и порождает L4 |
| Условная кривая «replaces» биомную | `LevelSelection.cs:170-174`: сразу после присваивания условной кривой её безусловно перетирает `biome_settings.CustomCreatureLevelUpChance` |

**Дрейф `Package/README.md`:**

| README | Факт |
|---|---|
| `:68` `spawnRateModifier: 1.5` в биоме `All` | `LevelSystemData.cs:70` — `SpawnRateModifier = 1.1f` |
| `:69-78` `creatureBaseValueModifiers` / `creaturePerLevelValueModifiers` внутри штатного `All` | `LevelSystemData.cs:68-79` — там их нет; реальные дефолты приходят из `DamageModifications.cs:162-169` |
| `:79` `damageRecievedModifiers` | Текущее имя — `DamageReceivedModifiers` (`DataObjects.cs:577`); старое написание читается только как легаси (`:582-586`) и обратно не записывается |
| `:105` `1: 100  # Gauranteed level 1` | Инвертировано. Порог 100 означает, что `roll >= 100` практически никогда не выполняется, то есть уровень 1 **пропускается**. Тот же README на `:143` объясняет это верно. Штатный `piece_TrainingDummy` (`LevelSystemData.cs:141-146`, `{1:100, 2:100, 3:0}`) поэтому всегда 3-го уровня |
| `:177-190` `defaultCreatureLevelUpChance` на 12 уровней (`20, 15, 12, 10, 8, 6.5…`) | `LevelSystemData.cs:39-65` — 25 уровней с другими значениями (`20, 10, 5, 2, 1, 0.5…`) |
| `:67-120` ключи в camelCase | `YamlFormat.cs:56` сериализует в PascalCase. На чтение безвредно (десериализатор регистронезависим), но сгенерированный файл не похож на README |

Корневой `README.MD` — 33-байтовая заглушка, содержащая буквальный текст `StarLevelSystem/Package/README.md` (не симлинк), поэтому титульная страница репозитория отображает эту строку.

Совсем не описаны в README: генераторы уровней и `CustomLevelupGenerators`, `LevelupGeneratorRefs`, `ConditionalCreatureLevelupChance`, `LevelupWeightTablesBySpan` и стиль `Table`, вся система Zone Scaling. Они задокументированы только в заголовке `StarLevelConfigFiles.cs`, который пользователь увидит лишь после первого запуска.

---

## Блок 5. Панель настроек: контрол за контролом

Отдельный проход по каждому элементу панели: что он показывает, куда пишет и совпадает ли это с тем, что параметр означает в конфиге.

### G — Генератор уровней и кривые шанса звёзд

Весь блок «Default level generator» на странице 2 (`QuickConfigureTool.cs:341-362`) пишет в `staged.generator`, а тот при сохранении уходит в `LevelSettings.yaml → DefaultLevelupGenerators`. Дальше `LevelSystemData.ApplyLevelupGenerators` (`:578-584`) разворачивает генератор в кривую и **перезаписывает** ей `DefaultCreatureLevelUpChance`:

```csharp
SortedDictionary<int, float> defaultChances = LevelGeneratorResolver.BuildLevelupChance(settings.DefaultLevelupGenerators, settings.DefaultLevelupGeneratorRefs);
if (defaultChances != null) { settings.DefaultCreatureLevelUpChance = defaultChances; }
```

Математика кривых — `DataObjects.cs:372-462`. Потребитель (`LevelSelection.DetermineLevelRollResult`) идёт по ключам по возрастанию и берёт первый уровень, чей порог `<= roll`, то есть **пороги обязаны строго убывать**, а `threshold[MinLevel]` и есть «шанс подняться выше минимума» в процентах.

#### G1 — Открыть панель и нажать «Apply & Save», ничего не меняя, = молчаливый ребаланс всего мода

В штатной конфигурации `DefaultLevelupGenerators` **не задан вообще** (проверено `grep`: поле объявлено в `DataObjects.cs:492`, записывается только из панели, в `LevelSystemData.DefaultConfiguration` отсутствует). Работает вручную настроенная кривая на 25 уровней — `LevelSystemData.cs:39-65`.

Но `CloneOrDefaultGenerator` (`QuickConfigureTool.cs:975-987`) при отсутствии генератора **выдумывает** его:

```csharp
return new LevelGenerator {
    MinLevel = 1,
    MaxLevel = Mathf.Max(1, maxLevel),
    LevelUpChance = 0.2f,
    LevelupCalculationStyle = LevelupCalculationStyle.Exponential,
    ...
```

и сохранение безусловно кладёт его в файл. `ApplyLevelupGenerators` тут же затирает штатную кривую сгенерированной. Сравнение при `MaxLevel = 20`:

| Порог | Штатная кривая | Сгенерированная | Во сколько раз чаще |
|---|---|---|---|
| `P(уровень ≥ 3)` | 10 % | 13,4 % | 1,3× |
| `P(уровень ≥ 5)` | 2 % | 6,0 % | 3× |
| `P(уровень ≥ 8)` | 0,25 % | 1,8 % | 7,3× |
| `P(уровень ≥ 10)` | 0,0625 % | 0,82 % | 13× |

Пользователь открыл меню, нажал «применить» и получил семи-тринадцатикратный рост частоты высокозвёздных мобов, ничего не трогая. Обратной дороги нет: после первого сохранения в файле лежат и генератор, и развёрнутая им таблица, и генератор побеждает при каждой загрузке — правка `DefaultCreatureLevelUpChance` вручную не помогает, нужно удалять `DefaultLevelupGenerators`.

**Что сделать:** не выдумывать генератор. Если его нет — либо не писать секцию вовсе, либо строить стартовый генератор так, чтобы он воспроизводил штатную кривую, и в любом случае предупреждать в панели, что сохранение заменит ручную кривую.

#### G2 — Gaussian: ползунок «Level-up chance» не управляет шансом подъёма

`DataObjects.cs:402-424`. Ветка строит колокол и переводит его в пороги через функцию выживания, **полностью игнорируя `start`** (то есть `LevelUpChance`). `LevelUpChance` используется только здесь:

```csharp
float sigma = Mathf.Max(0.05f, 1f - Mathf.Clamp01(LevelUpChance));
```

то есть управляет **шириной** колокола, а не шансом. Первый порог получается как `(1 - w₀/Σw) · 100`, и он почти всегда близок к 100:

| `LevelUpChance` | σ | `threshold[MinLevel]` | Доля мобов выше минимума |
|---|---|---|---|
| 0 | 1,0 | 96,4 | **96,4 %** |
| 0,2 | 0,8 | 97,0 | **97,0 %** |
| 1,0 | 0,05 | 100 | **≈100 %** |

(расчёт для `MinLevel = 1`, `MaxLevel = 20`, `GaussianOffset = 0`)

Админ ставит «шанс подъёма = 0» и получает 96 % мобов со звёздами. Увеличение ползунка шанс не повышает, а сужает распределение вокруг центра. При `LevelUpChance = 1` порог первого уровня становится ровно `100`, а `Random.Range(0f, 100f)` до 100 не дотягивает — уровень `MinLevel` перестаёт выпадать в принципе.

**Что сделать:** либо масштабировать кривую так, чтобы `threshold[MinLevel] == LevelUpChance · 100` (как в остальных трёх ветках), либо переименовать ползунок в «ширину распределения» и показывать его только для Gaussian, как уже сделано с `GaussianOffset`.

Дополнительно: `Gaussian` — **нулевой член** перечисления (`DataObjects.cs:277`), а `TolerantEnumConverter` при опечатке откатывается именно на нулевой член (Y6). Опечатка в имени стиля молча даёт Gaussian — ровно ту ветку, где шанс не работает.

#### G3 — Table: в поставке не работает ни при каких настройках панели

`DataObjects.cs:425-445`. Таблица ищется по числу уровней `spanCount = MaxLevel - MinLevel + 1`, а в поставке заданы таблицы только для 4, 5 и 6 (`LevelSystemData.cs:334-338`). Ползунки панели по умолчанию дают `MinLevel = 1`, `MaxLevel = 20` → `spanCount = 20` → таблицы нет:

```csharp
Logger.LogWarning($"LevelGenerator '{PrefabName}' uses Table style but no LevelupWeightTablesBySpan entry exists for span {spanCount}; falling back to single level {min}.");
chances.Add(min, 0f);
```

Порог `0` означает, что `roll >= 0` истинно всегда → выбирается `MinLevel` и только он. Выбор «Table» в панели и сохранение = **все существа становятся 1 уровня, звёзды пропадают полностью**. В UI об этом ничего не сказано, редактора `LevelupWeightTablesBySpan` нет, единственный признак — строка в логе.

Чтобы Table сработал, нужно вручную выставить `Max level = Min level + 3` (или `+4`, `+5`) — то есть диапазон максимум в 6 уровней. Панель этого не подсказывает и не ограничивает.

**Что сделать:** либо гасить пункт «Table» в переборе, когда для текущего span нет таблицы, либо показывать рядом предупреждение и список доступных span'ов; фолбэк заменить на сохранение прежней кривой, а не на схлопывание в один уровень.

#### G4 — Linear: «плоская» кривая делает 19 звёзд такой же частой, как 1 звезду

`DataObjects.cs:388-396`: `threshold[lvl] = start · (max - lvl) / span`. Пороги убывают равномерно, а значит **вероятность каждого уровня выше минимума одинакова** и равна `LevelUpChance / span`.

При `MinLevel = 1`, `MaxLevel = 20`, `LevelUpChance = 0.2`: уровень 1 — 80 %, и каждый из уровней 2…20 — по ~1,05 %. То есть примерно каждый 95-й моб в мире получает максимальные 19 звёзд. Для стиля, который в UI выглядит как «ровное распределение», это скорее всего не то, чего ждёт админ, и никакого предупреждения нет.

Отдельно: `[DefaultValue(LevelupCalculationStyle.Linear)]` + инициализатор (`DataObjects.cs:363-364`) означают, что генератор, записанный в YAML **без** явного стиля, получает именно Linear. При этом `CloneOrDefaultGenerator` выдумывает Exponential, а ветка `default:` в `switch` — тоже Exponential. Три разных «значения по умолчанию» для одного поля.

#### G5 — Exponential: «Max level» работает не как потолок, а как регулятор сложности

`DataObjects.cs:446-456`: `decay` подбирается так, чтобы на `MaxLevel` порог гарантированно упал до `epsilon`:

```csharp
float decay = Mathf.Pow(epsilon / start, 1f / span);
```

Значит форма кривой целиком определяется `span`. Одно и то же значение «Level-up chance» при разном `MaxLevel` даёт разную частоту промежуточных уровней:

| `MaxLevel` | `P(уровень ≥ 3)` при `LevelUpChance = 0.2` |
|---|---|
| 5 | 3,0 % |
| 20 | 13,4 % |

Подняв ползунок «Max level» с 5 до 20, админ ожидает «разрешить более высокие звёзды», а получает вчетверо более частых двухзвёздочных мобов на всей карте. То же верно для Linear и Gaussian. В панели и в документации это нигде не сказано.

#### G6 — Панель не показывает результат кривой

Превью справа (`UpdateExampleMath`, `QuickConfigureTool.cs:567-579`) считает только HP и урон от уровня и **не вызывается** ни одним из ползунков генератора. Админ меняет стиль кривой, минимум, максимум и шанс вслепую — ни распределения, ни хотя бы «X % мобов будут иметь ≥ N звёзд» панель не показывает. Учитывая G2–G5, это и есть причина, по которой поломки кривых незаметны до игры.

Плюс в самом превью: ползунок подписан «Max level (stars)», и его значение передаётся как число звёзд напрямую (`:570`, `:575`), хотя уровень 1 = 0 звёзд. Превью завышает количество звёзд на единицу.

### M — Карта «контрол → параметр»

Полный список из 26 контролов панели. Колонка «Проблема» пуста там, где привязка корректна.

#### Страница 1 «Scaling Mechanisms» (`:252-288`)

| Контрол | Пишет в | Проблема |
|---|---|---|
| Distance scaling | `EnableDistanceLevelScalingBonus` (`:596`) | Дублирует YAML-поле `EnableDistanceLevelBonus`, которое панель не трогает; обе половины нужны для полного отключения (см. C2) |
| Distance overlay | `EnableMapRingsForDistanceBonus` (`:597`) | — |
| Zone scaling | `EnableZoneScalingBonus` (`:598`) | — |
| Zone overlay | `EnableZoneMapOverlay` (`:599`) | Нет `SettingChanged` (C7): оверлей не появляется и не исчезает до следующей перерисовки карты |
| Conditional scaling | YAML `EnableConditionalCreatureLevelupChance` (`:635`) | Включает систему, штатные данные которой сломаны (L4): все мобы в Meadows станут 6 уровня |

#### Страница 2 «Stats & Level Generator» (`:290-364`)

| Контрол | Диапазон ползунка | Пишет в | Диапазон в конфиге | Проблема |
|---|---|---|---|---|
| Creature HP / level | 0–5 | `EnemyHealthMultiplier` (`:601`) | 0–5 | Не «за уровень»: код применяет плоский множитель (C2) |
| Creature dmg / level | 0–2 | `EnemyDamageLevelMultiplier` (`:602`) | 0–2 | — |
| Max level (stars) | 1–200 | `MaxLevel` (`:603`) | 1–200 | Это уровни, не звёзды: 20 = 19 звёзд (C2) |
| Boss HP / level | 0–5 | `BossEnemyHealthMultiplier` (`:604`) | 0–5 | Не «за уровень»; при дефолте 0,3 босс получает 30 % базового HP (C2) |
| Boss dmg / level | 0–5 | `BossEnemyDamageMultiplier` (`:605`) | 0–5 | — |
| Max boss level | 1–200 | `MaxBossLevel` (`:606`) | 1–200 | Безусловно перекрывается `BiomeMaxLevelOverride` — при штатных дефолтах недостижим (L10); нет `SettingChanged` (C7) |
| Enemies gain HP with more players | — | `EnableMultiplayerEnemyHealthScaling` (`:607`) | — | HP не растёт: код меняет получаемый урон (C2) |
| HP per extra player | 0–0,99 | `MultiplayerEnemyHealthModifier` (`:608`) | 0–0,99 | Ползунок подписан про HP, параметр — про урон (C2) |
| Enemies gain dmg with more players | — | `EnableMultiplayerEnemyDamageScaling` (`:609`) | — | — |
| Dmg per extra player | 0–2 | `MultiplayerEnemyDamageModifier` (`:610`) | 0–2 | Код даёт `(1+N)·x` вместо `N·x` (C2) |
| Players needed nearby | 1–20 | `MultiplayerScalingRequiredPlayersNearby` (`:611`) | 1–20 | — |
| **Min level** | 1–50 | YAML `generator.MinLevel` | **аналога в `.cfg` нет** | См. M1 ниже |
| **Max level** | 1–200 | YAML `generator.MaxLevel` | **аналога в `.cfg` нет** | Второй ползунок с тем же названием на той же странице; фактически регулятор сложности (G5) |
| Level-up chance | 0–1 | YAML `generator.LevelUpChance` | — | Не работает для Gaussian (G2), игнорируется для Table (G3) |
| Curve style | перебор | YAML `generator.LevelupCalculationStyle` | — | Table ломает уровни (G3), Gaussian ломает шанс (G2) |
| Gaussian offset | −1…1 | YAML `generator.GaussianOffset` | — | Показывается только для Gaussian — единственное место, где панель учла зависимость от стиля |
| Night multiplier | 0–5 | YAML `generator.NightMultiplier` | — | — |

#### M1 — «Min level» в UI против «только максимум» в конфиге

Это ровно то расхождение, о котором вы написали. На одной странице соседствуют:

- **«Max level (stars)»** → BepInEx-настройка `LevelSystem/MaxLevel`, глобальный потолок, синхронизируется с сервера;
- **«Min level»** → поле `MinLevel` внутри генератора в `LevelSettings.yaml`.

Глобального минимума в `.cfg` нет вообще — в секции `LevelSystem` есть только `MaxLevel` и `MaxBossLevel`. Настоящий минимум уровня живёт в двух совсем других YAML-полях, которых панель не показывает — `LevelSelection.cs:72-74`:

```csharp
if (biome_settings != null && biome_settings.BiomeMinLevelOverride > 0) { min_level = biome_settings.BiomeMinLevelOverride; }
if (creature_settings != null && creature_settings.CreatureMinLevelOverride > -1) { min_level = creature_settings.CreatureMinLevelOverride; }
min_level += 1;
```

`generator.MinLevel` действует как минимум лишь побочно — как нижний ключ построенной таблицы. Но при этом:

- он не участвует в клампе `if (min_level > 0 && level < min_level) { level = min_level; }` (`LevelSelection.cs:107`);
- он полностью игнорируется, если кривую перекрыли `CustomCreatureLevelUpChance` биома или существа (`LevelSelection.cs:170-174`);
- он не проверяется против `MaxLevel` — ползунки независимы, `GetLevelUpDefinition` молча меняет границы местами (`DataObjects.cs:376`);
- он может превысить потолок биома, и тогда срабатывает L4.

То есть подпись «Min level» обещает гарантированный минимум звёзд, а поле таким гарантом не является. Два ползунка «Min level» и «Max level» выглядят парой, но пишутся в разные файлы, имеют разную область действия (один — глобальный потолок, другой — форма одной кривой) и не валидируются относительно друг друга.

**Что сделать:** переименовать в «Начало кривой» / «Конец кривой» и явно отделить их от глобального `MaxLevel`; либо вывести в панель `BiomeMinLevelOverride` как настоящий минимум. Добавить проверку `Min ≤ Max` и предупреждение, если `Min` выше действующего потолка биома.

#### Страница 3 «Modifiers» (`:480-524`)

| Контрол | Диапазон ползунка | Пишет в | Диапазон в конфиге | Проблема |
|---|---|---|---|---|
| Max major modifiers | 0–6 | `MaxMajorModifiersPerCreature` (`:614`) | **0–150** | Сервер с 10 показывает 6 и молча уронит до 6 при сохранении |
| Max minor modifiers | 0–6 | `MaxMinorModifiersPerCreature` (`:615`) | **0–150** | То же |
| Major modifier chance | 0–1 | `ChanceMajorModifier` (`:616`) | 0–1 | — |
| Minor modifier chance | 0–1 | `ChanceMinorModifier` (`:617`) | 0–1 | — |
| Limit modifier count to star level | — | `LimitCreatureModifiersToCreatureStarLevel` (`:618`) | — | — |
| Bosses can have modifiers | — | `EnableBossModifiers` (`:619`) | — | — |
| Boss modifier chance | 0–1 | `ChanceOfBossModifier` (`:620`) | 0–1 | — |
| Max boss modifiers | 0–6 | `MaxBossModifiersPerBoss` (`:621`) | **0–150** | То же усечение |
| Max name prefixes | 0–6 | `LimitCreatureModifierPrefixes` (`:622`) | **0–150** | То же усечение |
| Minor modifiers first in name | — | `MinorModifiersFirstInName` (`:623`) | — | — |
| Icon display style | перебор | `ModifierIconDisplayStyle` (`:624`) | список строк | Индекс приводится к enum напрямую (`:502`); работает только потому, что `ModifierDisplayStyle` нумеруется с нуля подряд |
| Список модификаторов | — | YAML `Modifiers.yaml → Enabled` (`:650-663`) | — | — |

#### Страница 4 «Raids» (`:366-415`)

| Контрол | Диапазон ползунка | Пишет в | Диапазон в конфиге | Проблема |
|---|---|---|---|---|
| Enable SLS Raids | — | `UseVanillaRaidConfiguration`, инвертировано (`:666`) | — | — |
| Raid frequency | 0,1–10 | `RaidEventRate` (`:667`) | **0,001–10** | Сервер на 0,005 показывает 0,10 (U8) |
| Minutes between checks | 1–120 | `ServerTimeBetweenRaidStartChecks` (`:668`) | 1–120 | — |
| Max attempts / player | 0–50 | `MaxRaidAttemptsPerPlayer` (`:669`) | 0–50 | — |
| Max active raids | 1–20 | `MaxActiveRaids` (`:670`) | **0–150** | Сервер на 50 показывает 20 и будет урезан |
| Список рейдов | — | YAML `RaidSettings.yaml → Enabled` (`:675-685`) | — | Панель не показывает `LevelMin`/`LevelMax` рейдов, а `LevelMin` вообще не читается кодом (C1) |

#### Страница 5 «Nemesis» (`:443-478`)

| Контрол | Диапазон ползунка | Пишет в | Тип поля | Проблема |
|---|---|---|---|---|
| Enable Nemesis system | — | `EnableNemesisSystem` (`:688`) | bool | — |
| Action cooldown (sec) | 0–120 | `NemesisActionCooldownSeconds` (`:694`) | — | — |
| Influence radius (m) | 0–1000 | `NemesisInfluenceRadius` (`:695`) | float | В YAML не ограничен: значение выше 1000 отобразится усечённым (U8) |
| Min spawn distance (m) | 0–500 | `NemesisMinSpawnDistance` (`:696`) | float | То же выше 500 |
| Neutral score | 0–20000, целые | `ScoreSystem.NeutralScore` (`:698`) | **float** | Дробная часть теряется |
| Min score | 0–20000, целые | `ScoreSystem.MinScore` (`:699`) | **float** | Нет проверки `Min ≤ Neutral ≤ Max` — три независимых ползунка |
| Max score | 0–20000, целые | `ScoreSystem.MaxScore` (`:700`) | **float** | То же |
| Decay per update | 0–2000, целые | `ScoreSystem.DecayPerUpdate` (`:701`) | **float** | Значение 0,5 рисуется как 0 или 1 |
| Score interval (sec) | 1–120, целые | `ScoreSystem.ScoreIntervalSeconds` (`:702`) | **float** | Дробная часть теряется |
| Boss-kill bonus | 0–5000, целые | `ScoreSystem.BossKillBonus` (`:703`) | **float** | Дробная часть теряется |
| Death score reduction | 0–5000, целые | `ScoreSystem.DeathScoreReduction` (`:704`) | **float** | Дробная часть теряется |

Семь ползунков очков объявлены `wholeNumbers: true` (`:469-475`), хотя все семь полей — `float` (`DataObjects.cs:1591-1615`). Любое дробное значение, заданное в YAML, при открытии панели округляется, и если ползунок тронуть — округлённое значение уходит в файл.

### M2 — Что панель не показывает, хотя пользователь этого ждёт

Из 144 настроек BepInEx панель пишет 26. Помимо общего охвата, на её собственных страницах не хватает прямых соседей уже показанных настроек:

- страница 2: `EnemyHealthPerWorldLevel`, `EnableCreatureScalingPerLevel`, `PerLevelScaleBonus`, `MultiplayerEnemyMinDamageTaken` (нижний порог того же множителя, что и «HP per extra player»);
- страница 4: `RaidCooldownClock`, `RaidWindDownSeconds`, `RaidForceDeleteStragglers`;
- вся секция `ObjectLevels` (`FishMaxLevel`, `BirdMaxLevel`, `TreeMaxLevel`, `RockMaxLevel`, `DestructibleMaxLevel`) — при том, что страница 2 называется «Stats & Level Generator»;
- `BiomeMinLevelOverride` / `BiomeMaxLevelOverride`, которые фактически решают исход настроек со страницы 2 (L4, L10).

---

## С чего начинать

Порядок с учётом соотношения «эффект / объём правки»:

1. **U1** — строить панель через `ConfigUI.CreatePanel`. Одна строка, снимает главную жалобу: ввод перестанет утекать в игру.
2. **G1** — перестать выдумывать генератор уровней в `CloneOrDefaultGenerator` и писать его в файл. Сейчас простое нажатие «Apply & Save» без единого изменения перезаписывает штатную кривую и делает высокозвёздных мобов в 7–13 раз чаще, необратимо.
3. **U2** — Escape должен закрывать активную панель мода; добавить `ConfigUI.AddCloseX`.
4. **L1** — порядок статических инициализаторов в `LevelSystemData.cs:22-24`. Одна строка, корневой NRE модели данных.
5. **G2 + G3** — Gaussian должен подчиняться «Level-up chance» (сейчас 96–100 % мобов поднимаются при любом значении); Table не должен схлопывать все уровни в один, когда таблицы для span нет. Это два из четырёх стилей кривой.
6. **U3 + U4** — не закрывать панель при отказе применения и показывать сообщение через `ConfigUI.SetMessages`; перестать затирать список генераторов. Для удалённого админа — подключить `ConfigNetwork.RequestEdit`.
7. **L2** — `GetLevelBonus` должен возвращать `1f + (ZoneLevel-1) * x`; привести описание настройки в соответствие с тем, что код умножает.
8. **L4 + L3** — согласовать штатные условные таблицы с лимитами биомов и добавить `Mathf.Min(kvp.Key, maxLevel)`.
9. **L6 + L7** — убрать NRE на `cdc_parent.Level`, исправить `Random.Range(1, inheritedLevel + 1)` и писать в ZDO выпавший уровень.
10. **Y2** — начальная синхронизация должна отправлять значения из памяти, как это уже делает `ReloadFromDisk`.

Отдельно, малой кровью и с заметным эффектом: **U7** (`InvariantCulture`), **M1** (переименовать «Min level» и добавить проверку `Min ≤ Max`), **C3** (дефолт вне диапазона), **U10** (счётчик блокировки ввода), **L5** (патч `ZoneSystem.SetGlobalKey`), приведение диапазонов ползунков к диапазонам конфига (таблицы блока M).

Полезно и дёшево: **G6** — вывести под генератором строку «X % существ получат ≥ N звёзд». Без неё поломки G2–G5 не видны до попадания в игру.
