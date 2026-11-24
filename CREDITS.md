# Credits и сторонние ассеты

Этот репозиторий — **фанатский мод** для BABBDI (© manu1400). Сам мод не связан с автором игры;
файлы игры здесь не распространяются, только код мода и сгенерированные/сторонние ассеты,
перечисленные ниже.

## Сторонние модели и текстуры

| Файл | Источник | Условия |
|---|---|---|
| `BabbdiCoop/model/soldier.glb` | [three.js `Soldier.glb`](https://github.com/mrdoob/three.js) by **@vxdesign** | **CC-BY 4.0** — требуется указание автора (эта таблица) |
| `BabbdiCoop/model/villager.glb` | модель `Villager Male 3` из **Legacy of Kain: Soul Reaver** (Crystal Dynamics / Square Enix), авто-риг на скелет солдата | фанатское использование; права принадлежат правообладателю |
| `BabbdiCoop/model/mi26.glb` | свободная модель Ми-26 (`MI26_unz.wrl`) + чертёж-референс, переработано | по условиям исходной модели |
| `BabbdiCoop/model/arcade_cabinet.glb` | `BabineauS_ArcadeCabinet.fbx` (free asset pack) | по условиям исходного пака |
| `BabbdiCoop/model/airhockey.glb` | `psx-low-poly-air-hockey-table` (free asset), перетекстурено | по условиям исходного ассета |
| `BabbdiCoop/model/character-r.glb`, `character-m.glb` | Kenney, blocky characters | CC0 |
| `BabbdiCoop/airhockey_patterns/`, `arcade_posters/`, `arcade_neon/` | сгенерировано Stable Diffusion 1.5 для этого проекта | — |
| `BabbdiCoop/model/figures/*.glb` | сгенерировано процедурно (`figs3.py`, `pilots.py`) | — |
| `BabbdiCoop/model/arcade_art/*.png`, `arcade_game/*` | сгенерировано Stable Diffusion 1.5 | — |
| `arcade_digits/` (`digits_v3_chrome`) | шрифт из папки автора проекта, происхождение не установлено | уточняется |

## Код

Весь C#-код мода написан для этого проекта. Используются библиотеки, поставляемые с игрой:
`BepInEx 5`, `Harmony`, `Facepunch.Steamworks`, `Newtonsoft.Json`, `UnityEngine`.

## Если вы правообладатель

Если какой-то файл здесь нарушает ваши права — откройте issue в репозитории или напишите автору,
спорный файл будет удалён по первому требованию.
