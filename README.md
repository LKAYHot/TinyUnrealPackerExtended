# TinyPackerExtended

![TinyPackerExtended](docs/logo.png)

## 📖 Описание / Description

**TinyPackerExtended** — универсальный инструмент для обратной упаковки ассетов из игровых пакетов Unreal Engine. Это своего рода «обратный FModel»: вместо распаковки он позволяет собрать и упаковать ресурсы обратно, поддерживая широкий набор форматов.

## 🚀 Возможности / Features

- **Locres Toolkit** — работа с локализованными ресурсами (.locres, .csv).
- **Excel Toolkit** — импорт/экспорт таблиц (.csv, .xlsx).
- **Pak Toolkit** — упаковка и компрессия папок в `.pak`.
- **UAsset Injector** — встраивание текстур в `.uasset` файлы.
- **Auto Injector** — автоматическая группировка и инжект `.uasset` + `.png`.
- **Folder Editor** — интерактивный проводник для просмотра и управления файлами проекта.

## 📂 Структура проекта / Project Structure

```
/src
├─ /Core           # Общие библиотеки и утилиты
├─ /Modules        # Отдельные тулкиты (Locres, Excel, Pak и т.д.)
├─ /UI             # WPF-интерфейс приложения
└─ /Tests          # Тесты и примеры
```

## ⚙️ Установка / Installation

1. Клонируйте репозиторий:
   ```bash
   git clone https://github.com/ВашЛогин/TinyPackerExtended.git
   ```
2. Откройте решение в Visual Studio 2022 (или выше).
3. Соберите проект в конфигурации `Release`.
4. Запустите `TinyPackerExtended.exe`.

## 💡 Использование / Usage

1. Выберите нужный модуль на верхней панели.
2. Загрузите файлы или папки через Drag & Drop или кнопку `Выбрать`.
3. Настройте параметры (например, компрессию в Pak Toolkit).
4. Нажмите кнопку `Запуск` или `Импорт/Экспорт` для выполнения операции.

## 📸 Скриншоты / Screenshots

![Главное окно](docs/screenshots/main.png)
![Folder Editor](docs/screenshots/folder-editor.png)

## 🔧 Настройка / Customization

- Все настройки доступны в **AppSettings.json** или через UI в разделе `Settings`.
- Локализация текстов хранится в `/src/Core/Localization`.
- Для добавления нового модуля создайте папку в `/Modules` и зарегистрируйте в `ModuleLoader`.

## 🤝 Вклад / Contributing

1. Форкните репозиторий.
2. Создайте ветку: `git checkout -b feature/НазваниеФичи`.
3. Внесите изменения и закоммитьте: `git commit -m "Добавил новую фичу"`.
4. Отправьте в свой форк: `git push origin feature/НазваниеФичи`.
5. Создайте Pull Request.

## 📜 Лицензия / License

Этот проект распространяется под лицензией MIT. Подробнее читайте в [LICENSE](LICENSE).

---

> _Шаблон README можно менять и дополнять по своему усмотрению._
