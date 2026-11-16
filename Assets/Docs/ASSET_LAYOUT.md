# Структура Assets

Реорганизация выполнена через AssetDatabase.MoveAsset с сохранением GUID,
метаданных импорта и имён файлов. Исходные модели, анимации, материалы, сцены,
префабы, звук, настройки рендеринга и игровой runtime-код сохранены по содержимому.
Код редакторских инструментов изменён только для новых путей.

```
Assets/
├── Art/
│   ├── Animations/Characters, Weapons/
│   ├── Materials/Gameplay, Home, Weapons/
│   ├── Models/Authoring, Characters, Environment, Weapons/
│   └── Textures/Home, Weapons/
├── Audio/Music, Sound/
├── Code/Scripts, Shaders/
├── Docs/Home, References, TutorialInfo, LegacyFolders/
├── Level/Prefabs, Scenes, UI/
├── Settings/Input, Lighting, Rendering, Resources, Volumes/
├── Resources/
├── StreamingAssets/
├── TextMesh Pro/
└── ThirdParty/
```

Code/Scripts разделён на Player, Weapons, Interaction, Stress, UI, Scenes.
Редакторские инструменты находятся в Code/Scripts/Editor/{Home,Project}.
Сборки Editor/runtime по-прежнему разделяет специальное имя Editor. В Assets
до переноса не было .asmdef/.asmref; установленные Packages не перемещались.

Сцены сборки: Level/Scenes/Locations/{Metro,Home}, затем
Level/Scenes/Gameplay/{ConsciousnessScene,CarxDemo,StressDemo}.
Индексы сохранены: 0, 1, 2, 3, 4. Recovery, тестовые сцены и папки запечённого
освещения тоже сохранены. Level/UI/Input хранит CarxUIActions; встроенный UI
игрока остаётся в его префабе и сценах, чтобы не менять их устройство.

Активные руки: Art/Models/Weapons/Hands/source/AnimatedPistol.fbx.
Производный префаб: Level/Prefabs/Weapons/HandsReload.prefab.
Клипы/контроллер: Art/Animations/Weapons/Hands, клипы — в Retargeted.
Производный меш: Art/Models/Weapons/Hands/Derived/HandsPistolReloadMesh.asset.
Конфигурация: Settings/Resources/HandsWeapon.asset. MakarovWeapon тоже сохранён.

Исходные наборы FBX/OBJ/glTF с source/textures и относительными зависимостями
перемещены целиком в Art/Models. Поэтому часть их текстур/материалов остаётся
рядом с моделями. Отдельные игровые материалы и производные текстуры размещены
в Art/Materials и Art/Textures. Встроенные клипы FBX остаются подресурсами модели.

Исключения:

- StreamingAssets/Smoking/model.gltf сохраняет точный путь для glTFast.
- Level/Prefabs/Resources содержит MergedGameplay и MergedNpc; ключи загрузки
  остаются MergedGameplay и MergedNpc.
- Settings/Resources содержит конфигурации оружия и BillingMode; ключи прежние.
- Audio/Sound/Resources содержит MergeStressSound.mp3; ключ прежний.
- Исходная пустая Resources сохранена ради исходного GUID папки.
- TextMesh Pro оставлен целиком на прежнем пути из-за внутренних Resources
  и include-путей шейдеров. Его шейдеры не разбирались по Code/Shaders.
- ThirdParty содержит целые npc_casual_set_00, VintageVictorianWallpapersFree,
  YughuesFreeFlooringMaterials и прежний Sketchfab, включая demo/credits/материалы.
- Docs/TutorialInfo сохраняет комплект readme и его Editor-скрипт; внутренние
  пути загрузки layout обновлены. Пустая исходная Project сохранена с GUID
  в Docs/LegacyFolders/Project.
- Music и Code/Shaders созданы для структуры: музыки и собственных шейдеров
  в исходном Assets не было. Найдены только два звуковых эффекта.

Резервная копия и точная карта путей находятся вне Assets:
F:/Projects/KaramazovMerge/_merge-backups/assets-layout-2026-11-15/.
См. plan.json, path-map.tsv, move-journal.tsv и RESTORE.md. В Assets не помещены
копии исходных импортируемых ресурсов. Ничего не удалялось как неиспользуемое,
предполагаемые дубликаты не объединялись.

Проверка в редакторе: Tools > Project > Validate Assets layout.
Не запускайте Apply Assets layout повторно: это одноразовая миграция с журналом.
Полный отчёт и результаты проверок — ASSET_LAYOUT_REPORT.md в корне проекта.
Ранние JSON-отчёты с SHA256 и старой структурой оставлены как исторические снимки;
для текущего расположения используйте карту переноса, а не прежние JSON-пути.
