# Радиоархипелаг — рабочий Unity-проект

В Unity Hub рабочий проект подписан **Радиоархипелаг**; его папка — **DancingCatsClone**:
`C:/Users/Derek/Games_develop/Cat/DancingCatsClone`.
Unity Editor: **6000.5.5f1**.

Единственная рабочая сцена: **Assets/RadioArchipelago/Scenes/EmeraldAbyssPilot.unity**.
Она указана единственной в списке сцен запуска. В Editor нажмите Play, затем «ИГРАТЬ».
Быстрый переход: меню **Radio Archipelago → Project → Open current island**.

Исходники игры находятся в `Assets/RadioArchipelago` и `Assets/Clone`; название старой папки не означает отдельный актуальный проект. Старые сцены, нужные генераторам и проверкам, находятся только в `Assets/Editor/SceneTemplates` и не входят в список запуска. Их GUID и содержимое сохранены.

Прежний проект `The Cat`, лишние Sample/Recovery-сцены вынесены за пределы рабочего проекта в `C:/Users/Derek/Games_develop/_Archive/RadioArchipelago/2026-10-28`. Список изменений и инструкции восстановления: [отчёт об очистке](../Docs/Maintenance/UnityCleanup20261028/README.md).

Музыка, карты, готовые модели персонажей и островов, авторские Blender/Tripo-исходники и сохранения сохраняются. Внутренний productName пока оставлен прежним, чтобы при уборке не сменилось хранилище PlayerPrefs.

Историческая документация реконструкции: [RECOVERY.md](../Docs/Clone/RECOVERY.md). Последний визуальный и игровой прогон: [NaturalTrees](../Docs/Design/RadioArchipelago/IslandPilotReview/LivingPass20261028/NaturalTrees/README.md). Билды во время очистки не собираются.