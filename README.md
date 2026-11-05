# HSE Metaverse Mods / CodeCraft

Инструментарий создания локаций HSE Metaverse: граф логики на Unity Entities, компоненты механики, гибридный UI и экспорт локаций.

## Установка

Используйте отдельный проект Unity **6000.3.18f1**, Universal Render Pipeline.

В Package Manager выберите **Install package from git URL**:

```text
https://github.com/BurgProject/HSEVirtualSpaceMods.git
```

Либо добавьте запись в существующий объект dependencies файла Packages/manifest.json (не заменяйте весь манифест):

```json
"com.hseverse.mods": "https://github.com/BurgProject/HSEVirtualSpaceMods.git"
```

## Первый запуск

Дождитесь установки зависимостей и завершения компиляции. В SDK есть автоматический мастер настройки; повторный запуск доступен через **HSEverse/Setup**. Он добавляет scripting defines, импортирует CodeCraftModes.unitypackage и настраивает проект. Используйте новый проект: мастер изменяет настройки проекта. При запросе перезапуска редактора выполните его.

Откройте `Assets/Scenes/Main/ContentScene.unity`, затем активируйте подсцену `ContSub` в этой сцене. Создавайте логику в графе CodeCraftScenario. Для сборки локации используйте ScriptableObject `PackageExporter`: задайте Build Path и выберите платформу. Экспорт папки локации, или ее unity package являются разными операциями и не являются одним и тем же. Первое для загрузки в метаверс, второе для дебага вашей локации разработчиками при выявлении проблем.
