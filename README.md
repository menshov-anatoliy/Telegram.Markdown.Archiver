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

Скопируйте `.env.example` в `.env` и настройте переменные окружения:

```bash
cp .env.example .env
```

Отредактируйте файл `.env`:
```env
# Обязательные параметры
TELEGRAM_BOT_TOKEN=ваш_токен_бота
TELEGRAM_USER_ID=ваш_id_пользователя

# Опциональные параметры
NOTES_HOST_PATH=./data/notes  # Путь к директории для заметок на хост-машине
```

**Важно:** Никогда не коммитьте файл `.env` в систему контроля версий!

### 3. Запуск с Docker Compose

```bash
# Убедитесь, что файл .env настроен (см. шаг 2)

# Создайте директории для данных (если используете локальные пути)
mkdir -p data/notes models

# Скачайте модель Whisper для транскрипции голосовых сообщений (опционально)
# Если модель не загружена, голосовые сообщения будут сохраняться без транскрипции
wget https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-base.bin -O models/ggml-base.bin

# Запустите сервис в фоновом режиме
docker-compose up -d

# Проверьте состояние сервиса
docker-compose ps

# Просмотр логов (опционально)
docker-compose logs -f telegram-archiver
```

**Управление сервисом:**
```bash
# Остановка сервиса
docker-compose down

# Перезапуск сервиса
docker-compose restart

# Обновление и пересборка
docker-compose down
docker-compose build --no-cache
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

## Управление данными в Docker

### Именованные тома
Приложение использует именованные Docker тома для сохранения критически важных данных:

- **`state_data`** - хранит файл состояния (`state.json`) и другие служебные данные
- **`models_data`** - хранит модели Whisper для транскрипции голосовых сообщений

Эти тома автоматически создаются при первом запуске и сохраняются между перезапусками контейнеров.

### Директория заметок
Директория с заметками монтируется с хост-машины через переменную `NOTES_HOST_PATH`:
- По умолчанию: `./data/notes` (относительно docker-compose.yml)
- Можно изменить в файле `.env`: `NOTES_HOST_PATH=/path/to/your/notes`

### Резервное копирование данных
```bash
# Резервная копия именованных томов
docker run --rm -v telegram-markdown-archiver_state_data:/data -v $(pwd):/backup alpine tar czf /backup/state_backup.tar.gz -C /data .
docker run --rm -v telegram-markdown-archiver_models_data:/models -v $(pwd):/backup alpine tar czf /backup/models_backup.tar.gz -C /models .

# Восстановление из резервной копии
docker run --rm -v telegram-markdown-archiver_state_data:/data -v $(pwd):/backup alpine tar xzf /backup/state_backup.tar.gz -C /data
docker run --rm -v telegram-markdown-archiver_models_data:/models -v $(pwd):/backup alpine tar xzf /backup/models_backup.tar.gz -C /models
```

### Очистка данных
```bash
# Остановка сервиса
docker-compose down

# Удаление именованных томов (ВНИМАНИЕ: данные будут потеряны!)
docker volume rm telegram-markdown-archiver_state_data
docker volume rm telegram-markdown-archiver_models_data

# Директория заметок останется на хост-машине
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

**Обязательные:**
- `TELEGRAM_BOT_TOKEN` - токен Telegram бота (получите у @BotFather)
- `TELEGRAM_USER_ID` - ID пользователя Telegram (получите у @userinfobot)

**Опциональные для Docker:**
- `NOTES_HOST_PATH` - путь к директории заметок на хост-машине (по умолчанию: `./data/notes`)

**Внутренние переменные (настраиваются автоматически):**
- `Paths__NotesRoot` - путь к корневой директории заметок внутри контейнера
- `Paths__MediaDirectoryName` - имя поддиректории для медиафайлов  
- `Paths__StateFile` - путь к файлу состояния
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
# Просмотр логов в реальном времени
docker-compose logs -f telegram-archiver

# Просмотр последних логов
docker-compose logs --tail=50 telegram-archiver

# Проверка состояния здоровья контейнера
docker-compose ps
docker inspect telegram-markdown-archiver --format='{{.State.Health.Status}}'
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