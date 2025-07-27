# Быстрый старт для Telegram Markdown Archiver

Этот файл содержит пошаговую инструкцию для быстрого развертывания приложения.

## Предварительные требования

- Docker и Docker Compose установлены на вашей системе
- Токен Telegram бота (получите у [@BotFather](https://t.me/botfather))
- Ваш Telegram User ID (получите у [@userinfobot](https://t.me/userinfobot))

## Пошаговая инструкция

### 1. Клонирование репозитория
```bash
git clone https://github.com/menshov-anatoliy/Telegram.Markdown.Archiver.git
cd Telegram.Markdown.Archiver
```

### 2. Настройка переменных окружения
```bash
# Скопируйте пример файла окружения
cp .env.example .env

# Отредактируйте .env файл
nano .env  # или любой другой редактор
```

Пример содержимого файла `.env`:
```env
# Обязательные параметры
TELEGRAM_BOT_TOKEN=123456789:ABCDEFGHIJKLMNOPQRSTUVWXYZ
TELEGRAM_USER_ID=987654321

# Опциональные параметры
NOTES_HOST_PATH=./my_telegram_notes
```

### 3. Создание директорий (опционально)
```bash
# Создайте директории для данных
mkdir -p my_telegram_notes models

# Загрузите модель Whisper для транскрипции (опционально)
wget https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-medium.bin -O models/ggml-medium.bin
```

### 4. Запуск приложения
```bash
# Запустите в фоновом режиме
docker-compose up -d

# Проверьте статус
docker-compose ps

# Просмотрите логи
docker-compose logs -f telegram-archiver
```

### 5. Проверка работы
1. Отправьте сообщение в "Избранное" (Saved Messages) в Telegram
2. Проверьте директорию с заметками - должен появиться новый файл
3. Убедитесь, что контейнер здоров: `docker-compose ps`

## Управление приложением

### Остановка
```bash
docker-compose down
```

### Перезапуск
```bash
docker-compose restart
```

### Обновление
```bash
docker-compose down
docker-compose build --no-cache
docker-compose up -d
```

### Просмотр логов
```bash
# В реальном времени
docker-compose logs -f telegram-archiver

# Последние 50 строк
docker-compose logs --tail=50 telegram-archiver
```

## Устранение неполадок

### Проблема: Контейнер не запускается
1. Проверьте логи: `docker-compose logs telegram-archiver`
2. Убедитесь, что переменные в `.env` файле корректны
3. Проверьте, что токен бота действителен

### Проблема: Сообщения не архивируются
1. Убедитесь, что бот добавлен в чат "Избранное"
2. Проверьте правильность User ID
3. Проверьте логи на наличие ошибок

### Проблема: Нет транскрипции голосовых сообщений
1. Убедитесь, что модель Whisper загружена в директорию `models/`
2. Проверьте путь к модели в логах

## Структура файлов

После успешного запуска у вас будет следующая структура:

```
Telegram.Markdown.Archiver/
├── docker-compose.yml
├── .env                    # Ваши настройки (НЕ коммитить!)
├── my_telegram_notes/      # Архивированные заметки
│   ├── 2024-01-15_Notes.md
│   └── media/
│       ├── photo_001.jpg
│       └── voice_001.ogg
└── models/                 # Модели Whisper
    └── ggml-medium.bin
```

## Безопасность

⚠️ **ВАЖНО**: Никогда не коммитьте файл `.env` в Git!

Файл `.env` содержит секретные данные и автоматически исключен из Git через `.gitignore`.