# TinyPackerExtended

![TinyPackerExtended](Screenshots/logo.png)

## 📖 Описание / Description

**TinyPackerExtended** — универсальный инструмент для обратной упаковки ассетов из игровых пакетов Unreal Engine. Вместо распаковки он позволяет собрать и упаковать ресурсы обратно, поддерживая широкий набор форматов.

## 🚀 Возможности / Features

- **Locres Toolkit** — работа с локализованными ресурсами (.locres, .csv).
- **Excel Toolkit** — импорт/экспорт таблиц (.csv, .xlsx).
- **Pak Toolkit** — упаковка папок в `.pak`.
- **UAsset Injector** — встраивание текстур в `.uasset` файлы.
- **Auto Injector** — автоматическая группировка и инжект `.uasset` + `.png`.
- **Folder Editor** — интерактивный проводник для просмотра и управления файлами проекта.

## ⚙️ Установка / Installation

1. Клонируйте репозиторий:
   ```bash
   git clone https://github.com/LKAYHot/TinyUnrealPackerExtended.git
   ```
2. Откройте решение в Visual Studio 2022 (или выше).
3. Соберите проект в конфигурации `Release`.
4. Запустите `TinyPackerExtended.exe`.

## 💡 Использование / Usage

1. Выберите нужный модуль на верхней панели.
2. Загрузите файлы или папки через Drag & Drop или нажмите кнопку `Выбрать файлы/папку`.
3. Настройте параметры (например, компрессию в Pak Toolkit) - пока недоступна гибкая настройка.
4. Нажмите кнопку `Запуск` или `Импорт/Экспорт` для выполнения операции.

## 📸 Скриншоты / Screenshots

![Главное окно](Screenshots/main.png)
![Folder Editor](Screenshots/folder-editor.png)

## 🔧 Настройка / Customization

- Все настройки доступны в **AppSettings.json** или через UI в разделе `Settings`.
- Локализация текстов хранится в `/src/Core/Localization`.
- Для добавления нового модуля создайте папку в `/Modules` и зарегистрируйте в `ModuleLoader`.

## 📜 Лицензия / License

Этот проект распространяется под лицензией MIT. Подробнее читайте в [LICENSE](LICENSE).

---