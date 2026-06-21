# NotificationSystem

Пет-проект — система уведомлений на базе очередей сообщений. Два микросервиса общаются асинхронно через RabbitMQ: первый принимает заказы, второй отправляет email-уведомления и сохраняет результат в PostgreSQL.

## Стек

- **ASP.NET Core 8** — Web API для приёма заказов
- **Worker Service** — фоновый сервис, читает очередь и отправляет email
- **RabbitMQ** — брокер сообщений между сервисами
- **PostgreSQL** — хранение логов отправленных уведомлений
- **MailKit** — отправка email
- **Docker Compose** — оркестрация всех сервисов
- **smtp4dev** — локальный SMTP-сервер для разработки

## Как запустить

**Требования:** Docker Desktop

```bash
git clone https://github.com/your-username/NotificationSystem.git
cd NotificationSystem
docker compose up --build
```

После запуска:

| Сервис | URL |
|---|---|
| Swagger | http://localhost:8080/swagger |
| RabbitMQ UI | http://localhost:15672 (guest / guest) |
| Входящие письма | http://localhost:5000 |

При желании можно изменить значения переменных в docker-compose.yml

## Как проверить

1. Открыть http://localhost:8080/swagger
2. Выполнить `POST /api/orders`:
```json
{
  "customerEmail": "test@example.com",
  "amount": 1500.00
}
```
3. Письмо появится на http://localhost:5000
