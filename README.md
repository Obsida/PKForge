<p align="center">
  <img src="docs/images/logo-transparent.png" alt="PKForge" width="140" />
</p>

<h1 align="center">PKForge</h1>

<p align="center">
  <b>Универсальный редактор сохранений и межпоколенческий Банк Pokémon с поддержкой Windows и русским интерфейсом.</b>
</p>

<p align="center">
  <img src="https://img.shields.io/badge/Платформа-Windows%20x64%20%7C%20Android-blue" alt="Platform" />
  <img src="https://img.shields.io/badge/.NET-10.0-purple" alt=".NET 10" />
  <img src="https://img.shields.io/badge/Движок-PKHeX.Core-green" alt="PKHeX.Core" />
  <img src="https://img.shields.io/badge/Лицензия-GPLv3-orange" alt="License" />
</p>

---

## 📖 О проекте

**PKForge** — это современный автономный редактор сохранений Pokémon со встроенным банком для переноса покемонов между играми разных поколений.

Приложение построено на основе библиотеки **[PKHeX.Core](https://github.com/kwsch/PKHeX)** со встроенным модулем **[Auto Legality Mod](https://github.com/santacrab2/PKHeX-Plugins)**: проверка и исправление легальности покемонов выполняются полностью офлайн, прямо на вашем ПК.

<p align="center">
  <img src="docs/screenshots/home.png" alt="Главное меню PKForge" width="49%" />
  <img src="docs/screenshots/editor.png" alt="Редактор покемона" width="49%" />
</p>

---

## ✨ Основные возможности

- **Полное редактирование покемонов:**
  - Управление командой и боксами в любых сохранениях.
  - Редактирование IV, EV, атак, способностей, природы, шайни-статуса, Tera-типа, гипертренировки, дружбы, Покеруса и данных о встрече (Met / Origin).
  - Проверка легальности покемона в реальном времени во время редактирования.

- **Легализация в один клик:**
  - Встроенный Auto Legality Mod автоматически приводит характеристики и происхождение покемона к легальному виду без подключения к интернету.

- **Межпоколенческий Банк (аналог Pokémon HOME / PKSM):**
  - Неограниченное число тематических боксов для хранения коллекции.
  - Свободный перенос покемонов между играми с **Gen 1** по **Gen 9** с автоматической адаптацией форматов и предварительным просмотром изменений.

- **Living Dex и Автопилот:**
  - Трекер полного национального декса на 1025 покемонов (включая шайни).
  - Режим *Living Dex Autopilot*: автоматически находит, перемещает и эволюционирует покемонов из ваших сохранений и банка для заполнения недостающих ячеек коллекции.

- **База Mystery Gift / Wonder Card:**
  - Встроенный каталог официальных раздач и событий всех поколений с возможностью прямой инъекции карт в сохранение.

- **Редактор сумки и предметов:**
  - Настройка содержимого любых карманов сумки, изменение количества и быстрое пополнение предметов.

- **Редактор тренера:**
  - Изменение имени тренера, TID, SID, пола, денег и игровых рекордов.

- **Интеграция с Pokémon Showdown:**
  - Быстрый импорт и экспорт команд в текстовом формате Showdown, а также генерация QR-кодов.

- **Инструменты генерации и разведения:**
  - Генератор покемонов и яиц, управление детским садом (Day Care / Nursery).

- **Безопасность сохранений:**
  - Автоматическое создание точек восстановления перед любой записью в файл. До 20 последних бэкапов сохраняются в истории.

<p align="center">
  <img src="docs/screenshots/bank.png" alt="Банк покемонов" width="49%" />
  <img src="docs/screenshots/living-dex.png" alt="Living Dex" width="49%" />
</p>

<p align="center">
  <img src="docs/screenshots/mystery-gift.png" alt="Mystery Gift" width="49%" />
  <img src="docs/screenshots/bag.png" alt="Редактор сумки" width="49%" />
</p>

---

## 🎮 Поддерживаемые игры

PKForge поддерживает оригинальные игры всех основных поколений, спин-оффы и популярные ROM-хаки:

| Поколение | Игры |
|:---:|---|
| **Gen I** | Red, Blue, Yellow, Green (JP) |
| **Gen II** | Gold, Silver, Crystal |
| **Gen III** | Ruby, Sapphire, Emerald, FireRed, LeafGreen, Colosseum, XD: Gale of Darkness, Box R&S |
| **Gen IV** | Diamond, Pearl, Platinum, HeartGold, SoulSilver |
| **Gen V** | Black, White, Black 2, White 2 |
| **Gen VI** | X, Y, Omega Ruby, Alpha Sapphire |
| **Gen VII** | Sun, Moon, Ultra Sun, Ultra Moon, Let's Go Pikachu & Eevee |
| **Gen VIII** | Sword, Shield, Brilliant Diamond, Shining Pearl, Legends: Arceus |
| **Gen IX** | Scarlet, Violet |
| **ROM-хаки** | Pokémon Unbound, Radical Red, GS Chronicles, Luminescent Platinum, Pokémon Compass |

---

## 💾 Поддерживаемые форматы и эмуляторы

- **Форматы сохранений:** `.sav`, `.srm`, `.dsv`, `.gci`, файлы `main` (3DS и Switch).
- **Эмуляторы:** mGBA, RetroArch, melonDS, DraStic, Dolphin (отдельные `.gci`), Lime3DS / Citra / Azahar, Ryujinx / Eden.
- Поддерживается как прямое открытие одиночного файла сохранения, так и сканирование папок эмуляторов.

> [!IMPORTANT]
> Поддерживаются стандартные файлы сохранений в игре. Состояния эмулятора (*Save States*) не поддерживаются. Перед редактированием сохранитесь внутри игры и закройте эмулятор.

---

## 🚀 Запуск на Windows

1. Скачайте или откройте готовую сборку в `dist/windows-x64/`.
2. Запустите файл `Start-PKForge.cmd` или `dist/windows-x64/PKForge.App.exe`.
3. Требуется **Windows 10 версии 2004 или новее (x64)**. Все необходимые компоненты .NET и Windows App SDK уже включены в сборку.

### Управление на ПК

| Клавиша / Ввод | Действие |
|---|---|
| **Левый клик мыши** | Выбор / перетаскивание покемонов |
| **Правый клик** | Контекстное меню слота / игры |
| **Стрелки клавиатуры** | Навигация по сетке и элементам |
| **Enter** | Кнопка **A** (выбрать / подтвердить) |
| **Esc** | Кнопка **B** (назад / отмена) |
| **Page Up / Page Down** | Кнопки **L / R** (переключение боксов и страниц) |
| **F10** | Кнопка **Start** (главное меню) |
| **F2** | Кнопка **Select** (режим множественного выбора) |
| **X / Y** | Дополнительные действия, отображаемые внизу экрана |

---

## 🛠️ Сборка из исходников

Для сборки проекта на Windows:

```powershell
# Клонирование с субмодулями
git submodule update --init --recursive

# Сборка проекта под Windows
.\build-windows.ps1
```

Тесты выполняются следующими командами:
```powershell
dotnet test tests/PKForge.Domain.Tests/PKForge.Domain.Tests.csproj
dotnet test tests/PKForge.Engine.Tests/PKForge.Engine.Tests.csproj
```

---

## 📜 Благодарности и лицензия

- **[PKHeX](https://github.com/kwsch/PKHeX)** (Kaphotics и авторы) — базовый движок работы с сохранениями.
- **[PKHeX-Plugins / Auto Legality Mod](https://github.com/santacrab2/PKHeX-Plugins)** — алгоритм офлайн-легализации.
- **[PKSM](https://github.com/FlagBrew/PKSM)** — стилистика пиксельного интерфейса.
- **[Pokémon Showdown](https://pokemonshowdown.com)** — база спрайтов и текстовые форматы команд.
- **[sofianeelhor/PKForge](https://github.com/sofianeelhor/PKForge)** — автор оригинального Android-приложения.

Проект распространяется под лицензией **GPLv3** (унаследована от PKHeX.Core). См. файл [LICENSE](LICENSE).

*Не аффилировано с Nintendo, Game Freak или The Pokémon Company. Названия покемонов и графические материалы являются собственностью их владельцев.*
