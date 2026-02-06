# shoppy-bot

Telegram bot for shared grocery list

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- PostgreSQL database
- Telegram Bot Token (get one from [@BotFather](https://t.me/BotFather))

## Development Setup

1. Clone the repository:
   ```bash
   git clone https://github.com/your-username/shoppy-bot.git
   cd shoppy-bot
   ```

2. Create a `.env` file from the example:
   ```bash
   cp .env.example .env
   ```

3. Edit `.env` with your configuration:
   ```
   TELEGRAM_BOT_TOKEN=your_bot_token_here
   DATABASE_CONNECTION_STRING="Host=localhost;Database=shoppybot;Username=postgres;Password=your_password"
   ```

4. Restore dependencies:
   ```bash
   dotnet restore
   ```

5. Run the application:
   ```bash
   cd src/ShoppyBot
   dotnet run
   ```

   The application will automatically apply database migrations on startup.

## Project Structure

```
src/
├── ShoppyBot/           # Main application
│   ├── Data/            # Database context and migrations
│   ├── Handlers/        # Telegram command and callback handlers
│   ├── Models/          # Entity models
│   ├── Services/        # Business logic services
│   └── Utils/           # Utility classes
└── ShoppyBot.Tests/     # Unit tests
```

## Running Tests

```bash
dotnet test
```
