# Telegram Markdown Archiver

Фоновое .NET Core приложение для архивирования сообщений из чата "Избранное" (Saved Messages) в Telegram в локальные Markdown файлы с поддержкой медиафайлов и транскрипции голосовых сообщений.

## Возможности

- ✅ Архивирование сообщений в реальном времени
- ✅ Сохранение медиафайлов (фото, видео, документы, аудио)
- ✅ Транскрипция голосовых сообщений с помощью Whisper
- ✅ Форматирование в Markdown с поддержкой ответов (replies)
- ✅ Управление состоянием для обработки пропущенных сообщений
- ✅ Контейнеризация с Docker
- ✅ Логирование всех операций

## Структура файлов

```
/data/notes/
├── 2023-12-25_Notes.md
├── 2023-12-26_Notes.md
└── media/
    ├── photo_20231225_143502.jpg
    ├── voice_20231225_144503.ogg
    └── document.pdf
```

## Формат Markdown

### Заголовки сообщений
```markdown
### [[2023-12-25 пн]] 14:35:02
```

### Ответы (Replies)
```markdown
### [[2023-12-25 пн]] 14:35:02

> Исходное сообщение
> на которое отвечаем

Текст ответа
```

### Медиафайлы
- **Фото**: `![](./media/image.jpg)`
- **Видео/Документы**: `[document.pdf](./media/document.pdf)`
- **Голосовые сообщения**: 
  ```markdown
  [voice.ogg](./media/voice.ogg)
  
  > Текст транскрипции
  ```

## Установка и настройка

### 1. Клонирование репозитория
```bash
git clone https://github.com/menshov-anatoliy/Telegram.Markdown.Archiver.git
cd Telegram.Markdown.Archiver
```

### 2. Настройка конфигурации

Скопируйте `.env.example` в `.env` и настройте:
```env
TELEGRAM_BOT_TOKEN=ваш_токен_бота
TELEGRAM_USER_ID=ваш_id_пользователя
```

### 3. Запуск с Docker Compose

```bash
# Создайте директории для данных
mkdir -p data/notes models

# Скачайте модель Whisper (опционально)
# wget https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-base.bin -O models/ggml-base.bin

# Запустите сервис
docker-compose up -d
```

### 4. Локальная разработка

```bash
# Установка зависимостей и сборка
dotnet restore
dotnet build

# Для тестирования с локальными путями
# (по умолчанию использует ./test_data)
dotnet run --project Telegram.Markdown.Archiver

# Для продакшена с Docker путями
ASPNETCORE_ENVIRONMENT=Production dotnet run --project Telegram.Markdown.Archiver

# Тестирование
dotnet test
```

## Конфигурация

### appsettings.json
```json
{
  "Telegram": {
    "BotToken": "ваш_токен_бота",
    "UserId": 123456789
  },
  "Paths": {
    "NotesRoot": "/data/notes",
    "MediaDirectoryName": "media",
    "StateFile": "/data/state.json"
  },
  "Whisper": {
    "ModelPath": "/models/ggml-base.bin"
  }
}
```

### Переменные окружения
- `TELEGRAM_BOT_TOKEN` - токен Telegram бота
- `TELEGRAM_USER_ID` - ID пользователя Telegram
- `Paths__NotesRoot` - путь к корневой директории заметок
- `Whisper__ModelPath` - путь к модели Whisper

## Получение токена бота и ID пользователя

### 1. Создание бота
1. Напишите [@BotFather](https://t.me/botfather) в Telegram
2. Используйте команду `/newbot`
3. Следуйте инструкциям для создания бота
4. Скопируйте полученный токен

### 2. Получение ID пользователя
1. Напишите [@userinfobot](https://t.me/userinfobot)
2. Отправьте любое сообщение
3. Скопируйте ваш ID из ответа

## Архитектура

- **TelegramArchiverHostedService** - главный фоновый сервис
- **TelegramService** - работа с Telegram Bot API
- **StateService** - управление состоянием приложения
- **FileSystemService** - операции с файловой системой
- **MarkdownService** - форматирование в Markdown
- **WhisperService** - транскрипция голосовых сообщений

## Логирование

Приложение логирует все ключевые операции:
- Получение и обработка сообщений
- Скачивание и сохранение медиафайлов
- Транскрипция голосовых сообщений
- Ошибки и предупреждения

## Устранение неполадок

### Проверка логов
```bash
docker-compose logs -f telegram-archiver
```

### Проверка состояния
Файл `state.json` содержит ID последнего обработанного сообщения.

### Ошибки транскрипции
Если Whisper модель не загружена, голосовые сообщения будут сохраняться без транскрипции.

## Разработка и тестирование

```bash
# Запуск тестов
dotnet test

# Сборка
dotnet build

# Проверка стиля кода
dotnet format --verify-no-changes
```

## Лицензия

MIT License - см. файл LICENSE для деталей.